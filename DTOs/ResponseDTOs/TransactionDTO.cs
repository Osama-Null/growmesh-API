namespace growmesh_API.DTOs.ResponseDTOs
{
    public class TransactionDTO
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public Type Type { get; set; }
        public int AccountId { get; set; }
        public int SavingsGoalId { get; set; }
    }
}
