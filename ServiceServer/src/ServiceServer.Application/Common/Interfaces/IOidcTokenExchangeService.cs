using ServiceServer.Application.DTOs;

namespace ServiceServer.Application.Common.Interfaces;

public interface IOidcTokenExchangeService
{
    Task<TokenExchangeResultDto> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);
}
