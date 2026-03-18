using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace Framework.Core.Utilities
{
    /// <summary>
    /// Common explicit wait helper around <see cref="WebDriverWait"/> for Selenium.
    /// Default timeout = 15 s, default poll = 250 ms; ignores stale-element and not-found
    /// exceptions by default. Provides high-level waits for element visibility, clickability,
    /// text content, URL/title changes, page-load state, Ajax idle, frames, windows, and alerts.
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

        public WaitHelper(IWebDriver driver, int timeoutInSeconds = 15, int pollingMs = 250)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _timeout = TimeSpan.FromSeconds(timeoutInSeconds);
            _polling = TimeSpan.FromMilliseconds(pollingMs);
        }

        public T Until<T>(Func<IWebDriver, T> condition, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(condition);
        }

        public bool UntilTrue(Func<IWebDriver, bool> predicate, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv => predicate(drv));
        }

        public IWebElement WaitForElementPresent(By locator, TimeSpan? timeout = null)
            => CreateWait(timeout).Until(drv => drv.FindElement(locator));

        public IWebElement WaitForElementVisible(By locator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(ExpectedConditions.ElementIsVisible(locator));
        }

        public IWebElement WaitForElementClickable(By locator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(ExpectedConditions.ElementToBeClickable(locator));
        }

        public IReadOnlyCollection<IWebElement> WaitForAllElementsPresent(By locator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var list = drv.FindElements(locator);
                return list; // presence, not necessarily visible
            });
        }

        public IReadOnlyCollection<IWebElement> WaitForElementsCountAtLeast(By locator, int count, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var list = drv.FindElements(locator);
                return list.Count >= count ? list : null;
            });
        }

        public bool WaitForElementInvisible(By locator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var el = drv.FindElement(locator);
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
            });
        }

        public bool WaitForTextPresentInElement(By locator, string text, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var el = drv.FindElement(locator);
                var content = el.Text ?? string.Empty;
                return ignoreCase
                    ? content.Contains(text, StringComparison.OrdinalIgnoreCase)
                    : content.Contains(text, StringComparison.Ordinal);
            });
        }

        public bool WaitForElementTextEquals(By locator, string expected, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var el = drv.FindElement(locator);
                var actual = el.Text ?? string.Empty;
                return ignoreCase
                    ? string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(actual, expected, StringComparison.Ordinal);
            });
        }

        public bool WaitForAttributeContains(By locator, string attribute, string value, TimeSpan? timeout = null, bool ignoreCase = true)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var el = drv.FindElement(locator);
                var attr = el.GetAttribute(attribute) ?? string.Empty;
                return ignoreCase
                    ? attr.Contains(value, StringComparison.OrdinalIgnoreCase)
                    : attr.Contains(value, StringComparison.Ordinal);
            });
        }

        public bool WaitForValueNotEmpty(By locator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var el = drv.FindElement(locator);
                var val = el.GetAttribute("value");
                return !string.IsNullOrWhiteSpace(val);
            });
        }


        public bool WaitForUrlContains(string partialUrl, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
                drv.Url?.Contains(partialUrl, StringComparison.OrdinalIgnoreCase) == true);
        }

        public bool WaitForUrlIs(string url, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
                string.Equals(drv.Url, url, StringComparison.OrdinalIgnoreCase));
        }

        public bool WaitForTitleContains(string partial, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
                drv.Title?.Contains(partial, StringComparison.OrdinalIgnoreCase) == true);
        }

        public bool WaitForTitleIs(string title, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
                string.Equals(drv.Title, title, StringComparison.Ordinal));
        }

        public bool WaitForPageLoaded(TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                var js = (IJavaScriptExecutor)drv;
                var readyState = js.ExecuteScript("return document.readyState")?.ToString();
                return string.Equals(readyState, "complete", StringComparison.Ordinal);
            });
        }

        public bool WaitForAjaxIdle(TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv =>
            {
                try
                {
                    var js = (IJavaScriptExecutor)drv;
                    var active = js.ExecuteScript("return window.jQuery ? jQuery.active : 0");
                    return Convert.ToInt32(active) == 0;
                }
                catch (Exception)
                {
                    // If no JS context or jQuery undefined, consider 'idle'
                    return true;
                }
            });
        }

        public IWebDriver WaitForFrameAndSwitchToIt(By frameLocator, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(frameLocator));
        }

        public bool WaitForWindowCount(int expectedCount, TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(drv => drv.WindowHandles.Count == expectedCount);
        }

        public IAlert WaitForAlertPresent(TimeSpan? timeout = null)
        {
            return CreateWait(timeout).Until(ExpectedConditions.AlertIsPresent());
        }

        public void TypeWhenVisible(By locator, string text, TimeSpan? timeout = null, bool clear = true)
        {
            var el = WaitForElementVisible(locator, timeout);
            if (clear) el.Clear();
            el.SendKeys(text);
        }

    }
}