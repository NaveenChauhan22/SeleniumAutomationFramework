using Framework.API;

namespace APITests.APIPages;

public sealed class AuthAPIPage(
    APIClient apiClient,
    Serilog.ILogger logger,
    AuthClient authClient,
    ApiSuiteData.EndpointData.AuthEndpointData endpoints)
    : BaseAPIPage(apiClient, logger, authClient)
{
    private readonly ApiSuiteData.EndpointData.AuthEndpointData _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints), "Auth endpoint data cannot be null.");

    public Task<ApiCallResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(_endpoints.Login))
        {
            throw new InvalidOperationException("Login endpoint is not configured.");
        }

        var payload = new
        {
            email,
            password
        };

        return SendAsync(HttpMethod.Post, _endpoints.Login, payload, cancellationToken: cancellationToken);
    }

    public Task<ApiCallResult> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoints.Me))
        {
            throw new InvalidOperationException("Get current user endpoint is not configured.");
        }

        return SendAsync(HttpMethod.Get, _endpoints.Me, requiresAuth: true, cancellationToken: cancellationToken);
    }
}
