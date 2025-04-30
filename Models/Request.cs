using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.Models
{
    public class Request
    {
        public int RequestId { get; set; }

        [Required]
        public RequestType Type { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? WithdrawalAmount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string? Reason { get; set; }

        [Required]
        [ForeignKey("SavingsGoal")]
        public int SavingsGoalId { get; set; }
        public SavingsGoal? SavingsGoal { get; set; }
    }

    public enum RequestType
    {
        Unlock,
        PartialWithdrawal,
        DeleteGoal
    }
}