# Role-Based Auth Integration Guide (Planned + Current Snapshot)

## Purpose
This document describes how to integrate role-specific login in the current framework without changing existing test behavior now.

See also:
- [API Auth Test Suite Brief](ApiAuthTestSuiteBrief.md) for the current positive/negative auth execution model used by APITests.

## Current State Analysis
- API auth currently uses one credential source: `validCredentials` from `resources/testdata/loginData.json`.
- Session token and credentials are stored in `ApiSessionContext`.
- Positive flows now reuse a suite-level token (single login in `OneTimeSetUp`).
- `SetUp` refreshes that suite token only when expired via `EnsureSuiteTokenAsync()`.
- Negative scenarios are test-driven through `LoginAsync(..., tokenScenario, tokenState)` and can disable refresh intent.
- This supports positive/negative token scenarios, but role credential selection is still pending.

## Target Architecture (Future)
Keep the same lightweight architecture and add role resolution as a provider/resolver utility, not a new framework layer.

### Proposed Components
- `RoleCredentialsModel`
  - Represents a single role's credentials and optional metadata.
- `RoleCredentialProvider`
  - Loads role map from test data (JSON) with environment variable overrides.
- `RoleCredentialResolver`
  - Resolves credentials by role key (`admin`, `user`, `organizer`, `viewer`).
- `AuthClient.LoginAsync(...)`
  - Reused as-is for role-specific login; no special per-role auth path required.

## Proposed Test Data Structure
Add a `roles` section to `resources/testdata/loginData.json` in a future increment.

```json
{
  "validCredentials": {
    "email": "${TEST_USER_EMAIL}",
    "password": "${TEST_USER_PASSWORD}"
  },
  "roles": {
    "admin": {
      "email": "${TEST_ADMIN_EMAIL}",
      "password": "${TEST_ADMIN_PASSWORD}"
    },
    "user": {
      "email": "${TEST_USER_EMAIL}",
      "password": "${TEST_USER_PASSWORD}"
    },
    "organizer": {
      "email": "${TEST_ORGANIZER_EMAIL}",
      "password": "${TEST_ORGANIZER_PASSWORD}"
    },
    "viewer": {
      "email": "${TEST_VIEWER_EMAIL}",
      "password": "${TEST_VIEWER_PASSWORD}"
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

Fallback behavior recommendation:
1. Try role-specific env vars.
2. Fallback to role values in JSON (non-secret/dev only).
3. Fail fast with clear message if role credentials are missing.

## How Role-Specific Tests Would Look
Future usage pattern in tests:

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

## Migration Plan (Safe, Incremental)
1. Add `roles` object to `loginData.json` while keeping `validCredentials` unchanged.
2. Introduce `RoleCredentialResolver` utility and unit tests.
3. Keep existing tests unchanged.
4. Add new role-focused tests using resolver.
5. Optionally migrate old tests from `validCredentials` to explicit role keys.

## Backward Compatibility
- Existing tests can continue using `validCredentials` with no changes.
- Existing suite setup can keep authenticating as default user until role tests are introduced.
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

## Current Implementation Snapshot (As of 2026-04-14)

This section describes what is already implemented today, independent of the planned role resolver work above.

- Positive flows use one suite-level login in `OneTimeSetUp` and reuse the cached token.
- `SetUp` calls `EnsureSuiteTokenAsync()` and refreshes only when the suite token is expired.
- Refresh is lock-protected (`_suiteLock`) to avoid concurrent refresh storms.
- Negative scenarios still call `LoginAsync(..., tokenScenario, false)` explicitly from tests.
- Negative tokens for `expired` and `invalid` are stamped with `AllowRefresh = false`.

### Quick Decision Matrix

| Flow type | Test setup | Token expected in context | `AllowRefresh` | Refresh behavior | Typical API result |
|---|---|---|---|---|---|
| Positive happy path | Suite `OneTimeSetUp` login with `(tokenScenario: "valid", tokenState: true)` | Yes (real token) | `true` | Reused across tests; refreshed only if expired | `2xx` |
| Positive with expired suite token | No test change; handled in `EnsureSuiteTokenAsync()` | Yes (fresh token after refresh) | `true` | Single refresh under `_suiteLock` | `2xx` |
| Negative expired token | `LoginAsync(..., tokenScenario: "expired", tokenState: false)` | Yes (forced expired) | `false` | Never auto-refreshed | `401/4xx` |
| Negative invalid token | `LoginAsync(..., tokenScenario: "invalid", tokenState: false)` | Yes (broken token string) | `false` | Never auto-refreshed | `401/4xx` |
| Negative missing token | `LoginAsync(..., tokenScenario: "missing", tokenState: false)` | No | N/A | No token to refresh | `401/4xx` |
| Invalid test config | `LoginAsync(..., non-valid, true)` | N/A | N/A | Throws before request | `InvalidOperationException` |

### Practical Note

Use the planned role resolver to choose credentials, then apply the same matrix rules for token scenario behavior. In other words, roles decide "who" logs in; token scenario decides "how auth should behave" in that test.
