namespace Domain.Entities;

public class OidcPkceState
{
    public string CodeVerifier { get; }
    public string CodeChallenge { get; }
    public string State { get; }
    public string AuthorizeUrl { get; }
    public string ReturnUrl { get; }
    public DateTime CreatedAtUtc { get; }

    public OidcPkceState(string codeVerifier, string codeChallenge, string state, string authorizeUrl, string returnUrl)
    {
        CodeVerifier = codeVerifier;
        CodeChallenge = codeChallenge;
        State = state;
        AuthorizeUrl = authorizeUrl;
        ReturnUrl = returnUrl;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
