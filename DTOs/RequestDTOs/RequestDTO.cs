using growmesh_API.Models;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class RequestDTO
    {
        [Required(ErrorMessage = "Request type is required")]
        public RequestType Type { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Withdrawal amount must be greater than zero")]
        public decimal? WithdrawalAmount { get; set; }

        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }

        [Required(ErrorMessage = "Savings goal ID is required")]
        public int SavingsGoalId { get; set; }
    }
}