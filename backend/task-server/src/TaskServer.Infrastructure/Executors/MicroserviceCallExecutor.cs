using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

namespace TaskServer.Infrastructure.Executors;

/// <summary>
/// Payload for microservice call tasks.
/// </summary>
public class MicroserviceCallPayload
{
    /// <summary>
    /// The URL of the microservice endpoint to call.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method to use (GET, POST, PUT, DELETE, etc.). Default: GET.
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Optional request body (for POST/PUT). JSON string.
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// Optional content type for request body. Default: application/json.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Number of retry attempts on failure. Default: 0.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay between retries in milliseconds. Default: 1000.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Request timeout in seconds. Default: 30.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Progress payload for microservice calls - returned in the progress update.
/// </summary>
public class MicroserviceCallProgressPayload
{
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("attempt")]
    public int Attempt { get; set; }

    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("responseBody")]
    public string? ResponseBody { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("hasAuthToken")]
    public bool HasAuthToken { get; set; }
}

/// <summary>
/// Task executor that makes authenticated HTTP calls to microservices.
///
/// This executor demonstrates how to:
/// 1. Access the JWT auth token captured at task creation time
/// 2. Use ITaskHttpClientFactory to create authenticated HttpClient
/// 3. Pass the original user's credentials to downstream services
///
/// Usage:
/// - Create a task with type "microservice-call"
/// - Provide payload with URL and optional method/body
/// - The executor will use the auth token from the original request
/// </summary>
public class MicroserviceCallExecutor : ITaskExecutor
{
    private readonly ITaskHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public string TaskType => "microservice-call";

    public MicroserviceCallExecutor(ITaskHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<MicroserviceCallPayload>(task.Payload, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid payload for microservice-call task");

        if (string.IsNullOrWhiteSpace(payload.Url))
        {
            throw new InvalidOperationException("URL is required for microservice-call task");
        }

        var maxAttempts = payload.RetryCount + 1;
        var hasAuthToken = !string.IsNullOrWhiteSpace(task.AuthToken);

        // Report initial progress
        progress.Report(new TaskProgressUpdate(
            0,
            $"Starting {payload.Method} request to {payload.Url}",
            JsonSerializer.Serialize(new MicroserviceCallProgressPayload
            {
                Phase = "starting",
                Attempt = 0,
                MaxAttempts = maxAttempts,
                HasAuthToken = hasAuthToken
            }, JsonOptions)
        ));

        // Create HttpClient with the task's auth token
        // This is the key feature - the auth token from the original API call
        // is passed to the downstream microservice
        using var httpClient = _httpClientFactory.CreateClient(task.AuthToken);
        httpClient.Timeout = TimeSpan.FromSeconds(payload.TimeoutSeconds);

        Exception? lastException = null;
        HttpResponseMessage? response = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var attemptProgress = (double)(attempt - 1) / maxAttempts * 50;
                progress.Report(new TaskProgressUpdate(
                    attemptProgress,
                    $"Attempt {attempt}/{maxAttempts}: Calling {payload.Method} {payload.Url}",
                    JsonSerializer.Serialize(new MicroserviceCallProgressPayload
                    {
                        Phase = "calling",
                        Attempt = attempt,
                        MaxAttempts = maxAttempts,
                        HasAuthToken = hasAuthToken
                    }, JsonOptions)
                ));

                // Build the request
                var request = new HttpRequestMessage(new HttpMethod(payload.Method), payload.Url);

                if (!string.IsNullOrWhiteSpace(payload.RequestBody))
                {
                    request.Content = new StringContent(
                        payload.RequestBody,
                        Encoding.UTF8,
                        payload.ContentType
                    );
                }

                // Execute the request
                response = await httpClient.SendAsync(request, cancellationToken);

                // If successful, break out of retry loop
                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                // Non-success status code
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                lastException = new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                );

                // Report retry if not last attempt
                if (attempt < maxAttempts)
                {
                    progress.Report(new TaskProgressUpdate(
                        attemptProgress + 10,
                        $"Attempt {attempt} failed with status {(int)response.StatusCode}, retrying in {payload.RetryDelayMs}ms...",
                        JsonSerializer.Serialize(new MicroserviceCallProgressPayload
                        {
                            Phase = "retrying",
                            Attempt = attempt,
                            MaxAttempts = maxAttempts,
                            StatusCode = (int)response.StatusCode,
                            Error = errorBody,
                            HasAuthToken = hasAuthToken
                        }, JsonOptions)
                    ));

                    await Task.Delay(payload.RetryDelayMs, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    progress.Report(new TaskProgressUpdate(
                        (double)(attempt - 1) / maxAttempts * 50 + 10,
                        $"Attempt {attempt} failed: {ex.Message}, retrying in {payload.RetryDelayMs}ms...",
                        JsonSerializer.Serialize(new MicroserviceCallProgressPayload
                        {
                            Phase = "retrying",
                            Attempt = attempt,
                            MaxAttempts = maxAttempts,
                            Error = ex.Message,
                            HasAuthToken = hasAuthToken
                        }, JsonOptions)
                    ));

                    await Task.Delay(payload.RetryDelayMs, cancellationToken);
                }
            }
        }

        // Check final result
        if (response == null || !response.IsSuccessStatusCode)
        {
            var errorMessage = lastException?.Message ?? "Unknown error";
            progress.Report(new TaskProgressUpdate(
                100,
                $"Request failed after {maxAttempts} attempt(s): {errorMessage}",
                JsonSerializer.Serialize(new MicroserviceCallProgressPayload
                {
                    Phase = "failed",
                    Attempt = maxAttempts,
                    MaxAttempts = maxAttempts,
                    StatusCode = response != null ? (int)response.StatusCode : null,
                    Error = errorMessage,
                    HasAuthToken = hasAuthToken
                }, JsonOptions)
            ));

            throw new InvalidOperationException($"Microservice call failed: {errorMessage}");
        }

        // Read successful response
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        // Truncate response body for progress payload if too long
        var truncatedBody = responseBody.Length > 1000
            ? responseBody.Substring(0, 1000) + "... (truncated)"
            : responseBody;

        progress.Report(new TaskProgressUpdate(
            100,
            $"Completed {payload.Method} {payload.Url} with status {(int)response.StatusCode}",
            JsonSerializer.Serialize(new MicroserviceCallProgressPayload
            {
                Phase = "completed",
                Attempt = maxAttempts,
                MaxAttempts = maxAttempts,
                StatusCode = (int)response.StatusCode,
                ResponseBody = truncatedBody,
                HasAuthToken = hasAuthToken
            }, JsonOptions)
        ));
    }
}
