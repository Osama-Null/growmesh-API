namespace growmesh_API.DTOs.ResponseDTOs
{
    public class AuthResponseDTO
    {
        public string Token { get; set; }
        public DateTime Expiration { get; set; }
        public string UserId { get; set; }
    }
}