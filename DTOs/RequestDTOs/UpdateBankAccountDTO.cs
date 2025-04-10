using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class UpdateBankAccountDTO
    {
        [Range(0, double.MaxValue, ErrorMessage = "Balance must be a positive value")]
        public decimal Balance { get; set; }
    }
}