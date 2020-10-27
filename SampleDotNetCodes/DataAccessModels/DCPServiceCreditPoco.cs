using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.DataAccess.Models;
using Domain.Repository;

namespace Domain.DataAccess.ModelsPoco
{
    public class DCPServiceCreditPoco
    {

        public DCPServiceCreditPoco()
        { }
        public DCPServiceCreditPoco(DCPServiceCredit db) : this()
        {
            if (db != null)
            {
                SocialSecurityNo = db.SocialSecurityNo;
                Program = db.Program;
                ReasonCode = db.ReasonCode;
                PensionNo = db.PensionNo;
                RequestAmount = db.RequestAmount;
                DisburseAmount = db.DisburseAmount;
                StatusCode = db.StatusCode;
                StatusDate = db.StatusDate;
                CreatedDate = db.CreatedDate;
            }
        }
        public DCPServiceCreditPoco(DCPServiceCredit db, IUnitOfWork uow) : this(db)
        {
            var elig = uow.CreateEFRepository<Eligibility>().GetAll().Where(w => w.SocialSecurityNo == db.SocialSecurityNo).FirstOrDefault();
            FirstName = elig == null ? "" : elig.FirstName;
            LastName  = elig == null ? "" : elig.LastName;
        }
        public int SocialSecurityNo { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string SocialSecurityNoStr
        {
            get
            {
                return SocialSecurityNo > 0 ? string.Format("{0:000-00-0000}", SocialSecurityNo) : string.Empty;
            }
        }
        public string Program { get; set; }
        public string ReasonCode { get; set; }
        public string FullName
        {

            get
            {
                return LastName + ", " + FirstName;
            }

        }
        public short StatusCode { get; set; }
        public string PensionNo { get; set; }
        public string PensionName { get; set; }
        public string PensionAddr { get; set; }
        public decimal RequestAmount { get; set; }
        public string RequestAmountStr
        {
            get
            {
                return RequestAmount > 0 ? RequestAmount.ToString("N2") : string.Empty;
            }
        }
        public DateTime StatusDate { get; set; }
        public string StatusDateStr
        {
            get
            {
                return StatusDate.ToString("MM/dd/yyyy");
            }
        }
        public decimal? DisburseAmount { get; set; }
        public string DisburseAmountStr
        {
            get
            {
                return DisburseAmount.HasValue ? DisburseAmount.Value.ToString("N2") : string.Empty;
            }
        }
        public string PaymentMethodCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public string StateName { get; set; }
        public List<BenefitProgramPoco> Plans { get; set; }
        public List<string> PaymentMethods { get; set; }
        public string FascoreGroupAcct { get; set; }
        public bool IsSuccess { get; set; }
    }
}

