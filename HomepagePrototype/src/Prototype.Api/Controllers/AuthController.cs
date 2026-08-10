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
    private const string SecretKey = "SuperSecretKeyForDevelopmentTesting1234567890!";

    /// <summary>
    /// 관리자 로그인 (JSON 방식)
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request.Email == "test@company.local" && request.Password == "Test1234!")
        {
            var jwtString = GenerateJwtToken("1", request.Email, "관리자(Admin)", "Admin");
            return Ok(new
            {
                message = "로그인 성공! JWT 토큰이 자동으로 생성되었습니다.",
                role = "Admin",
                email = request.Email,
                token_type = "Bearer",
                access_token = jwtString
            });
        }

        return Unauthorized(new { message = "이메일 또는 비밀번호가 올바르지 않습니다." });
    }

    /// <summary>
    /// Swagger UI OAuth2 자동 로그인 엔드포인트 (Form 방식)
    /// </summary>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult OAuthToken([FromForm] string username, [FromForm] string password)
    {
        if (username == "test@company.local" && password == "Test1234!")
        {
            var jwtString = GenerateJwtToken("1", username, "관리자(Admin)", "Admin");
            return Ok(new
            {
                access_token = jwtString,
                token_type = "Bearer",
                expires_in = 604800
            });
        }

        return BadRequest(new { error = "invalid_grant", error_description = "이메일 또는 비밀번호가 일치하지 않습니다." });
    }

    /// <summary>
    /// 개발 테스트용 JWT 토큰 발급 API
    /// </summary>
    [HttpGet("dev-token")]
    public IActionResult GetDevAdminToken()
    {
        var jwtString = GenerateJwtToken("1", "test@company.local", "관리자(Admin)", "Admin");
        return Ok(new
        {
            role = "Admin",
            email = "test@company.local",
            token_type = "Bearer",
            access_token = jwtString
        });
    }

    private static string GenerateJwtToken(string userId, string email, string name, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: "https://localhost:7001",
            audience: "company-homepage",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Email, string Password);
