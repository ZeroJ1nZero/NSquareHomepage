using System.IdentityModel.Tokens.Jwt;
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
            _logger.LogError("토큰 교환 실패: HTTP {StatusCode}, 응답: {Response}", response.StatusCode, responseContent);
            return new TokenExchangeResultDto(false, responseContent, new List<Claim>(), default);
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
            _logger.LogError("토큰 갱신 실패: HTTP {StatusCode}, 응답: {Response}", response.StatusCode, responseContent);
            return new TokenExchangeResultDto(false, responseContent, new List<Claim>(), default);
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
