namespace Framework.Data;

/// <summary>
/// Represents resolved credentials for a named test role.
/// </summary>
/// <param name="Email">Role email/username.</param>
/// <param name="Password">Role password.</param>
public sealed record RoleCredentials(string Email, string Password);

/// <summary>
/// Provides role-based credentials with environment variable overrides.
/// </summary>
public sealed class RoleCredentialProvider
{
    private readonly IReadOnlyDictionary<string, RoleCredentials> _roles;
    private readonly Func<string, string?> _environmentValueProvider;

    private RoleCredentialProvider(
        IReadOnlyDictionary<string, RoleCredentials> roles,
        Func<string, string?> environmentValueProvider)
    {
        _roles = roles;
        _environmentValueProvider = environmentValueProvider;
    }

    /// <summary>
    /// Builds a provider from role map.
    /// </summary>
    /// <param name="roles">Role map, keyed by role name.</param>
    /// <param name="environmentValueProvider">Optional environment value accessor for testability.</param>
    /// <returns>Configured provider instance.</returns>
    public static RoleCredentialProvider Create(
        IDictionary<string, RoleCredentials> roles,
        Func<string, string?>? environmentValueProvider = null)
    {
        var normalized = new Dictionary<string, RoleCredentials>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            var key = NormalizeRole(role.Key);
            normalized[key] = role.Value;
        }

        return new RoleCredentialProvider(
            normalized,
            environmentValueProvider ?? Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Resolves credentials for the provided role.
    /// Credentials are resolved in order: environment variables (TEST_ROLE_EMAIL/PASSWORD) → JSON config → error if missing.
    /// </summary>
    /// <param name="role">Role key (for example: admin, user, organizer, viewer).</param>
    /// <returns>Resolved credentials.</returns>
    /// <exception cref="InvalidOperationException">Thrown for unknown roles, missing environment configuration, or incomplete credentials.</exception>
    public RoleCredentials Resolve(string role)
    {
        var normalizedRole = NormalizeRole(role);

        var overridden = TryGetEnvironmentOverride(normalizedRole);
        if (overridden is not null)
        {
            return overridden;
        }

        if (_roles.TryGetValue(normalizedRole, out var roleCredentials))
        {
            ValidateCredentials(roleCredentials, $"roles.{normalizedRole}", normalizedRole);
            return roleCredentials;
        }

        throw new InvalidOperationException(
            $"Unknown role '{role}'. Supported roles: {string.Join(", ", GetSupportedRoles())}.");
    }

    /// <summary>
    /// Returns supported roles currently known by the provider.
    /// </summary>
    /// <returns>Role names in lowercase.</returns>
    public IReadOnlyCollection<string> GetSupportedRoles()
    {
        return _roles.Keys
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private RoleCredentials? TryGetEnvironmentOverride(string normalizedRole)
    {
        var prefix = normalizedRole.ToUpperInvariant();
        var email = _environmentValueProvider($"TEST_{prefix}_EMAIL");
        var password = _environmentValueProvider($"TEST_{prefix}_PASSWORD");

        var emailHasValue = !string.IsNullOrWhiteSpace(email);
        var passwordHasValue = !string.IsNullOrWhiteSpace(password);

        if (!emailHasValue && !passwordHasValue)
        {
            return null;
        }

        if (!emailHasValue || !passwordHasValue)
        {
            throw new InvalidOperationException(
                $"Environment override for role '{normalizedRole}' is incomplete. " +
                $"Both TEST_{prefix}_EMAIL and TEST_{prefix}_PASSWORD must be set.");
        }

        return new RoleCredentials(email!, password!);
    }

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new InvalidOperationException("Role name is required.");
        }

        return role.Trim().ToLowerInvariant();
    }

    private static void ValidateCredentials(RoleCredentials credentials, string source, string roleName)
    {
        var emailEmpty = string.IsNullOrWhiteSpace(credentials.Email);
        var passwordEmpty = string.IsNullOrWhiteSpace(credentials.Password);

        if (emailEmpty || passwordEmpty)
        {
            var envPrefix = roleName.ToUpperInvariant();
            throw new InvalidOperationException(
                $"Role '{roleName}' credentials are not configured. "
                + $"Set TEST_{envPrefix}_EMAIL and TEST_{envPrefix}_PASSWORD environment variables, "
                + $"or configure '{source}' in your test data JSON file with non-empty values.");
        }
    }
}

/// <summary>
/// Resolves role credentials through a configured provider.
/// </summary>
public static class RoleCredentialResolver
{
    /// <summary>
    /// Resolves credentials for the provided role.
    /// </summary>
    /// <param name="role">Role key to resolve.</param>
    /// <param name="provider">Credential provider instance.</param>
    /// <returns>Resolved role credentials.</returns>
    public static RoleCredentials Resolve(string role, RoleCredentialProvider provider)
    {
        if (provider is null)
        {
            throw new InvalidOperationException("Role credential provider must be initialized before resolving credentials.");
        }

        return provider.Resolve(role);
    }
}
