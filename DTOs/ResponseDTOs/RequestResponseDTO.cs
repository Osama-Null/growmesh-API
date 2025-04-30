using growmesh_API.Models;

namespace growmesh_API.DTOs.ResponseDTOs
{
    public class RequestResponseDTO
    {
        public int RequestId { get; set; }
        public RequestType Type { get; set; }
        public decimal? WithdrawalAmount { get; set; }
        public DateTime RequestDate { get; set; }
        public string? Reason { get; set; }
        public int SavingsGoalId { get; set; }
    }
}