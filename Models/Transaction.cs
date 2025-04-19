using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growmesh_API.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [Required]
        public TransactionType Type { get; set; }

        [Required]
        [ForeignKey("BankAccount")]
        public int BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        [ForeignKey("SavingsGoal")]
        public int? SavingsGoalId { get; set; }
        public SavingsGoal? SavingsGoal { get; set; }
    }
    // ----------------------- Enums -----------------------
    public enum TransactionType
    {
        Deposit,
        Withdrawal,
        TransferToGoal,
        TransferFromGoal
    }
    // ----------------------- Enums -----------------------
}