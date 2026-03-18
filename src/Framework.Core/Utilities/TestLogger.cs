using Framework.Reporting;
using Serilog;

namespace Framework.Core.Utilities;

/// <summary>
/// Factory for per-test execution Serilog instances.
/// Creates a new logger for each test execution with a unique file name that includes
/// the test type, suite name, and test name.
/// </summary>
public static class TestLogger
{
    public static ILogger CreateExecutionLogger(string testType, string suiteName, string testName)
    {
        var logsDirectory = Path.Combine(ReportHelper.GetReportsDirectory(), "logs");
        Directory.CreateDirectory(logsDirectory);

        var safeType = SanitizeForFileName(testType);
        var safeSuite = SanitizeForFileName(suiteName);
        var safeTest = SanitizeForFileName(testName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var fileName = $"{timestamp}_{safeType}_{safeSuite}_{safeTest}.log";

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, fileName),
                rollingInterval: RollingInterval.Infinite,
                retainedFileCountLimit: 50,
                shared: false)
            .CreateLogger();
    }

    private static string SanitizeForFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var normalized = new string(value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "Unknown" : normalized;
    }
}
