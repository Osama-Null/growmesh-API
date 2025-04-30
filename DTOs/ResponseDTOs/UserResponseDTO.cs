namespace growmesh_API.DTOs.ResponseDTOs
{
    public class UserResponseDTO
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Phone { get; set; }
        public string? ProfilePicture { get; set; }
    }
}
