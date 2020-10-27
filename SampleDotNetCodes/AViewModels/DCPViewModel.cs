using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using Common;
using Common.Models;
using Common.Extensions;
using Domain.DataAccess.Models;
using Domain.DataAccess.ModelsPoco;
using Domain.DataAccess.Validations;
using Domain.Repository;
using System.Text.RegularExpressions;
using System.Configuration;
using System.IO;
using Common.Helpers;
using System.Xml.Linq;
using BenefitManager.StateMachines;

namespace BenefitManager.ViewModels
{
    public class DCPViewModel
    {
        #region member variables and constructors

        private readonly IUnitOfWork _uow;

        private IEFRepository<BenefitAddress> _bnfAddrRepo
        {
            get
            {
                return _uow.CreateEFRepository<BenefitAddress>();
            }
        }

        private IEFRepository<ContributionDCP> _contribDcpRepo
        {
            get
            {
                return _uow.CreateEFRepository<ContributionDCP>();
            }
        }

        private IEFRepository<DCPCatchUpYear> _dcpCatchUpYearRepo
        {
            get
            {
                return _uow.CreateEFRepository<DCPCatchUpYear>();
            }
        }
        public DCPViewModel(IUnitOfWork uow)
        {
            _uow = uow;
        }

    
        #endregion

        #region member methods

        public List<MenuItemMapPoco.Alert> GetDashboardAlerts()
        {
            List<MenuItemMapPoco.Alert> alerts = new List<MenuItemMapPoco.Alert>();

            List<DcpPayrollDeductFile> duePaydays = GetPayrollDeductFiles(); //new DCPViewModel(_uow).GetPayrollDeductFiles();

            // Expired paydays
            List<DcpPayrollDeductFile> passedPaydays = duePaydays.Where(d => d.DaysToCutoffDate < 0).ToList();
            if (passedPaydays.Count > 0)
            {
                alerts.Add(new MenuItemMapPoco.Alert()
                {
                    Name = passedPaydays.Count.ToString() + " Passed cutoff days: ",
                    Terms = passedPaydays.OrderBy(p => p.DaysToCutoffDate).Select(p => p.DeferralPayrollGroup + " cutoff of is " + p.CutoffDateStr + " for the " + p.PayDayStr + " pay date; " +
                            "It has passed " + (-1 * p.DaysToCutoffDate).ToString() + " days ago.").Distinct().ToList(),
                    IsDanger = true
                });
            }

            // Paydays that have not expired yet
            duePaydays = duePaydays.Where(d => d.DaysToCutoffDate >= 0).ToList();
            if (duePaydays.Count > 0)
            {
                alerts.Add(new MenuItemMapPoco.Alert()
                {
                    Name = duePaydays.Count.ToString() + " Coming cutoff days: ",
                    Terms = duePaydays.OrderBy(p => p.DaysToCutoffDate).Select(p => p.DeferralPayrollGroup + " cutoff of is " + p.CutoffDateStr + " for the " + p.PayDayStr + " pay date; " +
                            "There are " + p.DaysToCutoffDate.ToString() + " days remaining.").Distinct().ToList(),
                    IsDanger = false
                });
            }


            return alerts;
        }

        public List<DCPEnrollSearch> GetEnrollsGridPage(GridPaginationOptions paginationOpts, out int totalRows)
        {
            var query = _enrollRepo
                .FindGraph(e => e.SocialSecurityNo > 0 && e.BenefitAbbr == Constants.BnftDcp && e.Program != string.Empty && e.PlanYear == 0,
                    x => x.Agency, x => x.Eligibility)
                .Select(e => new DCPEnrollSearch
                {
                    SocialSecurityNo = e.SocialSecurityNo,
                    FirstName = e.Eligibility.FirstName,
                    MiddleInit = e.Eligibility.MiddleName,
                    LastName = e.Eligibility.LastName,
                    BudgetCode = e.Agency.CityBudgetCode,
                    AgencyCode = e.Agency.AgencyCode,
                    Program = e.Program,
                    Birth = e.Eligibility.Birth
                })
                .Distinct()
                .OrderBy(e => e.SocialSecurityNo)
                .AsQueryable();

            List<DCPEnrollSearch> enrolls = query.ToList();
            
            for (var i = 0; i < paginationOpts.Programs.Length; i++)
            {
                if (paginationOpts.Programs[i]!=null)
                paginationOpts.Filters.Add(new Filter { field = "Program", @operator = "eq", value = paginationOpts.Programs[i] });
            }

            if (paginationOpts.Filters.Count > 0)
            {
                Expression<Func<DCPEnrollSearch, bool>> filtersExp = ExpressionBuilder.GetFilterExpression<DCPEnrollSearch>(paginationOpts.Filters);
                if (filtersExp != null)
                {
                    enrolls = enrolls.AsQueryable().Where(filtersExp).ToList();
                }
            }

            // sort by single column 
            if (paginationOpts.Sorts.Count > 0)
            {
                var sort = paginationOpts.Sorts[0];
                enrolls = sort.dir == "asc" ? enrolls.OrderBy(sort.field).ToList() : enrolls.OrderByDescending(sort.field).ToList();
            }

            // run query
            totalRows = enrolls.Count;

            int skipRows = (paginationOpts.PageNo - 1) * paginationOpts.PageSize;
            int takeRows = (totalRows - skipRows) > paginationOpts.PageSize ? paginationOpts.PageSize : totalRows - skipRows;

            List<DCPEnrollSearch> enrollsPage = enrolls.Skip(skipRows).Take(takeRows).ToList();

            return enrollsPage;
        }

