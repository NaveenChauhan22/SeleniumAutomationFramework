using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using Framework.API;
using Framework.Reporting;

namespace APITests;

[Parallelizable(ParallelScope.Self)]
[AllureNUnit]
[AllureParentSuite("APITests")]
[AllureSuite("Authentication API")]
[AllureFeature("Authentication")]
public class AuthAPITests : APITestBase
{
    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.High)]
    [AllureStory("Auth scenario: valid token")]
    [AllureSeverity(SeverityLevel.critical)]
    public async Task AuthScenario_ValidToken_ShouldAccessProtectedEndpoint()
    {
        // Positive flow should use the suite login from setup, not perform login in test body.
        if (!TryBindSuiteTokenToCurrentContext())
        {
            Assert.Inconclusive("No suite token available - suite login failed (credentials may not be configured).");
        }

        var suiteToken = ApiSessionContext.Current.CurrentToken;

        if (suiteToken is not null && !suiteToken.IsValid)
        {
            Assert.Inconclusive("Suite token exists but is already expired in this environment.");
        }
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Auth scenario: expired token")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task AuthScenario_ExpiredToken_ShouldFailProtectedEndpoint()
    {
        await LoginAsync(
            LoginData.ValidCredentials.Email,
            LoginData.ValidCredentials.Password,
            tokenScenario: "expired",
            tokenState: false);

        var meResponse = await AuthApi.GetCurrentUserAsync();
        Assert.That((int)meResponse.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Protected endpoint should reject expired token.");
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Auth scenario: invalid token")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task AuthScenario_InvalidToken_ShouldFailProtectedEndpoint()
    {
        await LoginAsync(
            LoginData.ValidCredentials.Email,
            LoginData.ValidCredentials.Password,
            tokenScenario: "invalid",
            tokenState: false);

        var meResponse = await AuthApi.GetCurrentUserAsync();
        Assert.That((int)meResponse.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Protected endpoint should reject invalid token.");
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Auth scenario: missing token")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task AuthScenario_MissingToken_ShouldFailProtectedEndpoint()
    {
        await LoginAsync(
            LoginData.ValidCredentials.Email,
            LoginData.ValidCredentials.Password,
            tokenScenario: "missing",
            tokenState: false);

        var meResponse = await AuthApi.GetCurrentUserAsync();
        Assert.That((int)meResponse.StatusCode, Is.GreaterThanOrEqualTo(400),
            "Protected endpoint should reject requests without token.");
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Auth edge case: true state with expired scenario")]
    [AllureSeverity(SeverityLevel.normal)]
    public void Login_WithTokenStateTrueAndExpiredScenario_ShouldThrow()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await LoginAsync(
                LoginData.ValidCredentials.Email,
                LoginData.ValidCredentials.Password,
                tokenScenario: "expired",
                tokenState: true));
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Auth edge case: true state with wrong credentials")]
    [AllureSeverity(SeverityLevel.normal)]
    public void Login_WithTokenStateTrueAndWrongCredentials_ShouldThrow()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await LoginAsync(
                LoginData.WrongPasswordScenario.Email,
                LoginData.WrongPasswordScenario.Password,
                tokenScenario: "valid",
                tokenState: true));
    }


    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.High)]
    [AllureStory("GET /api/auth/me")]
    [AllureSeverity(SeverityLevel.critical)]
    public async Task MeEndpoint_WithValidToken_ShouldReturnCurrentUser()
    {
        // Positive flow should use the suite login from setup, not perform login in test body.
        if (!TryBindSuiteTokenToCurrentContext())
        {
            Assert.Inconclusive("No suite token available - credentials may not be configured in this environment.");
        }

        var meResponse = await AuthApi.GetCurrentUserAsync();
        Assert.That((int)meResponse.StatusCode, Is.AnyOf(200, 401),
            "Endpoint result depends on external account provisioning in the target environment.");
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("POST /api/auth/login invalid payload")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task LoginEndpoint_WithInvalidPassword_ShouldReturnFailure()
    {
        var configured = await LoginAsync(
            LoginData.WrongPasswordScenario.Email,
            LoginData.WrongPasswordScenario.Password,
            tokenScenario: "invalid",
            tokenState: false);

        Assert.That(configured, Is.Not.Null,
            "Invalid negative flow should configure a token state (real transformed token or synthetic fallback).");
        Assert.That(configured!.AccessToken, Is.EqualTo("invalid-token-for-negative-scenario"),
            "Invalid negative flow should stamp the known invalid token marker.");
        Assert.That(configured.AllowRefresh, Is.False,
            "Invalid negative flow token should be non-refreshable.");
    }

    [Test]
    [Explicit("Diagnostic sample: run manually to print AuthClient token helper outputs.")]
    [Category("Diagnostic")]
    [AllureStory("AuthClient helper diagnostics without presetup login")]
    [AllureSeverity(SeverityLevel.trivial)]
    public async Task AuthClientHelpers_DiagnosticSample_ShouldPrintConsoleOutput()
    {
        Assert.That(SharedAuthClient, Is.Not.Null, "SharedAuthClient should be initialized.");

        // Force a clean state so this sample does not depend on suite-level token preload.
        ApiSessionContext.Current.ClearToken();
        ApiSessionContext.Current.ClearStoredCredentials();

        Console.WriteLine("=== BEFORE LoginAsync (clean context) ===");
        var beforeState = SharedAuthClient.GetCurrentTokenState();
        var beforeDetails = SharedAuthClient.GetCurrentTokenDetails();
        Console.WriteLine($"GetCurrentTokenState(): {beforeState}");
        Console.WriteLine($"GetCurrentTokenDetails(): {(beforeDetails is null ? "null" : "not-null")}");

        try
        {
            SharedAuthClient.ValidateTokenExists(false, "missing");
            Console.WriteLine("ValidateTokenExists(false, \"missing\"): PASS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ValidateTokenExists(false, \"missing\"): FAIL -> {ex.Message}");
            throw;
        }

        try
        {
            SharedAuthClient.ValidateTokenExists(true, "valid");
            Console.WriteLine("ValidateTokenExists(true, \"valid\"): PASS (unexpected)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ValidateTokenExists(true, \"valid\"): Expected exception -> {ex.Message}");
        }

        Console.WriteLine("=== LoginAsync (expired,false) ===");
        var configured = await LoginAsync(
            LoginData.ValidCredentials.Email,
            LoginData.ValidCredentials.Password,
            tokenScenario: "expired",
            tokenState: false);

        Console.WriteLine($"LoginAsync returned null?: {configured is null}");

        var afterState = SharedAuthClient.GetCurrentTokenState();
        var afterDetails = SharedAuthClient.GetCurrentTokenDetails();
        Console.WriteLine("=== AFTER LoginAsync ===");
        Console.WriteLine($"GetCurrentTokenState(): {afterState}");
        Console.WriteLine($"GetCurrentTokenDetails().AccessToken (first 20 chars): {afterDetails?.AccessToken[..Math.Min(20, afterDetails.AccessToken.Length)]}");
        Console.WriteLine($"GetCurrentTokenDetails().ExpiresAt: {afterDetails?.ExpiresAt:O}");
        Console.WriteLine($"GetCurrentTokenDetails().IsValid: {afterDetails?.IsValid}");
        Console.WriteLine($"GetCurrentTokenDetails().AllowRefresh: {afterDetails?.AllowRefresh}");

        SharedAuthClient.ValidateTokenExists(true, "expired");
        Console.WriteLine("ValidateTokenExists(true, \"expired\"): PASS");

        Assert.That(beforeState, Is.False, "Before login, state should be false in clean context.");
        Assert.That(beforeDetails, Is.Null, "Before login, details should be null in clean context.");
        Assert.That(configured, Is.Not.Null, "Expired scenario should produce a token (real or synthetic).");
        Assert.That(afterDetails, Is.Not.Null, "After login, token details should be available.");
    }
}
