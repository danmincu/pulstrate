namespace TaskServer.Core.Interfaces;

/// <summary>
/// Factory for creating HttpClient instances configured with authentication tokens.
/// Used by task executors to make authenticated calls to downstream microservices.
/// </summary>
public interface ITaskHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient pre-configured with the Bearer token in the Authorization header.
    /// </summary>
    /// <param name="authToken">The JWT token to use for authentication. If null/empty, no auth header is added.</param>
    /// <param name="baseAddress">Optional base address for the HttpClient.</param>
    /// <returns>A configured HttpClient instance.</returns>
    HttpClient CreateClient(string? authToken, string? baseAddress = null);

    /// <summary>
    /// Creates an HttpClient pre-configured with the Bearer token in the Authorization header.
    /// </summary>
    /// <param name="authToken">The JWT token to use for authentication. If null/empty, no auth header is added.</param>
    /// <param name="baseAddress">Optional base address for the HttpClient.</param>
    /// <returns>A configured HttpClient instance.</returns>
    HttpClient CreateClient(string? authToken, Uri? baseAddress);
}
