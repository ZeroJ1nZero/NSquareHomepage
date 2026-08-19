using System.Security.Claims;

namespace ServiceServer.Domain.Entities;

public class AuthenticatedUser
{
    public string? Sub { get; }
    public string? Name { get; }
    public string? Email { get; }
    public string? Role { get; }
    public IReadOnlyList<Claim> Claims { get; }

    public AuthenticatedUser(string? sub, string? name, string? email, string? role, IEnumerable<Claim> claims)
    {
        Sub = sub;
        Name = name;
        Email = email;
        Role = role;
        Claims = claims.ToList().AsReadOnly();
    }
}
