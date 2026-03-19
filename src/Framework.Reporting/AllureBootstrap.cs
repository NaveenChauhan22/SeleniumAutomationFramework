using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Serilog;
using Allure.Net.Commons;

namespace Framework.Reporting;

/// <summary>
/// Manages the Allure report lifecycle for a test run. On startup it initialises the results
/// directory, copies historical trend data, and writes executor/category/environment metadata.
/// On shutdown it updates the environment summary and attempts to generate an HTML report via
/// the Allure CLI (silently skipped when the CLI is not installed).
/// Also owns the helpers that persist environment data as <c>environment.json</c> and
/// <c>environment.properties</c> (previously a separate <c>AllureEnvironmentWriter</c> class).
/// </summary>
public static class AllureBootstrap
{
    private static readonly object SyncLock = new();
    private static bool _initialized;

    public static string ResultsDirectory => Path.Combine(ReportHelper.GetReportsDirectory(), "allure-results");

    public static string ReportDirectory => Path.Combine(ReportHelper.GetReportsDirectory(), "allure-report");

    public static void InitializeRun()
    {
        lock (SyncLock)
        {
            if (_initialized)
            {
                return;
            }

            // RuntimeContext.StartTime is initialized in RuntimeContext static constructor

            // Ensure parent directories exist with proper error handling
            try
            {
                var resultsDir = ResultsDirectory;
                var reportDir = ReportDirectory;

                // Create directories explicitly with parent path validation
                Directory.CreateDirectory(Path.GetDirectoryName(resultsDir) ?? resultsDir);
                Directory.CreateDirectory(resultsDir);
                Directory.CreateDirectory(Path.GetDirectoryName(reportDir) ?? reportDir);
                Directory.CreateDirectory(reportDir);

                Log.Debug("Initialized Allure directories: Results={ResultsDirectory}, Report={ReportDirectory}",
                    resultsDir, reportDir);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create Allure directories");
                throw;
            }

            PreserveHistory();
            try
            {
                AllureLifecycle.Instance.CleanupResultDirectory();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Warning during AllureLifecycle cleanup - proceeding with initialization");
            }
            PreserveHistory();

            WriteCategories();
            WriteExecutorInfo();
            WriteEnvironmentInfo(DateTimeOffset.UtcNow, null);

            _initialized = true;
        }
    }

    public static void FinalizeRun()
    {
        lock (SyncLock)
        {
            if (!_initialized)
            {
                return;
            }

            var endTime = DateTimeOffset.UtcNow;
            WriteEnvironmentInfo(RuntimeContext.StartTime, endTime);

            // Generate HTML report once per run
            var htmlReportPath = ReportHelper.GenerateHtmlReport();
            Log.Information("Generated HTML execution report at {ReportPath}", htmlReportPath);

            GenerateReportIfPossible();
            _initialized = false;
        }
    }

