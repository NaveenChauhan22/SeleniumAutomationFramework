using NUnit.Framework.Interfaces;
using Framework.Core.Utilities;
using Framework.Reporting;
using System.Diagnostics;

namespace APITests;

public abstract class ApiTestBase : AllureTestBase
{
    protected Serilog.ILogger Logger = Serilog.Log.Logger;
    private IDisposable? _executionLoggerHandle;
    private readonly Stopwatch _executionTimer = new();
    private DateTimeOffset _testStartedAt;
    private string _priority = "Unspecified";
    private string _suiteName = string.Empty;
    [SetUp]
    public void SetUp()
    {
        _testStartedAt = DateTimeOffset.Now;
        _executionTimer.Restart();
        _priority = GetCurrentPriorityLevel()?.ToString() ?? "Unspecified";
        _suiteName = GetCurrentSuiteName();

        var executionLogger = TestLogger.CreateExecutionLogger("API", _suiteName, TestContext.CurrentContext.Test.Name);
        Logger = executionLogger.ForContext<ApiTestBase>();
        Serilog.Log.Logger = executionLogger;
        _executionLoggerHandle = executionLogger as IDisposable;

        Logger.Information("[API] Starting test {TestName}", TestContext.CurrentContext.Test.Name);
        RuntimeContext.TestType = "API";
        RuntimeContext.BrowserName = "N/A";
        BeginAllureTest();
        ReportHelper.BeginTest(TestContext.CurrentContext.Test.Name);
    }

    [TearDown]
    public void TearDown()
    {
        var outcome = TestContext.CurrentContext.Result.Outcome.Status.ToString();
        var errorMessage = TestContext.CurrentContext.Result.Message;
        _executionTimer.Stop();
        var finishedAt = DateTimeOffset.Now;
        CompleteAllureTest();
        Logger.Information("[API] Completing test {TestName} with status {Status}", TestContext.CurrentContext.Test.Name, outcome);

        var fullClassName = TestContext.CurrentContext.Test.ClassName ?? string.Empty;
        var shortClassName = fullClassName.Contains('.') ? fullClassName[(fullClassName.LastIndexOf('.') + 1)..] : fullClassName;

        ReportHelper.RecordTestResult(
            TestContext.CurrentContext.Test.Name,
            shortClassName,
            _suiteName,
            outcome,
            _executionTimer.Elapsed,
            "N/A",
            "API",
            _priority,
            _testStartedAt,
            finishedAt,
            errorMessage,
            null);

        // Report is generated once per test run in global teardown (AllureBootstrap.FinalizeRun)

        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            Logger.Warning("[API] Test {TestName} failed with message: {Message}",
                TestContext.CurrentContext.Test.Name,
                errorMessage);
        }

        Logger.Information("[API] Test {TestName} finished in {DurationMs} ms",
            TestContext.CurrentContext.Test.Name,
            _executionTimer.Elapsed.TotalMilliseconds);

        _executionLoggerHandle?.Dispose();
        _executionLoggerHandle = null;
    }
}