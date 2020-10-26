
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.Identity.Owin;
using Common.Models;
using Domain.DataAccess.ModelsPoco;
using Domain.DataAccess.Validations;
using Domain.Repository;
using BenefitManager.ViewModels;
using Domain.DataAccess.Models;
using System.Linq;
using BenefitManager.StateMachines;
using Common;
using BenefitManager.Models;


namespace BenefitManager.Controllers
{
    public class DCPController : ApiController
    {
        #region member variables and constructors

        private readonly IUnitOfWork _uow;

        public DCPController()
        {
            _uow = HttpContext.Current.GetOwinContext().Get<IUnitOfWork>();
        }

        private IEFRepository<Enrollment> _enrollRepo
        {
            get
            {
                return _uow.CreateEFRepository<Enrollment>();
            }
        }

      
        #endregion

        [HttpGet]
        [Route("api/DCP/GetDashboardAlerts")]
        public HttpResponseMessage GetDashboardAlerts()
        {
            List<MenuItemMapPoco.Alert> alerts = new DCPViewModel(_uow).GetDashboardAlerts();

            return Request.CreateResponse(HttpStatusCode.OK, alerts);
        }

        [HttpPost]
        [Route("api/DCP/GetAllEnrollments")]
        public HttpResponseMessage GetAllEnrollments([FromBody] GridPaginationOptions paginationOptObj)
        {
            int totalRows = 0;

            var enrollsPage = new DCPViewModel(_uow)
                .GetEnrollsGridPage(paginationOptObj, out totalRows);

            return Request.CreateResponse(HttpStatusCode.OK, new { totalRows = totalRows, data = enrollsPage });
        }

        [HttpGet]
        [Route("api/DCP/GetEmployeeDcpPlans/{selectedSSN}")]
        public HttpResponseMessage GetEmployeeDcpPlans(int selectedSSN)
        {
            DcpPlansPoco plans = new DCPViewModel(_uow)
                .GetEmployeeDcpPlans(selectedSSN);

            return Request.CreateResponse(HttpStatusCode.OK, plans);
        }

        [HttpGet]
        [Route("api/DCP/GetPayrollDeductFiles")]
        public HttpResponseMessage GetPayrollDeductFiles()
        {
            List< DcpPayrollDeductFile> newFiles = new DCPViewModel(_uow)
                .GetPayrollDeductFiles();

            return Request.CreateResponse(HttpStatusCode.OK, newFiles);
        }

        [HttpGet]
        [Route("api/DCP/LastProcessedPaydays")]
        public HttpResponseMessage LastProcessedPaydays()
        {
            List<dynamic> data = new DCPViewModel(_uow)
                .LastProcessedPaydays();

            return Request.CreateResponse(HttpStatusCode.OK, data);
        }

        [HttpGet]
        [Route("api/DCP/NotYetProcessedPaydays")]
        public HttpResponseMessage NotYetProcessedPaydays()
        {
            List<DcpPayrollDeductFile> newFiles = new DCPViewModel(_uow).GetPayrollDeductFiles();

            List<DateTime> uniqDays = newFiles
                .Where(df => df.PayDay.HasValue)
                .OrderByDescending(df => df.PayDay.Value)
                .Select(df => df.PayDay.Value)
                .Distinct()
                .ToList();

            return Request.CreateResponse(HttpStatusCode.OK, uniqDays.Select(d => new { PayDay = d, PayDayStr = d.ToString("MM/dd/yyyy") }).ToList<object>());
        }

        [HttpPost]
        [Route("api/DCP/SavePayrollDeductRequest/{fund}")]
        public HttpResponseMessage SavePayrollDeductRequest(EnrollmentPoco enroll, string fund = "")
        {
            DataServiceViewModel dataService = new DataServiceViewModel(_uow);
            DataServiceResult dsResult;
            string dsErr = string.Empty;

            try
            {
                dsResult = dataService.EnrollmentDS(CRUDEnum.Update, enroll, enroll.ModifiedFrom);
                dsErr += dsResult.Status == CRUDStatus.Failed ? ", " + dsResult.Message : string.Empty;

                if (!string.IsNullOrEmpty(fund))
                {
                    EnrollmentFundAlloc fundData = new EnrollmentFundAlloc()
                    {
                        BenefitAbbr = enroll.BenefitAbbr,
                        InvestmentOptionID = fund,
                        Percentage = 100,
                        Program = enroll.Program,
                        SocialSecurityNo = enroll.SocialSecurityNo,
                        TermLength = 0,
                        TermQualifer = "M",
                    };

                    dsResult = dataService.EnrollmentFundAllocDS(CRUDEnum.Create, fundData);
                    dsErr += dsResult.Status == CRUDStatus.Failed ? ", " + dsResult.Message : string.Empty;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, ex);
            }

            // Get new deferral status obj after saving enrollment. Status is changed by a database trigger during saving.  
            DeferralStatusPoco statusObj = new DeferralStatusPoco(_uow.CreateEFRepository<DeferralStatu>().Find(s => s.StatusCode == enroll.DeferralStatusCode).FirstOrDefault());

            return Request.CreateResponse(HttpStatusCode.OK, statusObj);
        }

