using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Framework.Reporting;

namespace Framework.API;

/// <summary>
/// HTTP client wrapper for making REST API calls in tests. Wraps <see cref="HttpClient"/> with
/// a fixed base URL, supports bearer-token and custom-header auth, records every
/// request/response exchange in <see cref="Framework.Reporting.RuntimeContext"/> for Allure
/// attachment on failure, and provides convenience <c>GetAsync</c> / <c>PostAsync</c> methods
/// with automatic JSON deserialisation.
/// </summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private bool _disposed;

    public ApiClient(string baseUrl, ILogger<ApiClient>? logger = null)
    {
        _logger = logger ?? NullLogger<ApiClient>.Instance;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public void SetDefaultHeader(string key, string value)
    {
        _httpClient.DefaultRequestHeaders.Remove(key);
        _httpClient.DefaultRequestHeaders.Add(key, value);
    }

    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending API request: {Method} {Url}", request.Method, request.RequestUri);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        _logger.LogInformation("Received API response: {StatusCode}", (int)response.StatusCode);
        RuntimeContext.RecordApiExchange(
            await FormatRequestAsync(request, cancellationToken),
            await FormatResponseAsync(response, cancellationToken));
        return response;
    }

    public async Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        return await DeserializeAsync<TResponse>(response, cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(payload);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        return await DeserializeAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonConvert.DeserializeObject<T>(body);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private static async Task<string> FormatRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{request.Method} {request.RequestUri}");

        foreach (var header in request.Headers)
        {
            builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            builder.AppendLine();
            builder.AppendLine(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return builder.ToString().Trim();
    }

    private static async Task<string> FormatResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

        foreach (var header in response.Headers)
        {
            builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            builder.AppendLine();
            builder.AppendLine(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        return builder.ToString().Trim();
    }
}
