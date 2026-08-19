using Microsoft.AspNetCore.Http;

namespace ServiceServer.Application.Common.Interfaces;

public interface IOidcStateService
{
    (string Verifier, string Challenge, string State, string AuthorizeUrl) GenerateAndStorePkce(HttpContext context, string? returnUrl = null);
    bool ValidateCsrfState(HttpContext context, string? incomingState);
    string? GetStoredVerifier(HttpContext context);
    void ClearSession(HttpContext context);
}
