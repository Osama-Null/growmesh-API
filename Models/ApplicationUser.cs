using System.ComponentModel.DataAnnotations;
using growmesh_API.Models.Attributes;
using Microsoft.AspNetCore.Identity;

namespace growmesh_API.Models
{
    public class ApplicationUser : IdentityUser
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

        public string? ProfilePicture { get; set; }
    }
}