        public DcpPlansPoco GetEmployeeDcpPlans(int ssn)
        {
            DcpPlansPoco plans = new DcpPlansPoco();
            plans.Enrolls = new List<EmployeeDcpPlan>();
            plans.EmpPgrmInclusion = new List<DcpPlansPoco.ProgramInclusion>();

            List<Enrollment> enrEntities = _enrollRepo
                .FindGraph(enr => enr.SocialSecurityNo == ssn && enr.BenefitAbbr == Constants.BnftDcp && enr.ContribPayrollEffDate.HasValue,
                    x => x.Eligibility, x => x.Eligibility.EligibilityEmployers,
                    x => x.Agency, x => x.Agency.Payroll, x => x.DeferralStatu)
                .ToList();

            //Create list of all programs.
            plans.EmpPgrmInclusion.Add(new DcpPlansPoco.ProgramInclusion { Program = Constants.Pgm401K, DisplayName = Constants.Pgm401K + @"/" + Constants.Pgm401KRoth, HasIncluded = false, HasEnrolled = false });
            plans.EmpPgrmInclusion.Add(new DcpPlansPoco.ProgramInclusion { Program = Constants.Pgm457, DisplayName = Constants.Pgm457 + @"/" + Constants.Pgm457Roth, HasIncluded = false, HasEnrolled = false });
            plans.EmpPgrmInclusion.Add(new DcpPlansPoco.ProgramInclusion { Program = Constants.PgmIRA, DisplayName = Constants.PgmIRA + @"/" + Constants.PgmIRR, HasIncluded = false, HasEnrolled = false });

            //Get list of programs for employee from inclusion table.
            List<string> includedPrograms = _dcpEligIncludeRepo.Find(f => f.SocialSecurityNo == ssn).Select(s => s.DCPProgram).ToList();

            //Set Include Status of included programs.
            plans.EmpPgrmInclusion
                .Join(includedPrograms, empPrgInc => empPrgInc.Program, incPrg => incPrg, (empPrgInc, incPrg) => empPrgInc)
                .ToList()
                .ForEach(f => f.HasIncluded = true);

            // Get deferral goals dropdown list
            plans.Goals = _uow.CreateEFRepository<DeferralGoal>()
                .Find(g => g.HasExpired == false)
                .Select(g => new DeferralGoalPoco { GoalCode = g.GoalCode, Goal = g.Goal, CanGoalAmountChange = g.CanGoalAmountChange } )
                .ToList();

            // Get Job Seq Nos tool tip
            List<EligibilityEmployerPoco> JobSeqNos = _eligEmployerRepo
                .FindGraph(empr => empr.SocialSecurityNo ==ssn && empr.TerminationDate == null, x=>x.Agency)
                .OrderByDescending(empr => new { empr.AgencyCode, empr.BudgetCode, empr.SalaryAmount } )
                .Select(empr => new EligibilityEmployerPoco() { AgencyBudgetCode = empr.AgencyCode + empr.Agency.CityBudgetCode,JobSeqNo = empr.JobSeqNo, SalaryAmount = empr.SalaryAmount })
                .ToList();

            plans.JsnTooltipHtml = @"<table style='text-align:left'><tr><td>Agency</td><td>JSN</td><td>Salary</td></tr>";
            foreach (var jsn in JobSeqNos)
            {
                plans.JsnTooltipHtml += "<tr><td width='30%'>" + jsn.AgencyBudgetCode + "</td><td width='30%'>" + jsn.JobSeqNo + "</td><td width='40%'>" + jsn.SalaryAmountStr + "</td></tr>";
            }
            plans.JsnTooltipHtml += "</table>";

            foreach (var enty in enrEntities)
            {
                var enroll = new EmployeeDcpPlan();
                enroll.Address = !string.IsNullOrEmpty(enty.AgencyCode) ?
                    _uow.CreateEFRepository<DCPEligibility>().Find(de => de.SocialSecurityNo == ssn && de.AgencyCode == enty.AgencyCode).FirstOrDefault()
                    : null;
                enroll.EnrollInfo = new EnrollmentPoco(enty);

                //Set enrolled Status of enrolled programs.
                plans.EmpPgrmInclusion.Where(p => p.DisplayName.Contains(enroll.EnrollInfo.Program)).ToList().ForEach(f => f.HasEnrolled = true);

                // Pre-tax and post-tax plan share one set of invenestment allocations          
                enroll.Funds = new List<EnrollmentFundAllocPoco>();
                if (enty.Program == Constants.Pgm401KRoth || enty.Program == Constants.Pgm457Roth)
                {
                    var pretaxPgm = enty.Program.Replace("Roth", string.Empty);
                    var pretaxEnroll = plans.Enrolls.Where(e => e.EnrollInfo.Program == pretaxPgm && e.Funds.Count > 0).FirstOrDefault();
                    if (pretaxEnroll != null)
                    {
                        pretaxEnroll.Funds.ForEach(f => { enroll.Funds.Add(f); } );
                    }
                }
                else
                {
                    var funds = _enrollFundAllocRepo
                        .Find(f => f.SocialSecurityNo == enty.SocialSecurityNo && f.BenefitAbbr == enty.BenefitAbbr && f.Program == enty.Program)
                        .Select(f => f);
                    foreach (var fund in funds)
                    {
                        var fundPoco = new EnrollmentFundAllocPoco(fund);

                        // Database table EnrollmentFundAlloc does not have direct assoc to DCPInvestOption on purpose,
                        // because data from FASCore does not need to be sync'd with OLR database
                        fundPoco.OptionName = _invOptRepo.Find(io => io.OptionID == fund.InvestmentOptionID).Select(io => io.OptionName).FirstOrDefault() ?? "";
                        enroll.Funds.Add(fundPoco);
                    }
                }

                //var test = _uow.CreateEFRepository<EnrollmentDCPBeneficiary>()
                //    .FindGraph(b => b.ParticipantSSN == ssn && b.Program == enty.Program, x => x.Relationship, x => x.Designation, x => x.BeneficiaryType)
                //    .ToList();

                enroll.Beneficiaries = _uow.CreateEFRepository<EnrollmentDCPBeneficiary>()
                    .FindGraph(b => b.ParticipantSSN == ssn && b.Program == enty.Program, x => x.Relationship, x => x.Designation, x => x.BeneficiaryType)
                    .AsEnumerable()
                    .Select(b => new EmployeeDcpPlan.Beneficiary()
                    {
                        Address = ((!String.IsNullOrEmpty(b.Address1) ? b.Address1 + "," : " ") + " " +
                                    (!String.IsNullOrEmpty(b.Address2) ? b.Address2 + "," : " ") + " " +
                                    (!String.IsNullOrEmpty(b.City) ? b.City + "," : " ") + " " +
                                    (!String.IsNullOrEmpty(b.StateAbbr) ? b.StateAbbr : " ") + " " +
                                    (!String.IsNullOrEmpty(b.ZipCode) ? b.ZipCode : " ") + " " +
                                    (!String.IsNullOrEmpty(b.CountryCode) ? b.CountryCode : " ")).TrimEnd(),
                        BeneficiarySSN = b.BeneficiarySSN,
                        Designation = b.Designation != null ? b.Designation.DesignationDesc : string.Empty,
                        BeneficiaryType = b.BeneficiaryType != null ? b.BeneficiaryType.BeneficiaryType1 : string.Empty,
                        Relationship = b.Relationship != null ? b.Relationship.RelationshipDesc : string.Empty,
                        FullName = (b.FirstName ?? string.Empty) + " " + (b.MiddleName ?? string.Empty) + " " + (b.LastName ?? string.Empty),
                        Percentage = b.Percentage
                    })
                    .ToList();

                enroll.Disburses = _uow.CreateEFRepository<DCPDisbursement>()
                    .Find(d => d.SocialSecurityNo == ssn)
                    .OrderBy(d => d.EffectiveDate)
                    .AsEnumerable()
                    .Select(d => new DCPDisbursementPoco(d))
                    .ToList();

                enroll.GoalObj = enroll.EnrollInfo != null && !string.IsNullOrEmpty(enroll.EnrollInfo.GoalCode) ?
                                plans.Goals.Where(g => g.GoalCode == enroll.EnrollInfo.GoalCode).FirstOrDefault()
                                : null;



                plans.Enrolls.Add(enroll);
            }
   
            return plans;
        }

        public List<object> LastProcessedPaydays()
        {
            List<DcpPayrollDeductFile> lastProcessed =
                _dedReqRepo
                .Find(req => req.CreatedFrom != "init")
                .GroupBy(req => new { req.PayrollID, req.Program }, req => req.DeductPayday)
                .Select(g => new DcpPayrollDeductFile() { PayrollID = g.Key.PayrollID, Program = g.Key.Program, PayDay = g.Max() })
                .ToList();

            List<DateTime> uniqDays = lastProcessed.OrderByDescending(lp => lp.PayDay.Value).Select(lp => lp.PayDay.Value).Distinct().ToList();

            dynamic payday;
            List<dynamic> paydays = new List<dynamic>();
            foreach (var day in uniqDays)
            {
                payday = new ExpandoObject();
                payday.PayDay = day;
                payday.PayDayStr = day.ToString("MM/dd/yyyy");
                paydays.Add(payday);
            }

            return paydays;
            //return uniqDays.Select(d => new { PayDay = d, PayDayStr = d.ToString("MM/dd/yyyy") }).ToList<object>();
        }

