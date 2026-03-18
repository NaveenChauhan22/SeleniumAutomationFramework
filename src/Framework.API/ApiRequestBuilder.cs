using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Framework.API;

/// <summary>
/// Fluent builder for <see cref="HttpRequestMessage"/>. Configure the HTTP method, endpoint path,
/// request headers (including bearer-token shorthand), and an optional JSON body, then call
/// <see cref="Build"/> to get the assembled message ready for <see cref="ApiClient.SendAsync"/>.
/// </summary>
public class ApiRequestBuilder
{
    private HttpMethod _httpMethod = HttpMethod.Get;
    private string _endpoint = string.Empty;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private object? _body;

    public ApiRequestBuilder WithMethod(HttpMethod method)
    {
        _httpMethod = method;
        return this;
    }

    public ApiRequestBuilder WithEndpoint(string endpoint)
    {
        _endpoint = endpoint;
        return this;
    }

    public ApiRequestBuilder WithHeader(string key, string value)
    {
        _headers[key] = value;
        return this;
    }

    public ApiRequestBuilder WithBearerToken(string token)
    {
        _headers["Authorization"] = $"Bearer {token}";
        return this;
    }

    public ApiRequestBuilder WithJsonBody(object body)
    {
        _body = body;
        return this;
    }

    public HttpRequestMessage Build()
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new InvalidOperationException("Endpoint must be provided before building the request.");
        }

        var request = new HttpRequestMessage(_httpMethod, _endpoint);

        foreach (var header in _headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                if (AuthenticationHeaderValue.TryParse(header.Value, out var authHeader))
                {
                    request.Headers.Authorization = authHeader;
                }
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (_body is not null)
        {
            var json = JsonConvert.SerializeObject(_body);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        return request;
    }
}
