# Token Storage and Expiry Behavior

## Overview

This document explains the current API auth design after the suite-level token update:

- Positive flows reuse per-role suite tokens instead of logging in per test.
- Each role token is refreshed only when it naturally expires.
- Negative flows keep full control and can intentionally disable refresh.

See also:
- [API Auth Test Suite Brief](ApiAuthTestSuiteBrief.md) for a concise end-to-end summary and non-breaking improvement options.

## 1. Storage Model

Token state is held in memory in `ApiSessionContext` using `AsyncLocal<ApiSessionContext?>`.

- Scope: per async execution flow (test-level isolation).
- Persistence: in-memory only (no disk/env persistence).
- Cleanup: cleared in teardown.

The token model is `TokenState` in `src/Framework.API/ApiSessionContext.cs`:

```csharp
public sealed record TokenState(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        string? RefreshToken = null,
        bool AllowRefresh = true)
```

Key flags/properties:

- `IsValid`: true only if token is not expired and not in the 30-second grace window.
- `AllowRefresh`: governs whether the framework is allowed to replace that token with a fresh one.

## 2. Auth Lifecycle in the Current Suite

Current fixture flow in `tests/APITests/ApiTestBase.cs`:

1. `OneTimeSetUp` initializes clients and performs one valid login.
2. Returned token is cached in private `_suiteTokens[role]`.
3. Every `[SetUp]` calls `EnsureSuiteTokenAsync()`.
4. `EnsureSuiteTokenAsync()` pushes role token into the current test context.
5. If cached role token is expired, it refreshes once under `_suiteLock` and updates that role cache.

This removes repeated login calls from all positive tests.

## 3. Request Pipeline Behavior

Authenticated requests in `src/ApiClients/BaseApiClient.cs` use:

```csharp
var token = ApiSessionContext.Current.CurrentAccessToken;
if (!string.IsNullOrWhiteSpace(token))
{
        requestBuilder.WithBearerToken(token);
}
```

Important details:

- Request layer attaches the raw token string when present.
- Token validity checks and refresh decisions are done in test setup (`EnsureSuiteTokenAsync`), not inside request send.

## 4. Positive vs Negative Scenario Rules

Auth scenario control remains in `AuthClient.LoginAsync(...)`:

- `(tokenScenario: "valid", tokenState: true)`:
    - Real login required.
    - Login failure throws `InvalidOperationException`.
    - Token is refresh-eligible (`AllowRefresh = true`).

- `(tokenScenario: "expired", tokenState: false)`:
    - Token is intentionally marked expired.
    - Token is marked `AllowRefresh = false`.

- `(tokenScenario: "invalid", tokenState: false)`:
    - Token string is intentionally broken.
    - Token is marked `AllowRefresh = false`.

- `(tokenScenario: "missing", tokenState: false)`:
    - Token cleared from context.

Guardrails:

- `tokenState=true` with any non-`valid` scenario throws immediately.
- `tokenState=true` with failed login throws immediately.

## 5. Backend Sequence (Detailed Preview)

### A. Positive test with reused suite token

```
Test fixture starts
    -> OneTimeSetUp
         -> HTTP POST /login once
         -> receive token + expiresIn
         -> build TokenState(AllowRefresh=true)
         -> cache in _suiteTokens[role]

Each test start
    -> SetUp
         -> EnsureSuiteTokenAsync
                 -> if _suiteTokens[role].IsValid: copy to ApiSessionContext.Current

API call
    -> BaseApiClient.SendAsync(requiresAuth=true)
    -> reads CurrentAccessToken
    -> adds Authorization: Bearer <token>
    -> backend validates and responds
```

### B. Positive flow when suite token expires between tests

```
SetUp
    -> EnsureSuiteTokenAsync
         -> detects !_suiteTokens[role].IsValid
         -> acquires _suiteLock (single refresh winner)
         -> re-checks validity after lock
         -> HTTP POST /login once
         -> replaces _suiteTokens[role] with fresh token
         -> writes fresh token into ApiSessionContext.Current
```

Outcome: one refresh request, not one per parallel test.

### C. Negative scenario that must not auto-heal

```
Test body
    -> LoginAsync(..., tokenScenario: "invalid", tokenState: false)
       or LoginAsync(..., tokenScenario: "expired", tokenState: false)
    -> AuthClient creates/marks token with AllowRefresh=false
    -> token pushed into ApiSessionContext.Current
    -> request uses intentionally broken token
    -> backend returns 401/4xx as expected
```

Because negative tokens are intentionally non-refreshable, framework behavior stays deterministic for negative assertions.

## 6. Why This Design Is Cost-Efficient

- Before: positive tests performed repeated logins.
- Now: one login per role per fixture, plus refresh only when actually expired.
- Parallel-safe refresh lock prevents stampede login calls.
- Negative tests still control token state explicitly.

## 7. Operational Caveats

- If credentials are missing in the environment, suite login can fail and positive tests may become inconclusive.
- Tokens remain in memory only for process lifetime.
- `AllowRefresh` policy is currently enforced by setup flow and scenario construction.

## 8. Related Source Files

- `src/Framework.API/AuthClient.cs`
- `src/Framework.API/ApiSessionContext.cs`
- `tests/APITests/ApiTestBase.cs`
- `src/ApiClients/BaseApiClient.cs`

## 9. Quick Decision Matrix

| Flow type | Test setup | Token expected in context | `AllowRefresh` | Refresh behavior | Typical API result |
|---|---|---|---|---|---|
| Positive happy path | Suite `OneTimeSetUp` login with `(tokenScenario: "valid", tokenState: true)` | Yes (real token) | `true` | Reused across tests; refreshed only if expired | `2xx` |
| Positive with expired suite token | No test change; handled in `EnsureSuiteTokenAsync()` | Yes (fresh token after refresh) | `true` | Single refresh under `_suiteLock` | `2xx` |
| Negative expired token | `LoginAsync(..., tokenScenario: "expired", tokenState: false)` | Yes (forced expired) | `false` | Never auto-refreshed | `401/4xx` |
| Negative invalid token | `LoginAsync(..., tokenScenario: "invalid", tokenState: false)` | Yes (broken token string) | `false` | Never auto-refreshed | `401/4xx` |
| Negative missing token | `LoginAsync(..., tokenScenario: "missing", tokenState: false)` | No | N/A | No token to refresh | `401/4xx` |
| Invalid test config | `LoginAsync(..., non-valid, true)` | N/A | N/A | Throws before request | `InvalidOperationException` |

### Reading this matrix

- Use the first two rows for normal authenticated tests to minimize login cost.
- Use the negative rows when validating auth failures; they intentionally prevent token self-healing.
- If a positive flow unexpectedly hits `401`, check whether suite refresh executed and whether credentials are configured.
