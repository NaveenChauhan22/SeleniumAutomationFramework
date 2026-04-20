# Role-Based Auth Integration Guide (Implemented)

## Purpose
This document describes the implemented role-specific login and execution model.

See also:
- [API Auth Test Suite Brief](ApiAuthTestSuiteBrief.md) for the current positive/negative auth execution model used by APITests.

## Current State Analysis
- API auth now uses role-based credential resolution from `roles` in `resources/testdata/loginData.json`.
- Session token and credentials are stored in `ApiSessionContext`.
- Positive flows now reuse a suite-level token (single login in `OneTimeSetUp`).
- `SetUp` refreshes that suite token only when expired via `EnsureSuiteTokenAsync()`.
- Negative scenarios are test-driven through `LoginAsync(..., tokenScenario, tokenState)` and can disable refresh intent.
- This supports positive/negative token scenarios with role-aware credential resolution.

## Implementation Status 
- Added `roles` map in `resources/testdata/loginData.json` with default keys: `admin`, `user`, `organizer`, `viewer`.
- Introduced provider/resolver components in `src/Framework.Data/RoleCredentialProvider.cs`:
  - `RoleCredentialProvider`
  - `RoleCredentialResolver`
  - `RoleCredentials`
- API and UI test bases now resolve credentials by role through the provider/resolver path.
- Backward compatibility preserved:
  - Existing tests can keep class-level role declarations.
  - Method-level role declarations override class-level declarations.
  - `TEST_EXECUTION_ROLE` can be used as runtime fallback for tests without attributes.

### Role Resolution Order (Now Implemented)
Execution role selection:
1. Method-level `TestRoleAttribute`
2. Class-level `TestRoleAttribute`
3. `TEST_EXECUTION_ROLE` environment variable
4. Fail fast if none is specified for auth-dependent tests

Credentials for the selected role:
1. Role-specific environment variables: `TEST_{ROLE}_EMAIL` and `TEST_{ROLE}_PASSWORD`
2. `roles.{role}` values from `loginData.json`
3. Fail fast for unknown role or incomplete credentials

### Role-Friendly Test Declaration (Now Implemented)
- Tests can be explicitly role-scoped via class or method attributes.
- Tests can be role-agnostic and use `TEST_EXECUTION_ROLE` as runtime fallback.
- Tests can declare a role at class or method level with `TestRoleAttribute`.
- Method-level role declarations override class-level declarations.
- API positive auth setup now caches tokens per role, so `admin`, `organizer`, and `viewer` tests can coexist in the same fixture without sharing the wrong token.

```csharp
[TestRole("admin")]
public class AdminEventsTests : APITestBase
{
  [Test]
  public async Task AdminCanCreateEvent()
  {
    EnsurePositiveAuthContextOrInconclusive("Events");
    // Test body stays role-agnostic.
  }
}

public class MixedRoleUiTests : BaseTest
{
  [Test]
  [TestRole("viewer")]
  public void ViewerCanLogIn()
  {
    var loginData = LoadLoginData();
    LoginAsCurrentRole(loginData);
  }
}
```

## Runtime Execution Notes

- Existing tests are now split across three strategies:
  - user-only tests (`[TestRole("user")]`)
  - admin-only tests (`[TestRole("admin")]`)
  - both-role tests via NUnit parameterization and explicit `LoginAsRole("user"|"admin")`
- Missing or invalid role credentials fail with clear setup/login errors.
- Role execution applies on both Windows and macOS shells.

