using growmesh_API.Models;

namespace growmesh_API.DTOs.ResponseDTOs
{
    public class SavingsGoalDTO
    {
        public int SavingsGoalId { get; set; }
        public string SavingsGoalName { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public string? Description { get; set; }
        public LockType LockType { get; set; }
        public SavingsGoalStatus Status { get; set; }
        public int BankAccountId { get; set; }
    }
}
