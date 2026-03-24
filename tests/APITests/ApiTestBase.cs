using NUnit.Framework.Interfaces;
using Framework.Core.Configuration;
using Framework.Core.Utilities;
using Framework.Data;
using Framework.API;
using Framework.Reporting;
using APITests.APIPages;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace APITests;

public abstract class APITestBase : AllureTestBase
{
    protected Serilog.ILogger Logger = Serilog.Log.Logger;
    protected HttpClient SharedHttpClient = null!;
    protected APIClient SharedApiClient = null!;
    protected AuthAPIPage AuthApi = null!;
    protected EventsAPIPage EventsApi = null!;
    protected BookingsAPIPage BookingsApi = null!;
    protected ApiSuiteData ApiData = null!;
    protected LoginDataModel LoginData = null!;

    private string? _bearerToken;
    private IDisposable? _executionLoggerHandle;
    private readonly Stopwatch _executionTimer = new();
    private DateTimeOffset _testStartedAt;
    private string _priority = "Unspecified";
    private string _suiteName = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var apiBaseUrl = ConfigManager.GetString("Api:BaseUrl");
        Assert.That(apiBaseUrl, Is.Not.Null.And.Not.Empty, "Api:BaseUrl configuration is required. Ensure it is set in appsettings.json or environment variables.");

        SharedHttpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        Assert.That(SharedHttpClient, Is.Not.Null, "SharedHttpClient initialization failed.");
        Assert.That(SharedHttpClient.BaseAddress, Is.Not.Null, "SharedHttpClient BaseAddress should not be null.");

        SharedHttpClient.DefaultRequestHeaders.Accept.Clear();
        SharedHttpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        LoginData = LoadLoginData();
        Assert.That(LoginData, Is.Not.Null, "LoginData should not be null after loading from loginData.json.");
        Assert.That(LoginData.ApiAuth, Is.Not.Null, "LoginData.ApiAuth should not be null.");
        
        ApiData = LoadApiTestData(LoginData.ApiAuth);
        Assert.That(ApiData, Is.Not.Null, "ApiData should not be null after merging from all data sources.");
        Assert.That(ApiData.Endpoints, Is.Not.Null, "ApiData.Endpoints should not be null.");
        Assert.That(ApiData.Assertions, Is.Not.Null, "ApiData.Assertions should not be null.");
        
        SharedApiClient = new APIClient(SharedHttpClient);
        Assert.That(SharedApiClient, Is.Not.Null, "SharedApiClient initialization failed.");
        
        SharedApiClient.ShowBearerToken = ConfigManager.GetBool("Api:ShowBearerToken");
        
        AuthApi = new AuthAPIPage(SharedApiClient, Logger, () => _bearerToken, ApiData.Endpoints.Auth);
        Assert.That(AuthApi, Is.Not.Null, "AuthApiPage initialization failed.");
        Assert.That(ApiData.Endpoints.Auth, Is.Not.Null, "ApiData.Endpoints.Auth should not be null.");
        
        EventsApi = new EventsAPIPage(SharedApiClient, Logger, () => _bearerToken, ApiData.Endpoints.Events);
        Assert.That(EventsApi, Is.Not.Null, "EventsApiPage initialization failed.");
        Assert.That(ApiData.Endpoints.Events, Is.Not.Null, "ApiData.Endpoints.Events should not be null.");
        
