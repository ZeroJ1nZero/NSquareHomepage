using Application.Interfaces;
using Application.DTOs;

namespace Application.UseCases.Public;

/// <summary>
/// [Clean Architecture: Application Layer] 회사 소개 공개 조회 UseCase
/// 비로그인 일반 사용자도 접근 가능하며, ResourceServer로부터 회사 소개 데이터를 조회합니다.
/// </summary>
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

/// <summary>
/// [Clean Architecture: Application Layer] 주요 서비스 공개 조회 UseCase
/// </summary>
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

/// <summary>
/// [Clean Architecture: Application Layer] 회사 연혁 공개 조회 UseCase
/// </summary>
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
