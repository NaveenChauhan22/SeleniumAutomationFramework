using Framework.Core.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using Serilog;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;
using Framework.Reporting;

namespace Framework.Core.Driver;

/// <summary>
/// Thread-safe factory and registry for Selenium <see cref="OpenQA.Selenium.IWebDriver"/> instances.
/// Browser type is resolved from the <c>TestSettings__Browser</c> environment variable (CI
/// override) or the <c>TestSettings:Browser</c> config key, defaulting to Chrome.
/// Headless mode, page-load timeout, and script timeout are similarly configurable.
/// Call <see cref="InitializeDriver"/> once per test thread and <see cref="QuitDriver"/> in
/// teardown; <see cref="GetDriver"/> retrieves the current thread's instance.
/// </summary>
public static class DriverManager
{
    // ThreadLocal makes the design ready for future parallel test execution.
    private static readonly ThreadLocal<IWebDriver?> DriverThread = new();

    public static IWebDriver GetDriver()
    {
        return DriverThread.Value ?? throw new InvalidOperationException(
            "WebDriver is not initialized for the current thread. Call InitializeDriver first.");
    }

    /// <summary>
    /// Initializes the WebDriver with browser selection from config or environment variables.
    /// Priority order: Environment variable 'TestSettings__Browser' > config TestSettings:Browser > default chrome
    /// Supported: chrome, edge, firefox
    /// </summary>
    public static IWebDriver InitializeDriver()
    {
        if (DriverThread.Value is not null)
        {
            return DriverThread.Value;
        }

        var browser = (Environment.GetEnvironmentVariable("TestSettings__Browser")
            ?? ConfigManager.GetString("TestSettings:Browser", "chrome")).ToLowerInvariant();

        try
        {
            var headless = bool.TryParse(Environment.GetEnvironmentVariable("TestSettings__Headless"), out var envHeadless)
                ? envHeadless
                : ConfigManager.GetBool("TestSettings:Headless", false);

            Log.Information("Initializing {Browser} WebDriver (Headless: {Headless})", browser, headless);

            DriverThread.Value = browser switch
            {
                "edge" => CreateEdgeDriver(headless),
                "firefox" => CreateFirefoxDriver(headless),
                _ => CreateChromeDriver(headless)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize WebDriver. Ensure the browser is installed on your system.");
            throw;
        }

        var driver = DriverThread.Value;
        if (driver is null)
        {
            throw new InvalidOperationException("Failed to initialize WebDriver instance.");
        }

        driver.Manage().Window.Maximize();
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(ConfigManager.GetInt("TestSettings:PageLoadTimeoutSeconds", 60));
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(ConfigManager.GetInt("TestSettings:ScriptTimeoutSeconds", 30));
        RuntimeContext.BrowserName = browser;

        return driver;
    }

    public static void QuitDriver()
    {
        if (DriverThread.Value is null)
        {
            return;
        }

        try
        {
            DriverThread.Value.Quit();
            DriverThread.Value.Dispose();
        }
        finally
        {
            DriverThread.Value = null;
        }
    }

    private static IWebDriver CreateChromeDriver(bool headless)
    {
        new WebDriverManager.DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);

        var options = new ChromeOptions();
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        if (headless)
        {
            options.AddArgument("--headless=new");
        }

        return new ChromeDriver(options);
    }

    private static IWebDriver CreateEdgeDriver(bool headless)
    {
        new WebDriverManager.DriverManager().SetUpDriver(new EdgeConfig(), VersionResolveStrategy.MatchingBrowser);

        var options = new EdgeOptions();
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");

        if (headless)
        {
            options.AddArgument("--headless=new");
        }

        return new EdgeDriver(options);
    }

    private static IWebDriver CreateFirefoxDriver(bool headless)
    {
        try
        {
            new WebDriverManager.DriverManager().SetUpDriver(new FirefoxConfig(), VersionResolveStrategy.MatchingBrowser);
            Log.Information("WebDriverManager successfully configured Firefox driver");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WebDriverManager failed for Firefox. Attempting direct instantiation. Error: {Message}", ex.Message);
            Log.Information("Ensure Firefox is installed on your system. Download from: https://www.mozilla.org/firefox/");
        }

        var options = new FirefoxOptions();
        options.AddArgument("--width=1920");
        options.AddArgument("--height=1080");

        if (headless)
        {
            options.AddArgument("--headless");
        }

        Log.Information("Firefox WebDriver created successfully");
        return new FirefoxDriver(options);
    }
}