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
    [AllureStory("GET /api/auth/me")]
    [AllureSeverity(SeverityLevel.critical)]
    public async Task MeEndpoint_WithValidToken_ShouldReturnCurrentUser()
    {
        var meResponse = await AuthApi.GetCurrentUserAsync();
        APIClient.ValidateStatusCode(meResponse.StatusCode, 200);

        // Validate response contains expected user fields using Response Validation Framework
        ResponseValidator
            .FromContent(meResponse.ResponseBody)
            .ValidateFieldExists(ApiData.Assertions.Auth.CurrentUserEmailJsonPath)
            .ValidateFieldExists("user.userId")
            .ValidateContains("user.email", LoginData.ValidCredentials.Email);
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("POST /api/auth/login invalid payload")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task LoginEndpoint_WithInvalidPassword_ShouldReturnFailure()
    {
        var response = await AuthApi.LoginAsync(
            LoginData.WrongPasswordScenario.Email,
            LoginData.WrongPasswordScenario.Password);
        APIClient.ValidateStatusCode(response.StatusCode, 400);

        // Validate error response using Response Validation Framework
        ResponseValidator
            .FromContent(response.ResponseBody)
            .ValidateFieldExists("error");
    }
}
