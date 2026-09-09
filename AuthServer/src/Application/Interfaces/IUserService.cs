using Domain.Entities;

namespace Application.Interfaces;

public record UserCreationResult(bool Succeeded, IReadOnlyList<string> Errors, long? UserId = null)
{
    public static UserCreationResult Success(long? userId = null) => new(true, [], userId);
    public static UserCreationResult Fail(params string[] errors) => new(false, errors);
}

public interface IUserService
{
    Task<UserCreationResult> CreateUserAsync(string email, string displayName, string password, UserRole role = UserRole.Customer, CancellationToken ct = default);
}
