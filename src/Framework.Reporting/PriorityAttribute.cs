namespace Framework.Reporting;

/// <summary>Defines the severity tiers that can be assigned to a test case.</summary>
public enum TestPriority
{
    High,
    Medium,
    Low
}

/// <summary>
/// Marks a test method or test class with a <see cref="TestPriority"/> level.
/// <see cref="AllureTestBase"/> reads this attribute to set the Allure severity label and
/// a human-readable <c>priority</c> label on the test node.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PriorityAttribute : Attribute
{
    public TestPriority Level { get; }

    public PriorityAttribute(TestPriority level)
    {
        Level = level;
    }
}