        public List<DcpPayrollDeductFile> DeductFilesAllowRecreate()
        {
            List<DcpPayrollDeductFile> lastProcessed =
                _dedReqRepo
                .FindGraph(req => req.CreatedFrom != "init" && req.DeductFile != null, x => x.Payroll)
                .GroupBy(req => new { req.PayrollID, req.Program, req.Payroll.DeferralPayrollGroup, req.Payroll.DeferralPayrollGroupSortOrder }, req => req.DeductPayday)
                .Select(g => new DcpPayrollDeductFile()
                {
                    PayrollID = g.Key.PayrollID,
                    Program = g.Key.Program,
                    DeferralPayrollGroup = g.Key.DeferralPayrollGroup,
                    SortOrder = g.Key.DeferralPayrollGroupSortOrder,
                    PayDay = g.Max()
                })
                .ToList();

            // currentPaydays = the most recent deduction paydays of a payroll. 
            List<DcpPayrollDeductFile> currentPaydays =
                _enrollRepo
                .FindGraph(enr => enr.SocialSecurityNo > 0 && enr.BenefitAbbr == Constants.BnftDcp && enr.ContribPayrollEffDate != null && enr.AgencyCode != null, x => x.Agency)
                .Where(enr => enr.Agency.PayrollID != null)
                .GroupBy(enr => new { enr.Agency.PayrollID, enr.Program }, enr => enr.ContribPayrollEffDate)
                .Select(g => new DcpPayrollDeductFile() { PayrollID = g.Key.PayrollID.Value, Program = g.Key.Program, PayDay = g.Max() })
                .ToList();

            // Filter out those have new imported payday already. 
            // Deduction file cannot be re-generated if there is new deductions have been imported for the payroll and program. 
            List<DcpPayrollDeductFile> recreates = lastProcessed
                .Join(currentPaydays,
                    last => new { last.PayrollID, last.Program },
                    curr => new { curr.PayrollID, curr.Program },
                    (last, curr) => new DcpPayrollDeductFile()
                    {
                        PayrollID = last.PayrollID,
                        DeferralPayrollGroup = last.DeferralPayrollGroup,
                        Program = last.Program,
                        LastDeductReqPayDay = last.PayDay.Value,
                        PayDay = curr.PayDay,
                        SortOrder = last.SortOrder
                    })
                .Where(lastcurr => lastcurr.LastDeductReqPayDay == lastcurr.PayDay)
                .ToList();

            // get export file schema id
            for (int i = 0; i < recreates.Count; i++)
            {
                var payrollID = recreates[i].PayrollID;
                var program = recreates[i].Program;

                recreates[i].DeductFileSchema =
                    _fileExportRepo
                    .Find(f => f.PayRollID == payrollID && f.BenefitAbbr == Constants.BnftDcp && f.Program == program && f.Process == Constants.ProcDCPDeferral)
                    .Select(f => f.FileSchemaID)
                    .FirstOrDefault();
            }

            return recreates.Where(f => f.PayDay != null).OrderBy(f => f.PayDay).ThenBy(f => f.SortOrder).ToList();
         }

        public List<DcpPayrollDeductFile> GetPayrollDeductFiles()
        {

            List<DcpPayrollDeductFile> files = _dedReqRepo.FindGraph(req => req.PayrollID > 0, x => x.Payroll)
                .Where(req => req.Payroll.DeferralPayrollGroup != null)
                .GroupBy(req => new { req.Payroll.DeferralPayrollGroup, req.PayrollID, req.Program, req.Payroll.DeferralPayrollGroupSortOrder })
                .Select(grp => new DcpPayrollDeductFile
                {
                    DeferralPayrollGroup = grp.Key.DeferralPayrollGroup,
                    PayrollID = grp.Key.PayrollID,
                    Program = grp.Key.Program,
                    LastDeductReqPayDay = DateTime.MinValue,
                    SortOrder = grp.Key.DeferralPayrollGroupSortOrder
                })
                .ToList();

            DateTime lastPayDay;
            string payrollGrp;
            short payrollID;
            string program;
            for (int i = 0; i < files.Count; i++)
            {

                payrollID = files[i].PayrollID;

                payrollGrp = files[i].DeferralPayrollGroup;
                program = files[i].Program;
                files[i].LastDeductReqPayDay = _dedReqRepo.Find(r => r.PayrollID == payrollID && r.Program == program).Max(r => r.DeductPayday);
                lastPayDay = files[i].LastDeductReqPayDay;

                // Find new pay days from the last deductions requested pay day    
                files[i].PayDay = _enrollRepo
                    .FindGraph(enr => enr.SocialSecurityNo > 0 && enr.BenefitAbbr == Constants.BnftDcp && enr.Program == program && enr.PlanYear == 0
                        && enr.ContribPayrollEffDate != null && enr.ContribPayrollEffDate > lastPayDay,
                        x => x.Agency)
                    .Where(enr => enr.Agency.PayrollID == payrollID)
                    .OrderBy(enr => enr.ContribPayrollEffDate)
                    .Select(enr => enr.ContribPayrollEffDate)
                    .FirstOrDefault();

                // Calculate cutoff date
                var payrollObj = _payrollRepo
                    .Find(p => p.PayrollID == payrollID)
                    .FirstOrDefault();

                if (payrollObj != null && files[i].PayDay.HasValue)
                {
                    // Making sure that pay date and summer start/end date is in the same year.
                    payrollObj.SummerPaydateStart = payrollObj.SummerPaydateStart.HasValue ?
                        new DateTime(files[i].PayDay.Value.Year, payrollObj.SummerPaydateStart.Value.Month, payrollObj.SummerPaydateStart.Value.Day)
                        : (DateTime?)null;

                    payrollObj.SummerPaydateEnd = payrollObj.SummerPaydateEnd.HasValue ?
                        new DateTime(files[i].PayDay.Value.Year, payrollObj.SummerPaydateEnd.Value.Month, payrollObj.SummerPaydateEnd.Value.Day)
                        : payrollObj.SummerPaydateStart.HasValue ? payrollObj.SummerPaydateStart.Value.AddMonths(3)
                        : (DateTime?)null;

                    if (payrollObj.SummerPaydateStart.HasValue &&
                        files[i].PayDay.Value >= payrollObj.SummerPaydateStart.Value &&
                        files[i].PayDay.Value <= payrollObj.SummerPaydateEnd.Value)
                    {
                        files[i].CutoffDate = payrollObj.SummerCutoffDate;
                    }

                    // If summer special cutoff date DOES NOT apply, get cutoff date from cutoff days 
                    if ( files[i].CutoffDate == null )
                    {
                        files[i].CutoffDate = files[i].PayDay.Value.AddDays(-1 * payrollObj.PaydateCutoffDays);
                    }
                }

                // Set default cutoff date to 10 calendar days prior to pay date if it is null
                files[i].CutoffDate = files[i].CutoffDate == null && files[i].PayDay.HasValue ? files[i].PayDay.Value.AddDays(-10) : files[i].CutoffDate;
                
                files[i].DeductFileSchema = _fileExportRepo
                    .Find(f => f.PayRollID == payrollID && f.BenefitAbbr == Constants.BnftDcp && f.Program == program && f.Process == Constants.ProcDCPDeferral)
                    .Select(f => f.FileSchemaID)
                    .FirstOrDefault();
            }

            files = files.Where(f => f.PayDay != null).OrderBy(f => f.PayDay).ThenBy(f => f.SortOrder).ToList();

            return files;
        }

