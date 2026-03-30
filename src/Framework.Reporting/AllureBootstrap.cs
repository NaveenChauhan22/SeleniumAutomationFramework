using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
    private static readonly TimeSpan UnknownRunIdMergeWindow = TimeSpan.FromSeconds(30);

    public static string ResultsDirectory => ResolveResultsDirectory();

    public static string ReportDirectory => Path.Combine(ReportHelper.GetReportsDirectory(), "allure-report");

    private static string SessionMarkerPath => Path.Combine(ReportHelper.GetReportsDirectory(), ".allure-merge-session.marker");

    private sealed class MergeSessionMarker
    {
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public string RunId { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = [];
    }

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

            var startedFreshExecution = PrepareResultsDirectoryForCurrentSession();
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

            if (startedFreshExecution)
            {
                ClearOldScreenshots();
                ClearOldLogs();
            }
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

            CopyAllureResultsFromBinToRoot();

            var endTime = DateTimeOffset.UtcNow;
            WriteEnvironmentInfo(RuntimeContext.StartTime, endTime);

            // Refresh the marker timestamp on completion so the merge window is measured
            // from when this suite FINISHED, not when it started. This is critical when
            // suites run longer than the merge window.
            TouchMergeSessionMarker();

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

        // Use a named cross-process mutex so that when dotnet test runs APITests and UITests
        // in parallel (as separate OS processes), only one process copies the history files
        // at a time, preventing IOException: "file is being used by another process".
        using var mutex = new Mutex(false, "SeleniumFrameworkAllureHistoryMutex");
        var acquired = false;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.FromSeconds(30));
            foreach (var sourceFile in Directory.GetFiles(sourceHistoryDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceHistoryDirectory, sourceFile);
                var destinationFile = Path.Combine(targetHistoryDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                File.Copy(sourceFile, destinationFile, true);
            }
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static bool PrepareResultsDirectoryForCurrentSession()
    {
        // Start fresh for every new execution. Merge only when the same deterministic
        // run id is present across multiple suite hosts (e.g., Test Explorer run-all).
        var markerPath = SessionMarkerPath;
        var currentSource = GetCurrentResultsSource();
        var currentRunId = ResolveExecutionRunId();
        var marker = ReadMergeSessionMarker(markerPath);
        var shouldResetResults = ShouldResetCombinedResults(marker, currentSource, currentRunId);

        if (shouldResetResults)
        {
            ClearOldTestResults();
            Log.Information("Started new Allure merge session and reset existing result files.");
            marker = new MergeSessionMarker
            {
                RunId = currentRunId
            };
        }

        marker ??= new MergeSessionMarker
        {
            RunId = currentRunId
        };

        try
        {
            marker.RunId = currentRunId;

            if (!marker.Sources.Contains(currentSource, StringComparer.OrdinalIgnoreCase))
            {
                marker.Sources.Add(currentSource);
            }

            marker.UpdatedAtUtc = DateTimeOffset.UtcNow;
            File.WriteAllText(markerPath, JsonConvert.SerializeObject(marker, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update Allure merge session marker at {MarkerPath}", markerPath);
        }

        return shouldResetResults;
    }

    private static bool ShouldResetCombinedResults(MergeSessionMarker? marker, string currentSource, string currentRunId)
    {
        if (marker is null)
        {
            return true;
        }

        if (string.Equals(currentRunId, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            var elapsed = DateTimeOffset.UtcNow - marker.UpdatedAtUtc;

            // Re-running same source should always start fresh.
            if (marker.Sources.Any(source => string.Equals(source, currentSource, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Information(
                    "Execution id unavailable and source '{Source}' already exists in session. Starting fresh.",
                    currentSource);
                return true;
            }

            // Allow immediate API->UI or UI->API terminal chaining to merge as one execution.
            if (elapsed <= UnknownRunIdMergeWindow)
            {
                Log.Information(
                    "Execution id unavailable, but different source '{Source}' started within {Window}. Merging results.",
                    currentSource,
                    UnknownRunIdMergeWindow);
                return false;
            }

            Log.Information(
                "Execution id unavailable and previous session is older than {Window}. Starting fresh.",
                UnknownRunIdMergeWindow);
            return true;
        }

        if (!string.Equals(marker.RunId, currentRunId, StringComparison.OrdinalIgnoreCase))
        {
            Log.Information(
                "Execution id changed from '{PreviousRunId}' to '{CurrentRunId}'. Starting fresh.",
                marker.RunId,
                currentRunId);
            return true;
        }

        // Running the same suite again always starts fresh.
        if (marker.Sources.Any(source => string.Equals(source, currentSource, StringComparison.OrdinalIgnoreCase)))
        {
            Log.Information(
                "Source '{Source}' already present in merge session. Starting fresh.",
                currentSource);
            return true;
        }

        Log.Information(
            "Merging with existing session. Current source '{Source}' not yet in session.",
            currentSource);
        return false;
    }

    private static void TouchMergeSessionMarker()
    {
        var markerPath = SessionMarkerPath;
        try
        {
            if (!File.Exists(markerPath))
            {
                return;
            }

            var json = File.ReadAllText(markerPath);
            var marker = JsonConvert.DeserializeObject<MergeSessionMarker>(json);
            if (marker is null)
            {
                return;
            }

            // Update the timestamp to completion time so next suite measures gap from here.
            marker.UpdatedAtUtc = DateTimeOffset.UtcNow;
            File.WriteAllText(markerPath, JsonConvert.SerializeObject(marker, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to touch Allure merge session marker at {MarkerPath}", markerPath);
        }
    }

    private static MergeSessionMarker? ReadMergeSessionMarker(string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(markerPath);
            var marker = JsonConvert.DeserializeObject<MergeSessionMarker>(json);
            return marker;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read Allure merge session marker. Resetting results as a safe fallback.");
            return null;
        }
    }

    private static string GetCurrentResultsSource()
    {
        var source = Environment.GetEnvironmentVariable("ALLURE_RESULTS_DIRECTORY");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = Path.Combine(AppContext.BaseDirectory, "allure-results");
        }

        return Path.GetFullPath(source);
    }

    private static string ResolveExecutionRunId()
    {
        // Prefer test-platform-provided run/session ids when available.
        var envCandidates = new[]
        {
            "VSTEST_SESSION_ID",
            "VSTEST_RUN_ID",
            "TEST_SESSION_ID",
            "ALLURE_EXECUTION_ID"
        };

        foreach (var key in envCandidates)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        // Fallback: parse testhost command line for session id hints.
        var commandLine = Environment.CommandLine;
        var sessionMatch = Regex.Match(
            commandLine,
            @"--(?:testsessionid|session-id|sessionid)\s+([\w\-]+)",
            RegexOptions.IgnoreCase);

        if (sessionMatch.Success && sessionMatch.Groups.Count > 1)
        {
            var parsed = sessionMatch.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed.Trim();
            }
        }

        return "unknown";
    }

    private static string ResolveResultsDirectory()
    {
        var configuredResultsDirectory = Environment.GetEnvironmentVariable("ALLURE_RESULTS_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(configuredResultsDirectory))
        {
            return Path.GetFullPath(configuredResultsDirectory);
        }

        return Path.Combine(ReportHelper.GetReportsDirectory(), "allure-results");
    }

    private static void ClearOldTestResults()
    {
        var resultsDir = ResultsDirectory;
        if (!Directory.Exists(resultsDir))
        {
            return;
        }

        // Delete all test result files and metadata files from the results directory,
        // but preserve the history subdirectory for trend data.
        foreach (var file in Directory.GetFiles(resultsDir))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete old result file: {FilePath}", file);
            }
        }

        Log.Debug("Cleared old test results from {ResultsDirectory}", resultsDir);
    }

    private static void ClearOldScreenshots()
    {
        var screenshotsDir = Path.Combine(ReportHelper.GetReportsDirectory(), "screenshots");
        if (!Directory.Exists(screenshotsDir))
        {
            return;
        }

        try
        {
            Directory.Delete(screenshotsDir, true);
            Directory.CreateDirectory(screenshotsDir);
            Log.Debug("Cleared old screenshots from {ScreenshotsDirectory}", screenshotsDir);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clear old screenshots directory: {ScreenshotsDirectory}", screenshotsDir);
        }
    }

    private static void ClearOldLogs()
    {
        var logsDir = Path.Combine(ReportHelper.GetReportsDirectory(), "logs");
        if (!Directory.Exists(logsDir))
        {
            return;
        }

        try
        {
            Directory.Delete(logsDir, true);
            Directory.CreateDirectory(logsDir);
            Log.Debug("Cleared old logs from {LogsDirectory}", logsDir);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clear old logs directory: {LogsDirectory}", logsDir);
        }
    }

    private static void CopyAllureResultsFromBinToRoot()
    {
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "allure-results");
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        // Use file-based locking for cross-process synchronization in parallel execution
        var lockFile = Path.Combine(ResultsDirectory, ".allure-copy-lock");
        try
        {
            // Wait up to 30 seconds for the lock
            using (var fileStream = new FileStream(lockFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.WriteLine($"Locked by process {Environment.ProcessId} at {DateTime.UtcNow:O}");

                foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    var destinationFile = Path.Combine(ResultsDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    File.Copy(sourceFile, destinationFile, true);
                }

                Log.Information("Copied {Count} Allure results files from {SourceDir} to {TargetDir}",
                    Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).Length,
                    sourceDirectory,
                    ResultsDirectory);
            }
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "Failed to acquire lock for Allure results copying. This may cause issues in parallel execution.");
        }
        finally
        {
            // Clean up the lock file
            try
            {
                if (File.Exists(lockFile))
                {
                    File.Delete(lockFile);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
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