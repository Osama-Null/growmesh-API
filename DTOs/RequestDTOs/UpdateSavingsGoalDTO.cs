using growmesh_API.Models;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class UpdateSavingsGoalDTO
    {
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters")]
        public string? SavingsGoalName { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero")]
        public decimal? TargetAmount { get; set; }

        public DateTime? TargetDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public LockType? LockType { get; set; }
    }
}