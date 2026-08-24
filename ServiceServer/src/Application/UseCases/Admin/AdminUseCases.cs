using Application.Interfaces;
using Application.DTOs;

namespace Application.UseCases.Admin;

public interface IUpdateCompanyAboutUseCase
{
    Task<AboutDto?> ExecuteAsync(UpdateAboutDto dto, string? accessToken, CancellationToken cancellationToken = default);
}

public class UpdateCompanyAboutUseCase : IUpdateCompanyAboutUseCase
{
    private readonly IResourceApiClient _client;
    public UpdateCompanyAboutUseCase(IResourceApiClient client) => _client = client;
    public Task<AboutDto?> ExecuteAsync(UpdateAboutDto dto, string? accessToken, CancellationToken cancellationToken = default)
        => _client.UpdateAboutAsync(dto, accessToken, cancellationToken);
}

public interface IUpdateCompanyServiceUseCase
{
    Task<ServiceDto?> ExecuteAsync(UpdateServiceDto dto, string? accessToken, CancellationToken cancellationToken = default);
}

public class UpdateCompanyServiceUseCase : IUpdateCompanyServiceUseCase
{
    private readonly IResourceApiClient _client;
    public UpdateCompanyServiceUseCase(IResourceApiClient client) => _client = client;
    public Task<ServiceDto?> ExecuteAsync(UpdateServiceDto dto, string? accessToken, CancellationToken cancellationToken = default)
        => _client.UpdateServiceAsync(dto, accessToken, cancellationToken);
}

public interface ISaveCompanyHistoriesUseCase
{
    Task<HistoryContainerDto?> ExecuteAsync(SaveHistoryRequestDto dto, string? accessToken, CancellationToken cancellationToken = default);
}

public class SaveCompanyHistoriesUseCase : ISaveCompanyHistoriesUseCase
{
    private readonly IResourceApiClient _client;
    public SaveCompanyHistoriesUseCase(IResourceApiClient client) => _client = client;
    public Task<HistoryContainerDto?> ExecuteAsync(SaveHistoryRequestDto dto, string? accessToken, CancellationToken cancellationToken = default)
        => _client.SaveHistoriesAsync(dto, accessToken, cancellationToken);
}

public interface IDeleteCompanyHistoryUseCase
{
    Task<bool> ExecuteAsync(int id, string? accessToken, CancellationToken cancellationToken = default);
}

public class DeleteCompanyHistoryUseCase : IDeleteCompanyHistoryUseCase
{
    private readonly IResourceApiClient _client;
    public DeleteCompanyHistoryUseCase(IResourceApiClient client) => _client = client;
    public Task<bool> ExecuteAsync(int id, string? accessToken, CancellationToken cancellationToken = default)
        => _client.DeleteHistoryAsync(id, accessToken, cancellationToken);
}
