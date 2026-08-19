using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ServiceServer.Api.Services;

public interface IOidcStateService
{
    (string Verifier, string Challenge, string State, string AuthorizeUrl) GenerateAndStorePkce(HttpContext context, string? returnUrl = null);
    bool ValidateCsrfState(HttpContext context, string? incomingState);
    string? GetStoredVerifier(HttpContext context);
    void ClearSession(HttpContext context);
}

public class OidcStateService : IOidcStateService
{
    private readonly IConfiguration _configuration;

    public OidcStateService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string IdpBaseUrl => _configuration["Authentication:Authority"] ?? "https://localhost:7213";
    private string ClientId => _configuration["Authentication:ClientId"] ?? "company-homepage";
    private string DefaultRedirectUri => _configuration["Authentication:RedirectUri"] ?? "https://localhost:7001/signin-oidc";

    public (string Verifier, string Challenge, string State, string AuthorizeUrl) GenerateAndStorePkce(HttpContext context, string? returnUrl = null)
    {
        // 1. PKCE 원본 키 (code_verifier) 생성: 32바이트 암호학적 난수 -> Base64Url 인코딩
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var verifier = Base64UrlEncoder.Encode(bytes);

        // 2. PKCE 공개 해시 키 (code_challenge) 생성: BASE64URL(SHA256(verifier))
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
            var challenge = Base64UrlEncoder.Encode(hash);

            // 3. CSRF 검증 키 (state) 생성: 32자 무작위 GUID
            var state = Guid.NewGuid().ToString("N");

            // 4. 서비스 서버 세션에 PKCE 원본 키와 CSRF 검증 키 보관
            context.Session.SetString("pkce_verifier", verifier);
            context.Session.SetString("oauth_state", state);
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                context.Session.SetString("return_url", returnUrl);
            }

            var scope = Uri.EscapeDataString("openid profile email offline_access");
            var targetRedirectUri = Uri.EscapeDataString(DefaultRedirectUri);
            var authorizeUrl = $"{IdpBaseUrl.TrimEnd('/')}/connect/authorize?client_id={ClientId}&response_type=code&redirect_uri={targetRedirectUri}&scope={scope}&code_challenge={challenge}&code_challenge_method=S256&state={state}";

            return (verifier, challenge, state, authorizeUrl);
        }
    }

    public bool ValidateCsrfState(HttpContext context, string? incomingState)
    {
        if (string.IsNullOrWhiteSpace(incomingState))
        {
            return false;
        }

        var sessionState = context.Session.GetString("oauth_state");
        return !string.IsNullOrWhiteSpace(sessionState) && string.Equals(sessionState, incomingState, StringComparison.Ordinal);
    }

    public string? GetStoredVerifier(HttpContext context)
    {
        return context.Session.GetString("pkce_verifier");
    }

    public void ClearSession(HttpContext context)
    {
        context.Session.Remove("pkce_verifier");
        context.Session.Remove("oauth_state");
        context.Session.Remove("return_url");
    }
}
