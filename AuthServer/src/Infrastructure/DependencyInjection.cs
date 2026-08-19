using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default 설정이 필요합니다.");

        var serverVersion = ServerVersion.Parse(config["Database:ServerVersion"] ?? "10.11.0-mariadb");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion);
            options.UseOpenIddict(); // OpenIddict 테이블(클라이언트, 토큰 등) 등록
        });

        // ASP.NET Identity 대신 최소 구성: 비밀번호 해싱만 프레임워크 것을 재사용
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILoginAuditor, LoginAuditor>();

        return services;
    }
}
