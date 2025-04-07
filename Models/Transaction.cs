using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growmesh_API.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        public Type Type { get; set; }

        [Required]
        [ForeignKey("BankAccount")]
        public int AccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        [Required]
        [ForeignKey("SavingsGoal")]
        public int SavingsGoalId { get; set; }
        public SavingsGoal? SavingsGoal { get; set; }
    }
    // ----------------------- Enums -----------------------
    public enum Type
    {
        Deposit,
        Withdrawal,
        TransferToGoal,
        TransferFromGoal,
    }
    // ----------------------- Enums -----------------------
}