        public List<DeferralLine> GetNewChangedDeferrals(GridPaginationOptions paginationOpts)
        {
            DateTime payday;
            DateTime.TryParse(paginationOpts.FromPaydayStr, out payday);
            string program = paginationOpts.Programs[0];
            DateTime checkPrevMissedFundStartingDate = new DateTime(2018, 7, 15);

            List<DeferralLine> deferrals = _enrollRepo.FindGraph(e =>
                e.SocialSecurityNo > 0
                && e.BenefitAbbr == Constants.BnftDcp
                && e.Program == program
                && e.PlanYear == 0
                && e.Agency.PayrollID == paginationOpts.PayrollID
                && e.ContribPayrollEffDate != null

                // 2018-08-15 Cindy: also includes previous deferrals that had no investment elections at the time of creating payroll files     
                && (e.ContribPayrollEffDate == payday
                    || (e.ContribPayrollEffDate < payday && e.ContribPayrollEffDate > checkPrevMissedFundStartingDate && e.ActualContribPayrollDate == null && e.DeferralStatusCode == Constants.DeferralStatusNoInvestFund))

                // FASCore could send GoalCode = 'C' for goal amount change only but agency stays the same
                // Therefore check Agency Change in the foreach loop below
                //&& e.GoalCode != Constants.DeferralChange
                && e.DeferralStatusCode != Constants.DeferralStatusRetired,
                    x => x.Agency,
                    x => x.Agency.Payroll,
                    x => x.Eligibility,
                    x => x.Eligibility.EligibilityEmployers)
                .AsEnumerable()
                .Select(d => new DeferralLine()
                {
                    BenefitAbbr = d.BenefitAbbr,
                    PlanYear = d.PlanYear,
                    PlanEffectiveDate = d.PlanEffectiveDate,
                    SocialSecurityNo = d.SocialSecurityNo,
                    ContribPayrollEffDate = d.ContribPayrollEffDate,
                    PlanCancelDate = d.PlanCancelDate,
                    ContributionAmount = d.ContributionAmount,
                    GoalCode = d.GoalCode,
                    GoalAmount = d.GoalAmount,
                    ContributionPercentage = d.ContributionPercentage,
                    Program = d.Program,
                    PlanEndDate = d.PlanEndDate,
                    ExpirationDate = d.PlanEndDate.HasValue ? d.PlanEndDate.Value.ToString("MM/dd/yyyy") : "999999",
                    PayrollID = d.Agency.PayrollID ?? 0,
                    HasPayrollChanged = false,
                    LastName = d.Eligibility.LastName,
                    FirstName = d.Eligibility.FirstName,
                    AgencyCode = d.AgencyCode,
                    DeferralStatusCode = d.DeferralStatusCode
                })
                .OrderBy(d => d.Program).ThenBy(d => d.SocialSecurityNo)
                .ToList();

            DeferralGoal deferralGoal = _uow.CreateEFRepository<DeferralGoal>()
                .Find(dg => dg.HasExpired == false)
                .FirstOrDefault()
                ?? new DeferralGoal { AnnualMaxContribution = 0m, Over50AnnualMaxContribution = 0m };
            
            EnrollmentDCPChangeTracking change;
            Eligibility employee;

            foreach (var enr in deferrals)
            {

                change = _enrollDcpTrackingRepo
                    .FindGraph(t => t.SocialSecurityNo == enr.SocialSecurityNo && t.BenefitAbbr == enr.BenefitAbbr && t.Program == enr.Program && t.PlanYear == enr.PlanYear && t.ChangeEffDate == enr.ContribPayrollEffDate,
                        x => x.Agency)
                    .FirstOrDefault();

                if (change != null)
                {
                    // deferral % changed
                    // 'Add' if changed from 0 %
                    enr.ActionCode = change.OldDeductPct == 0 && enr.ContributionPercentage > 0 ? Constants.DeferralNew : Constants.DeferralChange;
                    enr.Status = enr.ActionCode == Constants.DeferralNew ? Constants.DeferralStatusReinstatement : Constants.DeferralStatusChange;

                    if (enr.ActionCode == Constants.DeferralChange && enr.ContributionPercentage == 0)
                    {
                        // FoxPro treated this as cancelled, ContribPayrollEffDate to today (changed after the if bracket) and expiration date to next day.
                        enr.ExpirationDate = DateTime.Today.AddDays(1).ToString("MMddyy");
                        enr.Status = Constants.DeferralStatusSuspension;
                    }

                    // Do not send only when there is payroll change by comparing agency object of enrollment and change tracking, not rely on GoalCode = 'C'
                    enr.Send = change.Agency != null && enr.PayrollID != change.Agency.PayrollID ? false : true;
                    enr.HasPayrollChanged = enr.Send == false ? true : false;

                    // Do not send if status is still suspended
                    if (enr.DeferralStatusCode == Constants.DeferralStatusSuspensionExtended)
                    {
                        enr.Send = false;
                    }

                }
                else // new deferral %
                {
                    // Change will only be tracked when there is deduct % or goal amount change.
                    // FoxPro includes items that has no rate change as 'Change", BM is just mimicking what ever FoxPro has been doing.   
                    enr.ActionCode = enr.PlanEffectiveDate == enr.ContribPayrollEffDate ? Constants.DeferralNew : Constants.DeductChanged;
                    enr.Status = enr.ActionCode == Constants.DeferralNew ? Constants.DeferralStatusNewEnrollment : Constants.DeferralStatusChange;

                    enr.Send = enr.ContributionPercentage > 0 ? true : false;
                }

                // 2018-08-20 Cindy: do NOT send terminated employee (from payroll eligibility file) except those still receiving contributions last year
                if (enr.Status == Constants.DeferralStatusTerminated)
                {
                    var lastYear = DateTime.Today.Year - 1;
                    var contribLastYear = _contribDcpRepo
                        .Find(c => c.SocialSecurityNo == enr.SocialSecurityNo && program == enr.Program && c.Payday.HasValue && c.Payday.Value.Year == lastYear)
                        .Sum(c => c.ContributionAmount);
                    enr.Send = contribLastYear > 0 ? true : false;
                }

                // Check fund allocation existence.
                // ************ IMPORTANT !!! ****************
                // 401K and 401K Roth have same allocations while 457 and 457Roth also have the same allocations 
                var plan = enr.Program == Constants.Pgm401KRoth ? Constants.Pgm401K : enr.Program == Constants.Pgm457Roth ? Constants.Pgm457 : enr.Program;
                enr.EnrollmentFundAllocsCount = _enrollFundAllocRepo.Find(f => f.SocialSecurityNo == enr.SocialSecurityNo && f.BenefitAbbr == Constants.BnftDcp && f.Program == plan).Count();
                enr.Send = enr.Send == true && enr.EnrollmentFundAllocsCount > 0 ? true : false;

                // The following goal amount rule do not apply to payroll group HHC and SCA
                // If it is not agency change then set goal amount for employee >= 50 years old only
                if (paginationOpts.PayrollGroupName != Constants.PayrollGroupHHC)
                {
                    employee = _eligRepo.Find(elig => elig.SocialSecurityNo == enr.SocialSecurityNo).FirstOrDefault();

                    // max contrib apply to employee turn 50 through out the year
                    var dateTo50 = employee != null & employee.DeferredCompBirth != null ? employee.DeferredCompBirth.Value.AddYears(50) : DateTime.MaxValue;

                    // 2018-07-16 
                    // Per Tina,  take goal amount from FASCore's data feed unless:
                    // 1. reset max goal amount to this year's goal if it is still in last year's max of goal amount 
                    // 2. the prev's deferral change was in last year (ex 2017) AND goal amount is unchanged in current year (ex 2018)
                    if ( (enr.GoalAmount == deferralGoal.LastYearMaxContrib) ||
                         (enr.GoalAmount == deferralGoal.LastYearOver50MaxContrib) ||
                         ((change != null && change.OldPayrollEffDate.HasValue && enr.ContribPayrollEffDate.HasValue
                           && change.OldPayrollEffDate.Value.Year == enr.ContribPayrollEffDate.Value.Year - 1
                           && change.OldGoalAmount == enr.GoalAmount)) )
                    {
                        enr.GoalAmount = dateTo50 <= new DateTime(DateTime.Today.Year, 12, 31) ? deferralGoal.Over50AnnualMaxContribution : deferralGoal.AnnualMaxContribution; ;
                    }

                    // Per payroll's business rule,
                    // For PMS and participant under 50 years old, set goal amount to zero if it is = max goal amt of the year
                    if (paginationOpts.PayrollGroupName == Constants.PayrollGroupPMS && enr.GoalAmount == deferralGoal.AnnualMaxContribution)
                    {
                        enr.GoalAmount = 0m;
                    }
                }

                // Import!!! Always change effective date for PMS/BOE last.
                // Per PMS and BOE business requirement, sent file creation date as payroll effective date.
                // HHC and SCA do not require payroll effective date in the deduction request file 
                if (paginationOpts.PayrollGroupName != Constants.PayrollGroupHHC && paginationOpts.PayrollGroupName != Constants.PayrollGroupSCA)
                {
                    enr.ContribPayrollEffDate = DateTime.Today;
                }
            }

            deferrals = deferrals.Where(g => g.Send == true).ToList();

            return deferrals;
        }

