using growmesh_API.Models;

namespace growmesh_API.DTOs.ResponseDTOs
{
    public class TransactionDTO
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public TransactionType TransactionType { get; set; }
        public int BankAccountId { get; set; }
        public int? SavingsGoalId { get; set; }
    }
}