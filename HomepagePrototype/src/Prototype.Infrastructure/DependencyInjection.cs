using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prototype.Application.Common.Interfaces;
using Prototype.Infrastructure.Persistence;

namespace Prototype.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=homepage.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (connectionString.Contains(".db") || connectionString.Contains("Data Source="))
            {
                options.UseSqlite(connectionString, b =>
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlServer(connectionString, b =>
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