        public DataServiceResult AddEmployeeToPgrm(int ssn, string pgrm)
        {
            DataServiceResult dsResult = null;

            try
            {
                _dcpEligIncludeRepo.Add(new DCPEligInclude { SocialSecurityNo = ssn, DCPProgram = pgrm });
                _uow.Commit();
            }
            catch (DataException ex)
            {
                dsResult = new DataServiceResult { Status = CRUDStatus.Failed, Message = ex.Message, Exception = ex.InnerException.Message };
            };

            return dsResult;
        }

        public List<int> GetReportSSNList(string reportID, string paydayStr, short deductPct)
        {
            var ssns = new List<int>();

            DateTime payday;
            DateTime.TryParse(paydayStr, out payday);
            payday = payday == DateTime.MinValue ? DateTime.Today : payday;
            decimal pct = (decimal)deductPct;

            switch (reportID)
            {
                case Constants.SSRSDCPDeductPctOverLetter:
                    ssns = _enrollRepo
                    .Find(e => e.SocialSecurityNo > 0 && e.BenefitAbbr == Constants.BnftDcp && (e.Program == Constants.Pgm401K || e.Program == Constants.Pgm457)
                            && e.ContribPayrollEffDate == payday.Date
                            && e.ContributionPercentage >= pct)
                    .Select(e => e.SocialSecurityNo)
                    .Distinct()
                    .ToList();

                    break;
                case Constants.SSRSDCPNoFundAllocLetter:

                    // Plan 401K and 401KRoth only have one set of investment allocations.
                    // And plan 457 and 457Roth also apply the same rule.
                    ssns = _enrollRepo
                        .Find(e => e.SocialSecurityNo > 0 && e.BenefitAbbr == Constants.BnftDcp && (e.Program == Constants.Pgm401K || e.Program == Constants.Pgm401KRoth)
                                && e.ContribPayrollEffDate.HasValue && e.ContribPayrollEffDate.Value == payday
                                && e.ContributionPercentage > 0)
                        .GroupJoin(_enrollFundAllocRepo.Find(f => f.SocialSecurityNo > 0 && f.BenefitAbbr == Constants.BnftDcp && f.Program == Constants.Pgm401K),
                            e => e.SocialSecurityNo,
                            f => f.SocialSecurityNo,
                            (e, fGrp) => new { SocialSecurityNo = e.SocialSecurityNo, Funds = fGrp.ToList() })
                        .Where(enrFunds => enrFunds.Funds.Count() == 0)
                        .Select(enrFunds => enrFunds.SocialSecurityNo)
                        .Distinct()
                        .ToList();

                    var plan457SSNs = _enrollRepo
                        .Find(e => e.SocialSecurityNo > 0 && e.BenefitAbbr == Constants.BnftDcp && (e.Program == Constants.Pgm457 || e.Program == Constants.Pgm457Roth)
                                && e.ContribPayrollEffDate.HasValue && e.ContribPayrollEffDate.Value == payday
                                && e.ContributionPercentage > 0)
                        .GroupJoin(_enrollFundAllocRepo.Find(f => f.SocialSecurityNo > 0 && f.BenefitAbbr == Constants.BnftDcp && f.Program == Constants.Pgm457),
                            e => e.SocialSecurityNo,
                            f => f.SocialSecurityNo,
                            (e, fGrp) => new { SocialSecurityNo = e.SocialSecurityNo, Funds = fGrp.ToList() })
                        .Where(enrFunds => enrFunds.Funds.Count() == 0)
                        .Select(enrFunds => enrFunds.SocialSecurityNo)
                        .Distinct()
                        .ToList();

                    //ssns.AddRange(plan457SSNs);
                    ssns = ssns.OrderBy(s => s).Distinct().ToList();

                    break;
                default:
                    break;
            }

            return ssns;
        }

        public List<DCPCashfileTransferPaydays> GetCashfileXferByPayrollGrp(bool isLoan)
        {
            var xfers = new List<DCPCashfileTransferPaydays>();

            var minDate = DateTime.Today.AddYears(-1);

            if (isLoan == true)
                xfers = _ftpFileOutboundLogRepo
                    .Find(log => log.BenefitAbbr == Constants.BnftDcp && log.Program == Constants.PgmLoanDeduct && log.SourceContributionTotal.HasValue && log.PayDate.HasValue && log.PayDate > minDate)
                    .Select(log => new { PayrollGroup = log.DeferralPayrollGroup, TransDay = log.TransferDate })
                    .AsEnumerable()
                    .GroupBy(log => log.PayrollGroup, log => log.TransDay.Date)
                    .Select(grp => new DCPCashfileTransferPaydays { PayrollGroup = grp.Key, TransDays = grp.Distinct().ToList() })
                    .ToList();

            else
                xfers = _ftpFileOutboundLogRepo
                    .Find(log => log.BenefitAbbr == Constants.BnftDcp && log.Program != Constants.PgmLoanDeduct && log.SourceContributionTotal.HasValue && log.PayDate.HasValue && log.PayDate > minDate)
                    .Select(log => new { PayrollGroup = log.DeferralPayrollGroup, TransDay = log.TransferDate })
                    .AsEnumerable()
                    .GroupBy(log => log.PayrollGroup, log => log.TransDay.Date)
                    .Select(grp => new DCPCashfileTransferPaydays { PayrollGroup = grp.Key, TransDays = grp.Distinct().ToList() })
                    .ToList();

            xfers.ForEach(xfer =>
            {
                xfer.SortOrder = _uow.CreateEFRepository<Payroll>().Find(p => p.DeferralPayrollGroup == xfer.PayrollGroup).Select(p => p.DeferralPayrollGroupSortOrder).FirstOrDefault();
                xfer.TransDays = xfer.TransDays.OrderByDescending(day => day).ToList();
                xfer.TransDayStrs = new List<string>();
                xfer.TransDays.ForEach(day => xfer.TransDayStrs.Add(day.ToString("MM/dd/yyyy")));
            });

            xfers = xfers.OrderBy(xfer => xfer.SortOrder).ToList();

            return xfers;
        }

        public List<FileHistoryPoco> GetPayrollDeductFilesHistory(string payrollGrp, string deductPayDay)
        {

            DateTime deductPay;
            DateTime.TryParse(deductPayDay, out deductPay);
            string backupFolder = ConfigurationManager.AppSettings["TransferBackupFolder"].ToString() + @"\";

            var deductFilesHistory = _ftpFileOutboundLogRepo
                .Find(f => f.BenefitAbbr == Constants.BnftDcp && f.PayDate == deductPay
               && f.SourceFileName.StartsWith(payrollGrp))
               .AsEnumerable()
               .Select(f => new FileHistoryPoco()
                           {
                               PayrollGroup = payrollGrp,
                               DeductPayDay = deductPay,
                               Program = f.Program,
                               FileNamePrefix = f.SourceFileName.Split('_').Take(1).First(),
                               FilePath = backupFolder + f.SourceFileName + "__" + Regex.Replace(f.TransferDate.ToString("yyyy/M/d.HH:mm:ss"), "[:/]", ".", RegexOptions.IgnoreCase),
                               FileSchemaID = GetSchemaID(f.SourceFileName)
                           })
                           .Where(w => w.IsExist == true)
                           .OrderByDescending(o => o.Program)
                           .ToList();

            return deductFilesHistory;
        }        

