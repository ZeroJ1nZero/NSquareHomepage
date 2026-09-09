using Microsoft.Extensions.DependencyInjection;
using Application.UseCases.About;
using Application.UseCases.History;
using Application.UseCases.Service;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // About UseCases
        services.AddScoped<IGetAboutUseCase, GetAboutUseCase>();
        services.AddScoped<IUpdateAboutUseCase, UpdateAboutUseCase>();

        // Service UseCases
        services.AddScoped<IGetServiceUseCase, GetServiceUseCase>();
        services.AddScoped<IUpdateServiceUseCase, UpdateServiceUseCase>();

        // History UseCases
        services.AddScoped<IGetCompanyHistoriesUseCase, GetCompanyHistoriesUseCase>();
        services.AddScoped<IGetCompanyHistoryByIdUseCase, GetCompanyHistoryByIdUseCase>();
        services.AddScoped<ICreateCompanyHistoryUseCase, CreateCompanyHistoryUseCase>();
        services.AddScoped<IUpdateCompanyHistoryUseCase, UpdateCompanyHistoryUseCase>();
        services.AddScoped<IDeleteCompanyHistoryUseCase, DeleteCompanyHistoryUseCase>();

        return services;
    }
}
