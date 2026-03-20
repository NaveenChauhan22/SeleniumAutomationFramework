using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Framework.Core.Configuration;

namespace Framework.Core.Utilities
{
    /// <summary>
    /// Common explicit wait helper around <see cref="WebDriverWait"/> for Selenium.
    /// Timeout and polling interval are driven from ConfigManager for consistency.
    /// Ignores stale-element and not-found exceptions by default.
    /// Provides high-level waits for element visibility, clickability, text content,
    /// URL/title changes, page-load state, Ajax idle, frames, windows, and alerts.
    /// </summary>
    public class WaitHelper
    {
        private readonly IWebDriver _driver;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _polling;
        private readonly Type[] _ignoredExceptions =
        {
            typeof(NoSuchElementException),
            typeof(StaleElementReferenceException)
        };

        private WebDriverWait CreateWait(TimeSpan? timeout = null, TimeSpan? polling = null)
        {
            var wait = new WebDriverWait(_driver, timeout ?? _timeout)
            {
                PollingInterval = polling ?? _polling
            };
            wait.IgnoreExceptionTypes(_ignoredExceptions);
            return wait;
        }

        /// <summary>
        /// Initializes WaitHelper with timeout and polling driven from config or provided values.
        /// If timeout or polling are not provided (0), defaults are read from ConfigManager 
        /// (ExplicitWaitSeconds, ExplicitWaitPollingMs) with fallback defaults of 15s and 250ms respectively.
        /// </summary>
        public WaitHelper(IWebDriver driver, int timeoutInSeconds = 0, int pollingMs = 0)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));

            // If timeoutInSeconds is 0, read from ConfigManager, else use provided value
            var actualTimeoutSeconds = timeoutInSeconds > 0
                ? timeoutInSeconds
                : ConfigManager.GetInt("TestSettings:ExplicitWaitSeconds");

            // If pollingMs is 0, read from ConfigManager, else use provided value
            var actualPollingMs = pollingMs > 0
                ? pollingMs
                : ConfigManager.GetInt("TestSettings:ExplicitWaitPollingMs");

            _timeout = TimeSpan.FromSeconds(actualTimeoutSeconds);
            _polling = TimeSpan.FromMilliseconds(actualPollingMs);
        }

        public T Until<T>(Func<IWebDriver, T> condition, TimeSpan? timeout = null)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));
            return CreateWait(timeout).Until(condition);
        }

        public bool UntilTrue(Func<IWebDriver, bool> predicate, TimeSpan? timeout = null)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            return CreateWait(timeout).Until(drv => predicate(drv));
        }

        public IWebElement WaitForElementPresent(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            return CreateWait(timeout).Until(drv => drv?.FindElement(locator));
        }

        public IWebElement WaitForElementVisible(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            return CreateWait(timeout).Until(ExpectedConditions.ElementIsVisible(locator));
        }

        public IWebElement WaitForElementClickable(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            return CreateWait(timeout).Until(ExpectedConditions.ElementToBeClickable(locator));
        }

        public IReadOnlyCollection<IWebElement> WaitForAllElementsPresent(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            return CreateWait(timeout).Until(drv =>
            {
                var list = drv?.FindElements(locator);
                return list; // presence, not necessarily visible
            });
        }

        public IReadOnlyCollection<IWebElement> WaitForElementsCountAtLeast(By locator, int count, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            if (count < 0)
                throw new ArgumentException("Count cannot be negative", nameof(count));

            return CreateWait(timeout).Until(drv =>
            {
                var list = drv?.FindElements(locator);
                return list?.Count >= count ? list : null;
            });
        }

        public bool WaitForElementInvisible(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv?.FindElement(locator);
                    if (el == null) return true; // Element not found is invisible
                    return !el.Displayed;
                }
                catch (NoSuchElementException)
                {
                    return true; // not in DOM is also 'invisible'
                }
                catch (StaleElementReferenceException)
                {
                    return true; // stale is effectively 'gone'
                }
                catch (Exception)
                {
                    return false; // other exceptions mean we can't verify
                }
            });
        }

        public bool WaitForTextPresentInElement(By locator, string text, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text to search for cannot be null or empty", nameof(text));
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv.FindElement(locator);
                    if (el == null) return false;
                    var content = el.Text ?? string.Empty;
                    return ignoreCase
                        ? content.Contains(text, StringComparison.OrdinalIgnoreCase)
                        : content.Contains(text, StringComparison.Ordinal);
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });
        }

        public bool WaitForElementTextEquals(By locator, string expected, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv.FindElement(locator);
                    if (el == null) return false;
                    var actual = el.Text ?? string.Empty;
                    return ignoreCase
                        ? string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(actual, expected, StringComparison.Ordinal);
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });
        }

        public bool WaitForAttributeContains(By locator, string attribute, string value, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            if (string.IsNullOrWhiteSpace(attribute))
                throw new ArgumentException("Attribute name cannot be null or empty", nameof(attribute));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Attribute value cannot be null or empty", nameof(value));
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv.FindElement(locator);
                    if (el == null) return false;
                    var attr = el.GetAttribute(attribute) ?? string.Empty;
                    return ignoreCase
                        ? attr.Contains(value, StringComparison.OrdinalIgnoreCase)
                        : attr.Contains(value, StringComparison.Ordinal);
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });
        }

        public bool WaitForValueNotEmpty(By locator, TimeSpan? timeout = null)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv.FindElement(locator);
                    if (el == null) return false;
                    var val = el.GetAttribute("value");
                    return !string.IsNullOrWhiteSpace(val);
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });
        }


        public bool WaitForUrlContains(string partialUrl, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(partialUrl))
                throw new ArgumentException("URL cannot be null or empty", nameof(partialUrl));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var url = drv?.Url;
                    return !string.IsNullOrWhiteSpace(url) && url.Contains(partialUrl, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public bool WaitForUrlIs(string url, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var currentUrl = drv?.Url;
                    return string.Equals(currentUrl, url, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public bool WaitForTitleContains(string partial, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(partial))
                throw new ArgumentException("Title cannot be null or empty", nameof(partial));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var title = drv?.Title;
                    return !string.IsNullOrWhiteSpace(title) && title.Contains(partial, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public bool WaitForTitleIs(string title, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var currentTitle = drv?.Title;
                    return string.Equals(currentTitle, title, StringComparison.Ordinal);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public bool WaitForPageLoaded(TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var js = drv as IJavaScriptExecutor;
                    if (js == null) return false;
                    var readyState = js.ExecuteScript("return document.readyState")?.ToString();
                    return string.Equals(readyState, "complete", StringComparison.Ordinal);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Waits for network activity to become idle. Supports multiple frameworks:
        /// - jQuery: Checks jQuery.active == 0
        /// - Angular: Checks ng.probe() pending requests
        /// - React: Checks for pending promises
        /// - Vanilla: Falls back to document.readyState == 'complete'
        /// </summary>
        public bool WaitForAjaxIdle(TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var js = drv as IJavaScriptExecutor;
                    if (js == null) return false;

                    // Check jQuery if present
                    try
                    {
                        var jqueryActive = js.ExecuteScript("return (typeof jQuery !== 'undefined' && jQuery.active) ? jQuery.active : 0");
                        if (jqueryActive != null && Convert.ToInt32(jqueryActive) > 0)
                            return false;
                    }
                    catch { /* jQuery not present, continue */ }

                    // Check Angular if present
                    try
                    {
                        var angularReady = js.ExecuteScript(
                            "return (typeof angular !== 'undefined' && angular.element(document.body).injector()) ? " +
                            "angular.element(document.body).injector().get('$http').pendingRequests.length === 0 : true");
                        if (angularReady != null && !(bool)angularReady)
                            return false;
                    }
                    catch { /* Angular not present, continue */ }

                    // Check for pending promises (React, Vue, etc.)
                    try
                    {
                        var pendingPromises = js.ExecuteScript(
                            "return (window.__pendingPromises__ !== undefined) ? window.__pendingPromises__ : 0");
                        if (pendingPromises != null && Convert.ToInt32(pendingPromises) > 0)
                            return false;
                    }
                    catch { /* No custom promise tracking, continue */ }

                    // Fallback: Check document ready state
                    var readyState = js.ExecuteScript("return document.readyState")?.ToString();
                    return string.Equals(readyState, "complete", StringComparison.Ordinal);
                }
                catch (Exception)
                {
                    // If JavaScript execution fails, assume idle
                    return true;
                }
            });
        }

        public IWebDriver WaitForFrameAndSwitchToIt(By frameLocator, TimeSpan? timeout = null)
        {
            if (frameLocator == null)
                throw new ArgumentNullException(nameof(frameLocator));

            try
            {
                return CreateWait(timeout).Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(frameLocator));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to find and switch to frame with locator: {frameLocator}", ex);
            }
        }

        public bool WaitForWindowCount(int expectedCount, TimeSpan? timeout = null)
        {
            if (expectedCount <= 0)
                throw new ArgumentException("Expected window count must be greater than 0", nameof(expectedCount));

            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var handles = drv?.WindowHandles;
                    return handles != null && handles.Count == expectedCount;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public IAlert WaitForAlertPresent(TimeSpan? timeout = null)
        {
            try
            {
                return CreateWait(timeout).Until(ExpectedConditions.AlertIsPresent());
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new InvalidOperationException("Alert did not appear within the specified timeout", ex);
            }
        }

        public void TypeWhenVisible(By locator, string text, TimeSpan? timeout = null, bool clear = true)
        {
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            try
            {
                var el = WaitForElementVisible(locator, timeout);
                if (el == null)
                    throw new InvalidOperationException($"Element not found or not visible: {locator}");
                if (clear)
                    el.Clear();
                el.SendKeys(text);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                throw new InvalidOperationException($"Failed to type text in element {locator}", ex);
            }
        }

    }
}