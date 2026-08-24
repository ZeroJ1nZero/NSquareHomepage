using Microsoft.Extensions.DependencyInjection;
using Application.UseCases.Admin;
using Application.UseCases.Public;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Public UseCases (Track A)
        services.AddScoped<IGetCompanyAboutUseCase, GetCompanyAboutUseCase>();
        services.AddScoped<IGetCompanyServiceUseCase, GetCompanyServiceUseCase>();
        services.AddScoped<IGetCompanyHistoriesUseCase, GetCompanyHistoriesUseCase>();

        // Admin UseCases (Track B)
        services.AddScoped<IUpdateCompanyAboutUseCase, UpdateCompanyAboutUseCase>();
        services.AddScoped<IUpdateCompanyServiceUseCase, UpdateCompanyServiceUseCase>();
        services.AddScoped<ISaveCompanyHistoriesUseCase, SaveCompanyHistoriesUseCase>();
        services.AddScoped<IDeleteCompanyHistoryUseCase, DeleteCompanyHistoryUseCase>();

        // Auth & Pipeline UseCases (SOLID SRP & DIP)
        services.AddScoped<Application.UseCases.Auth.IInitiateSsoUseCase, Application.UseCases.Auth.InitiateSsoUseCase>();
        services.AddScoped<Application.UseCases.Auth.IVerifyCsrfStateUseCase, Application.UseCases.Auth.VerifyCsrfStateUseCase>();
        services.AddScoped<Application.UseCases.Auth.IExchangeTokenUseCase, Application.UseCases.Auth.ExchangeTokenUseCase>();

        return services;
    }
}
