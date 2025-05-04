using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using growmesh_API.Data;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using growmesh_API.Models;
using growmesh_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace growmesh_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;
        private readonly LlamaService _llamaService;

        public UserController(UserManager<ApplicationUser> userManager, IConfiguration configuration, ApplicationDbContext db, LlamaService llamaService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _db = db;
            _llamaService = llamaService;
        }

        // GET: api/User/profile
        [HttpGet("profile")]
        public async Task<ActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";

            var userDto = new UserResponseDTO
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth,
                Phone = user.PhoneNumber,
                ProfilePicture = string.IsNullOrEmpty(user.ProfilePicture)
                    ? null 
                    :$"{baseUrl}{user.ProfilePicture}"
            };

            return Ok(userDto);
        }

        // PUT: api/User/profile
        [HttpPut("edit-profile")]
        public async Task<ActionResult<UserResponseDTO>> UpdateProfile([FromForm] UserRequestDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            // Verify password
            if (string.IsNullOrEmpty(model.Password) || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return BadRequest(new { Errors = new[] { "Invalid password" } });
            }

            // Handle profile picture upload
            string imagePath = user.ProfilePicture;
            if (model.ProfilePicture != null)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfilePicture.FileName);
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePicture.CopyToAsync(stream);
                }
                imagePath = $"/images/{fileName}";

                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture) && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/'))))
                {
                    System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/')));
                }
            }

            // Update user fields only if provided
            if (!string.IsNullOrEmpty(model.Email))
            {
                user.Email = model.Email;
                user.UserName = model.Email; // Ensure UserName is updated to match Email (Identity requirement)
            }

            if (!string.IsNullOrEmpty(model.Phone))
            {
                user.PhoneNumber = model.Phone;
            }

            user.ProfilePicture = imagePath; // Update ProfilePicture regardless, since imagePath defaults to the existing value if no new file is uploaded

            // Validate the updated user
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(user);
            bool isValid = Validator.TryValidateObject(user, validationContext, validationResults, true);

            if (!isValid)
            {
                if (imagePath != null && imagePath != user.ProfilePicture && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'))))
                {
                    System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/')));
                }
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
                return BadRequest(new { Errors = errors });
            }

            // Update user in database
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                if (imagePath != null && imagePath != user.ProfilePicture && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'))))
                {
                    System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/')));
                }
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
            }

            // Return updated user data
            var updatedUserDto = new UserResponseDTO
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth,
                Phone = user.PhoneNumber,
                ProfilePicture = user.ProfilePicture
            };

            return Ok(updatedUserDto);
        }

        // PUT: api/User/password
        [HttpPut("change-password")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequestDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            // Verify current password
            if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
            {
                return BadRequest(new { Errors = new[] { "Invalid current password" } });
            }

            // Change password
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { Message = "Password changed successfully" });
        }

        // POST: api/User/forgot-password
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return BadRequest(new { Errors = new[] { "User with this email does not exist" } });
            }

            // Verify identity
            if (user.FirstName != model.FirstName ||
                user.LastName != model.LastName ||
                user.PhoneNumber != model.Phone)
            {
                return BadRequest(new { Errors = new[] { "Identity verification failed. Please check your details." } });
            }

            // Generate JWT for internal validation
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("ResetPassword", "true") // Custom claim to indicate reset operation
            };
            var token = GetToken(authClaims);

            // Validate token (optional, for additional security)
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]))
            };
            try
            {
                tokenHandler.ValidateToken(tokenHandler.WriteToken(token), validationParameters, out var validatedToken);
            }
            catch
            {
                return BadRequest(new { Errors = new[] { "Invalid token generation" } });
            }

            // Update password
            var passwordHash = _userManager.PasswordHasher.HashPassword(user, model.NewPassword);
            user.PasswordHash = passwordHash;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { Message = "Password reset successfully" });
        }

        private JwtSecurityToken GetToken(IEnumerable<Claim> claims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.UtcNow.AddHours(3),
                claims: claims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }

        // Llama ============================================
        [HttpPost("profile-agent")]
        public async Task<IActionResult> ProfileAgent([FromBody] AgentRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            var userData = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.DateOfBirth,
                user.ProfilePicture
            };

            try
            {
                var response = await _llamaService.SendProfileAgentMessageAsync(request.Message, userData);
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}
