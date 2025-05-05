using growmesh_API.Models;

namespace growmesh_API.DTOs.ResponseDTOs
{
    public class SavingsGoalDTO
    {
        public int SavingsGoalId { get; set; }
        public string SavingsGoalName { get; set; }
        public decimal? TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; } 
        public DateTime? TargetDate { get; set; }
        public string? Description { get; set; }
        public LockType LockType { get; set; }
        public SavingsGoalStatus Status { get; set; }
        public int BankAccountId { get; set; }
        public decimal? DepositAmount { get; set; }
        public DepositFrequency? DepositFrequency { get; set; }
        public int? CustomDepositIntervalDays { get; set; }
        public string? Emoji { get; set; }
        public string? Color { get; set; }

        // New fields
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool InitialManualPayment { get; set; }
        public bool InitialAutomaticPayment { get; set; }

        // New field
        public DateTime? DeletedAt { get; set; }
    }
}