        [HttpGet]
        [Route("api/DCP/AddEmployeeToPgrm/{ssn}/{pgrm}")]
        public HttpResponseMessage AddEmployeeToPgrm(int ssn, string pgrm)
        {
            new DCPViewModel(_uow)
                .AddEmployeeToPgrm(ssn, pgrm);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("api/DCP/GetReportSSNList/{report}/{paydayStr}/{deductPct}")]
        public HttpResponseMessage GetReportSSNList(string report, string paydayStr, short deductPct)
        {
            List<int> ssns = new DCPViewModel(_uow)
                .GetReportSSNList(report, paydayStr, deductPct);

            var ssnObjs = new List<object>();
            ssns.ForEach(ssn => ssnObjs.Add(new { ssn = ssn, ssnStr = string.Format("{0:000-00-0000}", ssn) }));

            return Request.CreateResponse(HttpStatusCode.OK, ssnObjs);
        }

        [HttpGet]
        [Route("api/DCP/GetCashfileTransferDays/{isLoan}")]
        public HttpResponseMessage GetCashfileTransferDays(bool isLoan)
        {
            List<DCPCashfileTransferPaydays> data = new DCPViewModel(_uow)
                .GetCashfileXferByPayrollGrp(isLoan);

            return Request.CreateResponse(HttpStatusCode.OK, data);
        }

