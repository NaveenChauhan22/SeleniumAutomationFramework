using Allure.NUnit;
using Allure.NUnit.Attributes;
using Framework.Reporting;
using UITests.Pages;

namespace UITests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
[AllureNUnit]
[AllureParentSuite("UITests")]
[AllureSuite("Home Navigation")]
[AllureFeature("Navigation")]
public class HomeNavigationTests : BaseTest
{
    private LoginTestData _loginData = null!;
    private HomePageAssertionData _homeData = null!;

    [SetUp]
    public void SetUpTests()
    {
        _loginData = LoadLoginData();
        _homeData = LoadHomePageAssertionData();
        LoginAsValidUser(_loginData);
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Featured events defaults")]
    public void HomePage_EventsWithDefaults()
    {
        var homePage = new HomePage(Driver, Wait);

        ReportHelper.AddStep("Verifying home page base elements are visible");
        Assert.That(homePage.IsHomePageLoaded(), Is.True, "Home page should be loaded after valid login");
        Assert.That(homePage.ArePrimaryNavigationLinksVisible(), Is.True, "Home navigation links should be visible");
        Assert.That(homePage.IsHeaderSectionVisible(), Is.True, "Home section should be visible");
        Assert.That(homePage.GetHeadingText(), Does.Contain(_homeData.HomePageHeading.ExpectedHeading),
            "Heading text is not matching expected value from data file");

        ReportHelper.AddStep("Verifying featured event cards and defaults");
        Assert.That(homePage.IsFeaturedEventsSectionVisible(), Is.True,
            "Featured Events section should be visible on home page");
        Assert.That(homePage.GetFeaturedEventCount(), Is.GreaterThanOrEqualTo(_homeData.FeaturedEvents.MinimumCardCount),
            "Featured event card count is less than expected minimum from data file");

        // Verify required fields are present if specified
        if (_homeData.FeaturedEvents.RequiredFields.Contains("title", StringComparer.OrdinalIgnoreCase) ||
            _homeData.FeaturedEvents.RequiredFields.Contains("price", StringComparer.OrdinalIgnoreCase))
        {
            Assert.That(homePage.DoFeaturedEventCardsContainTitleAndPrice(), Is.True,
                "Each featured event card should show a title and a valid price label");
        }

        if (_homeData.FeaturedEvents.RequiredFields.Contains("bookNowLink", StringComparer.OrdinalIgnoreCase))
        {
            Assert.That(homePage.AreAllFeaturedEventsBookable(), Is.True,
                "Each featured event card should have an enabled Book Now link");
        }
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.High)]
    [AllureStory("Browse events route")]
    public void HomePage_NavigateToEventsPage()
    {
        var homePage = new HomePage(Driver, Wait);

        ReportHelper.AddStep("Clicking Browse Events call-to-action from heading section");
        homePage.ClickBrowseEvents();
        ReportHelper.AddStep("Verifying navigation to events page");
        Wait.WaitForUrlContains(_homeData.Navigation.EventsPath);
        Assert.That(Driver.Url, Does.Contain(_homeData.Navigation.EventsPath),
            "User should be navigated to events page after clicking Browse Events");
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("Bookings route")]
    public void HomePage_NavigateToBookingsPage()
    {
        var homePage = new HomePage(Driver, Wait);

        ReportHelper.AddStep("Clicking My Bookings call-to-action from heading section");
        homePage.ClickMyBookings();
        ReportHelper.AddStep("Verifying navigation to bookings page");
        Wait.WaitForUrlContains(_homeData.Navigation.BookingsPath);
        Assert.That(Driver.Url, Does.Contain(_homeData.Navigation.BookingsPath),
            "User should be navigated to bookings page after clicking My Bookings");
    }

}
