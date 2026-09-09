using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Services;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var resourceBaseUrl = configuration["ResourceServer:BaseUrl"] ?? "https://localhost:7002";
        services.AddHttpClient<IResourceApiClient, ResourceApiClient>(client =>
        {
            client.BaseAddress = new Uri(resourceBaseUrl.TrimEnd('/') + "/");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHttpClient("IdpClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddScoped<IOidcTokenExchangeService, OidcTokenExchangeService>();
        services.AddScoped<IOidcStateService, OidcStateService>();

        return services;
    }
}