        [HttpGet]
        [Route("api/DCP/DeductFilesAllowRecreate")]
        public HttpResponseMessage DeductFilesAllowRecreate()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new DCPViewModel(_uow).DeductFilesAllowRecreate());
        }

        [HttpGet]
        [Route("api/DCP/GetDeductPayDays/{payrollID}")]
        public HttpResponseMessage GetDeductPayDays([FromUri]short payrollID)
        {
            DateTime startDate = (DateTime.Today).AddMonths(-12);
            DateTime endDate = DateTime.Today;

            List<string> deductPayDays = _uow.CreateEFRepository<DCPDeductRequest>()
                .Find(req => req.PayrollID == payrollID && req.DeductPayday >= startDate && req.DeductPayday <= endDate)
                .Select(req => req.DeductPayday)
                .Distinct()
                .AsEnumerable()
                .OrderByDescending(day => day)
                .Select(day => day.ToString("MM/dd/yyyy"))
                .ToList();

            return Request.CreateResponse(HttpStatusCode.OK, deductPayDays);

        }


        [HttpGet]
        [Route("api/DCP/GetDeductFilesHistory/{payrollGrp}/{deductPayDay}")]
        public HttpResponseMessage GetDeductFilesHistory(string payrollGrp, string deductPayDay)
        {
            var deductFilesHistory = new DCPViewModel(_uow).GetPayrollDeductFilesHistory(payrollGrp, deductPayDay);
            return Request.CreateResponse(HttpStatusCode.OK, deductFilesHistory);
        }

        [HttpGet]
        [Route("api/DCP/GetDeferralFileHistory")]
        public HttpResponseMessage GetDeferralFileHistory()
        {
            var deferralFileHistory = new DCPViewModel(_uow).GetDeferralFileHistory();
            return Request.CreateResponse(HttpStatusCode.OK, deferralFileHistory);
        }

        [HttpGet]
        [Route("api/DCP/GetDailyFilesHistory/{fromDate}/{toDate}")]
        public HttpResponseMessage GetDailyFilesHistory([FromUri]string fromDate, [FromUri]string toDate)
        {
            var dailyFilesHistory = new DCPViewModel(_uow).GetDailyFilesHistory(fromDate, toDate);
            return Request.CreateResponse(HttpStatusCode.OK, dailyFilesHistory);
        }

        [HttpGet]
        [Route("api/DCP/GetInvestmentFunds")]
        public HttpResponseMessage GetInvestmentFunds()
        {
            var investmentFunds = new DCPViewModel(_uow).GetInvestmentFunds();
            return Request.CreateResponse(HttpStatusCode.OK, investmentFunds);
        }

        [HttpGet]
        [Route("api/DCP/GetPendingServiceCredits")]
        public HttpResponseMessage GetPendingServiceCredits()
        {
            int totalRows = 0;
            var ServiceCreditPending = new DCPViewModel(_uow).GetPendingServiceCredits(out totalRows);
            return Request.CreateResponse(HttpStatusCode.OK, new { totalRows = totalRows, data = ServiceCreditPending });
        }

        [HttpGet]
        [Route("api/DCP/GetServiceCreditLastRequest")]
        public HttpResponseMessage GetServiceCreditLastRequest()
        {
            int totalRows = 0;
            var ServiceCreditLastRequest = new DCPViewModel(_uow).GetServiceCreditLastRequest(out totalRows);
            return Request.CreateResponse(HttpStatusCode.OK, new { totalRows = totalRows, data = ServiceCreditLastRequest });
        }

        [HttpPost]
        [Route("api/DCP/GetServiceCreditExport")]
        public HttpResponseMessage GetServiceCreditExport([FromBody] GridPaginationOptions paginationOpts)
        {
            int totalRows = 0;
            var ServiceCreditExport = new DCPViewModel(_uow).GetServiceCreditExport(paginationOpts, out totalRows);
            return Request.CreateResponse(HttpStatusCode.OK, new { totalRows = totalRows, data = ServiceCreditExport });
        }

        [HttpPost]
        [Route("api/DCP/ProcessServiceCreditRequest/{userName}/{process}")]
        public HttpResponseMessage ProcessServiceCreditRequest(string userName, string process, [FromBody]List<DCPServiceCreditPoco> serviceCreditRequest)
        {
            //List<JObject> serverData = new List<JObject>();
            List<ExportedFileModel> fileDetails = new List<ExportedFileModel>();
            foreach (DCPServiceCreditPoco dist in serviceCreditRequest)
            {
                dist.StatusCode = process == Constants.ProcDCPServiceCreditRequest ? (short)DCPServiceCreditStateEnum.Requested : (short)DCPServiceCreditStateEnum.Pending;
                dist.StatusDate = process == Constants.ProcDCPServiceCreditRequest ? DateTime.Now : dist.CreatedDate;
                new DataServiceViewModel(_uow).DCPServiceCreditDS(CRUDEnum.Update, dist, userName);
                if (process == Constants.ProcDCPServiceCreditRequest)
                {
                    new NewDCPServiceCreditState(dist, _uow);
                    //serverData.Add(JObject.FromObject(dist));
                }
            }

            if (process == Constants.ProcDCPServiceCreditRequest)
            {
                ExportParameters exportParamaeter = new ExportParameters();
                exportParamaeter.BenefitAbbr = Constants.BnftDcp;
                exportParamaeter.Program = Constants.PgmBuyBack;
                fileDetails = new ExportViewModel().Export("fascore.SCreditReqSchema", serviceCreditRequest, true,exportParamaeter);
                return Request.CreateResponse(HttpStatusCode.OK, new { alert = "Success", isSuccess = true,data=fileDetails });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { alert = "Success", isSuccess = true });
            }
        }


        [HttpPost]
        [Route("api/DCP/SaveNYCERServiceCreditRequest/{loginUser}")]
        public HttpResponseMessage SaveNYCERServiceCreditRequest([FromUri]string loginUser, [FromBody]DCPServiceCreditPoco data)
        {
            var serviceCredit= new DCPViewModel(_uow).SaveNYCERServiceCreditRequest(loginUser, data);

            return Request.CreateResponse(HttpStatusCode.OK, new { data=serviceCredit });
        }

        [HttpGet]
        [Route("api/DCP/GetNewServiceCredit")]
        public HttpResponseMessage GetNewServiceCredit()
        {
            DCPServiceCreditPoco serviceCredit = new DCPServiceCreditPoco();
            new NewDCPServiceCreditState(serviceCredit, _uow);

            return Request.CreateResponse(HttpStatusCode.OK, serviceCredit);
        }

        [HttpGet]
        [Route("api/DCP/GetNewDCPCatchUp/{ssn}")]
        public HttpResponseMessage GetNewDCPCatchUp(int ssn)
        {
            var dcpCatchUp = new DCPViewModel(_uow).GetNewDCPCatchUpDetail(ssn);

            return Request.CreateResponse(HttpStatusCode.OK, dcpCatchUp);
        }
      
        
        [HttpPost]
        [Route("api/DCP/SaveDCPCatchUp/{loginUser}")]
        public HttpResponseMessage SaveDCPCatchUp([FromUri]string loginUser, [FromBody]DCPCatchUpWrapperPoco data)
        {
            var dcpCatchUp = new DCPViewModel(_uow).SaveDCPCatchUp(loginUser, data);

            return Request.CreateResponse(HttpStatusCode.OK, dcpCatchUp);
        }


        [HttpGet]
        [Route("api/DCP/GetDcpMismatchAgencies")]
        public HttpResponseMessage GetDcpMismatchAgencies()
        {
            var mismatchAgencies = _uow.CreateEFRepository<vwDCPAgencyMisMatch>().GetAll().Select(s => s).ToList();

            return Request.CreateResponse(HttpStatusCode.OK,new { data = mismatchAgencies,totalRows = mismatchAgencies.Count() });
        }

        [HttpGet]
        [Route("api/DCP/GetPayrolls/{agency}/{payrollEffectiveDate}")]
        public HttpResponseMessage GetPayrolls([FromUri]string agency, [FromUri]DateTime payrollEffectiveDate)
        {

            var payrolls = new DCPViewModel(_uow).GetPayrolls(agency, payrollEffectiveDate);
     
            return Request.CreateResponse(HttpStatusCode.OK, new { data = payrolls });
        }

    }
}