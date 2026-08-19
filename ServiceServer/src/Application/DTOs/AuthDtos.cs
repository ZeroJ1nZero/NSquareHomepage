using System.Security.Claims;
using System.Text.Json;

namespace Application.DTOs;

public record TokenExchangeRequest(
    string Code,
    string? CodeVerifier = null,
    string? State = null,
    string? RedirectUri = null);

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
