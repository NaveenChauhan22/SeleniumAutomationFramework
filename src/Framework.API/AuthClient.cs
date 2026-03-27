using Newtonsoft.Json.Linq;
using System;

namespace Framework.API;

/// <summary>
/// Client for API authentication operations (login, token refresh).
/// Works in conjunction with ApiSessionContext to manage token lifecycle.
/// 
/// Thread safety:
/// - Uses ApiSessionContext's refresh lock to coordinate token updates across parallel tests
/// - Safe for concurrent calls in NUnit parallel test execution
/// 
/// Responsibilities:
/// - Perform login API call and extract token from response
/// - Perform token refresh API calls (when implemented by backend)
/// - Calculate token expiration time
/// - Store tokens in ApiSessionContext (in-memory only)
/// </summary>
public sealed class AuthClient
{
    private readonly APIClient _httpClient;
    private readonly Serilog.ILogger _logger;
    private readonly string _loginEndpoint;
    private readonly string _tokenJsonPath;
    private readonly int _defaultTokenTtlSeconds;

    /// <summary>
    /// Initializes a new instance of the AuthClient.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API calls.</param>
    /// <param name="logger">Logger for operational visibility.</param>
    /// <param name="loginEndpoint">The API endpoint for login (e.g., "/api/auth/login").</param>
    /// <param name="tokenJsonPath">JSONPath to extract token from login response (e.g., "$.token").</param>
    /// <param name="defaultTokenTtlSeconds">Default token TTL if not provided by server. Default: 3600 (1 hour).</param>
    public AuthClient(
        APIClient httpClient,
        Serilog.ILogger logger,
        string loginEndpoint,
        string tokenJsonPath,
        int defaultTokenTtlSeconds = 3600)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient), "APIClient cannot be null.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        _loginEndpoint = !string.IsNullOrWhiteSpace(loginEndpoint)
            ? loginEndpoint
            : throw new ArgumentException("Login endpoint cannot be null or empty.", nameof(loginEndpoint));
        _tokenJsonPath = !string.IsNullOrWhiteSpace(tokenJsonPath)
            ? tokenJsonPath
            : throw new ArgumentException("Token JSONPath cannot be null or empty.", nameof(tokenJsonPath));
        _defaultTokenTtlSeconds = defaultTokenTtlSeconds > 0
            ? defaultTokenTtlSeconds
            : throw new ArgumentException("Token TTL must be greater than 0.", nameof(defaultTokenTtlSeconds));
    }

    /// <summary>
    /// Authenticates using email and password credentials.
    /// Extracts the token from the response and stores it in ApiSessionContext.
    /// </summary>
    /// <param name="email">User email address.</param>
    /// <param name="password">User password.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The stored TokenState.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if login fails, token extraction fails, or session context update fails.
    /// </exception>
    public async Task<TokenState> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        try
        {
            _logger.Information("Authenticating user: {Email}", email);

            var payload = new { email, password };
            var request = new APIRequestBuilder()
                .WithMethod(HttpMethod.Post)
                .WithEndpoint(_loginEndpoint)
                .WithJsonBody(payload)
                .Build();

            using (request)
            {
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Login API call failed for endpoint '{_loginEndpoint}': {ex.Message}", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Login API returned unsuccessful status: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {errorBody}");
                }

                string responseBody;
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to read login response body: {ex.Message}", ex);
                }

                TokenState tokenState;
                try
                {
                    tokenState = ExtractTokenFromResponse(responseBody);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to extract authentication token from login response using JSONPath '{_tokenJsonPath}': {ex.Message}", ex);
                }

                _logger.Information("User authenticated successfully: {Email}", email);

                // Store token in session context
                try
                {
                    var sessionContext = ApiSessionContext.Current;
                    _logger.Debug("Obtained session context: {ContextHashCode}", sessionContext.GetHashCode());
                    
                    sessionContext.SetToken(tokenState);
                    sessionContext.StoreCredentials(email, password);
                    _logger.Information("Token stored in session context. AccessToken={TokenPreview}, Expires at: {ExpiresAt}, TTL: {TtlSeconds}s",
                        tokenState.AccessToken.Substring(0, Math.Min(20, tokenState.AccessToken.Length)) + "...",
                        tokenState.ExpiresAt, 
                        (int)tokenState.TimeRemaining.TotalSeconds);
                    
                    // Verify it was stored
                    var storedToken = ApiSessionContext.Current.CurrentAccessToken;
                    _logger.Information("Verification - Token retrieved from context: {IsStored}", !string.IsNullOrEmpty(storedToken));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to store token in session context");
                    throw;
                }

                return tokenState;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Authentication failed for user: {Email}", email);
            throw;
        }
    }

    /// <summary>
    /// Refreshes the current authentication token using the refresh-token endpoint.
    /// This is typically called automatically before API calls if the token is expiring.
    /// 
    /// NOTE: Only implement this if your backend supports token refresh endpoints.
    /// If not supported or if the refresh fails, you may need to re-login.
    /// </summary>
    /// <param name="refreshToken">The refresh token from the current session.</param>
    /// <param name="refreshEndpoint">The API endpoint for token refresh (e.g., "/api/auth/refresh").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The new TokenState.</returns>
    /// <exception cref="InvalidOperationException">Thrown if refresh fails.</exception>
    public async Task<TokenState> RefreshTokenAsync(
        string refreshToken,
        string refreshEndpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty.", nameof(refreshToken));
        }

        if (string.IsNullOrWhiteSpace(refreshEndpoint))
        {
            throw new ArgumentException("Refresh endpoint cannot be null or empty.", nameof(refreshEndpoint));
        }

        try
        {
            _logger.Information("Refreshing authentication token via endpoint: {RefreshEndpoint}", refreshEndpoint);

            var payload = new { refreshToken };
            var request = new APIRequestBuilder()
                .WithMethod(HttpMethod.Post)
                .WithEndpoint(refreshEndpoint)
                .WithJsonBody(payload)
                .Build();

            using (request)
            {
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Token refresh API call failed for endpoint '{refreshEndpoint}': {ex.Message}", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Token refresh API returned unsuccessful status: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {errorBody}");
                }

                string responseBody;
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to read token refresh response body: {ex.Message}", ex);
                }

                TokenState tokenState;
                try
                {
                    tokenState = ExtractTokenFromResponse(responseBody);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to extract refreshed token from response using JSONPath '{_tokenJsonPath}': {ex.Message}", ex);
                }

                _logger.Information("Token refreshed successfully. New expiration: {ExpiresAt}", tokenState.ExpiresAt);

                // Store new token in session context
                ApiSessionContext.Current.SetToken(tokenState);

                return tokenState;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Token refresh failed via endpoint: {RefreshEndpoint}", refreshEndpoint);
            throw;
        }
    }

    /// <summary>
    /// Gets the current valid token from the session context.
    /// If the token is expired or expiring, returns null (caller should refresh).
    /// </summary>
    public string? GetCurrentToken()
    {
        return ApiSessionContext.Current.AccessToken;
    }

    /// <summary>
    /// Gets the current token state from the session context.
    /// </summary>
    public TokenState? GetCurrentTokenState()
    {
        return ApiSessionContext.Current.CurrentToken;
    }

    /// <summary>
    /// Validates token existence and expiry state.
    /// </summary>
    public void ValidateTokenExists()
    {
        var token = ApiSessionContext.Current.CurrentToken;
        if (token is null)
        {
            throw new InvalidOperationException(
                "No authentication token found in session context. " +
                "Ensure LoginAsync() is called before making authenticated API requests.");
        }

        if (token.IsExpiredOrExpiring)
        {
            throw new InvalidOperationException(
                $"Authentication token has expired or is expiring soon (expires at {token.ExpiresAt:O}). " +
                "Token refresh is required. Call RefreshTokenAsync() to obtain a new token.");
        }
    }

    /// <summary>
    /// Forces re-authentication using stored credentials from the session context.
    /// Called automatically when an authenticated request fails due to token expiry mid-test.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The new TokenState after re-authentication.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if stored credentials are not available or re-authentication fails.
    /// </exception>
    public async Task<TokenState> ReauthenticateIfStoredCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var (email, password) = ApiSessionContext.Current.GetStoredCredentials();
        
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Cannot re-authenticate: no stored credentials found in session context. " +
                "Ensure LoginAsync() was called first to store credentials.");
        }

        _logger.Information("Token expired mid-test. Re-authenticating with stored credentials for: {Email}", email);
        
        try
        {
            return await LoginAsync(email, password, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to re-authenticate after token expiry for user: {Email}", email);
            throw;
        }
    }

    /// <summary>
    /// Extracts token from the API login/refresh response using JSONPath.
    /// Calculates expiration time based on default TTL.
    /// </summary>
    private TokenState ExtractTokenFromResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new InvalidOperationException("Response body cannot be null or empty when extracting token.");
        }

        try
        {
            var response = JObject.Parse(responseBody);
            var tokenValue = response.SelectToken(_tokenJsonPath);

            if (tokenValue is null)
            {
                throw new InvalidOperationException(
                    $"Token not found at JSONPath '{_tokenJsonPath}' in response: {responseBody}");
            }

            string? token = tokenValue.Value<string>();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"Token value at JSONPath '{_tokenJsonPath}' is null, empty, or not a string.");
            }

            // Calculate expiration time
            // If the API response includes tokenExpiresIn, use that; otherwise use default TTL
            var expiresInToken = response.SelectToken("$.expiresIn") ?? response.SelectToken("$.expires_in");
            int ttlSeconds = _defaultTokenTtlSeconds;

            if (expiresInToken is not null && int.TryParse(expiresInToken.ToString(), out int serverTtl) && serverTtl > 0)
            {
                ttlSeconds = serverTtl;
                _logger.Debug("Using server-provided token TTL: {TtlSeconds} seconds from response field 'expiresIn'/'expires_in'", ttlSeconds);
            }
            else
            {
                _logger.Debug("Server did not provide valid expiresIn/expires_in. Using default token TTL: {TtlSeconds} seconds", ttlSeconds);
            }

            var utcNow = DateTimeOffset.UtcNow;
            var expiresAt = utcNow.AddSeconds(ttlSeconds);
            
            _logger.Debug("Token extraction: Issued at UTC={IssuedAtUtc:O}, ExpiresAt UTC={ExpiresAtUtc:O}, TTL={TtlSeconds}s, TimeRemaining={TimeRemaining}ms",
                utcNow, expiresAt, ttlSeconds, (expiresAt - utcNow).TotalMilliseconds);

            if (expiresAt <= utcNow)
            {
                _logger.Warning("WARNING: Calculated expiration time ({ExpiresAt:O}) is not in the future! Current UTC: {UtcNow:O}. TTL was {TtlSeconds} seconds.",
                    expiresAt, utcNow, ttlSeconds);
            }

            return new TokenState(
                AccessToken: token,
                ExpiresAt: expiresAt,
                RefreshToken: null);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse or extract token from response body: {ex.Message}", ex);
        }
    }
}
