using OpenQA.Selenium;

namespace UITests.Pages;

/// <summary>
/// Page object for the application login screen (<c>/login</c>).
/// Provides fluent helpers for navigating to the page, filling in credentials, submitting the
/// form, and asserting the state of UI elements (email/password fields, login button, validation
/// error messages). Test classes obtain a valid session by calling <see cref="LoginAs"/>.
/// </summary>
public class LoginPage : BasePage
{
    public LoginPage(IWebDriver driver, Framework.Core.Utilities.WaitHelper wait) : base(driver, wait)
    {
    }

    // ── Form field locators (exact IDs from page HTML) ────────────────────────
    private readonly By _emailInput = By.Id("email");
    private readonly By _passwordInput = By.Id("password");
    private readonly By _loginButton = By.Id("login-btn");
    private readonly By _userEmailDisplay = By.Id("user-email-display");

    // ── Inline validation error locators (from <p class="text-red-600"> nodes) ─
    private readonly By _emailValidationError = By.XPath("//input[@id='password']/preceding::p[contains(@class,'text-red-600')]");
    private readonly By _passwordValidationError = By.XPath("//input[@id='password']/following-sibling::p[contains(@class,'text-red')]");

    // ── Navigation ────────────────────────────────────────────────────────────
    public LoginPage NavigateToLogin(string appUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appUrl);
        Driver.Navigate().GoToUrl(appUrl);
        return this;
    }

    // ── Positive-flow actions ─────────────────────────────────────────────────
    public LoginPage EnterEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace", nameof(email));

        EnterText(_emailInput, email);
        return this;
    }

    public LoginPage EnterPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

        EnterText(_passwordInput, password);
        return this;
    }

    public void ClickLogin()
    {
        Click(_loginButton);
    }

    /// <summary>
    /// Attempts login with provided credentials and waits for successful login confirmation.
    /// Use this for tests expecting successful login (e.g., positive flow tests).
    /// </summary>
    public void LoginAs(string email, string password)
    {
        try
        {
            EnterEmail(email);
            EnterPassword(password);
            ClickLogin();
            Wait.WaitForElementVisible(_userEmailDisplay);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to login with '{email}'", ex);
        }
    }

    /// <summary>
    /// Use this for tests expecting login failure (e.g., wrong password, unregistered email).
    /// After calling this, check page state to verify login failed.
    /// </summary>
    public void AttemptToLoginWithInvalidCreds(string email, string password)
    {
        EnterEmail(email);
        EnterPassword(password);
        ClickLogin();
        Wait.WaitForElementVisible(_loginButton);
    }

    // ── Negative-scenario helpers ─────────────────────────────────────────────

    /// <summary>
    /// Clears the email field so browser-level validation triggers on submit.
    /// </summary>
    public LoginPage ClearEmail()
    {
        Wait.WaitForElementVisible(_emailInput).Clear();
        return this;
    }

    /// <summary>
    /// Clears the password field so browser-level validation triggers on submit.
    /// </summary>
    public LoginPage ClearPassword()
    {
        Wait.WaitForElementVisible(_passwordInput).Clear();
        return this;
    }

    public bool IsUserEmailDisplayed() => IsElementDisplayed(_userEmailDisplay);

    /// <summary>
    /// Returns true when the inline "Enter a valid email" error message is shown.
    /// </summary>
    public bool IsEmailValidationErrorDisplayed() => IsElementDisplayed(_emailValidationError);

    /// <summary>
    /// Returns the text of the inline email validation error message.
    /// Expected: "Enter a valid email"
    /// </summary>
    public string GetEmailValidationErrorText() => Text(_emailValidationError);

    /// <summary>
    /// Returns true when the inline "Password must be at least 6 characters" error is shown.
    /// </summary>
    public bool IsPasswordValidationErrorDisplayed() => IsElementDisplayed(_passwordValidationError);

    /// <summary>
    /// Returns the text of the inline password validation error message.
    /// Expected: "Password must be at least 6 characters"
    /// </summary>
    public string GetPasswordValidationErrorText() => Text(_passwordValidationError);

    /// <summary>
    /// Returns true when the Sign In button is in its disabled state (opacity-60 class).
    /// The button is disabled when form has invalid input.
    /// </summary>
    public bool IsLoginButtonDisabled()
    {
        try
        {
            var btn = Wait.WaitForElementVisible(_loginButton);
            if (btn == null) return true;

            var disabledAttr = btn.GetAttribute("disabled");
            var classAttr = btn.GetAttribute("class") ?? string.Empty;

            return disabledAttr is not null || classAttr.Contains("disabled:opacity-60");
        }
        catch (Exception)
        {
            return true; // If element can't be found/checked, assume disabled
        }
    }

    // ── Field-state queries ────────────────────────────────────────────────────
    public bool IsEmailFieldDisplayed() => IsElementDisplayed(_emailInput);
    public bool IsPasswordFieldDisplayed() => IsElementDisplayed(_passwordInput);
    public bool IsLoginButtonDisplayed() => IsElementDisplayed(_loginButton);
    public string GetLoginButtonText() => Text(_loginButton);

    /// <summary>
    /// Returns the current value inside the email input.
    /// </summary>
    public string GetEmailFieldValue()
    {
        try
        {
            var el = Wait.WaitForElementVisible(_emailInput);
            if (el == null) return string.Empty;

            return el.GetAttribute("value") ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Submits the login form with no credentials at all to exercise empty-field validation.
    /// </summary>
    public LoginPage SubmitEmptyForm()
    {
        ClearEmail();
        ClearPassword();
        ClickLogin();
        return this;
    }
}
