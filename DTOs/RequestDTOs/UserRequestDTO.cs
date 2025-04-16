using growmesh_API.Models.Attributes;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class UserRequestDTO
    {
        [Required(ErrorMessage = "*Enter email")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "*Enter phone number")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Phone number must be 8 characters")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "*Enter password")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public IFormFile? ProfilePicture { get; set; }
    }
}
