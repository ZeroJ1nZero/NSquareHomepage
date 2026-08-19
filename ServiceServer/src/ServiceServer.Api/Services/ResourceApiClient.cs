using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceServer.Api.DTOs;

namespace ServiceServer.Api.Services;

public class ResourceApiClient : IResourceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResourceApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ResourceApiClient(HttpClient httpClient, ILogger<ResourceApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AboutDto?> GetAboutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 A] ResourceServer로 회사 소개 정보 조회 요청 (GET /api/Home/about)");
        var response = await _httpClient.GetAsync("api/Home/about", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AboutDto>(JsonOptions, cancellationToken);
    }

    public async Task<AboutDto?> UpdateAboutAsync(UpdateAboutDto dto, string? accessToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 B] ResourceServer로 회사 소개 정보 수정 요청 (PUT /api/Home/about, Bearer Token 첨부)");
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/Home/about")
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AboutDto>(JsonOptions, cancellationToken);
    }

    public async Task<ServiceDto?> GetServiceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 A] ResourceServer로 서비스 정보 조회 요청 (GET /api/Home/service)");
        var response = await _httpClient.GetAsync("api/Home/service", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceDto>(JsonOptions, cancellationToken);
    }

    public async Task<ServiceDto?> UpdateServiceAsync(UpdateServiceDto dto, string? accessToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 B] ResourceServer로 서비스 정보 수정 요청 (PUT /api/Home/service, Bearer Token 첨부)");
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/Home/service")
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceDto>(JsonOptions, cancellationToken);
    }

    public async Task<HistoryContainerDto?> GetHistoriesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 A] ResourceServer로 전체 연혁 목록 조회 요청 (GET /api/Home/history)");
        var response = await _httpClient.GetAsync("api/Home/history", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HistoryContainerDto>(JsonOptions, cancellationToken);
    }

    public async Task<HistoryContainerDto?> SaveHistoriesAsync(SaveHistoryRequestDto dto, string? accessToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[트랙 B] ResourceServer로 연혁 항목 저장 요청 (PUT /api/Home/history, Bearer Token 첨부)");
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/Home/history")
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HistoryContainerDto>(JsonOptions, cancellationToken);
    }
}