        BookingsApi = new BookingsAPIPage(SharedApiClient, Logger, () => _bearerToken, ApiData.Endpoints.Bookings);
        Assert.That(BookingsApi, Is.Not.Null, "BookingsApiPage initialization failed.");
        Assert.That(ApiData.Endpoints.Bookings, Is.Not.Null, "ApiData.Endpoints.Bookings should not be null.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            if (SharedApiClient != null)
            {
                SharedApiClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error disposing SharedApiClient: {Message}", ex.Message);
        }

        try
        {
            if (SharedHttpClient != null)
            {
                SharedHttpClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error disposing SharedHttpClient: {Message}", ex.Message);
        }
    }

    private ApiSuiteData LoadApiTestData(ApiAuthData authData)
    {
        Assert.That(authData, Is.Not.Null, "ApiAuthData cannot be null. Ensure apiAuth section exists in loginData.json.");
        Assert.That(authData.Endpoints, Is.Not.Null, "authData.Endpoints cannot be null.");
        Assert.That(authData.Assertions, Is.Not.Null, "authData.Assertions cannot be null.");
        
        Assert.That(authData.Endpoints.Login, Is.Not.Null.And.Not.Empty, "apiAuth.endpoints.login is required in loginData.json");
        Assert.That(authData.Endpoints.Me, Is.Not.Null.And.Not.Empty, "apiAuth.endpoints.me is required in loginData.json");
        Assert.That(authData.Assertions.TokenJsonPath, Is.Not.Null.And.Not.Empty, "apiAuth.assertions.tokenJsonPath is required in loginData.json");
        Assert.That(authData.Assertions.CurrentUserEmailJsonPath, Is.Not.Null.And.Not.Empty, "apiAuth.assertions.currentUserEmailJsonPath is required in loginData.json");

        var eventData = LoadEventTestData();
        Assert.That(eventData, Is.Not.Null, "EventApiDataModel should not be null after loading from eventData.json.");
        Assert.That(eventData.Endpoints, Is.Not.Null, "eventData.Endpoints should not be null.");
        Assert.That(eventData.Events, Is.Not.Null, "eventData.Events should not be null.");
        Assert.That(eventData.Assertions, Is.Not.Null, "eventData.Assertions should not be null.");
        
        var bookingData = LoadBookingTestData();
        Assert.That(bookingData, Is.Not.Null, "BookingApiDataModel should not be null after loading from bookingData.json.");
        Assert.That(bookingData.Endpoints, Is.Not.Null, "bookingData.Endpoints should not be null.");
        Assert.That(bookingData.Bookings, Is.Not.Null, "bookingData.Bookings should not be null.");
        Assert.That(bookingData.Assertions, Is.Not.Null, "bookingData.Assertions should not be null.");

        return new ApiSuiteData
        {
            Endpoints = new ApiSuiteData.EndpointData
            {
                Auth = authData.Endpoints,
                Events = eventData.Endpoints,
                Bookings = bookingData.Endpoints
            },
            Events = eventData.Events,
            Bookings = bookingData.Bookings,
            Queries = new ApiSuiteData.QueryData
            {
                Events = eventData.Queries,
                Bookings = bookingData.Queries
            },
            Assertions = new ApiSuiteData.AssertionData
            {
                Auth = new ApiSuiteData.AssertionData.AuthAssertionData
                {
                    TokenJsonPath = authData.Assertions.TokenJsonPath,
                    CurrentUserEmailJsonPath = authData.Assertions.CurrentUserEmailJsonPath
                },
                Events = eventData.Assertions,
                Bookings = bookingData.Assertions
            }
        };
    }

    private EventApiDataModel LoadEventTestData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "eventData.json");
        Assert.That(File.Exists(path), Is.True, $"eventData file should exist at {path}. Ensure the file is present and copied to the output directory.");

        var data = JsonDataProvider.Read<EventApiDataModel>(path);
        Assert.That(data, Is.Not.Null, "eventData.json must be valid JSON and deserializable to EventApiDataModel.");
        
        Assert.That(data!.Endpoints, Is.Not.Null, "data.Endpoints cannot be null in eventData.json.");
        Assert.That(data.Endpoints.List, Is.Not.Null.And.Not.Empty, "endpoints.list is required in eventData.json");
        Assert.That(data.Endpoints.Create, Is.Not.Null.And.Not.Empty, "endpoints.create is required in eventData.json");
        Assert.That(data.Endpoints.GetById, Is.Not.Null.And.Not.Empty, "endpoints.getById is required in eventData.json");
        Assert.That(data.Endpoints.UpdateById, Is.Not.Null.And.Not.Empty, "endpoints.updateById is required in eventData.json");
        Assert.That(data.Endpoints.DeleteById, Is.Not.Null.And.Not.Empty, "endpoints.deleteById is required in eventData.json");
        
        Assert.That(data.Events, Is.Not.Null, "data.Events cannot be null in eventData.json.");
        Assert.That(data.Events.CreatePayload, Is.Not.Null.And.Property("HasValues").True, "events.createPayload with valid structure is required in eventData.json");
        Assert.That(data.Events.UpdatePayload, Is.Not.Null.And.Property("HasValues").True, "events.updatePayload with valid structure is required in eventData.json");
        Assert.That(data.Events.InvalidCreatePayload, Is.Not.Null.And.Property("HasValues").True, "events.invalidCreatePayload with valid structure is required in eventData.json");
        
