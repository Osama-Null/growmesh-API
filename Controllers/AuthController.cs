using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using growmesh_API.Data;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using growmesh_API.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace growmesh_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration, ApplicationDbContext db, IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _configuration = configuration;
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
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
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized("Invalid credentials.");
            }

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = GetToken(authClaims);

            return Ok(new AuthResponseDTO
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = token.ValidTo,
                UserId = user.Id
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            //if (model.ProfilePicture != null)
            //{
            //    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            //    if (!Directory.Exists(uploadsDir))
            //    {
            //        Directory.CreateDirectory(uploadsDir);
            //    }

            //    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfilePicture.FileName);
            //    var filePath = Path.Combine(uploadsDir, fileName);

            //    using (var stream = new FileStream(filePath, FileMode.Create))
            //    {
            //        await model.ProfilePicture.CopyToAsync(stream);
            //    }
            //    imagePath = $"/images/{fileName}";
            //}
            string imagePath = null;

            try
            {
                imagePath = UploadedFile(model);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Errors = new[] { $"Failed to upload profile picture: {ex.Message}" } });
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = model.Phone,
                ProfilePicture = imagePath
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                // Cleanup the uploaded file if user creation fails
                if (imagePath != null)
                {
                    var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", imagePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                return BadRequest(result.Errors);
            }

            var bankAccount = new BankAccount
            {
                UserId = user.Id,
                Balance = 0
            };

            _db.BankAccounts.Add(bankAccount);
            await _db.SaveChangesAsync();

            // Generate claims
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Generate token
            var token = GetToken(authClaims);

            return Ok(new AuthResponseDTO
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = token.ValidTo,
                UserId = user.Id
            });
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

        private string UploadedFile(RegisterDTO model)
        {
            if (model.ProfilePicture == null)
                return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(model.ProfilePicture.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new Exception("Invalid file type. Only JPG, JPEG, and PNG are allowed.");
            }
            if (model.ProfilePicture.Length > 5 * 1024 * 1024) // 5MB limit
            {
                throw new Exception("File size exceeds 5MB.");
            }

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePicture.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                model.ProfilePicture.CopyTo(fileStream);
            }

            return $"/images/{uniqueFileName}";
        }
    }
}