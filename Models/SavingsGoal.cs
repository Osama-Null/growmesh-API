using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.Models
{
    public class SavingsGoal
    {
        public int SavingsGoalId { get; set; }
        [Required]
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters.")]
        public string SavingsGoalName { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero.")]
        public decimal TargetAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Current amount must be non-negative.")]
        public decimal CurrentAmount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required]
        public LockType LockType { get; set; }

        public bool IsCompleted { get; set; } = false;

        public bool IsUnlocked { get; set; } = false;

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser? User { get; set; }
    }
    // ----------------------- Enums -----------------------
    public enum LockType
    {
        TimeBased,
        AmountBased
    }
    // ----------------------- Enums -----------------------
}
