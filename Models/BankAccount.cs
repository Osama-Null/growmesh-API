using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.Models
{
    public class BankAccount
    {
        public int BankAccountId { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Balance must be a positive value.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        public List<SavingsGoal> SavingsGoals { get; set; } = new List<SavingsGoal>();

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser? User { get; set; }
    }
}