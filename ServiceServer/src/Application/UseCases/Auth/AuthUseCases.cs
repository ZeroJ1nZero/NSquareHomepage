using Microsoft.AspNetCore.Http;
using Application.DTOs;
using Application.Interfaces;

namespace Application.UseCases.Auth;

/// <summary>
/// [Step 1] SSO 시작 및 PKCE/CSRF 키 생성 UseCase (Single Responsibility: SRP)
/// </summary>
public interface IInitiateSsoUseCase
{
    StartSsoResultDto Execute(HttpContext context, string? returnUrl = null);
}

public class InitiateSsoUseCase : IInitiateSsoUseCase
{
    private readonly IOidcStateService _oidcStateService;

    public InitiateSsoUseCase(IOidcStateService oidcStateService)
    {
        _oidcStateService = oidcStateService;
    }

    public StartSsoResultDto Execute(HttpContext context, string? returnUrl = null)
    {
        var (verifier, challenge, state, authorizeUrl) = _oidcStateService.GenerateAndStorePkce(context, returnUrl);

        return new StartSsoResultDto
        {
            Success = true,
            Message = "서비스 세션 쿠키가 존재하지 않아 인증 서버(IdP) 인가 주소 및 PKCE/CSRF 키가 생성되었습니다.",
            AuthorizeUrl = authorizeUrl,
            ClientId = "company-homepage",
            ResponseType = "code",
            CodeVerifier = verifier,
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            State = state,
            Scope = "openid profile email roles offline_access",
            RedirectUri = "https://localhost:7001/api/auth/oidc-callback",
            ReturnUrl = returnUrl ?? "/"
        };
    }
}

/// <summary>
/// [Step 5] CSRF State 일치 검증 UseCase (Single Responsibility: SRP)
/// </summary>
public interface IVerifyCsrfStateUseCase
{
    VerifyStateResultDto Execute(HttpContext context, string state);
}

public class VerifyCsrfStateUseCase : IVerifyCsrfStateUseCase
{
    private readonly IOidcStateService _oidcStateService;

    public VerifyCsrfStateUseCase(IOidcStateService oidcStateService)
    {
        _oidcStateService = oidcStateService;
    }

    public VerifyStateResultDto Execute(HttpContext context, string state)
    {
        var isValid = _oidcStateService.ValidateCsrfState(context, state);
        var sessionHasState = context.Session.TryGetValue("oauth_state", out _);

        return new VerifyStateResultDto
        {
            IsValid = isValid,
            IncomingState = state,
            SessionHasState = sessionHasState,
            Message = isValid
                ? "state 값이 서비스 서버 세션의 oauth_state와 정확히 일치합니다."
                : "state 값이 일치하지 않거나 세션에 저장된 oauth_state가 없습니다."
        };
    }
}

/// <summary>
/// [Step 6] Back-Channel 토큰 교환 UseCase (Single Responsibility: SRP, Dependency Inversion: DIP)
/// </summary>
public interface IExchangeTokenUseCase
{
    Task<TokenExchangeResultDto> ExecuteAsync(string code, string? codeVerifier, string? redirectUri, HttpContext context, CancellationToken cancellationToken = default);
}

public class ExchangeTokenUseCase : IExchangeTokenUseCase
{
    private readonly IOidcTokenExchangeService _tokenExchangeService;
    private readonly IOidcStateService _oidcStateService;

    public ExchangeTokenUseCase(IOidcTokenExchangeService tokenExchangeService, IOidcStateService oidcStateService)
    {
        _tokenExchangeService = tokenExchangeService;
        _oidcStateService = oidcStateService;
    }

    public async Task<TokenExchangeResultDto> ExecuteAsync(string code, string? codeVerifier, string? redirectUri, HttpContext context, CancellationToken cancellationToken = default)
    {
        var verifier = !string.IsNullOrWhiteSpace(codeVerifier)
            ? codeVerifier
            : _oidcStateService.GetStoredVerifier(context);

        if (string.IsNullOrWhiteSpace(verifier))
        {
            return new TokenExchangeResultDto(
                false,
                "{\"error\":\"missing_code_verifier\",\"error_description\":\"PKCE 원본키(code_verifier)를 찾을 수 없습니다. Step 1을 먼저 실행하거나 codeVerifier를 입력해 주세요.\"}",
                new List<System.Security.Claims.Claim>(),
                default);
        }

        var effectiveRedirectUri = string.IsNullOrWhiteSpace(redirectUri)
            ? "https://localhost:7001/api/auth/oidc-callback"
            : redirectUri;

        return await _tokenExchangeService.ExchangeCodeForTokensAsync(code, verifier, effectiveRedirectUri, cancellationToken);
    }
}
