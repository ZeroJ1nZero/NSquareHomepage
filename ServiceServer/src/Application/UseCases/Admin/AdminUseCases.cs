using Application.Interfaces;
using Application.DTOs;

namespace Application.UseCases.Admin;

/// <summary>
/// [Clean Architecture: Application Layer] 회사 소개 수정 UseCase
/// 서비스 서버 세션에 보관된 Access Token(JWT)을 첨부하여 ResourceServer의 CUD API를 호출합니다.
/// </summary>
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

/// <summary>
/// [Clean Architecture: Application Layer] 주요 서비스 수정 UseCase
/// 서비스 서버 세션에 보관된 Access Token(JWT)을 첨부하여 ResourceServer의 CUD API를 호출합니다.
/// </summary>
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

/// <summary>
/// [Clean Architecture: Application Layer] 회사 연혁 일괄 저장/수정 UseCase
/// </summary>
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

/// <summary>
/// [Clean Architecture: Application Layer] 회사 연혁 단건 삭제 UseCase
/// </summary>
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
