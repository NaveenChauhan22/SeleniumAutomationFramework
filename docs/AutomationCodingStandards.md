
# Automation Coding Standards

Project: SeleniumAutomationFramework  
Purpose: Define coding standards and best practices for building and maintaining automation tests within this framework.

Related guides:

- [SetupGuide.md](./SetupGuide.md)
- [ExecutionGuide.md](./ExecutionGuide.md)
- [FrameworkArchitecture.md](./FrameworkArchitecture.md)
- [GitWorkflow.md](./GitWorkflow.md)
- [README.md](../README.md)

---

# 1. General Principles

Automation code should be treated with the same discipline as production code.

Goals:
- Maintainable
- Readable
- Reusable
- Scalable
- Stable

Key principles:

- Follow Single Responsibility Principle
- Avoid duplicated code
- Write clear and descriptive test names
- Prefer readability over cleverness

---

# 2. Naming Conventions

## Classes

| Type | Naming Example |
|-----|----------------|
Page Objects | LoginPage |
Test Classes | LoginTests |
Utility Classes | WaitHelper |
Configuration | ConfigManager |

Class names should always use PascalCase.

---

## Methods

Use PascalCase and meaningful names.

Examples:

LoginWithValidCredentials()  
CreateOrderSuccessfully()  
NavigateToCheckout()

Avoid generic names such as:

Test1()  
DoLogin()

---

## Variables

Use camelCase.

Examples:

driver  
loginPage  
testData

Avoid abbreviations unless widely known.

Bad example:

drv

---

# 3. Page Object Model Rules

All UI interactions must go through Page Objects.

Test classes should never directly interact with:

- Selenium locators
- WebDriver commands

Example structure:

Test → Page Object → Selenium Driver

Page Objects should:

- Store element locators
- Expose user actions
- Not contain assertions

Example:

public void Login(string username, string password)
{
    usernameField.SendKeys(username);
    passwordField.SendKeys(password);
    loginButton.Click();
}

---

# 4. Locator Strategy

Preferred locator order:

1. ID
2. Name
3. CSS Selector
4. XPath (only if necessary)

Avoid fragile locators such as:

//div[3]/table/tr[2]/td[1]

Prefer stable attributes:

data-testid  
data-qa  
id

---

# 5. Wait Strategy

Never use:

Thread.Sleep()

Always use explicit waits.

Example:

wait.Until(ExpectedConditions.ElementIsVisible(locator));

All wait logic should be implemented in:

WaitHelper

---

# 6. Test Design Principles

Tests should be:

- Independent
- Deterministic
- Fast

Each test should validate one logical scenario.

Good example:

Login_WithValidCredentials_ShouldNavigateToDashboard()

Avoid large end‑to‑end tests covering many flows.

---

# 7. Assertions

Use FluentAssertions for readability.

Example:

page.Title.Should().Be("Dashboard");

Avoid unclear assertions.

Bad example:

Assert.True(isDisplayed);

---

# 8. Logging

Use Serilog for framework logging.

Log important events such as:

- Test start
- Navigation steps
- API calls
- Failures

Avoid excessive logging inside loops or low-level methods.

---

# 9. Error Handling

Tests should fail clearly with useful messages.

Example:

Assert.True(isDisplayed, "Dashboard page should be visible after login");

Avoid catching exceptions unless required for validation.

---

# 10. Test Data

Test data should not be hardcoded.

Use:

- JSON files
- Excel files
- Configuration files

Location:

resources/

Example:

resources/TestData.xlsx

---

# 11. Test Independence

Tests must not depend on the result of other tests.

Avoid:

- sequential dependencies
- shared state between tests

Each test should:

- create its own data
- clean up if necessary

---

# 12. Code Reusability

Common logic should be implemented in:

- Utility classes
- Base classes
- Helper methods

Examples:

WaitHelper  
ScreenshotHelper  
ApiClient

---

# 13. Code Reviews

All changes should go through pull request reviews.

Review checklist:

- Naming conventions followed
- No duplicated code
- Page Object pattern respected
- No Thread.Sleep
- Proper assertions used

---

# 14. Formatting Rules

- Use consistent indentation
- Avoid long methods (> 40 lines if possible)
- Keep classes focused
- Group related methods together

Use `.editorconfig` to enforce formatting rules.

---

# 15. Future Enhancements

Additional standards may be introduced for:

- API automation patterns
- BDD scenarios
- Mobile automation
- Visual testing

---

# Summary

These coding standards ensure the framework remains:

- clean
- scalable
- easy to maintain
- easy to extend

All contributors should follow these guidelines when adding new automation code.
