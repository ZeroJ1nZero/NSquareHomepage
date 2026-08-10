using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Prototype.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// 개발 및 Swagger UI 테스트용 Admin JWT 토큰 발급 엔드포인트
    /// </summary>
    [HttpGet("dev-token")]
    public IActionResult GetDevAdminToken()
    {
        var secretKey = "SuperSecretKeyForDevelopmentTesting1234567890!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "1"),
            new Claim(JwtRegisteredClaimNames.Email, "test@company.local"),
            new Claim(ClaimTypes.Name, "관리자(Admin)"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(
            issuer: "https://localhost:7001",
            audience: "company-homepage",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            role = "Admin",
            email = "test@company.local",
            token_type = "Bearer",
            access_token = jwtString,
            swagger_header_value = $"Bearer {jwtString}"
        });
    }
}
