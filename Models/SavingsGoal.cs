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
        public decimal? TargetAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Current amount must be non-negative.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentAmount { get; set; }

        public string? Emoji { get; set; }

        public string? Color { get; set; }

        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Target date must be in the future")]
        public DateTime? TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        // ------------- Custom intervals && automatic deposits -------------
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DepositAmount { get; set; }

        public DepositFrequency? DepositFrequency { get; set; }

        public int? CustomDepositIntervalDays { get; set; }

        public DateTime? LastDepositDate { get; set; }
        // ------------- Custom intervals && automatic deposits -------------

        [Required(ErrorMessage = "Lock type is required")]
        public LockType LockType { get; set; }

        [Required]
        public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.InProgress;

        [Required]
        [ForeignKey("BankAccount")]
        public int BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        public List<Request> Requests { get; set; } = new List<Request>();
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();

        // ------------- For Chart new -------------
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Goal creation date

        [DataType(DataType.DateTime)]
        public DateTime? CompletedAt { get; set; } // Goal completion date, nullable

        public bool InitialManualPayment { get; set; } = false; // Option for initial manual payment
        public bool InitialAutomaticPayment { get; set; } = false; // Option for initial automatic payment

        // ------------- New field for soft delete -------------
        [DataType(DataType.DateTime)]
        public DateTime? DeletedAt { get; set; }
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
        MarkDone,
        Completed,
        Unlocked
    }

    public enum DepositFrequency
    {
        Monthly,
        Weekly,
        Custom
    }
    // ----------------------- Enums -----------------------

    public class FutureDateAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success; // Nullable, so null is valid
            }

            if (value is DateTime date && date <= DateTime.UtcNow)
            {
                return new ValidationResult(ErrorMessage ?? "Target date must be in the future");
            }
            return ValidationResult.Success;
        }
    }
}