        private string GetSchemaID(string SourceExportFile)
        {
            var fileSchemaID = _uow.CreateEFRepository<FileExportLog>().FindGraph(f => f.FTPFileFullName == SourceExportFile, x => x.FileExport).Select(s=>s.FileExport.FileSchemaID).FirstOrDefault();
            return fileSchemaID;
        }

        public DefFileHistory GetDeferralFileHistory()
        {
            DefFileHistory defFileHistory = new DefFileHistory();
            string mergedFilesFolder = AppSetting.MergedFilesFolder.Replace("{{folder}}", "DEFF");
            DateTime pastYearDate = DateTime.Today.AddYears(-1);

            defFileHistory.DefFiles = _fileImportLogRepo
                .FindGraph(f =>
                    f.FTPFolder == "DEFF"
                    && f.FTPDate > pastYearDate
                    && f.IsSuccessful == true,
                    x => x.FileImport)
                .AsEnumerable()
                .Select(s => new FileHistoryPoco
                {
                    PayrollId = s.PayrollID,
                    FTPDate = s.FTPDate,
                    FTPFileFullName = s.FTPFileFullName,
                    ProcessDate = s.ProcessDate,
                    PayrollGroup = _payrollRepo.Find(f => f.PayrollID == s.PayrollID).Select(def => def.DeferralPayrollGroup).FirstOrDefault() ?? "",
                    Program = s.FileImport.Program,
                    FileNamePrefix = s.FileImport.FileNamePrefix,
                    FilePath = mergedFilesFolder + @"\" + s.FTPFileFullName + "__" + Regex.Replace(s.ProcessDate.Value.ToString("yyyy.M.d.H:m:s"), "[:/]", ".", RegexOptions.IgnoreCase),
                    FileSchemaID = s.FileImport.FileSchemaID
                })
                .Where(w => w.IsExist == true)
                .OrderBy(o => o.PayrollGroup)
                .ThenBy(t => t.Program)
                .ThenByDescending(o => o.FTPDate)
                .ToList();

            defFileHistory.FTPDates = defFileHistory.DefFiles.OrderByDescending(o => o.FTPDate).Select(s => s.FTPDateStr).Distinct().ToList();
            defFileHistory.Programs = defFileHistory.DefFiles.Select(s => s.Program).Distinct().OrderBy(o => o).ToList();
            defFileHistory.PayrollGroups = defFileHistory.DefFiles.Select(s => s.PayrollGroup).Distinct().OrderBy(o => o).ToList();

            return defFileHistory;
        }

        public List<FileHistoryPoco> GetDailyFilesHistory(string fromDate, string toDate)
        {
            DateTime dailyFromDate;
            DateTime dailyToDate;
            DateTime.TryParse(fromDate, out dailyFromDate);
            DateTime.TryParse(toDate, out dailyToDate);
            dailyToDate = dailyToDate.AddDays(1);

            var dailyFilesHistory = _fileImportLogRepo.FindGraph(
                                    log => log.FTPFolder == "FENRL"
                                    && log.FTPDate >= dailyFromDate
                                    && log.FTPDate < dailyToDate
                                    && log.IsSuccessful == true, x => x.FileImport, x => x.FileImportLogExt)
                                    .Where(w => w.FileImport.FileSchemaID == "fascore.EnrolllmentSchema")
                                    .AsEnumerable()
                                    .Select(s => new FileHistoryPoco()
                                    {
                                        FTPDate = s.FTPDate,
                                        ProcessDate = s.ProcessDate,
                                        Program = s.FileImport.Program,
                                        FTPFileFullName = s.FTPFileFullName,
                                        Indicatives = s.FileImportLogExt != null ? s.FileImportLogExt.FascoreDailyIndicCount ?? 0 : 0,
                                        FundAllocation = s.FileImportLogExt != null ? s.FileImportLogExt.FascoreDailyFundAllocCount ?? 0 : 0,
                                        Beneficiaries = s.FileImportLogExt != null ? s.FileImportLogExt.FascoreDailyBnfCount ?? 0 : 0,
                                        FilePath = AppSetting.MergedFilesFolder.Replace("{{folder}}", s.FTPFolder) + @"\" + s.FTPFileFullName 
                                                   + "__" + Regex.Replace(s.ProcessDate.Value.ToString("yyyy/M/d.H:m:s"), "[:/]", ".", RegexOptions.IgnoreCase),
                                        FileSchemaID = s.FileImport.FileSchemaID
                                    })
                                   .Where(w => w.IsExist == true)
                                   .OrderBy(o => o.ProcessDate)
                                   .ToList();
            return dailyFilesHistory;
        }

        public List<DCPInvestOption> GetInvestmentFunds()
        {
            return _dcpInvestOptionRepo.GetAll().ToList();
        }

        public List<DCPServiceCreditPoco> GetPendingServiceCredits(out int totalRows)
        {
            var distributionPending = _uow.CreateEFRepository<DCPServiceCredit>().GetAll().Where(w => w.StatusCode == (short)DCPServiceCreditStateEnum.Pending)
                                        .AsEnumerable()
                                        .Select(s => new DCPServiceCreditPoco(s, _uow))
                                        .OrderBy(o => o.StatusDate)
                                        .ToList();
            totalRows = distributionPending.Count();
            return distributionPending;
        }

        public List<DCPServiceCreditPoco> GetServiceCreditExport(GridPaginationOptions paginationOpts, out int totalRows)
        {
            DateTime fromDate;
            DateTime toDate;
            DateTime.TryParse(paginationOpts.FromPaydayStr, out fromDate);
            DateTime.TryParse(paginationOpts.ToPaydayStr, out toDate);

            var sCreditExport = _uow.CreateEFRepository<DCPServiceCredit>().GetAll()
                                    .Where(w => w.StatusDate >= fromDate && w.StatusDate <= toDate && 
                                               w.StatusCode == (short)DCPServiceCreditStateEnum.Accepted)
                                    .Join(_uow.CreateEFRepository<Eligibility>().GetAll(), a => a.SocialSecurityNo, b => b.SocialSecurityNo,
                                               (a, b) => new { Eligibility = b, Distribution = a })
                                    .AsEnumerable()
                                    .Select(s => new DCPServiceCreditPoco(s.Distribution,_uow))
                                    .OrderBy(o => o.StatusDate)
                                    .ToList();
            totalRows = sCreditExport.Count();
            return sCreditExport;
        }

        public List<DCPServiceCreditPoco> GetServiceCreditLastRequest(out int totalRows)
        {
            var lastRequestDate = _uow.CreateEFRepository<DCPServiceCredit>().GetAll().Where(w => w.StatusCode == (short)DCPServiceCreditStateEnum.Requested).Max(m => m.StatusDate);


            var distributionPending = _uow.CreateEFRepository<DCPServiceCredit>().GetAll().Where(w => w.StatusCode == (short)DCPServiceCreditStateEnum.Requested && w.StatusDate == lastRequestDate)
                                          .AsEnumerable()
                                          .Select(s => new DCPServiceCreditPoco(s, _uow))
                                          .OrderBy(o => o.StatusDate)
                                          .ToList();
            totalRows = distributionPending.Count();
            return distributionPending;
        }

        public DCPServiceCreditPoco SaveNYCERServiceCreditRequest(string loginUser, DCPServiceCreditPoco dcpDistrb)
        {
            DCPServiceCreditPoco serviceCredit = dcpDistrb;
            new NewDCPServiceCreditState(dcpDistrb, _uow);
            if (dcpDistrb.StatusCode == (short)DCPServiceCreditStateEnum.New)
            {
                serviceCredit.IsSuccess = false;
            }
            else
            {
                DataServiceViewModel dService = new DataServiceViewModel(_uow);
                dService.DCPServiceCreditDS(CRUDEnum.Create, dcpDistrb, loginUser);
                serviceCredit.IsSuccess = true;
            }
            return serviceCredit;
        }

