using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growmesh_API.Models
{
    public class UnlockRequest
    {
        public int UnlockRequestId { get; set; }

        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required]
        public bool IsEarlyUnlock { get; set; } = false;

        [Required]
        public Status Status { get; set; }

        [Required]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string? Reason { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? RejectedDate { get; set; }

        [Required]
        [ForeignKey("SavingsGoal")]
        public int SavingsGoalId { get; set; }
        public SavingsGoal? SavingsGoal { get; set; }

        [Required]
        [ForeignKey("AdminId")]
        public ApplicationUser? Admin { get; set; }
        public string? AdminId { get; set; }
    }

    // ----------------------- Enums -----------------------
    public enum Status
    {
        Pending,
        Approved,
        Rejected
    }
    // ----------------------- Enums -----------------------
}