    private static void WriteEnvironmentInfo(DateTimeOffset startTime, DateTimeOffset? endTime)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Browser"] = RuntimeContext.GetBrowsersSummary(),
            ["OS"] = RuntimeInformation.OSDescription,
            ["Framework"] = $"{DetectTestFramework()} on .NET {Environment.Version}",
            ["ExecutionStart"] = startTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"),
            ["ExecutionEnd"] = endTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "In Progress",
            ["Duration"] = endTime.HasValue ? (endTime.Value - startTime).ToString("c") : "In Progress",
            ["TestType"] = RuntimeContext.GetTestTypesSummary()
        };

        WriteEnvironmentPropertiesFile(ResultsDirectory, values);
        WriteEnvironmentInfoFile(ResultsDirectory, values);
    }

    private static void WriteExecutorInfo()
    {
        var payload = new
        {
            name = "Local Automation Execution",
            type = "dotnet",
            reportName = "Selenium Automation Framework Allure Report",
            buildName = DetectTestFramework(),
            buildOrder = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        File.WriteAllText(
            Path.Combine(ResultsDirectory, "executor.json"),
            JsonConvert.SerializeObject(payload, Formatting.Indented));
    }

    private static void WriteCategories()
    {
        var categories = new[]
        {
            new { name = "Assertion Failures", matchedStatuses = new[] { "failed" }, messageRegex = ".*(Assert\\.|Expected:|Actual:).*" },
            new { name = "Timeouts", matchedStatuses = new[] { "broken", "failed" }, messageRegex = ".*(timeout|timed out|WebDriverTimeoutException).*" },
            new { name = "Element Not Found", matchedStatuses = new[] { "broken", "failed" }, messageRegex = ".*(NoSuchElementException|element not found|stale element).*" },
            new { name = "HTTP 5xx", matchedStatuses = new[] { "broken", "failed" }, messageRegex = ".*(HTTP\\s5\\d\\d|StatusCode: 5\\d\\d).*" }
        };

        File.WriteAllText(
            Path.Combine(ResultsDirectory, "categories.json"),
            JsonConvert.SerializeObject(categories, Formatting.Indented));
    }

    private static void PreserveHistory()
    {
        var sourceHistoryDirectory = Path.Combine(ReportDirectory, "history");
        var targetHistoryDirectory = Path.Combine(ResultsDirectory, "history");

        if (!Directory.Exists(sourceHistoryDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetHistoryDirectory);

        foreach (var sourceFile in Directory.GetFiles(sourceHistoryDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceHistoryDirectory, sourceFile);
            var destinationFile = Path.Combine(targetHistoryDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, true);
        }
    }

    private static void GenerateReportIfPossible()
    {
        var allureExecutable = ResolveAllureExecutable();
        if (allureExecutable is null)
        {
            Log.Warning("Allure CLI was not found on PATH or via ALLURE_HOME. Results were generated at {ResultsDirectory}", ResultsDirectory);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = allureExecutable,
            Arguments = $"generate \"{ResultsDirectory}\" -o \"{ReportDirectory}\" --clean",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Log.Warning("Failed to start Allure CLI process.");
            return;
        }

        process.WaitForExit();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            Log.Warning("Allure report generation failed with exit code {ExitCode}. Output: {Output}. Error: {Error}", process.ExitCode, standardOutput, standardError);
            return;
        }

        Log.Information("Generated Allure report at {ReportDirectory}", ReportDirectory);
    }

    private static string DetectTestFramework()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (loadedAssemblies.Contains("nunit.framework"))
        {
            return "NUnit";
        }

        if (loadedAssemblies.Contains("xunit.core"))
        {
            return "xUnit";
        }

        if (loadedAssemblies.Contains("Microsoft.VisualStudio.TestPlatform.TestFramework"))
        {
            return "MSTest";
        }

        return "Unknown";
    }

    private static string? ResolveAllureExecutable()
    {
        var allureHome = Environment.GetEnvironmentVariable("ALLURE_HOME");
        if (!string.IsNullOrWhiteSpace(allureHome))
        {
            var candidate = Path.Combine(allureHome, "bin", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "allure.bat" : "allure");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in pathEntries)
        {
            var candidate = Path.Combine(entry, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "allure.bat" : "allure");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    // ── Environment file helpers (absorbed from AllureEnvironmentWriter) ──────────────────────

    private static void WriteEnvironmentInfoFile(string outputDirectory, IDictionary<string, string> values)
    {
        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, "environment.json");
        File.WriteAllText(filePath, JsonConvert.SerializeObject(values, Formatting.Indented));
    }

    private static void WriteEnvironmentPropertiesFile(string outputDirectory, IDictionary<string, string> values)
    {
        Directory.CreateDirectory(outputDirectory);

        var lines = values
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{EscapePropertyValue(entry.Key)}={EscapePropertyValue(entry.Value)}");

        File.WriteAllLines(Path.Combine(outputDirectory, "environment.properties"), lines);
    }

    private static string EscapePropertyValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("=", "\\=", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal);
    }
}