        Assert.That(data.Assertions, Is.Not.Null, "data.Assertions cannot be null in eventData.json.");
        Assert.That(data.Assertions.CreatedEventIdJsonPath, Is.Not.Null.And.Not.Empty, "assertions.createdEventIdJsonPath is required in eventData.json");
        Assert.That(data.Queries, Is.Not.Null, "data.Queries cannot be null in eventData.json.");

        return data;
    }

    private BookingApiDataModel LoadBookingTestData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "bookingData.json");
        Assert.That(File.Exists(path), Is.True, $"bookingData file should exist at {path}. Ensure the file is present and copied to the output directory.");

        var data = JsonDataProvider.Read<BookingApiDataModel>(path);
        Assert.That(data, Is.Not.Null, "bookingData.json must be valid JSON and deserializable to BookingApiDataModel.");
        
        Assert.That(data!.Endpoints, Is.Not.Null, "data.Endpoints cannot be null in bookingData.json.");
        Assert.That(data.Endpoints.List, Is.Not.Null.And.Not.Empty, "endpoints.list is required in bookingData.json");
        Assert.That(data.Endpoints.Create, Is.Not.Null.And.Not.Empty, "endpoints.create is required in bookingData.json");
        Assert.That(data.Endpoints.GetById, Is.Not.Null.And.Not.Empty, "endpoints.getById is required in bookingData.json");
        Assert.That(data.Endpoints.GetByReference, Is.Not.Null.And.Not.Empty, "endpoints.getByReference is required in bookingData.json");
        Assert.That(data.Endpoints.CancelById, Is.Not.Null.And.Not.Empty, "endpoints.cancelById is required in bookingData.json");
        
        Assert.That(data.Bookings, Is.Not.Null, "data.Bookings cannot be null in bookingData.json.");
        Assert.That(data.Bookings.SupportingEventPayload, Is.Not.Null.And.Property("HasValues").True, "bookings.supportingEventPayload with valid structure is required in bookingData.json");
        Assert.That(data.Bookings.CreatePayload, Is.Not.Null.And.Property("HasValues").True, "bookings.createPayload with valid structure is required in bookingData.json");
        Assert.That(data.Bookings.InvalidCreatePayload, Is.Not.Null.And.Property("HasValues").True, "bookings.invalidCreatePayload with valid structure is required in bookingData.json");
        
        Assert.That(data.Assertions, Is.Not.Null, "data.Assertions cannot be null in bookingData.json.");
        Assert.That(data.Assertions.CreatedBookingIdJsonPath, Is.Not.Null.And.Not.Empty, "assertions.createdBookingIdJsonPath is required in bookingData.json");
        Assert.That(data.Assertions.BookingReferenceJsonPath, Is.Not.Null.And.Not.Empty, "assertions.bookingReferenceJsonPath is required in bookingData.json");
        Assert.That(data.Assertions.SupportingEventIdJsonPath, Is.Not.Null.And.Not.Empty, "assertions.supportingEventIdJsonPath is required in bookingData.json");
        Assert.That(data.Queries, Is.Not.Null, "data.Queries cannot be null in bookingData.json.");

        return data;
    }

    private LoginDataModel LoadLoginData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "loginData.json");
        Assert.That(File.Exists(path), Is.True, $"loginData file should exist at {path}. Ensure the file is present and copied to the output directory.");

        var data = JsonDataProvider.Read<LoginDataModel>(path);
        Assert.That(data, Is.Not.Null, "loginData.json must be valid JSON and deserializable to LoginDataModel.");
        
        Assert.That(data!.ValidCredentials, Is.Not.Null, "data.ValidCredentials cannot be null in loginData.json.");
        Assert.That(data.ValidCredentials.Email, Is.Not.Null.And.Not.Empty,
            "validCredentials.email is required in loginData.json. Ensure TEST_USER_EMAIL environment variable is set or 'validCredentials.email' exists in the JSON file.");
        Assert.That(data.ValidCredentials.Password, Is.Not.Null.And.Not.Empty,
            "validCredentials.password is required in loginData.json. Ensure TEST_USER_PASSWORD environment variable is set or 'validCredentials.password' exists in the JSON file.");
        
        Assert.That(data.ApiAuth, Is.Not.Null, "apiAuth section is required in loginData.json for API authentication configuration.");
        Assert.That(data.WrongPasswordScenario, Is.Not.Null, "wrongPasswordScenario section is required in loginData.json for negative test scenarios.");

        return data;
    }

    protected JObject BuildPayload(JObject template, IDictionary<string, JToken>? variables = null)
    {
        Assert.That(template, Is.Not.Null, "Payload template cannot be null. Ensure the payload template is loaded from test data.");
        Assert.That(template.HasValues, Is.True, "Payload template must have values. Ensure the template JSON object is not empty.");
        
        var payload = (JObject)template.DeepClone();
        Assert.That(payload, Is.Not.Null, "Failed to clone payload template.");
        
        ApplyTemplateValues(payload, variables ?? new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase));
        return payload;
    }

    protected int ExtractRequiredInt(string responseBody, string jsonPath, string missingMessage)
    {
        Assert.That(responseBody, Is.Not.Null.And.Not.Empty, "Response body cannot be null or empty when extracting integer values.");
        Assert.That(jsonPath, Is.Not.Null.And.Not.Empty, "JSONPath cannot be null or empty when extracting values.");
        Assert.That(missingMessage, Is.Not.Null.And.Not.Empty, "Error message cannot be null or empty.");
        
        var token = ExtractRequiredToken(responseBody, jsonPath, missingMessage);
        Assert.That(token.Type, Is.EqualTo(JTokenType.Integer).Or.EqualTo(JTokenType.String), 
            $"Token at JSONPath '{jsonPath}' should be an integer or numeric string, but found {token.Type}.");
        
        try
        {
            return token.Value<int>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert token at JSONPath '{jsonPath}' to integer. Value: {token}. {missingMessage}", ex);
        }
    }

    protected string ExtractRequiredString(string responseBody, string jsonPath, string missingMessage)
    {
        Assert.That(responseBody, Is.Not.Null.And.Not.Empty, "Response body cannot be null or empty when extracting string values.");
        Assert.That(jsonPath, Is.Not.Null.And.Not.Empty, "JSONPath cannot be null or empty when extracting values.");
        Assert.That(missingMessage, Is.Not.Null.And.Not.Empty, "Error message cannot be null or empty.");
        
        var token = ExtractRequiredToken(responseBody, jsonPath, missingMessage);
        
        try
        {
            var value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"{missingMessage} Extracted value at JSONPath '{jsonPath}' was null or empty.")
                : value;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to extract string value from JSONPath '{jsonPath}'. {missingMessage}", ex);
        }
    }

    private static JToken ExtractRequiredToken(string responseBody, string jsonPath, string missingMessage)
    {
        Assert.That(responseBody, Is.Not.Null.And.Not.Empty, "Response body cannot be null or empty when parsing JSON.");
        Assert.That(jsonPath, Is.Not.Null.And.Not.Empty, "JSONPath cannot be null or empty.");
        
        JObject root;
        try
        {
            root = JObject.Parse(responseBody);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse response body as JSON. Response: {responseBody.Substring(0, Math.Min(200, responseBody.Length))}...", ex);
        }
        
        var token = root.SelectToken(jsonPath);
        return token ?? throw new InvalidOperationException($"{missingMessage} JSONPath '{jsonPath}' not found in response.");
    }

    private static void ApplyTemplateValues(JToken token, IDictionary<string, JToken> variables)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties().ToList())
                {
                    ApplyTemplateValues(property.Value, variables);
                }
                break;

            case JArray array:
                foreach (var item in array)
                {
                    ApplyTemplateValues(item, variables);
                }
                break;

            case JValue value when value.Type == JTokenType.String && value.Value is string text:
                value.Replace(ResolveTemplateValue(text, variables));
                break;
        }
    }

    private static JToken ResolveTemplateValue(string template, IDictionary<string, JToken> variables)
    {
        var fullPlaceholderMatch = Regex.Match(template, @"^\{([^{}]+)\}$");
        if (fullPlaceholderMatch.Success)
        {
            return ResolvePlaceholder(fullPlaceholderMatch.Groups[1].Value, variables);
        }

        var resolved = Regex.Replace(template, "\\{([^{}]+)\\}", match =>
        {
            var token = ResolvePlaceholder(match.Groups[1].Value, variables);
            return token.Type == JTokenType.String ? token.Value<string>()! : token.ToString();
        });

        return new JValue(resolved);
    }

    private static JToken ResolvePlaceholder(string name, IDictionary<string, JToken> variables)
    {
        if (variables.TryGetValue(name, out var value))
        {
            return value.DeepClone();
        }

        if (string.Equals(name, "timestamp", StringComparison.OrdinalIgnoreCase))
        {
            return new JValue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        }

        var dateMatch = Regex.Match(name, @"^utcNowPlusDays:(-?\d+):(.+)$");
        if (dateMatch.Success)
        {
            var dayOffset = int.Parse(dateMatch.Groups[1].Value);
            var format = dateMatch.Groups[2].Value;
            return new JValue(DateTimeOffset.UtcNow.AddDays(dayOffset).ToString(format));
        }

        throw new InvalidOperationException($"Unknown payload template placeholder '{{{name}}}'.");
    }

    private async Task AuthenticateTestUserAsync()
    {
        Assert.That(AuthApi, Is.Not.Null, "AuthApi page object must be initialized before authentication.");
        Assert.That(LoginData, Is.Not.Null, "LoginData must be loaded before authentication.");
        Assert.That(LoginData.ValidCredentials, Is.Not.Null, "ValidCredentials must exist in LoginData.");
        Assert.That(LoginData.ValidCredentials.Email, Is.Not.Null.And.Not.Empty, "Valid credentials email must be available for authentication.");
        Assert.That(LoginData.ValidCredentials.Password, Is.Not.Null.And.Not.Empty, "Valid credentials password must be available for authentication.");
        Assert.That(ApiData, Is.Not.Null, "ApiData must be loaded before authentication.");
        Assert.That(ApiData.Assertions, Is.Not.Null, "ApiData.Assertions must not be null.");
        Assert.That(ApiData.Assertions.Auth, Is.Not.Null, "ApiData.Assertions.Auth must not be null.");
        Assert.That(ApiData.Assertions.Auth.TokenJsonPath, Is.Not.Null.And.Not.Empty, "Token JSONPath must be configured in ApiData.");
        
        Logger.Information("[AUTH] Authenticating test user with email: {Email}", "${TEST_USER_EMAIL}");
        
        ApiCallResult loginResponse;
        try
        {
            Logger.Debug("[AUTH] Sending login request to {LoginEndpoint}", ApiData.Endpoints.Auth.Login);
            loginResponse = await AuthApi.LoginAsync(LoginData.ValidCredentials.Email, LoginData.ValidCredentials.Password);
            Logger.Debug("[AUTH] Login response received with status: {StatusCode} ({StatusDescription})", (int)loginResponse.StatusCode, loginResponse.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[AUTH] Authentication failed - unable to send login request. Email: {Email}, Endpoint: {Endpoint}, Error: {Error}", 
                "${TEST_USER_EMAIL}", ApiData.Endpoints.Auth.Login, ex.Message);
            throw new InvalidOperationException($"Authentication failed - unable to send login request to {ApiData.Endpoints.Auth.Login}. Ensure API is accessible and credentials are valid.", ex);
        }
        
        Assert.That(loginResponse, Is.Not.Null, "Login response should not be null.");
        
        if (loginResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            var sanitizedResponse = APIContentSanitizer.SanitizeContent(loginResponse.ResponseBody);
            Logger.Error("[AUTH] Login returned 500 Internal Server Error. Response body: {ResponseBody}", sanitizedResponse);
            Assert.Fail($"Login request returned 500 Internal Server Error at {ApiData.Endpoints.Auth.Login}. Response: {sanitizedResponse}");
        }
        
        Logger.Debug("[AUTH] Asserting login success status. Expected: 2xx (Success), Got: {ActualStatus}", (int)loginResponse.StatusCode);
        
        Assert.That((int)loginResponse.StatusCode, Is.AnyOf(200, 201), "Login response status code should be 200 or 201.");
        
        Assert.That(loginResponse.ResponseBody, Is.Not.Null.And.Not.Empty, "Login response body should not be empty.");
        
        try
        {
            _bearerToken = ExtractRequiredString(
                loginResponse.ResponseBody,
                ApiData.Assertions.Auth.TokenJsonPath,
                "JWT token was not found in login response");
            Logger.Debug("[AUTH] Bearer token extracted successfully");
        }
        catch (Exception ex)
        {
            var sanitizedResponse = APIContentSanitizer.SanitizeContent(loginResponse.ResponseBody);
            Logger.Error(ex, "[AUTH] Failed to extract bearer token. JSONPath: {TokenJsonPath}, Response: {ResponseBody}", 
                ApiData.Assertions.Auth.TokenJsonPath, sanitizedResponse);
            throw;
        }
        
        Assert.That(_bearerToken, Is.Not.Null.And.Not.Empty, "Bearer token extraction resulted in empty string.");
    }

    [SetUp]
    public async Task SetUp()
    {
        Assert.That(TestContext.CurrentContext, Is.Not.Null, "TestContext must be available in SetUp.");
        Assert.That(TestContext.CurrentContext.Test, Is.Not.Null, "TestContext.Test must not be null.");
        Assert.That(TestContext.CurrentContext.Test.Name, Is.Not.Null.And.Not.Empty, "Test name must be available.");
        Assert.That(AuthApi, Is.Not.Null, "AuthApi page object should be initialized from OneTimeSetUp.");
        Assert.That(SharedApiClient, Is.Not.Null, "SharedApiClient should be initialized from OneTimeSetUp.");
        Assert.That(LoginData, Is.Not.Null, "LoginData should be initialized from OneTimeSetUp.");
        
        _testStartedAt = DateTimeOffset.Now;
        _executionTimer.Restart();
        _priority = GetCurrentPriorityLevel()?.ToString() ?? "Unspecified";
        _suiteName = GetCurrentSuiteName();

        var executionLogger = TestLogger.CreateExecutionLogger("API", _suiteName, TestContext.CurrentContext.Test.Name);
        Assert.That(executionLogger, Is.Not.Null, "ExecutionLogger initialization failed.");
        
        Logger = executionLogger.ForContext<APITestBase>();
        Assert.That(Logger, Is.Not.Null, "Logger context creation failed.");
        
        Serilog.Log.Logger = executionLogger;
        _executionLoggerHandle = executionLogger as IDisposable;

        Logger.Debug("[API] Starting test {TestName}", TestContext.CurrentContext.Test.Name);
        RuntimeContext.TestType = "API";
        RuntimeContext.BrowserName = "N/A";
        
        try
        {
            BeginAllureTest();
        }
        catch (Exception ex)
        {
            Logger.Warning("Failed to begin Allure test: {Message}", ex.Message);
        }
        
        ReportHelper.BeginTest(TestContext.CurrentContext.Test.Name);

        // Ensure each test has a fresh bearer token and can run independently.
        await AuthenticateTestUserAsync();
    }

    [TearDown]
    public void TearDown()
    {
        var outcome = TestContext.CurrentContext.Result.Outcome.Status.ToString();
        var errorMessage = TestContext.CurrentContext.Result.Message;
        _executionTimer.Stop();
        var finishedAt = DateTimeOffset.Now;
        
        // Attach any collected error information to Allure report
        CompleteAllureTest();
        Logger.Debug("[API] Completing test {TestName} with status {Status}", TestContext.CurrentContext.Test.Name, outcome);

        var fullClassName = TestContext.CurrentContext.Test.ClassName ?? string.Empty;
        var shortClassName = fullClassName.Contains('.') ? fullClassName[(fullClassName.LastIndexOf('.') + 1)..] : fullClassName;

        ReportHelper.RecordTestResult(
            TestContext.CurrentContext.Test.Name,
            shortClassName,
            _suiteName,
            outcome,
            _executionTimer.Elapsed,
            "N/A",
            "API",
            _priority,
            _testStartedAt,
            finishedAt,
            errorMessage,
            null);

        // Report is generated once per test run in global teardown (AllureBootstrap.FinalizeRun)

        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            Logger.Debug("[API] Test {TestName} completed with failure status", TestContext.CurrentContext.Test.Name);
        }

        Logger.Debug("[API] Test {TestName} finished in {DurationMs} ms",
            TestContext.CurrentContext.Test.Name,
            _executionTimer.Elapsed.TotalMilliseconds);

        _executionLoggerHandle?.Dispose();
        _executionLoggerHandle = null;
    }

}

