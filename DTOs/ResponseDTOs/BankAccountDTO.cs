namespace growmesh_API.DTOs.ResponseDTOs
{
    public class BankAccountDTO
    {
        public int BankAccountId { get; set; }
        public decimal Balance { get; set; }
        public List<SavingsGoalDTO> SavingsGoals { get; set; }
        public string UserId { get; set; }
    }
}