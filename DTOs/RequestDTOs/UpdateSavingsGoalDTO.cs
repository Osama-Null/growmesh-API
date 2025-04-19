using growmesh_API.Models;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class UpdateSavingsGoalDTO
    {
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters")]
        public string? SavingsGoalName { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero")]
        public decimal? TargetAmount { get; set; }

        public DateTime? TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public LockType? LockType { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Deposit amount must be greater than zero")]
        public decimal? DepositAmount { get; set; }

        public DepositFrequency? DepositFrequency { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Custom deposit interval must be at least 1 day")]
        public int? CustomDepositIntervalDays { get; set; }
        public string? Emoji { get; set; }

        public class LockTypeTargetDateValidationAttribute : ValidationAttribute
        {
            protected override ValidationResult IsValid(object value, ValidationContext validationContext)
            {
                var dto = (UpdateSavingsGoalDTO)validationContext.ObjectInstance;
                if (dto.LockType.HasValue)
                {
                    if (dto.LockType == Models.LockType.TimeBased && !dto.TargetDate.HasValue)
                        return new ValidationResult("Target date is required for TimeBased goals");
                    if (dto.LockType == Models.LockType.AmountBased && dto.TargetDate.HasValue)
                        return new ValidationResult("Target date should not be provided for AmountBased goals");
                }
                return ValidationResult.Success;
            }
        }
    }
}