public sealed class LoginDataModel
{
    public CredentialsModel ValidCredentials { get; set; } = new();
    public CredentialsModel WrongPasswordScenario { get; set; } = new();
    public ApiAuthData ApiAuth { get; set; } = new();

    public sealed class CredentialsModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

public sealed class ApiAuthData
{
    public ApiSuiteData.EndpointData.AuthEndpointData Endpoints { get; set; } = new();
    public ApiAuthAssertionData Assertions { get; set; } = new();

    public sealed class ApiAuthAssertionData
    {
        public string TokenJsonPath { get; set; } = string.Empty;
        public string CurrentUserEmailJsonPath { get; set; } = string.Empty;
    }
}

public sealed class EventApiDataModel
{
    public ApiSuiteData.EndpointData.EventEndpointData Endpoints { get; set; } = new();
    public ApiSuiteData.EventData Events { get; set; } = new();
    public ApiSuiteData.QueryData.EventQueryData Queries { get; set; } = new();
    public ApiSuiteData.AssertionData.EventAssertionData Assertions { get; set; } = new();
}

public sealed class BookingApiDataModel
{
    public ApiSuiteData.EndpointData.BookingEndpointData Endpoints { get; set; } = new();
    public ApiSuiteData.BookingData Bookings { get; set; } = new();
    public ApiSuiteData.QueryData.BookingQueryData Queries { get; set; } = new();
    public ApiSuiteData.AssertionData.BookingAssertionData Assertions { get; set; } = new();
}

public sealed class ApiSuiteData
{
    public EndpointData Endpoints { get; set; } = new();
    public EventData Events { get; set; } = new();
    public BookingData Bookings { get; set; } = new();
    public QueryData Queries { get; set; } = new();
    public AssertionData Assertions { get; set; } = new();

