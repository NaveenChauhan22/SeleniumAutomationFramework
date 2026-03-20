using Framework.Core.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace UITests.Pages;

/// <summary>
/// Abstract base class for all Page Object Model (POM) pages.
/// Holds the shared <see cref="IWebDriver"/> and <see cref="WaitHelper"/> instances and
/// provides reusable low-level helpers — <see cref="Click"/> (with retry and JS-click fallback),
/// <see cref="EnterText"/>, <see cref="Text"/>, <see cref="IsElementDisplayed"/>, and
/// <see cref="SelectByText"/> — that all concrete page classes inherit.
/// </summary>
public abstract class BasePage
{
    protected readonly IWebDriver Driver;
    protected readonly WaitHelper Wait;

    protected BasePage(IWebDriver driver, WaitHelper wait)
    {
        if (driver == null) throw new ArgumentNullException(nameof(driver));
        if (wait == null) throw new ArgumentNullException(nameof(wait));
        Driver = driver;
        Wait = wait;
    }

    protected void Click(By locator, int maxRetries = 3)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var element = Wait.WaitForElementClickable(locator);
                element.Click();
                return;
            }
            catch (ElementClickInterceptedException) when (attempt < maxRetries)
            {
                // Overlay is obscuring the target — scroll to center then wait until clickable
                ScrollIntoView(locator);
                Wait.WaitForElementClickable(locator);
            }
            catch (ElementNotInteractableException) when (attempt < maxRetries)
            {
                // Element not yet interactable — scroll into view then wait until clickable
                ScrollIntoView(locator);
                Wait.WaitForElementClickable(locator);
            }
            catch (StaleElementReferenceException) when (attempt < maxRetries)
            {
                // DOM refreshed and reference is stale — wait for element to reappear and be clickable
                Wait.WaitForElementClickable(locator);
            }
            catch (MoveTargetOutOfBoundsException) when (attempt < maxRetries)
            {
                // Element is outside the viewport — scroll into view then wait until clickable
                ScrollIntoView(locator);
                Wait.WaitForElementClickable(locator);
            }
            catch (Exception) when (attempt == maxRetries)
            {
                // All retries exhausted — scroll element into view then fall back to JS click
                var element = Wait.WaitForElementVisible(locator);
                ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
                ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", element);
            }
        }
    }

    private void ScrollIntoView(By locator)
    {
        try
        {
            var element = Wait.WaitForElementPresent(locator);
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
        }
        catch
        {
            // Best-effort — if the element is not found during scroll, let the retry handle it
        }
    }

    protected void EnterText(By locator, string value)
    {
        if (locator == null)
            throw new ArgumentNullException(nameof(locator));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace", nameof(value));

        var element = Wait.WaitForElementVisible(locator);
        element.Clear();
        element.SendKeys(value);
    }

    protected string Text(By locator)
    {
        if (locator == null)
            throw new ArgumentNullException(nameof(locator));
        return Wait.WaitForElementVisible(locator).Text ?? string.Empty;
    }

    protected bool IsElementDisplayed(By locator)
    {
        try
        {
            return Wait.WaitForElementVisible(locator).Displayed;
        }
        catch
        {
            return false;
        }
    }

    protected void SelectByText(By locator, string text)
    {
        if (locator == null)
            throw new ArgumentNullException(nameof(locator));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or whitespace", nameof(text));

        var element = Wait.WaitForElementVisible(locator);
        var selectElement = new SelectElement(element);
        selectElement.SelectByText(text);
    }
}
