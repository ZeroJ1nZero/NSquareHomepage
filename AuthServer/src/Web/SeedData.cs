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

        // 1. AuthorizationCodes 테이블 생성 보장 (인가 코드 전용 - 1분 수명 및 SHA-256 해시 저장)
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `AuthorizationCodes` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `AuthorizationCodeHash` VARCHAR(128) NOT NULL,
                `CodeChallengeHash` VARCHAR(128) NOT NULL,
                `ClientId` VARCHAR(128) NOT NULL,
                `RedirectUri` VARCHAR(512) NOT NULL,
                `UserId` VARCHAR(128) NOT NULL,
                `UserEmail` VARCHAR(256) NOT NULL,
                `Scope` VARCHAR(512) NOT NULL,
                `CreatedAtUtc` DATETIME(6) NOT NULL,
                `ExpiresAtUtc` DATETIME(6) NOT NULL,
                `IsUsed` TINYINT(1) NOT NULL DEFAULT 0,
                `UsedAtUtc` DATETIME(6) NULL,
                PRIMARY KEY (`Id`),
                UNIQUE INDEX `UX_AuthorizationCodes_AuthorizationCodeHash` (`AuthorizationCodeHash`),
                INDEX `IX_AuthorizationCodes_ExpiresAtUtc` (`ExpiresAtUtc`),
                INDEX `IX_AuthorizationCodes_CreatedAtUtc` (`CreatedAtUtc`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ", ct);

        // 2. RefreshTokens 테이블 생성 보장 (리프레시 토큰 전용 - 14일 수명 및 해시 저장)
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `RefreshTokens` (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `RefreshTokenHash` VARCHAR(128) NOT NULL,
                `UserId` VARCHAR(128) NOT NULL,
                `UserEmail` VARCHAR(256) NOT NULL,
                `ClientId` VARCHAR(128) NOT NULL,
                `Scope` VARCHAR(512) NOT NULL,
                `CreatedAtUtc` DATETIME(6) NOT NULL,
                `ExpiresAtUtc` DATETIME(6) NOT NULL,
                `IsRevoked` TINYINT(1) NOT NULL DEFAULT 0,
                `RevokedAtUtc` DATETIME(6) NULL,
                `ReplacedByTokenHash` VARCHAR(128) NULL,
                PRIMARY KEY (`Id`),
                UNIQUE INDEX `UX_RefreshTokens_RefreshTokenHash` (`RefreshTokenHash`),
                INDEX `IX_RefreshTokens_UserId` (`UserId`),
                INDEX `IX_RefreshTokens_ExpiresAtUtc` (`ExpiresAtUtc`),
                INDEX `IX_RefreshTokens_CreatedAtUtc` (`CreatedAtUtc`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ", ct);

        // 테이블명 마이그레이션 (이전 명칭 -> 직관적인 명칭으로 마이그레이션)
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `user` TO `users`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `User` TO `users`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `log-block` TO `BlockedIps`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `BlockedIp` TO `BlockedIps`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `blockedip` TO `BlockedIps`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `IpBlocklist` TO `BlockedIps`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `log-login` TO `LoginLogs`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `LoginAudit` TO `LoginLogs`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `loginlog` TO `LoginLogs`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `LoginAuditLog` TO `LoginLogs`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `log-authorizationcodes` TO `AuthorizationCodes`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `authorizationcodes` TO `AuthorizationCodes`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `AuthorizationCodeIssuanceLog` TO `AuthorizationCodes`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `data-authorizationcodes` TO `OpenIddictAuthorizations`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `authorizationlog` TO `OpenIddictAuthorizations`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `data-oidctokens` TO `OpenIddictTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `oidctokens` TO `OpenIddictTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `data-scopes` TO `OpenIddictScopes`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `authorizationscopes` TO `OpenIddictScopes`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `data-refreshtokens` TO `RefreshTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `refreshtokens` TO `RefreshTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `IssuedRefreshTokens` TO `RefreshTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `RefreshTokenLedger` TO `RefreshTokens`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `data-trustedapplications` TO `OpenIddictApplications`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `trustedApplication` TO `OpenIddictApplications`;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("RENAME TABLE `trustedapplication` TO `OpenIddictApplications`;", ct); } catch { }

        // 컬럼명 마이그레이션
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `users` CHANGE COLUMN `UserName` `DisplayName` VARCHAR(256) NOT NULL;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `LoginLogs` CHANGE COLUMN `AttemptedIdentifier` `LoginId` VARCHAR(256) NOT NULL;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `LoginLogs` CHANGE COLUMN `UserName` `LoginId` VARCHAR(256) NOT NULL;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `AuthorizationCodes` CHANGE COLUMN `Subject` `UserId` VARCHAR(128) NOT NULL;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `AuthorizationCodes` CHANGE COLUMN `IsRedeemed` `IsUsed` TINYINT(1) NOT NULL DEFAULT 0;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `AuthorizationCodes` CHANGE COLUMN `RedeemedAtUtc` `UsedAtUtc` DATETIME(6) NULL;", ct); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE `RefreshTokens` CHANGE COLUMN `Subject` `UserId` VARCHAR(128) NOT NULL;", ct); } catch { }

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE `OpenIddictApplications` DROP CONSTRAINT IF EXISTS `CK_trustedApplication_HomepageName_EnglishOnly`;", ct);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE `OpenIddictApplications` DROP CONSTRAINT IF EXISTS `CK_OpenIddictApplications_HomepageName_EnglishOnly`;", ct);
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictApplications` CHANGE COLUMN `DisplayName` `ClientDisplayName` LONGTEXT NULL;
            ", ct);
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictApplications` CHANGE COLUMN `HomepageName` `ClientDisplayName` LONGTEXT NULL;
            ", ct);
        }
        catch { /* Column may already be renamed or not exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictApplications` DROP COLUMN IF EXISTS `DisplayNames`;
            ", ct);
        }
        catch { /* Column may not exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictApplications` DROP COLUMN IF EXISTS `JsonWebKeySet`;
            ", ct);
        }
        catch { /* Column may not exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictApplications` 
                ADD CONSTRAINT `CK_OpenIddictApplications_ClientDisplayName_EnglishOnly` 
                CHECK (`ClientDisplayName` IS NULL OR `ClientDisplayName` REGEXP '^[a-zA-Z0-9[:space:]_.,\'\""()-]+$');
            ", ct);
        }
        catch { /* Constraint may already exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictScopes` DROP COLUMN IF EXISTS `DisplayNames`;
            ", ct);
        }
        catch { /* Column may not exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictScopes` 
                ADD CONSTRAINT `CK_OpenIddictScopes_DisplayName_EnglishOnly` 
                CHECK (`DisplayName` IS NULL OR `DisplayName` REGEXP '^[a-zA-Z0-9[:space:]_.,\'\""()-]+$');
            ", ct);
        }
        catch { /* Constraint may already exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictScopes` DROP COLUMN IF EXISTS `Descriptions`;
            ", ct);
        }
        catch { /* Column may not exist */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE `OpenIddictScopes` 
                ADD CONSTRAINT `CK_OpenIddictScopes_Description_EnglishOnly` 
                CHECK (`Description` IS NULL OR `Description` REGEXP '^[a-zA-Z0-9[:space:]_.,\'\""()-]+$');
            ", ct);
        }
        catch { /* Constraint may already exist */ }

        // 회사 홈페이지 클라이언트 등록 (AuthServer/README.md 규격)
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = config["Clients:Homepage:ClientId"] ?? "company-homepage";
        var existingApp = await manager.FindByClientIdAsync(clientId, ct);

        // 1. 필수 Redirect URI 화이트리스트 (중복 및 미사용 레거시 경로 제거)
        var allowedRedirectUris = new HashSet<Uri>
        {
            new Uri("http://localhost:3000/callback"),
            new Uri("https://localhost:7001/api/auth/oidc-callback"),
            new Uri("http://localhost:5016/api/auth/oidc-callback")
        };

        // 2. 필수 Post Logout Redirect URI 화이트리스트 (중복 및 불필요 서브경로 제거)
        var allowedPostLogoutRedirectUris = new HashSet<Uri>
        {
            new Uri("http://localhost:3000/"),
            new Uri("http://localhost:3000/login"),
            new Uri("https://localhost:7001/"),
            new Uri("http://localhost:5016/")
        };

        // 3. 필수 권한 및 스코프 (중복 정의 제거 및 PKCE 기반 인가코드/리프레시 토큰 권한만 유지)
        var requiredPermissions = new HashSet<string>
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + Scopes.OpenId,
            Permissions.Prefixes.Scope + Scopes.Email,
            Permissions.Prefixes.Scope + Scopes.Profile,
            Permissions.Prefixes.Scope + Scopes.Roles,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess
        };

        if (existingApp is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "Company Homepage",
                ClientType = ClientTypes.Public,
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            };

            foreach (var perm in requiredPermissions) descriptor.Permissions.Add(perm);
            foreach (var uri in allowedRedirectUris) descriptor.RedirectUris.Add(uri);
            foreach (var uri in allowedPostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(uri);

            await manager.CreateAsync(descriptor, ct);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, existingApp, ct);

            descriptor.DisplayName = "Company Homepage";

            // DB 내 기존 중복/레거시 데이터를 정제된 세트로 전체 최신화
            descriptor.RedirectUris.Clear();
            foreach (var uri in allowedRedirectUris) descriptor.RedirectUris.Add(uri);

            descriptor.PostLogoutRedirectUris.Clear();
            foreach (var uri in allowedPostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(uri);

            descriptor.Permissions.Clear();
            foreach (var perm in requiredPermissions) descriptor.Permissions.Add(perm);

            descriptor.Requirements.Clear();
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

            await manager.UpdateAsync(existingApp, descriptor, ct);
        }

        // 개발 환경 전용 테스트 계정
        if (env.IsDevelopment() &&
            !await db.Users.AnyAsync(u => u.Email == "test@company.local", ct))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var user = new User { Email = "test@company.local", DisplayName = "테스트 사용자", PasswordHash = "", Role = UserRole.Admin };
            user.PasswordHash = hasher.HashPassword(user, "Test1234!");
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
