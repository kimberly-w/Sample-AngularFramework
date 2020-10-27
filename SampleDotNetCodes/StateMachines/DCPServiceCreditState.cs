using System;
using System.Collections.Generic;
using System.Linq;
using BenefitManager.ViewModels;
using BenefitManager.StateMachines;
using Domain.DataAccess.ModelsPoco;
using Domain.DataAccess.Validations;
using Domain.Repository;
using Common;
using Domain.DataAccess.Models;
using Newtonsoft.Json.Linq;

namespace BenefitManager.StateMachines
{
    //New = -1,Pending(OLR), Requested(OLR->Fascorp), Accepted(Fascore2OLR),
    //OLR a required form, sends the form to FAScorp, FASCorp sends to NYCERS, FAScrop notify OLR
    public abstract class DCPServiceCreditState
    {
        public DCPServiceCreditPoco ServiceCredit { get; set; }
        public IUnitOfWork uow { get; set; }
        public abstract void GetState();
        protected DCPServiceCreditState(DCPServiceCreditPoco serviceCredit, IUnitOfWork uow)
        {
            this.uow = uow;
            this.ServiceCredit = serviceCredit;
            this.ServiceCredit.ReasonCode = Constants.ServiceCreditReasonCode;
            this.ServiceCredit.PensionName = Constants.ServiceCreditPensionName;
            this.ServiceCredit.PensionAddr = uow.CreateEFRepository<Pension>().GetAll()
                                                .Where(w => w.PensionName == Constants.ServiceCreditPensionName)
                                                .Select(s => (s.ContactName ?? "") + "," + (s.Address ?? "") + "," + (s.City ?? "") + "," + (s.StateAbbr ?? "") + "," + (s.Zip ?? ""))
                                                .FirstOrDefault();
            this.ServiceCredit.Plans = uow.CreateEFRepository<BenefitProgram>()
                                           .GetAll()
                                           .Where(w => w.Program == Constants.Pgm401K || w.Program == Constants.Pgm457)
                                           .Select(s => new BenefitProgramPoco { Program = s.Program, FascoreGroupAcct = s.FascoreGroupAcct })
                                           .ToList();
            this.ServiceCredit.PaymentMethods = uow.CreateEFRepository<PaymentMethod>().GetAll()
                                                        .Select(s => s.PaymentMethodCode)
                                                        .ToList();
            GetState();
        }
    }
    public class NewDCPServiceCreditState : DCPServiceCreditState
    {
        public NewDCPServiceCreditState(DCPServiceCreditPoco serviceCredit, IUnitOfWork uow) : base(serviceCredit, uow) { }
        public override void GetState()
        {
            if (this.ServiceCredit.SocialSecurityNo > 0   && this.ServiceCredit.Program != null  && this.ServiceCredit.ReasonCode != null)
            {
                MoveToNextState();
            }
            else
            {
                this.ServiceCredit.StatusDate = DateTime.Now;
                this.ServiceCredit.StatusCode = (short)DCPServiceCreditStateEnum.New;
                this.ServiceCredit.StateName = ((DCPServiceCreditStateEnum)(int)this.ServiceCredit.StatusCode).ToString();
            }

        }
        private void MoveToNextState()
        {
            new PendingDCPServiceCreditState(this.ServiceCredit,uow);
        }
    }
    public class PendingDCPServiceCreditState : DCPServiceCreditState
    {
        public PendingDCPServiceCreditState(DCPServiceCreditPoco serviceCredit, IUnitOfWork uow) : base(serviceCredit,uow) { }

        public override void GetState()
        {
            if (this.ServiceCredit.StatusCode == (short)DCPServiceCreditStateEnum.New || 
                (this.ServiceCredit.StatusCode == (short)DCPServiceCreditStateEnum.Pending && this.ServiceCredit.RequestAmount > 0 && this.ServiceCredit.DisburseAmount == 0))
            {
                this.ServiceCredit.StatusDate = DateTime.Now;
                this.ServiceCredit.StatusCode = (short)DCPServiceCreditStateEnum.Pending;
                this.ServiceCredit.StateName = ((DCPServiceCreditStateEnum)(int)this.ServiceCredit.StatusCode).ToString();
            }
            else
            {
                MoveToNextState();
            }
        }
        private void MoveToNextState()
        {
            new RequestedDCPServiceCreditState(this.ServiceCredit,uow);
        }

    }
    public class RequestedDCPServiceCreditState : DCPServiceCreditState
    {
        public RequestedDCPServiceCreditState(DCPServiceCreditPoco serviceCredit, IUnitOfWork uow) : base(serviceCredit,uow) { }

        public override void GetState()
        {
            if (this.ServiceCredit.StatusCode == (short)DCPServiceCreditStateEnum.Requested && this.ServiceCredit.RequestAmount > 0 && this.ServiceCredit.DisburseAmount == 0)
            {
                this.ServiceCredit.StatusCode = (short)DCPServiceCreditStateEnum.Requested;
                this.ServiceCredit.StateName = ((DCPServiceCreditStateEnum)(int)this.ServiceCredit.StatusCode).ToString();
            }
            else
            {
                MoveToNextState();
            }
        }
        private void MoveToNextState()
        {
            new AcceptedDCPServiceCreditState(this.ServiceCredit,uow);
        }

    }
    public class AcceptedDCPServiceCreditState : DCPServiceCreditState
    {
        public AcceptedDCPServiceCreditState(DCPServiceCreditPoco serviceCredit, IUnitOfWork uow) : base(serviceCredit, uow) { }
        public override void GetState()
        {
            if (this.ServiceCredit.StatusCode == (short)DCPServiceCreditStateEnum.Accepted && this.ServiceCredit.DisburseAmount > 0)
            {
                this.ServiceCredit.StatusCode = (short)DCPServiceCreditStateEnum.Accepted;
                this.ServiceCredit.StateName = ((DCPServiceCreditStateEnum)(int)this.ServiceCredit.StatusCode).ToString();
            }
        }
    }
}


