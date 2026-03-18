using Allure.NUnit;
using Allure.NUnit.Attributes;
using UITests.Pages;
using Framework.Reporting;

namespace UITests;

[AllureNUnit]
[AllureParentSuite("UITests")]
[AllureSuite("Login")]
[AllureFeature("Authentication")]
public class LoginTests : BaseTest
{
    // Loaded once per test — all inputs and expected values come from loginData.json
    private LoginTestData _data = null!;

    [SetUp]
    public new void SetUp()
    {
        _data = LoadLoginData();
    }

    [Test]
    [Priority(TestPriority.High)]
    [AllureStory("Valid user login")]
    public void Login_WithValidCredentials()
    {
        ReportHelper.AddStep("Entering valid credentials");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.LoginAs(_data.ValidCredentials.Email, _data.ValidCredentials.Password);

        ReportHelper.AddStep("Verifying home page and login status");
        var homePage = new HomePage(Driver, Wait);

        Assert.That(homePage.IsUserLoggedIn(), Is.True, "User is not logged in based on home page signals.");
        Assert.That(homePage.IsHomePageLoaded(), Is.True, "User did not land on home page after login.");
        Assert.That(homePage.GetCurrentUrl(), Does.Not.Contain(_data.Assertions.LoginPagePath),
            "User URL still indicates login page.");
        Assert.That(homePage.GetCurrentUrl(), Does.Contain(_data.Assertions.HomePageDomain),
            "User is not on the expected application domain.");
        ReportHelper.AddStep("Login test completed successfully");
    }

    [Test]
    [Priority(TestPriority.Medium)]
    [AllureStory("Email validation")]
    public void Login_WithInvalidEmailFormat_ShouldShowEmailValidationError()
    {
        var scenario = _data.InvalidEmailScenario;
        ReportHelper.AddStep($"Entering invalid email: '{scenario.Email}' with password: '{scenario.Password}'");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.EnterEmail(scenario.Email);
        loginPage.EnterPassword(scenario.Password);
        loginPage.ClickLogin();

        ReportHelper.AddStep("Verifying inline email validation error is displayed");
        Assert.That(loginPage.IsEmailValidationErrorDisplayed(), Is.True,
            "Email validation error should be visible for invalid email format");

        var actualError = loginPage.GetEmailValidationErrorText();
        ReportHelper.AddStep($"Email validation error — expected: '{scenario.ExpectedEmailValidationError}', actual: '{actualError}'");
        Assert.That(actualError, Is.EqualTo(scenario.ExpectedEmailValidationError),
            "Email validation error text did not match expected value from data file");

        ReportHelper.AddStep($"Verifying URL contains '{scenario.ExpectedUrlContains}'");
        Assert.That(Driver.Url, Does.Contain(scenario.ExpectedUrlContains),
            "User should remain on login page when email format is invalid");
    }

    [Test]
    [Priority(TestPriority.Medium)]
    [AllureStory("Password validation")]
    public void Login_WithShortPassword_ShouldShowPasswordValidationError()
    {
        var scenario = _data.ShortPasswordScenario;
        ReportHelper.AddStep($"Entering email: '{scenario.Email}' with short password: '{scenario.Password}'");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.EnterEmail(scenario.Email);
        loginPage.EnterPassword(scenario.Password);
        loginPage.ClickLogin();

        ReportHelper.AddStep("Verifying inline password validation error is displayed");
        Assert.That(loginPage.IsPasswordValidationErrorDisplayed(), Is.True,
            "Password validation error should be visible for password shorter than 6 characters");

        var actualError = loginPage.GetPasswordValidationErrorText();
        ReportHelper.AddStep($"Password validation error — expected: '{scenario.ExpectedPasswordValidationError}', actual: '{actualError}'");
        Assert.That(actualError, Is.EqualTo(scenario.ExpectedPasswordValidationError),
            "Password validation error text did not match expected value from data file");

        ReportHelper.AddStep($"Verifying URL contains '{scenario.ExpectedUrlContains}'");
        Assert.That(Driver.Url, Does.Contain(scenario.ExpectedUrlContains),
            "User should remain on login page when password is too short");
    }

    [Test]
    [Priority(TestPriority.High)]
    [AllureStory("Required field validation")]
    public void Login_WithEmptyFields_ShouldShowBothValidationErrors()
    {
        var scenario = _data.EmptyFieldsScenario;
        ReportHelper.AddStep("Submitting the login form with no credentials entered");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.SubmitEmptyForm();

        ReportHelper.AddStep($"Verifying email validation error — expected: '{scenario.ExpectedEmailValidationError}'");
        Assert.That(loginPage.IsEmailValidationErrorDisplayed(), Is.True,
            "Email validation error should appear when email is empty");
        Assert.That(loginPage.GetEmailValidationErrorText(), Is.EqualTo(scenario.ExpectedEmailValidationError),
            "Email validation error text did not match expected value from data file");

        ReportHelper.AddStep($"Verifying password validation error — expected: '{scenario.ExpectedPasswordValidationError}'");
        Assert.That(loginPage.IsPasswordValidationErrorDisplayed(), Is.True,
            "Password validation error should appear when password is empty");
        Assert.That(loginPage.GetPasswordValidationErrorText(), Is.EqualTo(scenario.ExpectedPasswordValidationError),
            "Password validation error text did not match expected value from data file");

        ReportHelper.AddStep($"Verifying URL contains '{scenario.ExpectedUrlContains}'");
        Assert.That(Driver.Url, Does.Contain(scenario.ExpectedUrlContains),
            "User should remain on login page when both fields are empty");
    }

    [Test]
    [Priority(TestPriority.Low)]
    [AllureStory("Wrong password rejection")]
    public void Login_WithWrongPassword_ShouldBeOnLoginPage()
    {
        var scenario = _data.WrongPasswordScenario;
        ReportHelper.AddStep($"Entering valid email with wrong password: '{scenario.Password}'");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.LoginAs(_data.ValidCredentials.Email, scenario.Password);

        ReportHelper.AddStep($"Verifying URL contains '{scenario.ExpectedUrlContains}'");
        Assert.That(Driver.Url, Does.Contain(scenario.ExpectedUrlContains),
            "User should remain on login page after entering wrong password");
    }

    [Test]
    [Priority(TestPriority.Low)]
    [AllureStory("Unregistered email rejection")]
    public void Login_WithUnregisteredEmail_ShouldBeOnLoginPage()
    {
        var scenario = _data.UnregisteredEmailScenario;
        ReportHelper.AddStep($"Entering unregistered email: '{scenario.Email}' with password: '{scenario.Password}'");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.LoginAs(scenario.Email, scenario.Password);

        ReportHelper.AddStep($"Verifying URL contains '{scenario.ExpectedUrlContains}'");
        Assert.That(Driver.Url, Does.Contain(scenario.ExpectedUrlContains),
            "User should remain on login page when email is not registered");
    }
}
