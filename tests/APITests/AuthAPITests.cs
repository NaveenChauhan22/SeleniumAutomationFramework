using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using Framework.API;
using Framework.Reporting;

namespace APITests;

[AllureNUnit]
[AllureParentSuite("APITests")]
[AllureSuite("Authentication API")]
[AllureFeature("Authentication")]
public class AuthAPITests : APITestBase
{

    [Test]
    [Priority(TestPriority.High)]
    [AllureStory("GET /api/auth/me")]
    [AllureSeverity(SeverityLevel.critical)]
    public async Task MeEndpoint_WithValidToken_ShouldReturnCurrentUser()
    {
        var meResponse = await AuthApi.GetCurrentUserAsync();
        APIClient.ValidateStatusCode(meResponse.StatusCode, 200);

        var email = ExtractRequiredString(
            meResponse.ResponseBody,
            ApiData.Assertions.Auth.CurrentUserEmailJsonPath,
            "User email should be returned from current user response.");
        Assert.That(email, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Priority(TestPriority.Medium)]
    [AllureStory("POST /api/auth/login invalid payload")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task LoginEndpoint_WithInvalidPassword_ShouldReturnFailure()
    {
        var response = await AuthApi.LoginAsync(
            LoginData.WrongPasswordScenario.Email,
            LoginData.WrongPasswordScenario.Password);
        APIClient.ValidateStatusCode(response.StatusCode, 400);
    }
}
