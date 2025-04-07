using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.Models
{
    public class BankAccount
    {
        public int AccountId { get; set; }
        [StringLength(10, ErrorMessage = "Account number cannot exceed 10 characters.")]
        public string AccountNumber { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Balance must be a positive value.")]
        public decimal Balance { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser? User { get; set; }
    }
}
