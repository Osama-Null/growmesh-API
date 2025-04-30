using growmesh_API.Models;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class CreateTimeBasedSavingsGoalDTO
    {
        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters")]
        public string SavingsGoalName { get; set; }

        [Required(ErrorMessage = "Target date is required")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Target date must be in the future")]
        public DateTime TargetDate { get; set; } // Non-nullable, required for TimeBased

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Deposit amount must be greater than zero")]
        public decimal? DepositAmount { get; set; }

        public DepositFrequency? DepositFrequency { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Custom deposit interval must be at least 1 day")]
        public int? CustomDepositIntervalDays { get; set; }

        public string? Emoji { get; set; }

        public string? Color { get; set; }

        public bool InitialManualPayment { get; set; } = false;
        public bool InitialAutomaticPayment { get; set; } = false;

        [Range(0.01, double.MaxValue, ErrorMessage = "Initial manual payment amount must be greater than zero")]
        public decimal? InitialManualPaymentAmount { get; set; }
    }
}