    public sealed class EndpointData
    {
        public AuthEndpointData Auth { get; set; } = new();
        public EventEndpointData Events { get; set; } = new();
        public BookingEndpointData Bookings { get; set; } = new();

        public sealed class AuthEndpointData
        {
            public string Login { get; set; } = string.Empty;
            public string Me { get; set; } = string.Empty;
        }

        public sealed class EventEndpointData
        {
            public string List { get; set; } = string.Empty;
            public string Create { get; set; } = string.Empty;
            public string GetById { get; set; } = string.Empty;
            public string UpdateById { get; set; } = string.Empty;
            public string DeleteById { get; set; } = string.Empty;
        }

        public sealed class BookingEndpointData
        {
            public string List { get; set; } = string.Empty;
            public string Create { get; set; } = string.Empty;
            public string GetById { get; set; } = string.Empty;
            public string GetByReference { get; set; } = string.Empty;
            public string CancelById { get; set; } = string.Empty;
        }
    }

    public sealed class EventData
    {
        public JObject CreatePayload { get; set; } = new();
        public JObject UpdatePayload { get; set; } = new();
        public JObject InvalidCreatePayload { get; set; } = new();
    }

    public sealed class BookingData
    {
        public JObject SupportingEventPayload { get; set; } = new();
        public JObject CreatePayload { get; set; } = new();
        public JObject InvalidCreatePayload { get; set; } = new();
    }