        public DCPCatchUpWrapperPoco SaveDCPCatchUp(string loginUser, DCPCatchUpWrapperPoco data)
        {
            DCPCatchUpPoco dcpCatchUp = data.DCPCatchUp;
            DataServiceResult dsResultSet = null;

            DataServiceViewModel dsDCPCatchUp = new DataServiceViewModel(_uow);
            dsResultSet = dsDCPCatchUp.DCPCatchUpDS(CRUDEnum.Create, dcpCatchUp, loginUser);
            if (dsResultSet.Status == CRUDStatus.Succeed)
            {
                List<CatchUpYearPoco> UtilizeLines = data.UtilizeLines;
                DataServiceViewModel dcCatchUpLines = new DataServiceViewModel(_uow);
                dsResultSet = dcCatchUpLines.CatchUpLinesDS(CRUDEnum.Create, UtilizeLines, loginUser);
            }

            return data;
        }
      
        public DCPCatchUpWrapperPoco GetNewDCPCatchUpDetail(int ssn)
        {
            DCPCatchUpWrapperPoco dcpCatchUp = new DCPCatchUpWrapperPoco();
            dcpCatchUp.DCPEnroll = new DCPEnroll();
            dcpCatchUp.DCPCatchUp = new DCPCatchUpPoco();
            dcpCatchUp.UtilizeLines = new List<CatchUpYearPoco>();

            if (ssn > 0)
            {
                dcpCatchUp.DCPEnroll = _enrollRepo.FindGraph(e => e.SocialSecurityNo == ssn && e.BenefitAbbr == Constants.BnftDcp
                                     && (e.Program == Constants.Pgm457 || e.Program == Constants.Pgm457Roth) 
                                     && e.PlanYear == 0,
                                          x => x.Agency, x => x.Eligibility)
                                     .Select(e => new DCPEnroll
                                     {
                                        SocialSecurityNo = e.SocialSecurityNo,
                                        FirstName = e.Eligibility.FirstName,
                                        MiddleInit = e.Eligibility.MiddleName,
                                        LastName = e.Eligibility.LastName,
                                        AgencyCode = e.Agency.AgencyCode,
                                        Program = e.Program,
                                        Birth = e.Eligibility.Birth
                                      }).OrderBy(o => o.SocialSecurityNo).ThenBy(t => t.Program).FirstOrDefault();

                if (dcpCatchUp.DCPEnroll != null)
                //if (enroll.Count() > 0)
                {
                    //List<string> programs = enroll.Select(s => s.Program).ToList();
                    //bool pgm457 = programs.Equals(Constants.Pgm457) ? true : false;
                    //bool pgm457Roth = programs.Equals(Constants.Pgm457Roth) ? true : false;
                    //.FindGraph(e => e.SocialSecurityNo == ssn && (e.Is457Roth == pgm457 || e.Is457Roth == pgm457Roth))

                    var IsRoth = dcpCatchUp.DCPEnroll.Program == Constants.Pgm457 ? false : true;
                    dcpCatchUp.DCPCatchUp = _dcpCatchUpRepo
                         .FindGraph(e => e.SocialSecurityNo == ssn && (e.Is457Roth == IsRoth))
                         .Select(e => new DCPCatchUpPoco
                         {
                             SocialSecurityNo = e.SocialSecurityNo,
                             FirstName = dcpCatchUp.DCPEnroll.FirstName,
                             LastName = dcpCatchUp.DCPEnroll.LastName,
                             Birth = dcpCatchUp.DCPEnroll.Birth,
                             Is457Roth = e.Is457Roth,
                             AgencyCode = dcpCatchUp.DCPEnroll.AgencyCode,
                             OriginalEffectiveDate = e.OriginalEffectiveDate,
                             PayrollEffectiveDate = e.PayrollEffectiveDate,
                             PayrollEndDate = e.PayrollEndDate,
                             PayCycleCode = e.PayCycleCode,
                             TotalUnderutilizedAmt = e.TotalUnderutilizedAmt,
                             PrevContribAmt = e.PrevContribAmt,
                             WishContribAmt = e.WishContribAmt,
                             PayPeriodCount = e.PayPeriodCount,
                             ContribPerPayPeriod = e.ContribPerPayPeriod
                         }).FirstOrDefault();

                    dcpCatchUp.UtilizeLines = _dcpCatchUpYearRepo.Find(e => e.SocialSecurityNo == ssn)
                                                .AsEnumerable()
                                                .Select(s => new CatchUpYearPoco
                                                {
                                                    SocialSecurityNo = s.SocialSecurityNo,
                                                    CatchUpYear = s.CatchUpYear,
                                                    UnderutilizedContribAmt = s.UnderutilizedContribAmt,
                                                    EffectiveDate = s.EffectiveDate,
                                                    EndDate = s.EndDate,
                                                    ContribAmountPerPay = s.ContribAmountPerPay,
                                                    ChecksCount = s.ChecksCount,
                                                    AnnualMaxContribution = s.AnnualMaxContribution
                                                }).ToList();
                   
                } 
                
                    if (dcpCatchUp.DCPEnroll == null)
                    {
                        dcpCatchUp.DCPEnroll = new DCPEnroll();
                        dcpCatchUp.DCPEnroll.SocialSecurityNo = ssn;
                   }

                    if (dcpCatchUp.DCPCatchUp == null)
                    {
                        dcpCatchUp.DCPCatchUp = new DCPCatchUpPoco();
                        dcpCatchUp.DCPCatchUp.SocialSecurityNo = ssn;
                        dcpCatchUp.DCPCatchUp.FirstName = dcpCatchUp.DCPEnroll != null ? dcpCatchUp.DCPEnroll.FirstName : "";
                        dcpCatchUp.DCPCatchUp.LastName = dcpCatchUp.DCPEnroll != null ? dcpCatchUp.DCPEnroll.LastName : "";
                        dcpCatchUp.DCPCatchUp.Birth = dcpCatchUp.DCPEnroll.Birth;
                        dcpCatchUp.DCPCatchUp.AgencyCode = dcpCatchUp.DCPEnroll != null ? dcpCatchUp.DCPEnroll.AgencyCode : "";
                    dcpCatchUp.DCPCatchUp.Action = "New"; 
                   }
                
            }

            dcpCatchUp.PayCycles = _uow.CreateEFRepository<PayCycle>().GetAll()
                                 .Select(s => new PayCyclesPoco { PayCycleCode = s.PayCycleCode, PayCycleDesc = s.PayCycleDesc, PayDays = s.PayDays, IntervalDays = s.IntervalDays })
                                 .Where(w => w.PayCycleCode == "B" || w.PayCycleCode == "W" || w.PayCycleCode == "S")
                                 .OrderBy(o => o.PayCycleCode).ToList();

            foreach(var paycycle in dcpCatchUp.PayCycles)
            {
                if (paycycle.PayCycleCode == dcpCatchUp.DCPCatchUp.PayCycleCode) {
                    dcpCatchUp.DCPCatchUp.PayCycleDesc = paycycle.PayCycleDesc; }
            }
              
            dcpCatchUp.DeferralGoal = _uow.CreateEFRepository<DeferralGoal>().GetAll()
                                          .Where(w => w.GoalCode == "A" && w.AnnualMaxContribution > 0 && w.Over50AnnualMaxContribution > 0)
                                          .FirstOrDefault();

            return dcpCatchUp;
        }

