using System.Net.Http.Headers;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Services;

/// <summary>
/// Factory for creating HttpClient instances configured with authentication tokens.
/// Used by task executors to make authenticated calls to downstream microservices.
/// </summary>
public class TaskHttpClientFactory : ITaskHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TaskHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public HttpClient CreateClient(string? authToken, string? baseAddress = null)
    {
        return CreateClient(authToken, string.IsNullOrEmpty(baseAddress) ? null : new Uri(baseAddress));
    }

    /// <inheritdoc />
    public HttpClient CreateClient(string? authToken, Uri? baseAddress)
    {
        var client = _httpClientFactory.CreateClient("TaskExecutor");

        if (baseAddress != null)
        {
            client.BaseAddress = baseAddress;
        }

        if (!string.IsNullOrWhiteSpace(authToken))
        {
            // Remove "Bearer " prefix if already present
            var token = authToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authToken.Substring(7)
                : authToken;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
