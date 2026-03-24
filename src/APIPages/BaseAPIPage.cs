using System.Net;
using System.Text;
using Allure.Net.Commons;
using Framework.API;
using Framework.Reporting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APITests.APIPages;

/// <summary>
/// Base API page object with common HttpClient send helpers, auth support, logging, and assertions.
/// </summary>
public abstract class BaseAPIPage
{
    private readonly Func<string?> _tokenAccessor;

    protected BaseAPIPage(APIClient apiClient, Serilog.ILogger logger, Func<string?> tokenAccessor)
    {
        ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient), "ApiClient cannot be null.");
        Logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        _tokenAccessor = tokenAccessor ?? throw new ArgumentNullException(nameof(tokenAccessor), "Token accessor function cannot be null.");
    }

    protected APIClient ApiClient { get; }
    protected Serilog.ILogger Logger { get; }

    protected async Task<ApiCallResult> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        IDictionary<string, string?>? queryParams = null,
        bool requiresAuth = false,
        CancellationToken cancellationToken = default)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method), "HttpMethod cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("API path cannot be null or empty.", nameof(path));
        }

        var relativeUrl = BuildRelativeUrl(path, queryParams);

        return await AllureApi.Step($"{method} {relativeUrl}", async () =>
        {
            var requestBuilder = new APIRequestBuilder()
                .WithMethod(method)
                .WithEndpoint(relativeUrl);

            if (requiresAuth)
            {
                string? token = null;
                try
                {
                    token = _tokenAccessor();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to retrieve authentication token from accessor.", ex);
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException($"Endpoint '{path}' requires authentication, but bearer token is not available. Ensure user is authenticated before making this request.");
                }

                requestBuilder.WithBearerToken(token);
            }

            if (body is not null)
            {
                requestBuilder.WithJsonBody(body);
            }

            using var request = requestBuilder.Build();

            string requestText;
            try
            {
                requestText = await FormatRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to format request for {method} {relativeUrl}.", ex);
            }

            try
            {
                requestText = APIContentSanitizer.SanitizeDump(requestText, ApiClient.ShowBearerToken);
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Warning("Failed to sanitize request content: {Message}", ex.Message);
                // Continue with unsanitized content if sanitization fails
            }

            Serilog.Log.Logger.Information("[API] Request: {Method} {Url}\n{RequestDetails}", method, relativeUrl, requestText);
            ReportHelper.AttachContent($"API Request - {method} {relativeUrl}", "text/plain", requestText, "txt");

            HttpResponseMessage response;
            try
            {
                response = await ApiClient.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"API call failed for {method} {relativeUrl}: {ex.Message}", ex);
            }

            string responseBody;
            try
            {
                responseBody = response.Content is null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read response body from {method} {relativeUrl}.", ex);
            }

            string responseText;
            try
            {
                responseText = FormatResponse(response, responseBody);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to format response from {method} {relativeUrl}.", ex);
            }

            try
            {
                responseText = APIContentSanitizer.SanitizeDump(responseText, ApiClient.ShowBearerToken);
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Warning("Failed to sanitize response content: {Message}", ex.Message);
                // Continue with unsanitized content if sanitization fails
            }

            Serilog.Log.Logger.Information("[API] Response: {StatusCode}\n{ResponseDetails}", (int)response.StatusCode, responseText);
            ReportHelper.AttachContent($"API Response - {method} {relativeUrl}", "text/plain", responseText, "txt");

            try
            {
                RuntimeContext.RecordApiExchange(requestText, responseText);
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Warning("Failed to record API exchange: {Message}", ex.Message);
                // Continue even if recording fails
            }

            return new ApiCallResult(response.StatusCode, responseBody);
        });
    }

    protected static JObject ParseObject(ApiCallResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result), "ApiCallResult cannot be null while parsing.");
        }

        if (string.IsNullOrWhiteSpace(result.ResponseBody))
        {
            throw new InvalidOperationException("Response body is empty. Cannot parse JSON from an empty response.");
        }

        try
        {
            return JObject.Parse(result.ResponseBody);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse response body as JSON. Response body: {result.ResponseBody.Substring(0, Math.Min(100, result.ResponseBody.Length))}...", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error while parsing response body: {ex.Message}", ex);
        }
    }

    private static string BuildRelativeUrl(string path, IDictionary<string, string?>? queryParams)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        var cleanPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";

        if (queryParams is null || queryParams.Count == 0)
        {
            return cleanPath;
        }

        var filtered = queryParams
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}")
            .ToArray();

        return filtered.Length == 0
            ? cleanPath
            : $"{cleanPath}?{string.Join("&", filtered)}";
    }

    private static async Task<string> FormatRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "HttpRequestMessage cannot be null while formatting.");
        }

        var sb = new StringBuilder();
        var requestUri = request.RequestUri?.ToString() ?? "<unknown URI>";
        sb.AppendLine($"{request.Method} {requestUri}");

        foreach (var header in request.Headers)
        {
            sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            sb.AppendLine();
            sb.AppendLine(PrettifyJson(body));
        }

        return sb.ToString().Trim();
    }

    private static string FormatResponse(HttpResponseMessage response, string body)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response), "HttpResponseMessage cannot be null while formatting.");
        }

        var sb = new StringBuilder();
        var reasonPhrase = response.ReasonPhrase ?? "Unknown";
        sb.AppendLine($"HTTP {(int)response.StatusCode} {reasonPhrase}");

        foreach (var header in response.Headers)
        {
            sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            sb.AppendLine();
            sb.AppendLine(PrettifyJson(body));
        }

        return sb.ToString().Trim();
    }

    private static string PrettifyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            var token = JToken.Parse(json);
            return token.ToString(Formatting.Indented);
        }
        catch (JsonException)
        {
            // If JSON is invalid, return as-is
            return json;
        }
        catch (Exception)
        {
            // Log unexpected errors but return original content
            return json;
        }
    }
}

public sealed record ApiCallResult(
    HttpStatusCode StatusCode,
    string ResponseBody);
