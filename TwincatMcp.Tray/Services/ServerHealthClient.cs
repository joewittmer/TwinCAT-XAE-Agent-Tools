using System.Net.Http.Json;
using TwincatMcp.Tray.Models;

namespace TwincatMcp.Tray.Services;

internal sealed class ServerHealthClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    public async Task<bool> IsHealthyAsync(TraySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            HealthResponse? response = await _httpClient.GetFromJsonAsync<HealthResponse>(
                $"http://127.0.0.1:{settings.Port}/health",
                cancellationToken);

            return string.Equals(response?.Endpoint, "/mcp", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class HealthResponse
    {
        public string? Endpoint { get; set; }
    }
}
