using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Persistence;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3306;Database=NSquareResourceDb;User=root;Password=1234;";

        var serverVersionStr = configuration["Database:ServerVersion"] ?? "10.11.0-mariadb";
        var serverVersion = ServerVersion.Parse(serverVersionStr);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion, b =>
                b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
