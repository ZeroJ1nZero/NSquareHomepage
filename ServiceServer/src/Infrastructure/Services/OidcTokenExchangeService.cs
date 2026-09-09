using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Interfaces;
using Application.DTOs;

namespace Infrastructure.Services;

public class OidcTokenExchangeService : IOidcTokenExchangeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OidcTokenExchangeService> _logger;

    public OidcTokenExchangeService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OidcTokenExchangeService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string IdpBaseUrl => _configuration["Authentication:Authority"] ?? "https://localhost:7213";
    private string ClientId => _configuration["Authentication:ClientId"] ?? "company-homepage";

    public async Task<TokenExchangeResultDto> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = $"{IdpBaseUrl.TrimEnd('/')}/connect/token";
        var client = _httpClientFactory.CreateClient("IdpClient");

        var tokenParams = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        };

        _logger.LogInformation("[Step 10] 인증 서버(:7213/connect/token)로 Back-channel 토큰 교환 요청 전송 (Grant: authorization_code, Verifier 첨부)");
        using var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenParams), cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OIDC /connect/token 응답 실패(HTTP {Code}). Back-channel 전용 엔드포인트(/api/auth/token-exchange)로 직통신 재시도합니다.", response.StatusCode);
            var directEndpoint = $"{IdpBaseUrl.TrimEnd('/')}/api/auth/token-exchange";
            var directPayload = new
            {
                grantType = "authorization_code",
                clientId = ClientId,
                code = code,
                codeVerifier = codeVerifier,
                redirectUri = redirectUri
            };
            using var directResponse = await client.PostAsJsonAsync(directEndpoint, directPayload, cancellationToken);
            var directContent = await directResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!directResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Back-channel 토큰 교환 최종 실패: HTTP {StatusCode}, 응답: {Response}", directResponse.StatusCode, directContent);
                return new TokenExchangeResultDto(false, directContent, new List<Claim>(), default);
            }

            responseContent = directContent;
        }

        using var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement.Clone();

        var claims = new List<Claim>();
        var handler = new JwtSecurityTokenHandler();
        var idTokenStr = (root.TryGetProperty("id_token", out var itp) ? itp.GetString() : null)
                         ?? (root.TryGetProperty("idToken", out var itp2) ? itp2.GetString() : null);

        if (!string.IsNullOrWhiteSpace(idTokenStr) && handler.CanReadToken(idTokenStr))
        {
            var jwt = handler.ReadJwtToken(idTokenStr);
            claims.AddRange(jwt.Claims);
        }

        var accessTokenStr = (root.TryGetProperty("access_token", out var atp) ? atp.GetString() : null)
                             ?? (root.TryGetProperty("accessToken", out var atp2) ? atp2.GetString() : null);

        if (claims.Count == 0 && !string.IsNullOrWhiteSpace(accessTokenStr) && handler.CanReadToken(accessTokenStr))
        {
            var jwt = handler.ReadJwtToken(accessTokenStr);
            claims.AddRange(jwt.Claims);
        }

        return new TokenExchangeResultDto(true, responseContent, claims, root);
    }

    public async Task<TokenExchangeResultDto> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = $"{IdpBaseUrl.TrimEnd('/')}/connect/token";
        var client = _httpClientFactory.CreateClient("IdpClient");

        var tokenParams = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken
        };

        _logger.LogInformation("인증 서버(:7213/connect/token)로 Back-channel Refresh Token 갱신 요청 전송 (Grant: refresh_token)");
        using var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenParams), cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OIDC /connect/token 토큰 갱신 응답 실패(HTTP {StatusCode}). Back-channel 직통신 엔드포인트(/api/auth/refresh-token)로 재시도합니다.", response.StatusCode);
            var directEndpoint = $"{IdpBaseUrl.TrimEnd('/')}/api/auth/refresh-token";
            var directPayload = new
            {
                refresh_token = refreshToken
            };
            using var directResponse = await client.PostAsJsonAsync(directEndpoint, directPayload, cancellationToken);
            var directContent = await directResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!directResponse.IsSuccessStatusCode)
            {
                _logger.LogError("토큰 갱신 최종 실패: HTTP {StatusCode}, 응답: {Response}", directResponse.StatusCode, directContent);
                return new TokenExchangeResultDto(false, directContent, new List<Claim>(), default);
            }

            responseContent = directContent;
        }

        using var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement.Clone();

        var claims = new List<Claim>();
        if (root.TryGetProperty("id_token", out var idTokenProp) && !string.IsNullOrWhiteSpace(idTokenProp.GetString()))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(idTokenProp.GetString()))
            {
                var jwt = handler.ReadJwtToken(idTokenProp.GetString());
                claims.AddRange(jwt.Claims);
            }
        }
        else if (root.TryGetProperty("access_token", out var accessTokenProp) && !string.IsNullOrWhiteSpace(accessTokenProp.GetString()))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(accessTokenProp.GetString()))
            {
                var jwt = handler.ReadJwtToken(accessTokenProp.GetString());
                claims.AddRange(jwt.Claims);
            }
        }

        return new TokenExchangeResultDto(true, responseContent, claims, root);
    }
}
