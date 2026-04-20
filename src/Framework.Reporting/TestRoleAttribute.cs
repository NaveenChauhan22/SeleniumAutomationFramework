using NUnit.Framework;

namespace Framework.Reporting;

/// <summary>
/// Declares the execution role for a test class or test method.
/// Method-level usage overrides class-level usage.
/// If no attribute is present, the framework defaults to the <c>user</c> role.
/// <para>
/// Because this attribute extends <see cref="CategoryAttribute"/>, the role name is
/// registered as an NUnit category, enabling role-based test filtering:
/// <code>dotnet test --filter "Category=admin&amp;Category=Smoke"</code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class TestRoleAttribute : CategoryAttribute
{
    /// <summary>The normalised role name (lower-case, trimmed).</summary>
    public string Role => Name;

    public TestRoleAttribute(string role) : base(Normalize(role))
    {
    }

    private static string Normalize(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role name is required.", nameof(role));
        }

        return role.Trim().ToLowerInvariant();
    }
}