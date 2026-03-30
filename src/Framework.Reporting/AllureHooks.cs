using Framework.Core.Driver;
using Serilog;
using Allure.Net.Commons;
using System.Runtime.CompilerServices;

/// <summary>
/// NUnit global setup fixture that bootstraps and finalises the Allure report run lifecycle.
/// This source file is compiled directly into each test project (UITests, APITests) via a
/// <c>&lt;Compile Include&gt;</c> link in the project file; it is excluded from the
/// Framework.Reporting library to avoid hooking into non-test assemblies.
/// </summary>
[SetUpFixture]
public sealed class AllureHooks
{
    /// <summary>
    /// Module initializer that runs before any tests execute to configure Allure's
    /// results directory with an absolute path, preventing DirectoryNotFoundException.
    /// Also loads environment variables from .env file for test credentials.
    /// </summary>
    [ModuleInitializer]
    public static void InitializeAllureConfig()
    {
        try
        {
            // Load environment variables from .env file before running tests
            LoadEnvironmentVariablesFromEnvFile();

            // Resolve the solution root directory where reports/ folder should be created
            var solutionRoot = ResolveSolutionRoot();
            var reportsDirectory = Path.Combine(solutionRoot, "reports");
            var allureResultsDirectory = Path.Combine(reportsDirectory, "allure-results");
            var binResultsDirectory = Path.Combine(AppContext.BaseDirectory, "allure-results");

            // Ensure parent directories exist before Allure tries to use them
            Directory.CreateDirectory(allureResultsDirectory);

            // Create suite-local results directory (under bin) to avoid one suite clearing another.
            try
            {
                Directory.CreateDirectory(binResultsDirectory);
            }
            catch { /* Ignore failures for bin directory creation */ }

            // Respect externally provided paths (for cross-browser parallel process isolation).
            // Fallback to bin-local directory so default behavior remains unchanged.
            var configuredResultsDirectory = Environment.GetEnvironmentVariable("ALLURE_RESULTS_DIRECTORY");
            if (string.IsNullOrWhiteSpace(configuredResultsDirectory))
            {
                Environment.SetEnvironmentVariable("ALLURE_RESULTS_DIRECTORY", binResultsDirectory);
            }

            var configuredReportPath = Environment.GetEnvironmentVariable("ALLURE_REPORT_PATH");
            if (string.IsNullOrWhiteSpace(configuredReportPath))
            {
                Environment.SetEnvironmentVariable("ALLURE_REPORT_PATH", reportsDirectory);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - allow tests to proceed and handle in GlobalSetUp
            Serilog.Log.Warning(ex, "Failed to pre-initialize Allure configuration directory");
        }
    }

    /// <summary>
    /// Loads environment variables from .env file in the solution root.
    /// This ensures test credentials are available regardless of how tests are invoked.
    /// </summary>
    private static void LoadEnvironmentVariablesFromEnvFile()
    {
        try
        {
            var solutionRoot = ResolveSolutionRoot();
            var envFilePath = Path.Combine(solutionRoot, ".env");

            // Only load if .env file exists (it's optional, can use system env vars instead)
            if (!File.Exists(envFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(envFilePath);
            var loadedCount = 0;

            foreach (var line in lines)
            {
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        // Only set if not already set in system environment (system env takes precedence)
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                            loadedCount++;
                        }
                    }
                }
            }

            // Log loaded variables for debugging
            if (loadedCount > 0)
            {
                Serilog.Log.Information("Loaded {Count} environment variable(s) from .env file", loadedCount);
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't throw - tests might use system env vars instead
            Serilog.Log.Warning(ex, "Failed to load environment variables from .env file. Tests will use system environment variables instead.");
        }
    }

    private static string ResolveSolutionRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionFile = Path.Combine(currentDirectory.FullName, "SeleniumAutomationFramework.sln");
            if (File.Exists(solutionFile))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        // Fallback to base directory if solution root not found
        return AppContext.BaseDirectory;
    }

    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        Framework.Reporting.AllureBootstrap.InitializeRun();
    }

    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        // Dispose ThreadLocal WebDriver instances to prevent resource leaks, especially important for parallel test execution
        try
        {
            DriverManager.DisposeDriversForAllThreads();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error disposing WebDriver ThreadLocal instances");
        }

        Framework.Reporting.AllureBootstrap.FinalizeRun();
    }
}