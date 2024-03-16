using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using JwtAuth.Models;
using Microsoft.EntityFrameworkCore;
using Contracts;

namespace JwtAuth.Controllers
{
    [ApiController]
    public class IdentityController : ControllerBase
    {
        private const string TokenSecret = "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e";
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);

        private readonly AppDbContext _context;

        public IdentityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("token")]
        public IActionResult GenerateToken([FromBody] TokenGenerationRequest request)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(TokenSecret);

            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null)
                return NotFound();

            if (user.Password != request.Password)
                return BadRequest("Wrong Password");

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Jti, user.PublicId.ToString()),
                new(JwtRegisteredClaimNames.Sub, user.Email),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new("userid", user.PublicId.ToString()),
                new Claim(user.Role.ToString(), true.ToString(), ClaimValueTypes.Boolean)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(TokenLifetime),
                Issuer = "Id",
                Audience = "Tasks",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            var jwt = tokenHandler.WriteToken(token);
            return Ok(jwt);
        }

        [HttpGet("getallusers")]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetAllUsers()
        {
            var users = await _context.Users.ToListAsync();
            List<UserDTO> usersDto = [];
            foreach (var user in users)
            {
                usersDto.Add(new UserDTO
                {
                    Id = user.PublicId,
                    Email = user.Email,
                    Name = user.Name,
                    Role = user.Role
                });
            }

            return usersDto;
        }
    }
}
