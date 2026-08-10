using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Web;

/// <summary>시작 시 DB 스키마 생성 + 클라이언트/테스트 계정 시드.</summary>
public class SeedData(IServiceProvider services, IConfiguration config, IHostEnvironment env) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();

        // ponytail: 마이그레이션 없이 모델에서 스키마 직접 생성. 이미 DB가 있으면 아무것도 안 함 —
        // 엔티티 변경 시 DB를 드랍하고 재생성해야 반영됨. 운영에서 데이터 보존이 필요해지면 마이그레이션으로 전환.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        // 회사 홈페이지 클라이언트 등록
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = config["Clients:Homepage:ClientId"] ?? "company-homepage";
        if (await manager.FindByClientIdAsync(clientId, ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "회사 홈페이지",
                // Public: 브라우저에서 도는 클라이언트는 시크릿을 숨길 수 없음 → PKCE로 보호
                ClientType = ClientTypes.Public,
                RedirectUris =
                {
                    new Uri(config["Clients:Homepage:RedirectUri"] ?? "https://localhost:7001/signin-oidc"),
                },
                PostLogoutRedirectUris =
                {
                    new Uri(config["Clients:Homepage:PostLogoutRedirectUri"] ?? "https://localhost:7001/"),
                },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            }, ct);
        }

        // 개발 환경 전용 테스트 계정
        if (env.IsDevelopment() &&
            !await db.Users.AnyAsync(u => u.Email == "test@company.local", ct))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var user = new User { Email = "test@company.local", UserName = "테스트 사용자", PasswordHash = "", Role = UserRole.Admin };
            user.PasswordHash = hasher.HashPassword(user, "Test1234!");
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
