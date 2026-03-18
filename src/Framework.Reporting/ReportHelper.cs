using Serilog;
using Allure.Net.Commons;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Framework.Reporting;

/// <summary>
/// Central helper for both Allure and the custom HTML execution report.
/// Provides methods to attach files/content to Allure (<see cref="AttachFile"/>,
/// <see cref="AttachContent"/>), record named test steps (<see cref="AddStep"/>), accumulate
/// per-test result data (<see cref="RecordTestResult"/>), and finally render a self-contained
/// HTML report with run summary, suite breakdown, per-test rows, history trend, and optional
/// screenshot links (<see cref="GenerateHtmlReport"/>).
/// </summary>
public static class ReportHelper
{
    private static readonly List<TestExecutionRecord> TestExecutionRecords = [];
    private static readonly Dictionary<string, List<string>> TestSteps = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object ReportLock = new();
    private static readonly string RunTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    private static readonly DateTimeOffset RunStartedAt = DateTimeOffset.Now;
    private static readonly AsyncLocal<string?> CurrentTestName = new();
    private static readonly Lazy<bool> ExecutionHistoryEnabled = new(ResolveExecutionHistoryEnabled);

    public static void AttachFile(string name, string filePath, string contentType)
    {
        if (!File.Exists(filePath))
        {
            Log.Warning("Skipped report attachment because file was not found: {FilePath}", filePath);
            return;
        }

        AllureApi.AddAttachment(name, contentType, filePath);
    }

    public static void AttachContent(string name, string contentType, string content, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Log.Warning("Skipped report attachment because content was empty: {AttachmentName}", name);
            return;
        }