    public sealed class QueryData
    {
        public EventQueryData Events { get; set; } = new();
        public BookingQueryData Bookings { get; set; } = new();

        public sealed class EventQueryData
        {
            public int Page { get; set; }
            public int Limit { get; set; }
            public string Category { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Search { get; set; } = string.Empty;
        }

        public sealed class BookingQueryData
        {
            public int Page { get; set; }
            public int Limit { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    }

    public sealed class AssertionData
    {
        public AuthAssertionData Auth { get; set; } = new();
        public EventAssertionData Events { get; set; } = new();
        public BookingAssertionData Bookings { get; set; } = new();

        public sealed class AuthAssertionData
        {
            public string TokenJsonPath { get; set; } = string.Empty;
            public string CurrentUserEmailJsonPath { get; set; } = string.Empty;
        }

        public sealed class EventAssertionData
        {
            public string PaginationField { get; set; } = string.Empty;
            public string ValidationErrorField { get; set; } = string.Empty;
            public string CreatedEventIdJsonPath { get; set; } = string.Empty;
        }

        public sealed class BookingAssertionData
        {
            public string PaginationField { get; set; } = string.Empty;
            public string ValidationErrorField { get; set; } = string.Empty;
            public string CreatedBookingIdJsonPath { get; set; } = string.Empty;
            public string BookingReferenceJsonPath { get; set; } = string.Empty;
            public string SupportingEventIdJsonPath { get; set; } = string.Empty;
        }
    }
}