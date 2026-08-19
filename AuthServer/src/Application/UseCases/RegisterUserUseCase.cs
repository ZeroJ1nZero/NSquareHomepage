using Application.Interfaces;

namespace Application.UseCases;

public sealed class RegisterUserUseCase(IUserService userService)
{
    public async Task<UserCreationResult> ExecuteAsync(string email, string userName, string password, CancellationToken ct = default)
    {
        email = email.Trim();
        userName = userName.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return UserCreationResult.Fail("이메일 입력하세요.");

        if (string.IsNullOrWhiteSpace(userName))
            return UserCreationResult.Fail("이름을 입력하세요.");

        return await userService.CreateUserAsync(email, userName, password, ct);
    }
}
