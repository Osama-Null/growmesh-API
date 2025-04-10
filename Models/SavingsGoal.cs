using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.Models
{
    public class SavingsGoal
    {
        public int SavingsGoalId { get; set; }
        [Required(ErrorMessage = "Goal name is required")]
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters.")]
        public string SavingsGoalName { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Current amount must be non-negative.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentAmount { get; set; }

        [Required(ErrorMessage = "Target date is required")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Target date must be in the future")]
        public DateTime TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Lock type is required")]
        public LockType LockType { get; set; }

        [Required]
        public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.InProgress;

        [Required]
        [ForeignKey("BankAccount")]
        public int BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        public List<Request> Requests { get; set; } = new List<Request>();
    }
    // ----------------------- Enums -----------------------
    public enum LockType
    {
        TimeBased,
        AmountBased
    }

    public enum SavingsGoalStatus
    {
        InProgress,
        Completed,
        Unlocked
    }
    // ----------------------- Enums -----------------------

    public class FutureDateAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime date)
            {
                if (date <= DateTime.Now)
                {
                    return new ValidationResult(ErrorMessage ?? "Target date must be in the future");
                }
                return ValidationResult.Success;
            }
            return new ValidationResult("Invalid date format");
        }
    }
}