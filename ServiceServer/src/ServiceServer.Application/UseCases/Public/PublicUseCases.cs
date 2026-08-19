using ServiceServer.Application.Common.Interfaces;
using ServiceServer.Application.DTOs;

namespace ServiceServer.Application.UseCases.Public;

public interface IGetCompanyAboutUseCase
{
    Task<AboutDto?> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetCompanyAboutUseCase : IGetCompanyAboutUseCase
{
    private readonly IResourceApiClient _client;
    public GetCompanyAboutUseCase(IResourceApiClient client) => _client = client;
    public Task<AboutDto?> ExecuteAsync(CancellationToken cancellationToken = default) => _client.GetAboutAsync(cancellationToken);
}

public interface IGetCompanyServiceUseCase
{
    Task<ServiceDto?> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetCompanyServiceUseCase : IGetCompanyServiceUseCase
{
    private readonly IResourceApiClient _client;
    public GetCompanyServiceUseCase(IResourceApiClient client) => _client = client;
    public Task<ServiceDto?> ExecuteAsync(CancellationToken cancellationToken = default) => _client.GetServiceAsync(cancellationToken);
}

public interface IGetCompanyHistoriesUseCase
{
    Task<HistoryContainerDto?> ExecuteAsync(CancellationToken cancellationToken = default);
}

public class GetCompanyHistoriesUseCase : IGetCompanyHistoriesUseCase
{
    private readonly IResourceApiClient _client;
    public GetCompanyHistoriesUseCase(IResourceApiClient client) => _client = client;
    public Task<HistoryContainerDto?> ExecuteAsync(CancellationToken cancellationToken = default) => _client.GetHistoriesAsync(cancellationToken);
}
