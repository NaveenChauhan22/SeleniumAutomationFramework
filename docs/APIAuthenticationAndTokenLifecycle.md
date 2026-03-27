# API Authentication and Token Lifecycle

## Purpose
This document is the single reference for API test authentication flow, session-scoped token handling, token reuse strategy, and mid-suite token expiry behavior.

## Scope
- API test authentication in [tests/APITests/ApiTestBase.cs](tests/APITests/ApiTestBase.cs)
- Session-scoped token storage in [src/Framework.API/ApiSessionContext.cs](src/Framework.API/ApiSessionContext.cs)
- Login and re-authentication behavior in [src/Framework.API/AuthClient.cs](src/Framework.API/AuthClient.cs)
- Request retry path on token failure in [src/APIPages/BaseAPIPage.cs](src/APIPages/BaseAPIPage.cs)

## Architecture Summary
- Token model: `TokenState`
- Session container: `ApiSessionContext` (`AsyncLocal` + refresh lock)
- Auth operations: `AuthClient`
- API request integration: `BaseAPIPage.SendAsync(...)`

## Current Authentication Flow
1. API suite setup initializes shared HTTP and API clients.
2. Authentication is performed once and stored in session context.
3. A suite-shared token is reused across API test classes while valid.
4. Credentials are stored in memory for automatic re-authentication when needed.
5. API requests requiring auth read token from session context.

## Token Reuse Across Test Classes
To avoid generating a new token per class, [tests/APITests/ApiTestBase.cs](tests/APITests/ApiTestBase.cs) uses a suite-level cache:
- `SuiteSharedToken` (static)
- lock-protected setup path

Behavior:
- If suite token is valid, the class reuses it.
- If missing/expired, a new token is created once and cached.

Implementation reference:
- [tests/APITests/ApiTestBase.cs](tests/APITests/ApiTestBase.cs)

## Mid-Suite Expiry Handling
If an authenticated request fails because token is no longer valid:
1. [src/APIPages/BaseAPIPage.cs](src/APIPages/BaseAPIPage.cs) detects the invalid bearer token path.
2. `AuthClient.ReauthenticateIfStoredCredentialsAsync()` performs fresh login.
3. New token is stored in session context.
4. Original request is retried once automatically.

## Thread Safety and Parallel Runs
- `ApiSessionContext` uses `AsyncLocal` for execution-context isolation.
- Refresh/re-auth operations use an internal lock path to prevent concurrent refresh races.
- Token data is in-memory only.

## Security Notes
- Credentials are stored in memory only for test automation runtime.
- Session and credentials are cleared during suite teardown.
- Do not use this runtime credential strategy in production applications.

## Operational Expectations
- Completed tests remain passed even if token later expires.
- Subsequent authenticated tests should recover automatically through re-auth + retry.
- Non-auth endpoints are unaffected by token lifecycle.

## Latest Validation Status
- Suite-level token reuse has been implemented to avoid unnecessary per-class token regeneration.
- Mid-suite token failure path (re-authenticate + retry) remains active.
- API test suite passes after these changes (including validator-based tests).

## Troubleshooting Checklist
- Confirm login data is resolved correctly from test data/env.
- Confirm token exists in session context after setup.
- Confirm auth-required pages are created with `AuthClient`.
- Check Allure/Serilog logs for re-auth and retry entries.

## Related Files
- [tests/APITests/ApiTestBase.cs](tests/APITests/ApiTestBase.cs)
- [src/Framework.API/ApiSessionContext.cs](src/Framework.API/ApiSessionContext.cs)
- [src/Framework.API/AuthClient.cs](src/Framework.API/AuthClient.cs)
- [src/APIPages/BaseAPIPage.cs](src/APIPages/BaseAPIPage.cs)
