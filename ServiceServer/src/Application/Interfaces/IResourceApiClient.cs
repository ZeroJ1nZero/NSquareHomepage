using Application.DTOs;

namespace Application.Interfaces;

public interface IResourceApiClient
{
    Task<AboutDto?> GetAboutAsync(CancellationToken cancellationToken = default);
    Task<AboutDto?> UpdateAboutAsync(UpdateAboutDto dto, string? accessToken, CancellationToken cancellationToken = default);
    Task<ServiceDto?> GetServiceAsync(CancellationToken cancellationToken = default);
    Task<ServiceDto?> UpdateServiceAsync(UpdateServiceDto dto, string? accessToken, CancellationToken cancellationToken = default);
    Task<HistoryContainerDto?> GetHistoriesAsync(CancellationToken cancellationToken = default);
    Task<HistoryContainerDto?> SaveHistoriesAsync(SaveHistoryRequestDto dto, string? accessToken, CancellationToken cancellationToken = default);
    Task<bool> DeleteHistoryAsync(int id, string? accessToken, CancellationToken cancellationToken = default);
}
