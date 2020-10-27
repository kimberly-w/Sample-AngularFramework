namespace Domain.DataAccess.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DCPServiceCredit")]
    public partial class DCPServiceCredit
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int SocialSecurityNo { get; set; }

        [Key]
        [Column(Order = 1)]
        [StringLength(10)]
        public string Program { get; set; }

        [Key]
        [Column(Order = 2)]
        [StringLength(15)]
        public string ReasonCode { get; set; }

        [StringLength(10)]
        public string PensionName { get; set; }

        [StringLength(10)]
        public string PensionNo { get; set; }

        public int? RequestID { get; set; }

        public decimal RequestAmount { get; set; }

        public decimal? DisburseAmount { get; set; }

        [StringLength(10)]
        public string PaymentMethodCode { get; set; }

        public short StatusCode { get; set; }

        [Column(TypeName = "date")]
        public DateTime StatusDate { get; set; }

        public DateTime CreatedDate { get; set; }

        [Required]
        [StringLength(100)]
        public string CreatedFrom { get; set; }

        public DateTime? ModifiedDate { get; set; }

        [StringLength(100)]
        public string ModifiedFrom { get; set; }

        public virtual DisbursementReason DisbursementReason { get; set; }

        public virtual Pension Pension { get; set; }

        public virtual DCPServiceCreditStatu DCPServiceCreditStatu { get; set; }
    }
}
