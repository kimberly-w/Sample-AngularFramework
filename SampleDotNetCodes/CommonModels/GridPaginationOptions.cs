using System;
using System.Collections.Generic;

namespace Common.Models
{
    public class GridPaginationOptions
    {
        public int PageNo { get; set; }
        public int PageSize { get; set; }
        public List<Filter> Filters { get; set; }
        public List<SortDescription> Sorts { get; set; }
        public int PlanYear { get; set; }
        public string BenefitAbbr { get; set; }
        public string[] Programs { get; set; }
        public string AgencyCode { get; set; }
        public short PayrollID { get; set; }
        public string PayrollGroupName { get; set; }
        public string Source { get; set; }
        public string WelfareFundCode { get; set; }
        public string Cycle { get; set; }
        public string CycleTo { get; set; }

        public string FileSchemaID { get; set; }

        public string FilePath { get; set; }


        public DateTime? MscWaiverPeriodStart { get; set; }
        string _periodStartStr;
        public string MscWaiverPeriodStartStr
        {
            get
            {
                _periodStartStr = MscWaiverPeriodStart.HasValue ? MscWaiverPeriodStart.Value.ToString("MM/dd/yyyy") : string.Empty;
                return _periodStartStr;
            }
            set
            {
                if (_periodStartStr != value)
                {
                    _periodStartStr = value;
                    DateTime date;
                    if (DateTime.TryParse(_periodStartStr, out date))
                        MscWaiverPeriodStart = date;
                    else
                        MscWaiverPeriodStart = null;
                }
            }
        }

        public DateTime? MscWaiverPeriodEnd { get; set; }
        string _periodEndStr;
        public string MscWaiverPeriodEndStr
        {
            get
            {
                _periodEndStr = MscWaiverPeriodEnd.HasValue ? MscWaiverPeriodEnd.Value.ToString("MM/dd/yyyy") : string.Empty;
                return _periodEndStr;
            }
            set
            {
                if (_periodEndStr != value)
                {
                    _periodEndStr = value;
                    DateTime date;
                    if (DateTime.TryParse(_periodEndStr, out date))
                        MscWaiverPeriodEnd = date;
                    else
                        MscWaiverPeriodEnd = null;
                }
            }
        }

        public DateTime? PaymentDate { get; set; }
        string _paymentDateStr;
        public string PaymentDateStr
        {
            get
            {
                _paymentDateStr = PaymentDate.HasValue ? PaymentDate.Value.ToString("MM/dd/yyyy") : string.Empty;
                return _paymentDateStr;
            }
            set
            {
                if (_paymentDateStr != value)
                {
                    _paymentDateStr = value;
                    DateTime date;
                    if (DateTime.TryParse(_paymentDateStr, out date))
                        PaymentDate = date;
                    else
                        PaymentDate = null;
                }
            }
        }

        // MockPlanEffectDate is rarely used. It is used only when users want to override plan real effective date
        // in the exported file only. No effect on the plan effective date in the database.
        public DateTime? MockPlanEffectDate { get; set; }
        string _mockPlanEffectDateStr;
        public string MockPlanEffectDateStr
        {
            get
            {
                _mockPlanEffectDateStr = MockPlanEffectDate.HasValue ? MockPlanEffectDate.Value.ToString("MM/dd/yyyy") : string.Empty;
                return _mockPlanEffectDateStr;
            }
            set
            {
                if (_mockPlanEffectDateStr != value)
                {
                    _mockPlanEffectDateStr = value;
                    DateTime date;
                    if (DateTime.TryParse(_mockPlanEffectDateStr, out date))
                        MockPlanEffectDate = date;
                    else
                        MockPlanEffectDate = null;
                }
            }
        }

        public string XferFile { get; set; }
        public string FromPaydayStr { get; set; }       // Must in MM/dd/yyyy format
        public string ToPaydayStr { get; set; }         // Must in MM/dd/yyyy format

        public object Header { get; set; }
        public List<string> Fields { get; set; }
        public List<string> Names { get; set; }

        public int MetlifeScheduleID { get; set; }
    }
}
