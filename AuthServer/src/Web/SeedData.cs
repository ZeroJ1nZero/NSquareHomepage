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

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        // IssuedAuthorizationCodes 테이블 생성 보장 및 Zero-Trust 결합 해시 컬럼 추가
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `IssuedAuthorizationCodes` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `AuthorizationCode` VARCHAR(512) NOT NULL,
                `AuthorizationCodeHash` VARCHAR(128) NOT NULL,
                `CodeChallenge` VARCHAR(256) NOT NULL,
                `CodeChallengeHash` VARCHAR(128) NOT NULL,
                `CombinedBindingHash` VARCHAR(128) NOT NULL DEFAULT '',
                `CodeChallengeMethod` VARCHAR(32) NOT NULL,
                `ClientId` VARCHAR(128) NOT NULL,
                `RedirectUri` VARCHAR(512) NOT NULL,
                `Subject` VARCHAR(128) NOT NULL,
                `UserEmail` VARCHAR(256) NOT NULL,
                `State` VARCHAR(256) NOT NULL,
                `Scope` VARCHAR(512) NOT NULL,
                `CreatedAtUtc` DATETIME(6) NOT NULL,
                `ExpiresAtUtc` DATETIME(6) NOT NULL,
                `IsRedeemed` TINYINT(1) NOT NULL DEFAULT 0,
                `RedeemedAtUtc` DATETIME(6) NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_IssuedAuthorizationCodes_AuthorizationCodeHash` (`AuthorizationCodeHash`),
                INDEX `IX_IssuedAuthorizationCodes_CombinedBindingHash` (`CombinedBindingHash`),
                INDEX `IX_IssuedAuthorizationCodes_CreatedAtUtc` (`CreatedAtUtc`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ", ct);

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `IssuedAuthorizationCodes` ADD COLUMN IF NOT EXISTS `CombinedBindingHash` VARCHAR(128) NOT NULL DEFAULT '';
            ", ct);
        }
        catch { /* Column may already exist */ }

        // 회사 홈페이지 클라이언트 등록 (AuthServer/README.md 규격)
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = config["Clients:Homepage:ClientId"] ?? "company-homepage";
        var existingApp = await manager.FindByClientIdAsync(clientId, ct);

        var allowedRedirectUris = new HashSet<Uri>
        {
            new Uri(config["Clients:Homepage:RedirectUri"] ?? "https://localhost:7001/signin-oidc"),
            new Uri("https://localhost:7001/api/auth/oidc-callback"),
            new Uri("http://localhost:5016/api/auth/oidc-callback"),
            new Uri("http://localhost:5016/signin-oidc"),
            new Uri("http://localhost:5000/signin-oidc")
        };

        var allowedPostLogoutRedirectUris = new HashSet<Uri>
        {
            new Uri(config["Clients:Homepage:PostLogoutRedirectUri"] ?? "https://localhost:7001/"),
            new Uri("http://localhost:3000/"),
            new Uri("http://localhost:3000"),
            new Uri("http://localhost:5016/signout-callback-oidc"),
            new Uri("http://localhost:5016/"),
            new Uri("http://localhost:5000/")
        };

        if (existingApp is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "회사 홈페이지",
                ClientType = ClientTypes.Public,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.GrantTypes.Password,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + Scopes.OpenId,
                    Permissions.Prefixes.Scope + Scopes.Email,
                    Permissions.Prefixes.Scope + Scopes.Profile,
                    Permissions.Prefixes.Scope + Scopes.Roles,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            };

            foreach (var uri in allowedRedirectUris)
            {
                descriptor.RedirectUris.Add(uri);
            }

            foreach (var uri in allowedPostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(uri);
            }

            await manager.CreateAsync(descriptor, ct);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, existingApp, ct);

            bool needsUpdate = false;
            foreach (var uri in allowedRedirectUris)
            {
                if (!descriptor.RedirectUris.Contains(uri))
                {
                    descriptor.RedirectUris.Add(uri);
                    needsUpdate = true;
                }
            }

            foreach (var uri in allowedPostLogoutRedirectUris)
            {
                if (!descriptor.PostLogoutRedirectUris.Contains(uri))
                {
                    descriptor.PostLogoutRedirectUris.Add(uri);
                    needsUpdate = true;
                }
            }

            var requiredPermissions = new[]
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.GrantTypes.Password,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Prefixes.Scope + Scopes.Email,
                Permissions.Prefixes.Scope + Scopes.Profile,
                Permissions.Prefixes.Scope + Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            };

            foreach (var perm in requiredPermissions)
            {
                if (!descriptor.Permissions.Contains(perm))
                {
                    descriptor.Permissions.Add(perm);
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                await manager.UpdateAsync(existingApp, descriptor, ct);
            }
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
