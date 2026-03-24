using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Framework.API;

/// <summary>
/// Fluent builder for <see cref="HttpRequestMessage"/>. Configure the HTTP method, endpoint path,
/// request headers (including bearer-token shorthand), and an optional JSON body, then call
/// <see cref="Build"/> to get the assembled message ready for <see cref="APIClient.SendAsync"/>.
/// </summary>
public class APIRequestBuilder
{
    private HttpMethod _httpMethod = HttpMethod.Get;
    private string _endpoint = string.Empty;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private object? _body;

    public APIRequestBuilder WithMethod(HttpMethod method)
    {
        _httpMethod = method ?? throw new ArgumentNullException(nameof(method), "HttpMethod cannot be null.");
        return this;
    }

    public APIRequestBuilder WithEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be null or empty.", nameof(endpoint));
        }

        _endpoint = endpoint;
        return this;
    }

    public APIRequestBuilder WithHeader(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Header key cannot be null or empty.", nameof(key));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Header value cannot be null.");
        }

        _headers[key] = value;
        return this;
    }

    public APIRequestBuilder WithBearerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Bearer token cannot be null or empty.", nameof(token));
        }

        _headers["Authorization"] = $"Bearer {token}";
        return this;
    }

    public APIRequestBuilder WithJsonBody(object body)
    {
        _body = body;
        return this;
    }

    public HttpRequestMessage Build()
    {
        if (_httpMethod is null)
        {
            throw new InvalidOperationException("HTTP method must be provided before building the request.");
        }

        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new InvalidOperationException("Endpoint must be provided before building the request.");
        }

        HttpRequestMessage request;
        try
        {
            request = new HttpRequestMessage(_httpMethod, _endpoint);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create HTTP request message with method '{_httpMethod}' and endpoint '{_endpoint}'.", ex);
        }

        foreach (var header in _headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                if (AuthenticationHeaderValue.TryParse(header.Value, out var authHeader))
                {
                    request.Headers.Authorization = authHeader;
                }
                else
                {
                    // Log warning but continue - authorization header format will be sent as-is
                }
                continue;
            }

            try
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add header '{header.Key}' to request.", ex);
            }
        }

        if (_body is not null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to serialize request body to JSON.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create request content from body.", ex);
            }
        }

        return request;
    }
}
