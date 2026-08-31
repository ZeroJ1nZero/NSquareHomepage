using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities;

namespace Application.UseCases;

public sealed partial class RegisterUserUseCase(IUserService userService)
{
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z0-9]{2,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public async Task<UserCreationResult> ExecuteAsync(string email, string displayName, string password, UserRole role = UserRole.Customer, CancellationToken ct = default)
    {
        email = email?.Trim() ?? "";
        displayName = displayName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(email))
            return UserCreationResult.Fail("이메일을 입력하세요.");

        if (!EmailRegex().IsMatch(email))
            return UserCreationResult.Fail("올바른 이메일 형식(예: id@domain.com 또는 id@company.local)이어야 합니다.");

        if (string.IsNullOrWhiteSpace(displayName))
            return UserCreationResult.Fail("이름을 입력하세요.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return UserCreationResult.Fail("비밀번호는 최소 6자 이상이어야 합니다.");

        return await userService.CreateUserAsync(email, displayName, password, role, ct);
    }
}
