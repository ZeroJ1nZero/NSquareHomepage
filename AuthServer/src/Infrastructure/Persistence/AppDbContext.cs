using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<BlockedIp> BlockedIps => Set<BlockedIp>();
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique(); 
        });

        builder.Entity<LoginLog>(e =>
        {
            e.ToTable("LoginLogs");
            e.Property(x => x.LoginId).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 최대 길이
            e.HasIndex(x => x.AttemptedAtUtc);
            e.HasIndex(x => new { x.IpAddress, x.AttemptedAtUtc });
            e.HasIndex(x => new { x.LoginId, x.AttemptedAtUtc });
        });

        builder.Entity<BlockedIp>(e =>
        {
            e.ToTable("BlockedIps");
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => x.IpAddress).IsUnique();
        });

        builder.Entity<AuthorizationCode>(e =>
        {
            e.ToTable("AuthorizationCodes");
            e.Ignore(x => x.Subject);
            e.Property(x => x.AuthorizationCodeHash).HasMaxLength(128);
            e.Property(x => x.CodeChallengeHash).HasMaxLength(128);
            e.Property(x => x.ClientId).HasMaxLength(128);
            e.Property(x => x.RedirectUri).HasMaxLength(512);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.Scope).HasMaxLength(512);
            e.HasIndex(x => x.AuthorizationCodeHash).IsUnique();
            e.HasIndex(x => x.ExpiresAtUtc);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.Ignore(x => x.Subject);
            e.Property(x => x.RefreshTokenHash).HasMaxLength(128);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.ClientId).HasMaxLength(128);
            e.Property(x => x.Scope).HasMaxLength(512);
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            e.HasIndex(x => x.RefreshTokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ExpiresAtUtc);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication>(e =>
        {
            e.ToTable("OpenIddictApplications", t => 
                t.HasCheckConstraint("CK_OpenIddictApplications_ClientDisplayName_EnglishOnly", 
                    "`ClientDisplayName` IS NULL OR `ClientDisplayName` REGEXP '^[a-zA-Z0-9[:space:]_.,\\'\"()-]+$'"));
            e.Property(x => x.DisplayName).HasColumnName("ClientDisplayName");
            e.Ignore(x => x.DisplayNames);
            e.Ignore(x => x.JsonWebKeySet);
        });

        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization>(e =>
        {
            e.ToTable("OpenIddictAuthorizations");
        });

        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>(e =>
        {
            e.ToTable("OpenIddictScopes", t => 
            {
                t.HasCheckConstraint("CK_OpenIddictScopes_DisplayName_EnglishOnly", 
                    "`DisplayName` IS NULL OR `DisplayName` REGEXP '^[a-zA-Z0-9[:space:]_.,\\'\"()-]+$'");
                t.HasCheckConstraint("CK_OpenIddictScopes_Description_EnglishOnly", 
                    "`Description` IS NULL OR `Description` REGEXP '^[a-zA-Z0-9[:space:]_.,\\'\"()-]+$'");
            });
            e.Ignore(x => x.DisplayNames);
            e.Ignore(x => x.Descriptions);
        });

        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>(e =>
        {
            e.ToTable("OpenIddictTokens");
        });
    }

    public override int SaveChanges()
    {
        ValidateEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateEntities()
    {
        ValidateClientDisplayName();
        ValidateScopeDisplayName();
        ValidateScopeDescription();
    }

    private void ValidateClientDisplayName()
    {
        var appEntries = ChangeTracker.Entries<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in appEntries)
        {
            var name = entry.Entity.DisplayName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                // 영문(A-Z, a-z), 숫자(0-9), 공백 및 기본 문장부호만 허용 (한글 등 비영문 문자 차단)
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9\s\-_.,'&()]+$"))
                {
                    throw new InvalidOperationException($"ClientDisplayName ('{name}')에는 영문(English), 숫자, 기본 특수문자 및 공백만 등록할 수 있습니다.");
                }
            }
        }
    }

    private void ValidateScopeDisplayName()
    {
        var scopeEntries = ChangeTracker.Entries<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in scopeEntries)
        {
            var name = entry.Entity.DisplayName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                // 영문(A-Z, a-z), 숫자(0-9), 공백 및 기본 문장부호만 허용 (한글 등 비영문 문자 차단)
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9\s\-_.,'&()]+$"))
                {
                    throw new InvalidOperationException($"DisplayName ('{name}')에는 영문(English), 숫자, 기본 특수문자 및 공백만 등록할 수 있습니다.");
                }
            }
        }
    }

    private void ValidateScopeDescription()
    {
        var scopeEntries = ChangeTracker.Entries<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in scopeEntries)
        {
            var desc = entry.Entity.Description;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                // 영문(A-Z, a-z), 숫자(0-9), 공백 및 기본 문장부호만 허용 (한글 등 비영문 문자 차단)
                if (!System.Text.RegularExpressions.Regex.IsMatch(desc, @"^[a-zA-Z0-9\s\-_.,'&()]+$"))
                {
                    throw new InvalidOperationException($"Description ('{desc}')에는 영문(English), 숫자, 기본 특수문자 및 공백만 등록할 수 있습니다.");
                }
            }
        }
    }
}