See [ExecutionGuide.md](./ExecutionGuide.md#4a-role-based-execution) for command examples.

## Current Architecture
Keep the same lightweight architecture and expand role-specific assertions as needed.

### Proposed Components
- `RoleCredentialsModel`
  - Represents a single role's credentials and optional metadata.
- `RoleCredentialProvider`
  - Loads role map from test data (JSON) with environment variable overrides.
- `RoleCredentialResolver`
  - Resolves credentials by role key (`admin`, `user`, `organizer`, `viewer`).
- `AuthClient.LoginAsync(...)`
  - Reused as-is for role-specific login; no special per-role auth path required.

## Test Data Structure
`roles` is implemented in `resources/testdata/loginData.json`.

```json
{
  "roles": {
    "admin": {
      "email": "${TEST_ADMIN_EMAIL:-}",
      "password": "${TEST_ADMIN_PASSWORD:-}"
    },
    "user": {
      "email": "${TEST_USER_EMAIL:-}",
      "password": "${TEST_USER_PASSWORD:-}"
    },
    "organizer": {
      "email": "${TEST_ORGANIZER_EMAIL:-}",
      "password": "${TEST_ORGANIZER_PASSWORD:-}"
    },
    "viewer": {
      "email": "${TEST_VIEWER_EMAIL:-}",
      "password": "${TEST_VIEWER_PASSWORD:-}"
    }
  }
}
```

## Environment Variable Strategy
Use environment variables as the source of truth for secrets.

Recommended keys:
- `TEST_ADMIN_EMAIL`
- `TEST_ADMIN_PASSWORD`
- `TEST_USER_EMAIL`
- `TEST_USER_PASSWORD`
- `TEST_ORGANIZER_EMAIL`
- `TEST_ORGANIZER_PASSWORD`
- `TEST_VIEWER_EMAIL`
- `TEST_VIEWER_PASSWORD`

Fallback behavior:
1. Placeholder resolution is non-blocking for all roles (empty default via `:-`).
2. Role-specific env vars override JSON when present.
3. Credentials are validated lazily when a role is actually resolved for a test.
4. If that role has missing/empty credentials, fail fast with a clear role-specific message.

## How Role-Specific Tests Look
Current usage pattern in tests:

```csharp
var credentials = RoleCredentialResolver.Resolve("organizer");
await SharedAuthClient.LoginAsync(
    credentials.Email,
    credentials.Password,
    tokenScenario: "valid",
    tokenState: true,
    cancellationToken: CancellationToken.None);
```

Negative auth role scenario:

```csharp
var credentials = RoleCredentialResolver.Resolve("viewer");
await SharedAuthClient.LoginAsync(
    credentials.Email,
    "wrong-password",
    tokenScenario: "invalid",
    tokenState: false,
    cancellationToken: CancellationToken.None);
```

## Session Handling Considerations for Roles
Because `ApiSessionContext` stores one active token/credential set per async context, multi-role tests should follow one of these patterns:
- Single-role-per-test: preferred and simplest.
- Sequential role switch in same test:
  - Authenticate role A
  - Execute assertions
  - Re-authenticate role B
  - Execute assertions
- Avoid sharing one async context across parallel role workflows in the same test method.

## Migration Status
Completed:
1. Added `roles` object to `loginData.json` and set `roles.user` as default credentials.
2. Introduced `RoleCredentialResolver` utility.
3. Kept existing test contracts stable.
4. Added role-focused tests and role-aware test helpers.

## Backward Compatibility
- Existing suite setup and token contracts remain unchanged.
- Role-specific behavior is additive through attributes and runtime env variable selection.
- The current `LoginAsync(...)` contract remains compatible with role login because it is credential-input based.

## Recommended Guardrails
- Validate supported role keys centrally and fail fast for unknown roles.
- Do not log raw passwords or bearer tokens.
- Keep role names normalized (lowercase invariant) in resolver.
- Keep environment variable names stable and documented.

## Why This Fits Current Refactor
- No extra architectural layer required.
- Reuses existing `AuthClient` and `ApiSessionContext` behavior.
- Maintains plug-and-play flexibility and externalized secrets.
- Supports positive and negative role scenarios using the same common auth API.

---

## Current Implementation Snapshot

- Positive flows use suite-level login per role and reuse cached tokens.
- `SetUp` calls `EnsureSuiteTokenAsync()` and refreshes only the resolved role token when expired.
- Refresh is lock-protected (`_suiteLock`) to avoid concurrent refresh storms.
- Negative scenarios still call `LoginAsync(..., tokenScenario, false)` explicitly from tests.
- Negative tokens for `expired` and `invalid` are stamped with `AllowRefresh = false`.

### Quick Decision Matrix

| Flow type | Test setup | Token expected in context | `AllowRefresh` | Refresh behavior | Typical API result |
|---|---|---|---|---|---|
| Positive happy path | Suite `OneTimeSetUp` login with `(tokenScenario: "valid", tokenState: true)` for current role | Yes (real token) | `true` | Reused across tests for that role; refreshed only if expired | `2xx` |
| Positive with expired suite token | No test change; handled in `EnsureSuiteTokenAsync()` for current role | Yes (fresh token after refresh) | `true` | Single refresh under `_suiteLock` | `2xx` |
| Negative expired token | `LoginAsync(..., tokenScenario: "expired", tokenState: false)` | Yes (forced expired) | `false` | Never auto-refreshed | `401/4xx` |
| Negative invalid token | `LoginAsync(..., tokenScenario: "invalid", tokenState: false)` | Yes (broken token string) | `false` | Never auto-refreshed | `401/4xx` |
| Negative missing token | `LoginAsync(..., tokenScenario: "missing", tokenState: false)` | No | N/A | No token to refresh | `401/4xx` |
| Invalid test config | `LoginAsync(..., non-valid, true)` | N/A | N/A | Throws before request | `InvalidOperationException` |

### Practical Note

Use the planned role resolver to choose credentials, then apply the same matrix rules for token scenario behavior. In other words, roles decide "who" logs in; token scenario decides "how auth should behave" in that test.