        var normalizedExtension = fileExtension.TrimStart('.');
        AllureApi.AddAttachment(name, contentType, Encoding.UTF8.GetBytes(content), normalizedExtension);
    }

    public static void AddStep(string stepMessage, bool createAllureStep = true)
    {
        if (createAllureStep)
        {
            AllureApi.Step(stepMessage);
        }

        lock (ReportLock)
        {
            var testName = CurrentTestName.Value;
            if (string.IsNullOrWhiteSpace(testName))
            {
                return;
            }

            if (!TestSteps.TryGetValue(testName, out var steps))
            {
                steps = [];
                TestSteps[testName] = steps;
            }

            steps.Add($"{DateTime.Now:HH:mm:ss} - {stepMessage}");
        }
    }

    public static void BeginTest(string testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return;
        }

        lock (ReportLock)
        {
            TestSteps[testName] = [];
        }

        CurrentTestName.Value = testName;
    }

    public static string GetReportsDirectory()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionFile = Path.Combine(currentDirectory.FullName, "SeleniumAutomationFramework.sln");
            if (File.Exists(solutionFile))
            {
                var reportDirectory = Path.Combine(currentDirectory.FullName, "reports");
                Directory.CreateDirectory(reportDirectory);
                return reportDirectory;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var fallbackDirectory = Path.Combine(AppContext.BaseDirectory, "reports");
        Directory.CreateDirectory(fallbackDirectory);
        return fallbackDirectory;
    }

    public static string GetAllureReportDirectory()
    {
        var reportDirectory = Path.Combine(GetReportsDirectory(), "allure-report");
        Directory.CreateDirectory(reportDirectory);
        return reportDirectory;
    }

    public static void RecordTestResult(
        string testName,
        string className,
        string suiteName,
        string status,
        TimeSpan duration,
        string browser,
        string testType,
        string priority,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        string? errorMessage = null,
        string? screenshotPath = null)
    {
        lock (ReportLock)
        {
            var stepLogs = TestSteps.TryGetValue(testName, out var steps)
                ? steps.ToList()
                : new List<string>();

            TestExecutionRecords.Add(new TestExecutionRecord(
                startedAt,
                finishedAt,
                testName,
                className,
                suiteName,
                status,
                duration,
                browser,
                testType,
                priority,
                stepLogs,
                errorMessage,
                screenshotPath));
        }
    }

    public static string GenerateHtmlReport()
    {
        lock (ReportLock)
        {
            var reportDirectory = GetAllureReportDirectory();
            var browserTag = GetRunBrowserTag();
            var reportPath = Path.Combine(reportDirectory, $"TestExecutionReport_{RunTimestamp}_{browserTag}.html");
            var orderedRecords = TestExecutionRecords.OrderBy(record => record.StartedAt).ToList();
            var summary = BuildRunSummary(orderedRecords);
            var executionHistoryEnabled = IsExecutionHistoryEnabled();
            var previousRun = executionHistoryEnabled ? GetPreviousRunSummary(reportDirectory) : null;
            var history = executionHistoryEnabled
                ? UpsertRunHistory(reportDirectory, summary)
                : DeleteHistoryAndReturnEmpty(reportDirectory);
            var reportTitle = DeriveReportTitle(summary);

            var summaryCards = string.Join(Environment.NewLine, new[]
            {
                BuildMetricCard("Total", summary.Total, "metric-card total", BuildTrendText(summary.Total, previousRun?.Total)),
                BuildMetricCard("Passed", summary.Passed, "metric-card passed", BuildTrendText(summary.Passed, previousRun?.Passed)),
                BuildMetricCard("Failed", summary.Failed, "metric-card failed", BuildTrendText(summary.Failed, previousRun?.Failed)),
                BuildMetricCard("Broken", summary.Broken, "metric-card broken", BuildTrendText(summary.Broken, previousRun?.Broken)),
                BuildMetricCard("Skipped", summary.Skipped, "metric-card skipped", BuildTrendText(summary.Skipped, previousRun?.Skipped))
            });

            var suiteRows = string.Join(Environment.NewLine, summary.Suites.Select(BuildSuiteRow));
            var historyRows = history.Count == 0
                ? BuildEmptyHistoryRow(executionHistoryEnabled
                    ? "No previous executions captured yet."
                    : "Execution history is disabled by configuration.")
                : string.Join(Environment.NewLine, history.Take(6).Select(BuildHistoryRow));
            var testRows = string.Join(Environment.NewLine, orderedRecords.Select(BuildTestRow));

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine($"<html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>{WebUtility.HtmlEncode(reportTitle)}</title>");
            html.AppendLine("""
<style>
:root{--bg:#f3f6fb;--card:#ffffff;--ink:#162033;--muted:#5f6b7c;--line:#d9e2ef;--navy:#12325b;--blue:#1f66cc;--green:#16794d;--red:#b42318;--amber:#e59600;--orange:#d97706;--yellow:#b98b00;--shadow:0 18px 40px rgba(18,50,91,.08);}*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:linear-gradient(180deg,#eef3f9 0%,#f8fbff 100%);color:var(--ink)}a{color:var(--blue);text-decoration:none}a:hover{text-decoration:underline}.shell{max-width:1400px;margin:0 auto;padding:28px 24px 56px}.hero{background:linear-gradient(135deg,#163962 0%,#24558f 100%);color:#fff;border-radius:22px;padding:28px 32px;box-shadow:var(--shadow)}.hero h1{margin:0 0 8px;font-size:2rem}.hero p{margin:0;color:rgba(255,255,255,.82)}.nav{display:flex;gap:14px;flex-wrap:wrap;margin:18px 0 0}.nav a{padding:10px 14px;border-radius:999px;background:rgba(255,255,255,.12);color:#fff;font-size:.92rem}.grid{display:grid;gap:18px}.metrics{grid-template-columns:repeat(auto-fit,minmax(170px,1fr));margin:24px 0}.metric-card,.panel{background:var(--card);border:1px solid var(--line);border-radius:18px;box-shadow:var(--shadow)}.metric-card{padding:18px 20px}.metric-card .label{font-size:.88rem;color:var(--muted);text-transform:uppercase;letter-spacing:.06em}.metric-card .value{font-size:2rem;font-weight:700;margin:10px 0 6px}.metric-card .trend{font-size:.92rem;color:var(--muted)}.metric-card.passed{border-top:5px solid var(--green)}.metric-card.failed{border-top:5px solid var(--red)}.metric-card.broken{border-top:5px solid var(--orange)}.metric-card.skipped{border-top:5px solid var(--amber)}.metric-card.total{border-top:5px solid var(--navy)}.overview{grid-template-columns:2fr 1fr;align-items:start}.panel{padding:20px}.panel h2{margin:0 0 16px;font-size:1.12rem}.facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px}.fact{padding:14px 16px;border:1px solid var(--line);border-radius:14px;background:#fbfdff}.fact .key{font-size:.82rem;color:var(--muted);text-transform:uppercase;letter-spacing:.05em}.fact .val{margin-top:6px;font-weight:600}.section{margin-top:24px}.section-header{display:flex;justify-content:space-between;align-items:end;gap:16px;margin-bottom:12px}.section-header h2{margin:0}.section-header p{margin:0;color:var(--muted)}table{width:100%;border-collapse:collapse}th,td{padding:14px 12px;border-bottom:1px solid var(--line);text-align:left;vertical-align:top}th{font-size:.82rem;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);background:#f7faff}.status-pill,.priority-pill{display:inline-flex;align-items:center;padding:6px 10px;border-radius:999px;font-size:.82rem;font-weight:700}.status-passed{background:#e7f6ee;color:var(--green)}.status-failed{background:#fdecea;color:var(--red)}.status-broken{background:#fff1e8;color:var(--orange)}.status-skipped{background:#fff8dd;color:var(--yellow)}.priority-high{background:#fdecea;color:var(--red)}.priority-medium{background:#fff1e1;color:var(--orange)}.priority-low{background:#fff7d6;color:var(--yellow)}.priority-unspecified{background:#edf2f7;color:var(--muted)}.suite-bar{width:100%;height:12px;border-radius:999px;overflow:hidden;background:#edf2f7;display:flex;min-width:180px}.suite-bar span{height:100%}.suite-pass{background:var(--green)}.suite-fail{background:var(--red)}.suite-broken{background:var(--orange)}.suite-skip{background:var(--amber)}details{border:1px solid var(--line);border-radius:12px;background:#fbfdff;padding:10px 12px}details summary{cursor:pointer;font-weight:600}ol{margin:10px 0 0;padding-left:20px}.muted{color:var(--muted)}.history-note{font-size:.9rem;color:var(--muted)}@media (max-width:980px){.overview{grid-template-columns:1fr}}@media (max-width:720px){.shell{padding:18px}.hero{padding:24px}.hero h1{font-size:1.6rem}th,td{padding:12px 10px}}</style>
</head><body>
""");
            html.AppendLine("<div class='shell'>");
            html.AppendLine($"<section class='hero'><h1>{WebUtility.HtmlEncode(reportTitle)}</h1><p>Execution summary, suite analytics, trend, browser context, and per-test priority in one report.</p><div class='nav'><a href='#overview'>Overview</a><a href='#suites'>Suites</a><a href='#trend'>Trend</a><a href='#details'>Details</a></div></section>");
            html.AppendLine($"<section id='overview' class='grid metrics'>{summaryCards}</section>");
            html.AppendLine("<section class='grid overview'>");
            html.AppendLine($"<div class='panel'><h2>Execution Overview</h2><div class='facts'><div class='fact'><div class='key'>Execution Date</div><div class='val'>{summary.RunEndedAt:yyyy-MM-dd}</div></div><div class='fact'><div class='key'>Execution Time</div><div class='val'>{summary.RunEndedAt:HH:mm:ss}</div></div><div class='fact'><div class='key'>Run Start</div><div class='val'>{summary.RunStartedAt:yyyy-MM-dd HH:mm:ss zzz}</div></div><div class='fact'><div class='key'>Run End</div><div class='val'>{summary.RunEndedAt:yyyy-MM-dd HH:mm:ss zzz}</div></div><div class='fact'><div class='key'>Duration</div><div class='val'>{FormatDuration(summary.RunDuration)}</div></div><div class='fact'><div class='key'>Browsers</div><div class='val'>{WebUtility.HtmlEncode(summary.BrowsersSummary)}</div></div><div class='fact'><div class='key'>Test Types</div><div class='val'>{WebUtility.HtmlEncode(summary.TestTypesSummary)}</div></div><div class='fact'><div class='key'>Suites</div><div class='val'>{WebUtility.HtmlEncode(summary.SuitesSummary)}</div></div></div></div>");
            html.AppendLine($"<aside class='panel'><h2>Run Health</h2><div class='facts'><div class='fact'><div class='key'>Pass Rate</div><div class='val'>{summary.PassRate:P0}</div></div><div class='fact'><div class='key'>Fail + Broken</div><div class='val'>{summary.Failed + summary.Broken}</div></div><div class='fact'><div class='key'>Generated</div><div class='val'>{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}</div></div><div class='fact'><div class='key'>Run Identifier</div><div class='val'>{RunTimestamp}</div></div></div><p class='history-note' style='margin-top:14px'>{(executionHistoryEnabled ? "Trend values compare this run to the most recent earlier report in reports/allure-report/execution-history.json." : "Execution history persistence is disabled. Only current run metrics are shown.")}</p></aside>");
            html.AppendLine("</section>");
            html.AppendLine($"<section id='suites' class='section'><div class='section-header'><div><h2>Suites</h2><p>Suite names, totals, duration, and colored pass/fail distribution for this run.</p></div></div><div class='panel'><table><thead><tr><th>Suite</th><th>Total</th><th>Passed</th><th>Failed</th><th>Broken</th><th>Skipped</th><th>Duration</th><th>Distribution</th></tr></thead><tbody>{suiteRows}</tbody></table></div></section>");
            html.AppendLine($"<section id='trend' class='section'><div class='section-header'><div><h2>Trend</h2><p>Recent execution history and pass/fail movement across runs.</p></div></div><div class='panel'><table><thead><tr><th>Run</th><th>Executed At</th><th>Total</th><th>Passed</th><th>Failed</th><th>Broken</th><th>Skipped</th><th>Duration</th><th>Browsers</th></tr></thead><tbody>{historyRows}</tbody></table></div></section>");
            html.AppendLine($"<section id='details' class='section'><div class='section-header'><div><h2>Test Details</h2><p>Per-test browser, suite, status, priority, timing, steps, and failure evidence.</p></div></div><div class='panel'><table><thead><tr><th>Start</th><th>End</th><th>Suite</th><th>Test</th><th>Browser</th><th>Priority</th><th>Status</th><th>Duration</th><th>Steps</th><th>Error</th><th>Attachment</th></tr></thead><tbody>{testRows}</tbody></table></div></section>");
            html.AppendLine("</div></body></html>");

            File.WriteAllText(reportPath, html.ToString());
            return reportPath;
        }
    }

    private static string DeriveReportTitle(RunSummary summary)
    {
        var distinctTestNames = TestExecutionRecords.Select(r => r.TestName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctTestNames.Count == 1)
        {
            return $"{distinctTestNames[0]} Execution Summary";
        }

        var distinctClassNames = TestExecutionRecords.Select(r => r.ClassName).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctClassNames.Count == 1)
        {
            return $"{distinctClassNames[0]} Execution Summary";
        }

        return $"Automation Execution Summary - {summary.RunEndedAt:yyyy-MM-dd HH:mm}";
    }

    private static string GetRunBrowserTag()
    {
        var browsers = TestExecutionRecords
            .Select(record => record.Browser)
            .Where(browser => !string.IsNullOrWhiteSpace(browser))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (browsers.Count == 0)
        {
            return "unknown";
        }

        if (browsers.Count == 1)
        {
            return SanitizeForFileName(browsers[0].ToLowerInvariant());
        }

        return "mixed";
    }

    private static string SanitizeForFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized.Replace(' ', '-');
    }

    private static bool IsExecutionHistoryEnabled()
    {
        return ExecutionHistoryEnabled.Value;
    }

    private static bool ResolveExecutionHistoryEnabled()
    {
        var envValue = Environment.GetEnvironmentVariable("Reporting__EnableExecutionHistory")
            ?? Environment.GetEnvironmentVariable("TestSettings__EnableExecutionHistory");

        if (bool.TryParse(envValue, out var parsedFromEnv))
        {
            return parsedFromEnv;
        }

        var configValue = ReadBooleanFromAppSettings("Reporting", "EnableExecutionHistory");
        if (configValue.HasValue)
        {
            return configValue.Value;
        }

        return true;
    }

    private static bool? ReadBooleanFromAppSettings(string section, string key)
    {
        var reportsDirectory = GetReportsDirectory();
        var solutionDirectory = Directory.GetParent(reportsDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(solutionDirectory))
        {
            return null;
        }

        var appSettingsPath = Path.Combine(solutionDirectory, "config", "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty(section, out var sectionElement))
            {
                return null;
            }

            if (!sectionElement.TryGetProperty(key, out var keyElement))
            {
                return null;
            }

            if (keyElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (keyElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (keyElement.ValueKind == JsonValueKind.String && bool.TryParse(keyElement.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static RunSummary BuildRunSummary(IReadOnlyList<TestExecutionRecord> records)
    {
        if (records.Count == 0)
        {
            return new RunSummary(
                RunTimestamp,
                RunStartedAt,
                DateTimeOffset.Now,
                TimeSpan.Zero,
                0,
                0,
                0,
                0,
                0,
                "N/A",
                "Unknown",
                "None",
                0,
                []);
        }

        var suites = records
            .GroupBy(record => record.SuiteName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var suiteRecords = group.ToList();
                return new SuiteSummary(
                    group.Key,
                    suiteRecords.Count,
                    suiteRecords.Count(record => record.DisplayStatus == "Passed"),
                    suiteRecords.Count(record => record.DisplayStatus == "Failed"),
                    suiteRecords.Count(record => record.DisplayStatus == "Broken"),
                    suiteRecords.Count(record => record.DisplayStatus == "Skipped"),
                    TimeSpan.FromTicks(suiteRecords.Sum(record => record.Duration.Ticks)));
            })
            .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var runStartedAt = records.Min(record => record.StartedAt);
        var runEndedAt = records.Max(record => record.FinishedAt);
        var total = records.Count;
        var passed = records.Count(record => record.DisplayStatus == "Passed");
        var failed = records.Count(record => record.DisplayStatus == "Failed");
        var broken = records.Count(record => record.DisplayStatus == "Broken");
        var skipped = records.Count(record => record.DisplayStatus == "Skipped");
        var passRate = total == 0 ? 0 : (double)passed / total;

        return new RunSummary(
            RunTimestamp,
            runStartedAt,
            runEndedAt,
            runEndedAt - runStartedAt,
            total,
            passed,
            failed,
            broken,
            skipped,
            string.Join(", ", records.Select(record => record.Browser).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join(", ", records.Select(record => record.TestType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join(", ", suites.Select(suite => suite.Name)),
            passRate,
            suites);
    }

    private static IReadOnlyList<HistoricalRunSummary> UpsertRunHistory(string reportDirectory, RunSummary summary)
    {
        var historyPath = Path.Combine(reportDirectory, "execution-history.json");
        var history = LoadRunHistory(historyPath)
            .Where(entry => !string.Equals(entry.RunId, summary.RunId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        history.Add(new HistoricalRunSummary(
            summary.RunId,
            summary.RunEndedAt,
            summary.Total,
            summary.Passed,
            summary.Failed,
            summary.Broken,
            summary.Skipped,
            summary.RunDuration.TotalSeconds,
            summary.BrowsersSummary,
            summary.Suites.Select(suite => suite.Name).ToArray()));

        var orderedHistory = history
            .OrderByDescending(entry => entry.ExecutedAt)
            .Take(20)
            .ToList();

        File.WriteAllText(historyPath, JsonSerializer.Serialize(orderedHistory, new JsonSerializerOptions { WriteIndented = true }));
        return orderedHistory;
    }

    private static IReadOnlyList<HistoricalRunSummary> DeleteHistoryAndReturnEmpty(string reportDirectory)
    {
        var historyPath = Path.Combine(reportDirectory, "execution-history.json");
        if (File.Exists(historyPath))
        {
            File.Delete(historyPath);
        }

        return [];
    }

    private static HistoricalRunSummary? GetPreviousRunSummary(string reportDirectory)
    {
        var historyPath = Path.Combine(reportDirectory, "execution-history.json");
        return LoadRunHistory(historyPath)
            .Where(entry => !string.Equals(entry.RunId, RunTimestamp, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.ExecutedAt)
            .FirstOrDefault();
    }

    private static IReadOnlyList<HistoricalRunSummary> LoadRunHistory(string historyPath)
    {
        if (!File.Exists(historyPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(historyPath);
            return JsonSerializer.Deserialize<List<HistoricalRunSummary>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildMetricCard(string label, int value, string cssClass, string trendText)
    {
        return $"<article class='{cssClass}'><div class='label'>{WebUtility.HtmlEncode(label)}</div><div class='value'>{value}</div><div class='trend'>{WebUtility.HtmlEncode(trendText)}</div></article>";
    }

    private static string BuildSuiteRow(SuiteSummary summary)
    {
        var total = Math.Max(summary.Total, 1);
        var passWidth = summary.Passed * 100d / total;
        var failWidth = summary.Failed * 100d / total;
        var brokenWidth = summary.Broken * 100d / total;
        var skipWidth = summary.Skipped * 100d / total;

        return $"<tr><td>{WebUtility.HtmlEncode(summary.Name)}</td><td>{summary.Total}</td><td>{summary.Passed}</td><td>{summary.Failed}</td><td>{summary.Broken}</td><td>{summary.Skipped}</td><td>{FormatDuration(summary.Duration)}</td><td><div class='suite-bar'><span class='suite-pass' style='width:{passWidth:F2}%'></span><span class='suite-fail' style='width:{failWidth:F2}%'></span><span class='suite-broken' style='width:{brokenWidth:F2}%'></span><span class='suite-skip' style='width:{skipWidth:F2}%'></span></div></td></tr>";
    }

    private static string BuildHistoryRow(HistoricalRunSummary summary)
    {
        return $"<tr><td>{WebUtility.HtmlEncode(summary.RunId)}</td><td>{summary.ExecutedAt:yyyy-MM-dd HH:mm:ss}</td><td>{summary.Total}</td><td>{summary.Passed}</td><td>{summary.Failed}</td><td>{summary.Broken}</td><td>{summary.Skipped}</td><td>{FormatDuration(TimeSpan.FromSeconds(summary.DurationSeconds))}</td><td>{WebUtility.HtmlEncode(summary.Browsers)}</td></tr>";
    }

    private static string BuildEmptyHistoryRow(string message)
    {
        return $"<tr><td colspan='9' class='muted'>{WebUtility.HtmlEncode(message)}</td></tr>";
    }

    private static string BuildTestRow(TestExecutionRecord record)
    {
        var errorText = string.IsNullOrWhiteSpace(record.ErrorMessage)
            ? "<span class='muted'>None</span>"
            : WebUtility.HtmlEncode(record.ErrorMessage);
        var stepsText = record.StepLogs.Count == 0
            ? "<span class='muted'>No steps recorded</span>"
            : $"<details><summary>View {record.StepLogs.Count} step(s)</summary><ol>{string.Join(string.Empty, record.StepLogs.Select(step => $"<li>{WebUtility.HtmlEncode(step)}</li>"))}</ol></details>";
        var attachmentText = string.IsNullOrWhiteSpace(record.ScreenshotPath)
            ? "<span class='muted'>None</span>"
            : $"<a href='{WebUtility.HtmlEncode(record.ScreenshotPath)}'>Screenshot</a>";

        return $"<tr><td>{record.StartedAt:yyyy-MM-dd HH:mm:ss}</td><td>{record.FinishedAt:yyyy-MM-dd HH:mm:ss}</td><td>{WebUtility.HtmlEncode(record.SuiteName)}</td><td><strong>{WebUtility.HtmlEncode(record.TestName)}</strong><div class='muted'>{WebUtility.HtmlEncode(record.ClassName)}</div></td><td>{WebUtility.HtmlEncode(record.Browser)}</td><td>{BuildPriorityPill(record.Priority)}</td><td>{BuildStatusPill(record.DisplayStatus)}</td><td>{FormatDuration(record.Duration)}</td><td>{stepsText}</td><td>{errorText}</td><td>{attachmentText}</td></tr>";
    }

    private static string BuildPriorityPill(string priority)
    {
        var cssClass = priority switch
        {
            "High" => "priority-pill priority-high",
            "Medium" => "priority-pill priority-medium",
            "Low" => "priority-pill priority-low",
            _ => "priority-pill priority-unspecified"
        };

        return $"<span class='{cssClass}'>{WebUtility.HtmlEncode(priority)}</span>";
    }

    private static string BuildStatusPill(string status)
    {
        var cssClass = status switch
        {
            "Passed" => "status-pill status-passed",
            "Failed" => "status-pill status-failed",
            "Broken" => "status-pill status-broken",
            _ => "status-pill status-skipped"
        };

        return $"<span class='{cssClass}'>{WebUtility.HtmlEncode(status)}</span>";
    }

    private static string BuildTrendText(int current, int? previous)
    {
        if (!previous.HasValue)
        {
            return "First tracked run";
        }

        var delta = current - previous.Value;
        if (delta == 0)
        {
            return "No change vs previous run";
        }

        return $"{(delta > 0 ? "+" : string.Empty)}{delta} vs previous run";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{duration.TotalSeconds:F2}s";
    }

    private static string NormalizeStatus(string status, string? errorMessage)
    {
        if (status.Equals("Passed", StringComparison.OrdinalIgnoreCase))
        {
            return "Passed";
        }

        if (status.Equals("Skipped", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Ignored", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
        {
            return "Skipped";
        }

        if (status.Equals("Inconclusive", StringComparison.OrdinalIgnoreCase))
        {
            return "Broken";
        }

        if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return LooksLikeBrokenFailure(errorMessage) ? "Broken" : "Failed";
        }

        return status;
    }

    private static bool LooksLikeBrokenFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        var assertionIndicators = new[]
        {
            "assert",
            "expected:",
            "actual:",
            "fluentassertions",
            "should().",
            "AssertionException"
        };

        return !assertionIndicators.Any(indicator => errorMessage.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record TestExecutionRecord(
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        string TestName,
        string ClassName,
        string SuiteName,
        string Status,
        TimeSpan Duration,
        string Browser,
        string TestType,
        string Priority,
        IReadOnlyList<string> StepLogs,
        string? ErrorMessage,
        string? ScreenshotPath)
    {
        public string DisplayStatus => NormalizeStatus(Status, ErrorMessage);
    }

    private sealed record SuiteSummary(
        string Name,
        int Total,
        int Passed,
        int Failed,
        int Broken,
        int Skipped,
        TimeSpan Duration);

    private sealed record RunSummary(
        string RunId,
        DateTimeOffset RunStartedAt,
        DateTimeOffset RunEndedAt,
        TimeSpan RunDuration,
        int Total,
        int Passed,
        int Failed,
        int Broken,
        int Skipped,
        string BrowsersSummary,
        string TestTypesSummary,
        string SuitesSummary,
        double PassRate,
        IReadOnlyList<SuiteSummary> Suites);

    private sealed record HistoricalRunSummary(
        string RunId,
        DateTimeOffset ExecutedAt,
        int Total,
        int Passed,
        int Failed,
        int Broken,
        int Skipped,
        double DurationSeconds,
        string Browsers,
        string[] Suites);
}
