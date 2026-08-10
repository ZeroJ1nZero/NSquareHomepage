using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class UserService(AppDbContext db, IPasswordHasher<User> hasher) : IUserService
{
    public async Task<UserCreationResult> CreateUserAsync(string email, string userName, string password, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return UserCreationResult.Fail("이미 가입된 이메일입니다.");

        var user = new User { Email = email, UserName = userName, PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) 
        {
            return UserCreationResult.Fail("이미 가입된 이메일입니다.");
        }

        return UserCreationResult.Success;
    }
}
