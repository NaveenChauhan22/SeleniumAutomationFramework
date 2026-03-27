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
/// Uses ApiSessionContext for thread-safe, session-scoped token management.
/// Supports automatic token renewal before authenticated requests.
/// </summary>
public abstract class BaseAPIPage
{
    private readonly AuthClient? _authClient;

    /// <summary>
    /// Initializes a new instance of BaseAPIPage.
    /// </summary>
    /// <param name="apiClient">The HTTP client for API calls.</param>
    /// <param name="logger">Logger for operational visibility.</param>
    /// <param name="authClient">Optional AuthClient for automatic re-authentication. If provided, expiring tokens will be renewed before authenticated requests.</param>
    protected BaseAPIPage(APIClient apiClient, Serilog.ILogger logger, AuthClient? authClient = null)
    {
        ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient), "ApiClient cannot be null.");
        Logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        _authClient = authClient;
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

        try
        {
            // First attempt: make the request normally
            return await SendAsyncInternal(method, path, body, queryParams, requiresAuth, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no valid bearer token") && requiresAuth && _authClient is not null)
        {
            // Token has expired mid-test. Attempt automatic re-authentication using stored credentials.
            Logger.Warning("Authenticated request failed due to token expiry. Attempting automatic re-authentication with stored credentials. Error: {Error}", ex.Message);
            
            try
            {
                await _authClient.ReauthenticateIfStoredCredentialsAsync(cancellationToken).ConfigureAwait(false);
                Logger.Information("Re-authentication successful. Retrying the original request.");
                
                // Retry the request with the new token
                return await SendAsyncInternal(method, path, body, queryParams, requiresAuth, cancellationToken);
            }
            catch (Exception reauthEx)
            {
                Logger.Error(reauthEx, "Re-authentication failed. Original request cannot be retried.");
                throw new InvalidOperationException(
                    $"Request failed due to token expiry, and automatic re-authentication also failed. " +
                    $"Original error: {ex.Message}. Re-auth error: {reauthEx.Message}", 
                    reauthEx);
            }
        }
    }

    /// <summary>
    /// Internal implementation of SendAsync that performs the actual HTTP request.
    /// This method is called by SendAsync, potentially multiple times if token renewal is needed.
    /// </summary>
    private async Task<ApiCallResult> SendAsyncInternal(
        HttpMethod method,
        string path,
        object? body = null,
        IDictionary<string, string?>? queryParams = null,
        bool requiresAuth = false,
        CancellationToken cancellationToken = default)
    {
        var relativeUrl = BuildRelativeUrl(path, queryParams);

        return await AllureApi.Step($"{method} {relativeUrl}", async () =>
        {
            var requestBuilder = new APIRequestBuilder()
                .WithMethod(method)
                .WithEndpoint(relativeUrl);

            if (requiresAuth)
            {
                // Get token from session context, renewing it first when it is expired or close to expiry.
                string? token = await GetOrRefreshTokenAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException(
                        $"Endpoint '{path}' requires authentication, but no valid bearer token is available. " +
                        "Ensure user is authenticated via ApiSessionContext before making this request.");
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
                Serilog.Log.Logger.Error("Request content could not be sanitized; details redacted for security. Error: {Message}", ex.Message);
                requestText = "[Request content could not be sanitised.]";
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
                Serilog.Log.Logger.Error("Response content could not be sanitized; details redacted for security. Error: {Message}", ex.Message);
                responseText = "[Response content could not be sanitised.]";
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

    /// <summary>
    /// Gets the current token from the session context.
    /// If the token is expired or expiring, and an AuthClient is available, automatically re-authenticates using stored credentials.
    /// Thread-safe: uses a refresh lock to prevent concurrent token renewal operations.
    /// </summary>
    private async Task<string?> GetOrRefreshTokenAsync(CancellationToken cancellationToken)
    {
        var session = ApiSessionContext.Current;
        var tokenState = session.CurrentToken;

        // If no token exists at all, fail early
        if (tokenState is null)
        {
            return null;
        }

        // If token is still valid, return it
        if (tokenState.IsValid)
        {
            return tokenState.AccessToken;
        }

        // Token is expired/expiring. Attempt renewal if AuthClient is available.
        if (_authClient is null)
        {
            Logger.Warning("Token is expired or expiring, but no AuthClient is available for re-authentication. Token expires at: {ExpiresAt}", tokenState.ExpiresAt);
            return null;
        }

        Logger.Information("Token is expired or expiring. Attempting automatic re-authentication before sending the request. Token expires at: {ExpiresAt}", tokenState.ExpiresAt);

        try
        {
            // Acquire exclusive renewal lock to prevent concurrent re-authentication calls.
            using (await session.AcquireRefreshLockAsync(cancellationToken).ConfigureAwait(false))
            {
                // Check again after acquiring the lock in case another thread already renewed the token.
                tokenState = session.CurrentToken;
                if (tokenState?.IsValid == true)
                {
                    Logger.Debug("Token was already renewed by another thread. Using the current valid token.");
                    return tokenState.AccessToken;
                }

                var renewedToken = await _authClient.ReauthenticateIfStoredCredentialsAsync(cancellationToken).ConfigureAwait(false);
                if (renewedToken.IsValid)
                {
                    Logger.Information("Token renewal completed successfully. New expiration: {ExpiresAt}", renewedToken.ExpiresAt);
                    return renewedToken.AccessToken;
                }

                Logger.Warning("Re-authentication completed but returned a token that is already expired or within the grace period. Expires at: {ExpiresAt}", renewedToken.ExpiresAt);
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Automatic token renewal failed before the request was sent. The request will continue through the existing failure path if no valid token is available.");
            return null;
        }
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
            // Sanitize the response body before including in exception to prevent credential leakage
            string sanitizedContent;
            try
            {
                sanitizedContent = APIContentSanitizer.SanitizeDump(result.ResponseBody, showBearerToken: false);
                // Truncate to first 100 chars for context without being too verbose
                sanitizedContent = sanitizedContent.Substring(0, Math.Min(100, sanitizedContent.Length)) + "...";
            }
            catch
            {
                // If sanitization fails, use a generic message to avoid exposing raw content
                sanitizedContent = "[Response content could not be included in error message for security reasons.]";
            }
            
            throw new InvalidOperationException($"Failed to parse response body as JSON. Sanitized content: {sanitizedContent}", ex);
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
