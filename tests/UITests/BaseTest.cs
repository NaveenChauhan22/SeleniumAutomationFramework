using Framework.Core.Configuration;
using Framework.Core.Driver;
using Framework.Core.Utilities;
using Framework.Data;
using Framework.Reporting;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using System.Diagnostics;
using UITests.Pages;

namespace UITests;

public abstract class BaseTest
    : AllureTestBase
{
    protected Serilog.ILogger Logger = Serilog.Log.Logger;
    protected IWebDriver Driver => DriverManager.GetDriver();
    protected WaitHelper Wait = null!;
    private IDisposable? _executionLoggerHandle;
    private readonly Stopwatch _executionTimer = new();
    private DateTimeOffset _testStartedAt;
    private string _activeBrowser = "unknown";
    private string _priority = "Unspecified";
    private string _suiteName = string.Empty;

    protected string ApplicationBaseUrl => ConfigManager.GetString("TestSettings:BaseUrl", "https://eventhub.rahulshettyacademy.com");
    protected string LoginUrl => ConfigManager.GetString("TestSettings:AppUrl", $"{ApplicationBaseUrl}/login");
    protected string ConfiguredBrowser => ConfigManager.GetString("TestSettings:Browser", "chrome");

    [SetUp]
    public void SetUp()
    {
        _testStartedAt = DateTimeOffset.Now;
        _executionTimer.Restart();
        _activeBrowser = Environment.GetEnvironmentVariable("TestSettings__Browser") ?? ConfiguredBrowser;
        _priority = GetCurrentPriorityLevel()?.ToString() ?? "Unspecified";
        _suiteName = GetCurrentSuiteName();

        var executionLogger = TestLogger.CreateExecutionLogger("UI", _suiteName, TestContext.CurrentContext.Test.Name);
        Logger = executionLogger.ForContext<BaseTest>();
        Serilog.Log.Logger = executionLogger;
        _executionLoggerHandle = executionLogger as IDisposable;

        Logger.Information("[UI] Starting test {TestName}", TestContext.CurrentContext.Test.Name);
        RuntimeContext.TestType = "UI";
        RuntimeContext.BrowserName = _activeBrowser;
        Logger.Information("[UI] Browser resolved to {Browser}", _activeBrowser);
        BeginAllureTest();
        ReportHelper.BeginTest(TestContext.CurrentContext.Test.Name);

        try
        {
            DriverManager.InitializeDriver();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[UI] Driver initialization failed for browser {Browser}", _activeBrowser);
            throw new InvalidOperationException(
                $"Failed to initialize {_activeBrowser} WebDriver. " +
                $"Ensure the browser is installed on your system. " +
                $"Error: {ex.Message}", ex);
        }

        Wait = new WaitHelper(Driver, ConfigManager.GetInt("TestSettings:ExplicitWaitSeconds", 15));

        Driver.Navigate().GoToUrl(LoginUrl);
        Wait.WaitForPageLoaded();
        Logger.Information("[UI] Navigated to login URL {LoginUrl}", LoginUrl);

        // Log actual browser being used (may differ from config if overridden via env var)
        ReportHelper.AddStep($"Browser: {_activeBrowser}");
        ReportHelper.AddStep($"Navigated to {LoginUrl}");
    }

    protected LoginTestData LoadLoginData()
    {
        Logger.Information("[UI] Loading login test data");
        var loginData = LoadTestData<LoginTestData>("loginData.json");

        Assert.That(loginData, Is.Not.Null, "login test data must be valid JSON");
        Assert.That(loginData!.ValidCredentials.Email, Is.Not.Null.And.Not.Empty, "validCredentials.email is required in login data file");
        Assert.That(loginData.ValidCredentials.Password, Is.Not.Null.And.Not.Empty, "validCredentials.password is required in login data file");

        return loginData;
    }

    protected HomePageAssertionData LoadHomePageAssertionData()
    {
        Logger.Information("[UI] Loading home page assertion test data");
        var homePageData = LoadTestData<HomePageAssertionData>("homePageData.json");

        Assert.That(homePageData.Heading.ExpectedHeadingContains, Is.Not.Null.And.Not.Empty,
            "heading.expectedHeadingContains is required in homePageData.json");
        Assert.That(homePageData.Navigation.EventsPath, Is.Not.Null.And.Not.Empty,
            "navigation.eventsPath is required in homePageData.json");
        Assert.That(homePageData.Navigation.BookingsPath, Is.Not.Null.And.Not.Empty,
            "navigation.bookingsPath is required in homePageData.json");

        return homePageData;
    }

    protected EventTestData LoadEventData()
    {
        Logger.Information("[UI] Loading event test data from Excel");
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "TestData", "EventData.xlsx");
        Assert.That(File.Exists(dataFilePath), Is.True, $"event data file should exist at {dataFilePath}");

        var rows = ExcelDataProvider.ReadFirstSheet(dataFilePath);
        Assert.That(rows.Count, Is.GreaterThan(0), "EventData.xlsx should contain one data row after headers");

        var row = rows[0];

        var title = GetValue(row, "Title", "EventTitle");
        var description = GetValue(row, "Description", "EventDescription");
        var category = GetValue(row, "Category", "EventCategory");
        var city = GetValue(row, "City", "EventCity");
        var venue = GetValue(row, "Venue", "EventVenue");
        var price = GetValue(row, "Price", "EventPrice");
        var totalSeats = GetValue(row, "TotalSeats", "Seats");
        var eventsPath = GetValue(row, "EventsPath", "NavigationEventsPath");
        var successMessageContains = GetValue(row, "SuccessMessageContains", "PopupMessageContains");

        var eventData = new EventTestData
        {
            EventDetails = new EventTestData.EventDetailsData
            {
                Title = title,
                Description = description,
                Category = category,
                City = city,
                Venue = venue,
                Price = price,
                TotalSeats = totalSeats
            },
            Navigation = new EventTestData.NavigationData
            {
                EventsPath = string.IsNullOrWhiteSpace(eventsPath) ? "/events" : eventsPath
            },
            Assertions = new EventTestData.AssertionsData
            {
                SuccessMessageContains = string.IsNullOrWhiteSpace(successMessageContains) ? "event" : successMessageContains
            }
        };

        Assert.That(eventData.EventDetails.Title, Is.Not.Null.And.Not.Empty,
            "title is required in EventData.xlsx");
        Assert.That(eventData.EventDetails.Category, Is.Not.Null.And.Not.Empty,
            "category is required in EventData.xlsx");
        Assert.That(eventData.EventDetails.City, Is.Not.Null.And.Not.Empty,
            "city is required in EventData.xlsx");
        Assert.That(eventData.EventDetails.Venue, Is.Not.Null.And.Not.Empty,
            "venue is required in EventData.xlsx");
        Assert.That(eventData.EventDetails.Price, Is.Not.Null.And.Not.Empty,
            "price is required in EventData.xlsx");
        Assert.That(eventData.EventDetails.TotalSeats, Is.Not.Null.And.Not.Empty,
            "totalSeats is required in EventData.xlsx");

        return eventData;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> row, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (row.TryGetValue(candidate, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    protected T LoadTestData<T>(string fileName) where T : class
    {
        Logger.Information("[UI] Loading JSON test data from {FileName}", fileName);
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        Assert.That(File.Exists(dataFilePath), Is.True, $"test data file should exist at {dataFilePath}");

        var data = JsonDataProvider.Read<T>(dataFilePath);

        Assert.That(data, Is.Not.Null, $"{fileName} must contain valid JSON for {typeof(T).Name}");
        return data!;
    }

    protected void LoginAsValidUser(LoginTestData? loginData = null)
    {
        var data = loginData ?? LoadLoginData();
        ReportHelper.AddStep("Logging in as pre-requisite with valid credentials");
        var loginPage = new LoginPage(Driver, Wait);
        loginPage.LoginAs(data.ValidCredentials.Email, data.ValidCredentials.Password);
    }

    [TearDown]
    public void TearDown()
    {
        Logger.Information("[UI] Completing test {TestName}", TestContext.CurrentContext.Test.Name);
        string? screenshotPath = null;
        var failureAttachments = new List<AllureAttachment>();
        var fullClassName = TestContext.CurrentContext.Test.ClassName ?? string.Empty;
        var shortClassName = fullClassName.Contains('.') ? fullClassName[(fullClassName.LastIndexOf('.') + 1)..] : fullClassName;

        try
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                Logger.Warning("[UI] Test {TestName} failed. Capturing artifacts.", TestContext.CurrentContext.Test.Name);
                try
                {
                    screenshotPath = ScreenshotHelper.CaptureScreenshot(
                        Driver,
                        Path.Combine(ReportHelper.GetReportsDirectory(), "screenshots"),
                        $"{shortClassName}_{TestContext.CurrentContext.Test.Name}");

                    ReportHelper.AttachFile("Failure Screenshot", screenshotPath, "image/png");
                    failureAttachments.Add(new AllureAttachment("Failure Screenshot", "image/png", FilePath: screenshotPath));
                    Logger.Information("[UI] Screenshot captured at {ScreenshotPath}", screenshotPath);
                }
                catch (InvalidOperationException)
                {
                    // Driver was not initialized, skip screenshot
                }

                try
                {
                    var pageSource = Driver.PageSource;
                    if (!string.IsNullOrWhiteSpace(pageSource))
                    {
                        failureAttachments.Add(new AllureAttachment("Page Source", "text/html", Content: pageSource, FileExtension: "html"));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Driver was not initialized, skip page source
                }
            }

            var outcome = TestContext.CurrentContext.Result.Outcome.Status.ToString();
            var errorMessage = TestContext.CurrentContext.Result.Message;
            _executionTimer.Stop();
            var finishedAt = DateTimeOffset.Now;
            CompleteAllureTest(failureAttachments);

            ReportHelper.RecordTestResult(
                TestContext.CurrentContext.Test.Name,
                shortClassName,
                _suiteName,
                outcome,
                _executionTimer.Elapsed,
                _activeBrowser,
                "UI",
                _priority,
                _testStartedAt,
                finishedAt,
                errorMessage,
                screenshotPath);

            var reportPath = ReportHelper.GenerateHtmlReport();
            TestContext.AddTestAttachment(reportPath, "HTML Execution Report");
            Logger.Information("[UI] Test {TestName} finished with status {Status} in {DurationMs} ms",
                TestContext.CurrentContext.Test.Name,
                outcome,
                _executionTimer.Elapsed.TotalMilliseconds);
        }
        finally
        {
            try
            {
                DriverManager.QuitDriver();
            }
            catch (InvalidOperationException)
            {
                // Driver was never initialized, nothing to quit
            }

            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                Logger.Warning("[UI] Test {TestName} failed with message: {Message}",
                    TestContext.CurrentContext.Test.Name,
                    TestContext.CurrentContext.Result.Message);
            }

            _executionLoggerHandle?.Dispose();
            _executionLoggerHandle = null;
        }
    }

    protected sealed class LoginTestData
    {
        public Credentials ValidCredentials { get; init; } = new();
        public InvalidEmailScenarioData InvalidEmailScenario { get; init; } = new();
        public ShortPasswordScenarioData ShortPasswordScenario { get; init; } = new();
        public EmptyFieldsScenarioData EmptyFieldsScenario { get; init; } = new();
        public WrongPasswordScenarioData WrongPasswordScenario { get; init; } = new();
        public UnregisteredEmailScenarioData UnregisteredEmailScenario { get; init; } = new();
        public AssertionData Assertions { get; init; } = new();

        public sealed class Credentials
        {
            public string Email { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
        }

        public sealed class InvalidEmailScenarioData
        {
            public string Email { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
            public string ExpectedEmailValidationError { get; init; } = string.Empty;
            public string ExpectedUrlContains { get; init; } = string.Empty;
        }

        public sealed class ShortPasswordScenarioData
        {
            public string Email { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
            public string ExpectedPasswordValidationError { get; init; } = string.Empty;
            public string ExpectedUrlContains { get; init; } = string.Empty;
        }

        public sealed class EmptyFieldsScenarioData
        {
            public string ExpectedEmailValidationError { get; init; } = string.Empty;
            public string ExpectedPasswordValidationError { get; init; } = string.Empty;
            public string ExpectedUrlContains { get; init; } = string.Empty;
        }

        public sealed class WrongPasswordScenarioData
        {
            public string Password { get; init; } = string.Empty;
            public string ExpectedUrlContains { get; init; } = string.Empty;
        }

        public sealed class UnregisteredEmailScenarioData
        {
            public string Email { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
            public string ExpectedUrlContains { get; init; } = string.Empty;
        }

        public sealed class AssertionData
        {
            public string HomePageDomain { get; init; } = string.Empty;
            public string LoginPagePath { get; init; } = string.Empty;
        }
    }

    protected sealed class HomePageAssertionData
    {
        public HeadingData Heading { get; init; } = new();
        public FeaturedEventsData FeaturedEvents { get; init; } = new();
        public NavigationData Navigation { get; init; } = new();

        public sealed class HeadingData
        {
            public string ExpectedHeadingContains { get; init; } = string.Empty;
        }

        public sealed class FeaturedEventsData
        {
            public int MinimumCardCount { get; init; } = 1;
            public bool RequireTitleAndPrice { get; init; } = true;
            public bool RequireBookNowLink { get; init; } = true;
        }

        public sealed class NavigationData
        {
            public string EventsPath { get; init; } = string.Empty;
            public string BookingsPath { get; init; } = string.Empty;
        }
    }

    protected sealed class EventTestData
    {
        public EventDetailsData EventDetails { get; init; } = new();
        public NavigationData Navigation { get; init; } = new();
        public AssertionsData Assertions { get; init; } = new();

        public sealed class EventDetailsData
        {
            public string Title { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string Category { get; init; } = string.Empty;
            public string City { get; init; } = string.Empty;
            public string Venue { get; init; } = string.Empty;
            public string Price { get; init; } = string.Empty;
            public string TotalSeats { get; init; } = string.Empty;
        }

        public sealed class NavigationData
        {
            public string EventsPath { get; init; } = "/events";
        }

        public sealed class AssertionsData
        {
            public string SuccessMessageContains { get; init; } = "event";
        }
    }
}