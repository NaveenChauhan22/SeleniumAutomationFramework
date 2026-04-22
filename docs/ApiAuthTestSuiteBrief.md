# API Auth Test Suite Brief

## Purpose
This document summarizes how the API auth test suite currently behaves for positive and negative scenarios, and suggests safe improvements that preserve existing functionality.

## Current Behavior

### 1) Suite-level login and reuse
- `APITestBase.OneTimeSetUp` performs one positive login using:
  - `LoginAsync(email, password, tokenScenario: "valid", tokenState: true)`
- Tokens are cached by role in `_suiteTokens` and reused across tests.
- `[SetUp]` calls `EnsureSuiteTokenAsync()` before every test.

### 2) Refresh strategy (suite-level only)
- If a role token in `_suiteTokens` is valid, it is reused.
- If a role token is expired, a single thread refreshes it under `_suiteLock`.
- There is no per-request auto-refresh in request sending.

### 2A) Role selection strategy
- Execution role is selected by:
  1. Method-level `[TestRole("...")]`
  2. Class-level `[TestRole("...")]`
  3. `TEST_EXECUTION_ROLE` environment variable fallback
- Credentials for selected role are resolved from role env vars first, then JSON role map.

### 3) Positive test intent
- Positive intent contract:
  - `LoginAsync(email, password, tokenScenario: "valid", tokenState: true)`
- For positive API tests, the framework binds the suite token into the current test context with:
  - `EnsurePositiveAuthContextOrInconclusive("Events")`
  - `EnsurePositiveAuthContextOrInconclusive("Bookings")`
- If no valid suite token is available, tests are marked Inconclusive (not false-negative failures).

### 4) Negative test intent
- Negative intent contracts:
  - `LoginAsync(email, password, tokenScenario: "invalid", tokenState: false)`
  - `LoginAsync(email, password, tokenScenario: "expired", tokenState: false)`
  - `LoginAsync(email, password, tokenScenario: "missing", tokenState: false)`
- Negative tests override auth state in the test body after setup.
- Negative tokens are non-refreshable (`AllowRefresh = false`) so setup refresh does not override test intent.

### 5) Mismatch behavior and guardrails
- `tokenState=true` with non-`valid` scenario throws immediately.
- `tokenState=true` + wrong credentials throws immediately.
- Negative scenarios with wrong credentials may use synthetic failing tokens to keep outcomes deterministic.
- Request layer only attaches current token. It does not silently relogin mid-request.

## Auth API Examples (Navigation)

Use these direct links from `AuthAPITests` as concrete examples of each intent:

### Positive examples
- [AuthScenario_ValidToken_ShouldAccessProtectedEndpoint](../tests/APITests/AuthAPITests.cs#L22)
- [MeEndpoint_WithValidToken_ShouldReturnCurrentUser](../tests/APITests/AuthAPITests.cs#L134)

### Negative examples
- [AuthScenario_ExpiredToken_ShouldFailProtectedEndpoint](../tests/APITests/AuthAPITests.cs#L44)
- [AuthScenario_InvalidToken_ShouldFailProtectedEndpoint](../tests/APITests/AuthAPITests.cs#L63)
- [AuthScenario_MissingToken_ShouldFailProtectedEndpoint](../tests/APITests/AuthAPITests.cs#L82)
- [LoginEndpoint_WithInvalidPassword_ShouldReturnFailure](../tests/APITests/AuthAPITests.cs#L153)

## Why this model works well
- Minimizes login calls (cost/perf friendly).
- Keeps positive path fast and stable with suite token reuse.
- Keeps negative path deterministic and explicit.
- Reduces flakiness from environment login issues.

## Suggested Improvements (non-breaking)

### A) Introduce a tiny auth-intent helper layer (recommended)
Add base helpers that keep intent self-documenting and prevent ad-hoc patterns:
- `UsePositiveAuthOrInconclusive(flowName)`
- `UseNegativeInvalidAuth(email, password)`
- `UseNegativeExpiredAuth(email, password)`
- `UseNegativeMissingAuth(email, password)`

Benefit:
- Cleaner tests
- Consistent intent semantics
- No behavior change

### B) Add role-aware positive helper now (implemented)
- `EnsurePositiveAuthContextOrInconclusive(flowName, role = null)` already supports role-aware binding.
- `LoginAsRoleAsync(role, ...)` and `LoginAsCurrentRoleAsync(...)` already support explicit and resolved role login.

### C) Add lightweight auth diagnostics for troubleshooting
On test start or auth bind failure, log a compact auth state snapshot:
- suite token present/absent
- token validity
- refresh eligibility
- test flow name

Benefit:
- Faster debugging when environments misconfigure credentials
- No change to auth logic

### D) Keep synthetic tokens for negative fallback
Do not remove synthetic fallback for negative intent.
It is important for deterministic negative testing when real login fails.

## Role-based Login Implementation
The role-based auth model is now implemented:
- Token lifecycle is suite-scoped per role using `_suiteTokens`.
- `GetCurrentTestRole()` resolves method/class/env role and drives credential selection.
- Tests remain stable by using role-aware helpers instead of raw auth primitives.

## Practical Rule Set
- Positive tests: use suite token bind helper only.
- Negative tests: explicitly set scenario in test body via `LoginAsync(..., tokenScenario: "<scenario>", tokenState: false)`.
- Never mix contradictory intent (for example `tokenState=true` with `expired`).
- Keep refresh in setup only; do not add hidden per-request relogin.
