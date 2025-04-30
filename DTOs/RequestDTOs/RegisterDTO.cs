using growmesh_API.Models.Attributes;
using System.ComponentModel.DataAnnotations;

namespace growmesh_API.DTOs.RequestDTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "*Enter first name")]
        [StringLength(10, ErrorMessage = "First name cannot exceed 10 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "*Enter last name")]
        [StringLength(10, ErrorMessage = "Last name cannot exceed 10 characters")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "*Select date of birth")]
        [MinimumAge(18, ErrorMessage = "You must be at least 18 years old")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "*Enter email")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "*Enter password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
        public string Password { get; set; }

        [Required(ErrorMessage = "*Re-enter password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
        [Compare("Password", ErrorMessage = "*Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "*Enter phone number")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Phone number must be 8 characters")]
        public string Phone { get; set; }

        public IFormFile? ProfilePicture { get; set; }
    }
}