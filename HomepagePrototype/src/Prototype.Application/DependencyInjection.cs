using Microsoft.Extensions.DependencyInjection;
using Prototype.Application.UseCases.CompanyHistory;
using Prototype.Application.UseCases.CompanyInfo;

namespace Prototype.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // CompanyInfo UseCases
        services.AddScoped<IGetCompanyInfoUseCase, GetCompanyInfoUseCase>();
        services.AddScoped<IUpdateCompanyInfoUseCase, UpdateCompanyInfoUseCase>();

        // CompanyHistory UseCases
        services.AddScoped<IGetCompanyHistoriesUseCase, GetCompanyHistoriesUseCase>();
        services.AddScoped<IGetCompanyHistoryByIdUseCase, GetCompanyHistoryByIdUseCase>();
        services.AddScoped<ICreateCompanyHistoryUseCase, CreateCompanyHistoryUseCase>();
        services.AddScoped<IUpdateCompanyHistoryUseCase, UpdateCompanyHistoryUseCase>();
        services.AddScoped<IDeleteCompanyHistoryUseCase, DeleteCompanyHistoryUseCase>();

        return services;
    }
}
