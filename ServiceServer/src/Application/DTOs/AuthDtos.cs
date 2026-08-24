using System.Security.Claims;
using System.Text.Json;

namespace Application.DTOs;

public record TokenExchangeRequest(
    string Code,
    string? CodeVerifier = null,
    string? State = null,
    string? RedirectUri = null);

public class StartSsoResultDto
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = "company-homepage";
    public string ResponseType { get; set; } = "code";
    public string CodeVerifier { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = "S256";
    public string State { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid profile email roles offline_access";
    public string RedirectUri { get; set; } = "https://localhost:7001/api/auth/oidc-callback";
    public string? ReturnUrl { get; set; }
}

public class VerifyStateResultDto
{
    public bool IsValid { get; set; }
    public string IncomingState { get; set; } = string.Empty;
    public bool SessionHasState { get; set; }
    public string Message { get; set; } = string.Empty;
}

public record StartSsoResponseDto(
    string CodeChallenge,
    string CodeVerifier,
    string CodeChallengeMethod,
    string State,
    string AuthorizeUrl);

public record TokenExchangeResultDto(
    bool IsSuccess,
    string ResponseContent,
    List<Claim> Claims,
    JsonElement RootElement);

public record CurrentUserDto(
    bool IsAuthenticated,
    string? AuthenticationType,
    string? UserName,
    string? Role,
    IEnumerable<object> Claims);
