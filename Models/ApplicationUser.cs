using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace growmesh_API.Models
{
    public class ApplicationUser: IdentityUser
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}
