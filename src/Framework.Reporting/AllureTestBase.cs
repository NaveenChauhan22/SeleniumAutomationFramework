using System.Reflection;
using Allure.Net.Commons;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Framework.Reporting;

/// <summary>
/// Abstract base class that integrates NUnit tests with the Allure reporting framework.
/// Call <see cref="BeginAllureTest"/> in <c>[SetUp]</c> and <see cref="CompleteAllureTest"/> in
/// <c>[TearDown]</c>. Automatically adds browser/test-type/duration parameters, attaches
/// failure screenshots and API exchange logs, and enriches the Allure test node with priority,
/// suite hierarchy, and feature/story/epic labels derived from attributes on the test class or
/// method.
/// </summary>
public abstract class AllureTestBase
{
    private readonly AsyncLocal<DateTimeOffset?> _testStart = new();

    protected void BeginAllureTest()
    {
        _testStart.Value = DateTimeOffset.UtcNow;
        ApplyMetadata();
    }

    protected void CompleteAllureTest(IEnumerable<AllureAttachment>? failureAttachments = null)
    {
        try
        {
            var duration = DateTimeOffset.UtcNow - (_testStart.Value ?? DateTimeOffset.UtcNow);

            AllureApi.AddTestParameter("Browser", RuntimeContext.BrowserName);
            AllureApi.AddTestParameter("TestType", RuntimeContext.TestType);
            AllureApi.AddTestParameter("Duration", $"{duration.TotalSeconds:F2}s");

            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                foreach (var attachment in failureAttachments ?? [])
                {
                    Attach(attachment);
                }

                var apiExchange = RuntimeContext.GetLastApiExchange();
                if (apiExchange is not null)
                {
                    ReportHelper.AttachContent("API Request", "text/plain", apiExchange.Request, "txt");
                    ReportHelper.AttachContent("API Response", "text/plain", apiExchange.Response, "txt");
                }
            }
        }
        finally
        {
            RuntimeContext.ClearTestScope();
        }
    }

    protected void Step(string name, Action action)
    {
        AllureApi.Step(name, action);
    }

    protected Task StepAsync(string name, Func<Task> action)
    {
        return AllureApi.Step(name, action);
    }

    protected TestPriority? GetCurrentPriorityLevel()
    {
        var method = ResolveTestMethod();
        return method?.GetCustomAttribute<PriorityAttribute>(true)?.Level
            ?? GetType().GetCustomAttribute<PriorityAttribute>(true)?.Level;
    }

    protected string GetCurrentSuiteName()
    {
        var method = ResolveTestMethod();
        return GetNamedAttributeValue(method, GetType(), "AllureSuiteAttribute")
            ?? GetType().Name;
    }

    protected sealed record AllureAttachment(string Name, string ContentType, string? FilePath = null, string? Content = null, string FileExtension = "txt");

    private void ApplyMetadata()
    {
        var method = ResolveTestMethod();
        var testClass = GetType();

        ApplyPriority(method, testClass);
        ApplyLabels(method, testClass);
        ApplySuiteHierarchy(method, testClass);

        AllureApi.AddLabel("browser", RuntimeContext.BrowserName);
        AllureApi.AddLabel("testType", RuntimeContext.TestType);
    }

    private static void ApplyPriority(MethodInfo? method, MemberInfo testClass)
    {
        var priority = method?.GetCustomAttribute<PriorityAttribute>(true)
            ?? testClass.GetCustomAttribute<PriorityAttribute>(true);

        if (priority is null)
        {
            return;
        }

        switch (priority.Level)
        {
            case TestPriority.High:
                AllureApi.SetSeverity(SeverityLevel.critical);
                AllureApi.AddLabel("priority", "High");
                break;
            case TestPriority.Medium:
                AllureApi.SetSeverity(SeverityLevel.normal);
                AllureApi.AddLabel("priority", "Medium");
                break;
            default:
                AllureApi.SetSeverity(SeverityLevel.minor);
                AllureApi.AddLabel("priority", "Low");
                break;
        }
    }

    private static void ApplySuiteHierarchy(MethodInfo? method, Type testClass)
    {
        var parentSuiteApplied = HasNamedAttribute(method, testClass, "AllureParentSuiteAttribute");
        var suiteApplied = HasNamedAttribute(method, testClass, "AllureSuiteAttribute");
        var subSuiteApplied = HasNamedAttribute(method, testClass, "AllureSubSuiteAttribute");

        if (!parentSuiteApplied)
        {
            AllureApi.AddParentSuite(testClass.Assembly.GetName().Name ?? "Test Assembly");
        }

        if (!suiteApplied)
        {
            AllureApi.AddSuite(testClass.Name);
        }

        if (!subSuiteApplied && !string.IsNullOrWhiteSpace(testClass.Namespace))
        {
            AllureApi.AddSubSuite(testClass.Namespace!);
        }
    }

    private static void ApplyLabels(MethodInfo? method, Type testClass)
    {
        if (UsesNativeNUnitAllureAttributes())
        {
            return;
        }

        ApplyNamedAttributes(method, testClass, "AllureFeatureAttribute", AllureApi.AddFeature);
        ApplyNamedAttributes(method, testClass, "AllureStoryAttribute", AllureApi.AddStory);
        ApplyNamedAttributes(method, testClass, "AllureEpicAttribute", AllureApi.AddEpic);
    }

    private static bool HasNamedAttribute(MethodInfo? method, Type testClass, string attributeName)
    {
        return GetNamedAttributeValues(method, testClass, attributeName).Any();
    }

    private static bool ApplyNamedAttributes(MethodInfo? method, Type testClass, string attributeName, Action<string> apply)
    {
        var applied = false;

        foreach (var value in GetNamedAttributeValues(method, testClass, attributeName))
        {
            apply(value);
            applied = true;
        }

        return applied;
    }

    private static string? GetNamedAttributeValue(MethodInfo? method, Type testClass, string attributeName)
    {
        return GetNamedAttributeValues(method, testClass, attributeName).FirstOrDefault();
    }

    private static IEnumerable<string> GetNamedAttributeValues(MethodInfo? method, Type testClass, string attributeName)
    {
        foreach (var attribute in GetAttributeData(testClass, method))
        {
            if (!string.Equals(attribute.AttributeType.Name, attributeName, StringComparison.Ordinal))
            {
                continue;
            }

            var constructorValue = attribute.ConstructorArguments
                .FirstOrDefault(argument => argument.ArgumentType == typeof(string))
                .Value as string;

            if (!string.IsNullOrWhiteSpace(constructorValue))
            {
                yield return constructorValue;
                continue;
            }

            var namedValue = attribute.NamedArguments
                .FirstOrDefault(argument => argument.TypedValue.ArgumentType == typeof(string))
                .TypedValue.Value as string;

            if (!string.IsNullOrWhiteSpace(namedValue))
            {
                yield return namedValue;
            }
        }
    }

    private static IEnumerable<CustomAttributeData> GetAttributeData(Type testClass, MethodInfo? method)
    {
        foreach (var attribute in CustomAttributeData.GetCustomAttributes(testClass))
        {
            yield return attribute;
        }

        if (method is null)
        {
            yield break;
        }

        foreach (var attribute in CustomAttributeData.GetCustomAttributes(method))
        {
            yield return attribute;
        }
    }

    private MethodInfo? ResolveTestMethod()
    {
        var methodName = TestContext.CurrentContext.Test.MethodName;
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return GetType().GetMethods(flags)
            .FirstOrDefault(method => string.Equals(method.Name, methodName, StringComparison.Ordinal));
    }

    private static void Attach(AllureAttachment attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.FilePath))
        {
            ReportHelper.AttachFile(attachment.Name, attachment.FilePath, attachment.ContentType);
            return;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Content))
        {
            ReportHelper.AttachContent(attachment.Name, attachment.ContentType, attachment.Content, attachment.FileExtension);
        }
    }

    private static bool UsesNativeNUnitAllureAttributes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => string.Equals(assembly.GetName().Name, "Allure.NUnit", StringComparison.OrdinalIgnoreCase));
    }
}