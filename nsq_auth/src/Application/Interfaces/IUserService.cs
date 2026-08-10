namespace Application.Interfaces;

public record UserCreationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static UserCreationResult Success { get; } = new(true, []);
    public static UserCreationResult Fail(params string[] errors) => new(false, errors);
}


public interface IUserService
{
    Task<UserCreationResult> CreateUserAsync(string email, string userName, string password, CancellationToken ct = default);
}
