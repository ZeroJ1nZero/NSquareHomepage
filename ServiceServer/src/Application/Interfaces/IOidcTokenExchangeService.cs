using Application.DTOs;

namespace Application.Interfaces;

public interface IOidcTokenExchangeService
{
    Task<TokenExchangeResultDto> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);
}
