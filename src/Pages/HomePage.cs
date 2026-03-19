using OpenQA.Selenium;
using Framework.Core.Utilities;
using System.Globalization;

namespace UITests.Pages;

/// <summary>
/// Page object for the authenticated home page (<c>/</c>).
/// Exposes helpers to verify the page has fully loaded (<see cref="IsHomePageLoaded"/>), inspect
/// the navigation bar, header section, featured event cards (titles, prices, book-now links),
/// and the currently logged-in user's displayed email. Relies on <see cref="WaitHelper"/> for
/// explicit waits rather than implicit waits.
/// </summary>
public class HomePage : BasePage
{
    public HomePage(IWebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    private readonly By _headerLogo = By.CssSelector("a[href='/'] span");
    private readonly By _navHomeLink = By.Id("nav-home");
    private readonly By _eventsLink = By.Id("nav-events");
    private readonly By _bookingsLink = By.Id("nav-bookings");
    private readonly By _userEmailDisplay = By.Id("user-email-display");
    private readonly By _logoutButton = By.Id("logout-btn");

    private readonly By _heading = By.CssSelector("main section h1");
    private readonly By _browseEventsCta = By.XPath("//span[contains(normalize-space(.),'Browse Events')]");
    private readonly By _myBookingsCta = By.XPath("//button[contains(normalize-space(.),'My Bookings')]");
    private readonly By _featuredEventsSectionHeading = By.XPath("//h2[normalize-space()='Featured Events']");
    private readonly By _featuredEventCards = By.CssSelector("[data-testid='event-card']");
    private readonly By _featuredEventTitles = By.CssSelector("[data-testid='event-card'] h3");
    private readonly By _featuredEventPrices = By.XPath("//*[@data-testid='event-card']//p[starts-with(normalize-space(.),'$')]");
    private readonly By _bookNowButtons = By.CssSelector("[data-testid='event-card'] a[data-testid='book-now-btn']");

    public bool IsHomePageLoaded()
    {
        try
        {
            return Wait.Until(d =>
            {
                if (d == null) return false;

                var urlValid = !d.Url.Contains("/login", StringComparison.OrdinalIgnoreCase);
                var hasNav = d.FindElements(_navHomeLink).Count > 0;
                var hasCards = d.FindElements(_featuredEventCards).Count > 0;

                return urlValid && hasNav && hasCards;
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string GetCurrentUrl() => Driver.Url;

    public bool IsUserLoggedIn()
    {
        try
        {
            if (!IsHomePageLoaded())
            {
                return false;
            }

            return IsElementDisplayed(_headerLogo)
                && IsElementDisplayed(_eventsLink)
                && IsElementDisplayed(_logoutButton)
                && IsElementDisplayed(_userEmailDisplay);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool IsLogoutDisplayed() => IsElementDisplayed(_logoutButton);

    public void ClickEvents() => Click(_eventsLink);

    public void ClickBookings() => Click(_bookingsLink);

    public void ClickBrowseEvents() => Click(_browseEventsCta);

    public void ClickMyBookings() => Click(_myBookingsCta);

    public bool IsHeaderSectionVisible() => IsElementDisplayed(_heading);

    public string GetHeadingText() => Text(_heading);

    public bool IsFeaturedEventsSectionVisible() => IsElementDisplayed(_featuredEventsSectionHeading);

    public int GetFeaturedEventCount() => Wait.WaitForAllElementsPresent(_featuredEventCards).Count;

    public string GetDisplayedUserEmail() => Text(_userEmailDisplay).Trim();

    public bool ArePrimaryNavigationLinksVisible()
    {
        return IsElementDisplayed(_navHomeLink)
            && IsElementDisplayed(_eventsLink)
            && IsElementDisplayed(_bookingsLink);
    }

    public bool DoFeaturedEventCardsContainTitleAndPrice()
    {
        try
        {
            var titles = Wait.WaitForAllElementsPresent(_featuredEventTitles);
            var prices = Wait.WaitForAllElementsPresent(_featuredEventPrices);

            if (titles == null || titles.Count == 0 || prices == null || prices.Count == 0)
            {
                return false;
            }

            var validTitleCount = titles.Count(title => title != null && !string.IsNullOrWhiteSpace(title.Text));
            var validPriceCount = prices.Count(price => price != null && IsValidCurrencyLabel(price.Text));

            return validTitleCount == titles.Count && validPriceCount == prices.Count;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool AreAllFeaturedEventsBookable()
    {
        try
        {
            var cards = Wait.WaitForAllElementsPresent(_featuredEventCards);
            var buttons = Wait.WaitForAllElementsPresent(_bookNowButtons);

            if (cards == null || cards.Count == 0 || buttons == null || buttons.Count == 0 || cards.Count != buttons.Count)
            {
                return false;
            }

            return buttons.All(button =>
                button != null
                && button.Displayed
                && button.Enabled
                && !string.IsNullOrWhiteSpace(button.GetAttribute("href"))
                && button.GetAttribute("href")!.Contains("/events/", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsValidCurrencyLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace("$", string.Empty).Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }
}
