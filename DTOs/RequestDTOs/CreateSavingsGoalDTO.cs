using growmesh_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class CreateSavingsGoalDTO
    {
        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters")]
        public string SavingsGoalName { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TargetAmount { get; set; }

        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Target date must be in the future")]
        public DateTime? TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Lock type is required")]
        public LockType LockType { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Deposit amount must be greater than zero")]
        public decimal? DepositAmount { get; set; }

        public DepositFrequency? DepositFrequency { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Custom deposit interval must be at least 1 day")]
        public int? CustomDepositIntervalDays { get; set; }

        public string? Emoji { get; set; }

        // New fields for initial payment options
        public bool InitialManualPayment { get; set; } = false;
        public bool InitialAutomaticPayment { get; set; } = false;

        [Range(0.01, double.MaxValue, ErrorMessage = "Initial manual payment amount must be greater than zero")]
        public decimal? InitialManualPaymentAmount { get; set; }

        public class LockTypeTargetDateValidationAttribute : ValidationAttribute
        {
            protected override ValidationResult IsValid(object value, ValidationContext validationContext)
            {
                var dto = (CreateSavingsGoalDTO)validationContext.ObjectInstance;
                if (dto.LockType == LockType.TimeBased && !dto.TargetDate.HasValue)
                    return new ValidationResult("Target date is required for TimeBased goals");
                if (dto.LockType == LockType.AmountBased && dto.TargetDate.HasValue)
                    return new ValidationResult("Target date should not be provided for AmountBased goals");
                return ValidationResult.Success;
            }
        }
    }
}