        public List<PayrollsPoco> GetPayrolls(string agency, DateTime payrolEffectiveDate)
        {
           
            string payrolEffectiveEndDate = payrolEffectiveDate.ToString("MM/dd/yyyy").Substring(6, 4);

            var payrolls = _payCalendarRepo.Find(pc => pc.PayDate >= Convert.ToDateTime(payrolEffectiveDate)
           && pc.PayDate <= Convert.ToDateTime(payrolEffectiveEndDate))
           .Select( s => new PayrollsPoco { Paydate = s.PayDate}).
           ToList();

            return payrolls;
        }

        #endregion

    }

    #region models definition
    public class PayrollsPoco
    {
        public DateTime Paydate { get; set; }
    }

    public class DCPEnrollSearch
    {
        public int SocialSecurityNo { get; set; }
        public string SocialSecurityNoStr
        {
            get
            {
                return SocialSecurityNo > 0 ? string.Format("{0:000-00-0000}", SocialSecurityNo) : string.Empty;
            }
        }
        public string FirstName { get; set; }
        public string MiddleInit { get; set; }
        public string LastName { get; set; }
        public string AgencyBudgetCode
        {
            get
            {
                return AgencyCode + BudgetCode;
            }
        }
        public string Program { get; set; }
        public string AgencyCode { get; set; }
        public string BudgetCode { get; set; }
        
        public DateTime? Birth { get; set; }

        public string BirthStr
        {
            get
            {
                return Birth.HasValue ? Birth.Value.ToString("MM/dd/yyyy") : "";
            }
        }

    }
    
    public class DcpPlansPoco
    {
        public List<EmployeeDcpPlan> Enrolls { get; set; }
        public List<ProgramInclusion> EmpPgrmInclusion { get; set; }
        public List<DeferralGoalPoco> Goals { get; set; }

        public string JsnTooltipHtml { get; set; }

        public class ProgramInclusion
        {
            public string Program { get; set; }
            public string DisplayName { get; set; }
            public bool HasIncluded { get; set; }
            public bool HasEnrolled { get; set; }
        }
    }

    public class EmployeeDcpPlan
    {
        public DCPEligibility Address { get; set; }
        public EnrollmentPoco EnrollInfo { get; set; }
        public List<EnrollmentFundAllocPoco> Funds { get; set; }
        public List<Beneficiary> Beneficiaries { get; set; }
        public List<DCPDisbursementPoco> Disburses { get; set; }
        public DeferralGoalPoco GoalObj { get; set; }

        public class Beneficiary
        {
            public int? BeneficiarySSN { get; set; }
            public string BeneficiarySSNStr
            {
                get
                {
                    return BeneficiarySSN.HasValue ? string.Format("{0:000-00-0000}", BeneficiarySSN) : string.Empty;
                }
            }
            public string Address { get; set; }
            public string Designation { get; set; }
            public string BeneficiaryType { get; set; }
            public string Relationship { get; set; }
            public string FullName { get; set; }
            public decimal? Percentage { get; set; }
            public string PercentageStr
            {
                get
                {
                    return Percentage.HasValue && Percentage.Value > 0 ? Percentage.Value.ToString("G29") + "%" : string.Empty;
                }
            }
        }

    }

    public class DcpPayrollDeductFile
    {
        public DcpPayrollDeductFile()
        {
            DeductFile = string.Empty;
            DeductFilePath = string.Empty;
        }

        public DcpPayrollDeductFile(DcpPayrollDeductFile data)
        {
            PayrollID = data.PayrollID;
            DeferralPayrollGroup = data.DeferralPayrollGroup;
            Program = data.Program;
            LastDeductReqPayDay = data.LastDeductReqPayDay;
            PayDay = data.PayDay;
            CutoffDate = data.CutoffDate;
            DeductFileSchema = data.DeductFileSchema;
            DeductFile = data.DeductFile;
            SortOrder = data.SortOrder;
        }

       
        public string DeferralPayrollGroup { get; set; }
        public short PayrollID { get; set; }
        public string Program { get; set; }
        public DateTime LastDeductReqPayDay { get; set; }
        public string LastDeductReqPayDayStr
        {
            get
            {
                return LastDeductReqPayDay.ToString("MM/dd/yyyy"); 
            }
        }
        public DateTime? PayDay { get; set; } 
        public string PayDayStr
        {
            get
            {
                return PayDay.HasValue ? PayDay.Value.ToString("MM/dd/yyyy") : string.Empty;
            }
        }
        public int DaysToPayday
        {
            get
            {
                return PayDay.HasValue ? Convert.ToInt32((PayDay.Value.Date - DateTime.Now.Date).TotalDays) : 0;
            }
        }
        public DateTime? CutoffDate { get; set; }
        public string CutoffDateStr
        {
            get
            {
                return CutoffDate.HasValue ? CutoffDate.Value.ToString("MM/dd/yyyy") : string.Empty;
            }
        }
        public int DaysToCutoffDate
        {
            get
            {
                return CutoffDate.HasValue ? Convert.ToInt32((CutoffDate.Value.Date - DateTime.Now.Date).TotalDays) : 0;
            }
        }
        public string DeductFileSchema { get; set; }
        public string DeductFile { get; set; }
        public string DeductFilePath { get; set; }
        public byte SortOrder { get; set; }
        public string ErrorMsg { get; set; }
    }

    public class DeferralLine : EnrollmentPoco
    {
        public string LeftZeroPadSSNStr
        {
            get
            {
                return SocialSecurityNo > 0 ? string.Format("{0:000000000}", SocialSecurityNo) : string.Empty;
            }
        }
        public string SubmittingAgency { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string ActionCode { get; set; }
        public string OldAgencyBudgetCode { get; set; }
        public DateTime? OldPayrollEffDate { get; set; }
        public decimal? OldDeductPct { get; set; }
        public short PayrollID { get; set; }
        public bool HasPayrollChanged { get; set; }
        public string ExpirationDate { get; set; }
        public int EnrollmentFundAllocsCount { get; set; }
        public bool Send { get; set; }
        public string Status { get; set; }
        public string DeferralStatusCode { get; set; }
    }

    public class DCPCashfileTransferPaydays
    {
        public string PayrollGroup { get; set; }
        public byte SortOrder { get; set; }
        public List<DateTime> Paydays { get; set; }
        public List<string> PaydayStrs { get; set; }
        public List<DateTime> TransDays { get; set; }
        public List<string> TransDayStrs { get; set; }
    }

    public class FileHistoryPoco
    {
        public int PayrollId { get; set; }
        public string Program { get; set; }
        public string FileNamePrefix { get; set; }
        public string FilePath { get; set; }
        public string FileSchemaID { get; set; }
        public bool IsExist
        {
            get
            {
                return File.Exists(FilePath) ? true : false;
            }
        }

        //Deduct
        public string PayrollGroup { get; set; }
        public DateTime? DeductPayDay { get; set; }
        public string DeductPayDayStr
        {
            get
            {
                return DeductPayDay.HasValue ? DeductPayDay.Value.ToString("MM/dd/yyyy") : string.Empty;
            }
        }

        //Daily
        public DateTime? FTPDate { get; set; }
        public string FTPDateStr
        {
            get
            {
                return FTPDate.HasValue ? FTPDate.Value.ToString("MM/dd/yyyy") : string.Empty;
            }
        }
        public DateTime? ProcessDate { get; set; }
        public string ProcessDateStr
        {
            get
            {
                return ProcessDate.HasValue ? ProcessDate.Value.ToString("MM/dd/yyyy") : string.Empty;
            }
        }
        public string FTPFileFullName { get; set; }
        public int Indicatives { get; set; }
        public int FundAllocation { get; set; }
        public int Beneficiaries { get; set; }
    }

    public class DefFileHistory
    {
        public List<string> FTPDates { get; set; }
        public List<FileHistoryPoco> DefFiles { get; set; }
        public List<string> Programs { get; set; }
        public List<string> PayrollGroups { get; set; }       
    }   


    #endregion

}