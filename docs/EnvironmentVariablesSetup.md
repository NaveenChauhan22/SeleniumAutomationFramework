# Environment Variables Setup for Test Credentials

## Overview

Sensitive test credentials (email, password) are no longer stored directly in test data files. Instead, they are referenced via environment variables using the `${VARIABLE_NAME}` syntax in JSON files.

This approach ensures:
- ✅ Credentials are not exposed in source control
- ✅ Different credentials can be used per environment (CI/CD, local development, staging)
- ✅ Clear, actionable error messages when credentials are missing
- ✅ Test data files remain generic and shareable

---

## Required Environment Variables

| Variable Name | Purpose | Example |
|---|---|---|
| `TEST_USER_EMAIL` | Valid test user email for login tests | `correctemail@example.com` |
| `TEST_USER_PASSWORD` | Valid test user password for login tests | `correctpassword` |

---

## Setting Environment Variables

### Option 1: Windows PowerShell (Session-based)

Set variables for the current PowerShell session only:

```powershell
$env:TEST_USER_EMAIL = "correctemail@example.com"
$env:TEST_USER_PASSWORD = "correctpassword"
```

Then run tests:
```powershell
dotnet test tests/UITests/UITests.csproj
```

### Option 2: Windows Command Prompt (Session-based)

Set variables for the current CMD session:

```cmd
set TEST_USER_EMAIL=correctemail@example.com
set TEST_USER_PASSWORD=correctpassword
```

Then run tests:
```cmd
dotnet test tests/UITests/UITests.csproj
```

### Option 3: Windows Environment Variables (Permanent)

Set system/user environment variables permanently:

1. Open **System Properties** → **Environment Variables**
2. Click **New** under "User variables" (or "System variables" for all users)
3. Add each variable:
   - Variable name: `TEST_USER_EMAIL`
   - Variable value: `correctemail@example.com`
4. Repeat for all variables
5. Click **OK** and restart your terminal/IDE

**Command-line alternative (Admin required):**
```powershell
[Environment]::SetEnvironmentVariable("TEST_USER_EMAIL", "correctemail@example.com", "User")
[Environment]::SetEnvironmentVariable("TEST_USER_PASSWORD", "correctpassword", "User")
```

### Option 4: .env File and dotenv (Recommended for Local Development)

Create a `.env` file in the project root:

```
TEST_USER_EMAIL=correctemail@example.com
TEST_USER_PASSWORD=correctpassword
```

Add `.env` to `.gitignore` to prevent committing credentials.

Then load before running tests (requires a dotenv loader):

```powershell
# Using dotnet-env or similar tool
dotenv load
dotnet test tests/UITests/UITests.csproj
```

### Option 5: CI/CD Pipeline (GitHub Actions, Azure Pipelines, etc.)

Set secrets in your CI/CD platform and inject as environment variables during test execution.

**Example (GitHub Actions):**
```yaml
env:
  TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
  TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
```

**Example (Azure Pipelines):**
```yaml
variables:
  - group: 'Test Credentials'  # Variable group containing the 4 variables
```

---

## How It Works

### JSON File Example

**resources/testdata/loginData.json:**
```json
{
  "validCredentials": {
    "email": "${TEST_USER_EMAIL}",
    "password": "${TEST_USER_PASSWORD}"
  }
}
```

### Substitution Process

1. **Load JSON file** → `resources/testdata/loginData.json`
2. **Find placeholders** → Regex pattern `${VARIABLE_NAME}`
3. **Replace with environment values** → `Environment.GetEnvironmentVariable()`
4. **Deserialize to model** → `JsonConvert.DeserializeObject<T>()`

### Error Handling

If an environment variable is **not found**, you'll see:

```
System.InvalidOperationException: Environment variable '{TEST_USER_EMAIL}' not found. 
Please set the environment variable 'TEST_USER_EMAIL' before running tests. 
Required variables: TEST_USER_EMAIL, TEST_USER_PASSWORD
```

---

## Supported JSON Files

Currently, the following test data files support environment variable substitution:

- ✅ `resources/testdata/loginData.json` — Test credentials
- ✅ `resources/testdata/homePageData.json` — Can be extended if needed

---

## Best Practices

1. **Never commit real credentials** → Always use `.env` or secrets management
2. **Use different credentials per environment** → Local dev vs. staging vs. production
3. **Log which variables are missing** → The error message lists all required variables
4. **Keep test data files generic** → Use placeholders so the same JSON works in any environment
5. **Document required variables** → Maintain this guide in your repository

---

## Troubleshooting

### Tests fail with "Environment variable not found" error
- ✅ Verify all variables are set: `$env:TEST_USER_EMAIL`, etc.
- ✅ Use `Get-ChildItem env:` in PowerShell to list all set variables
- ✅ Check for typos in variable names (case-sensitive on Linux/Mac)

### Variables set but tests still fail
- ✅ Restart your IDE/terminal after setting environment variables
- ✅ Ensure you're running tests from the correct directory
- ✅ Use `$env:VARIABLE_NAME` in PowerShell to verify value is set

### Different values needed for different environments
- Create separate `.env` files: `.env.local`, `.env.staging`, `.env.prod`
- Load the appropriate file before running tests
- In CI/CD, use platform-specific secrets management

---

## Related Files

- **JSON Data Provider**: [Framework.Data/JsonDataProvider.cs](../src/Framework.Data/JsonDataProvider.cs)
  - Handles environment variable substitution logic
- **Test Data Files**: [resources/testdata/](../resources/testdata/)
  - Contains JSON files with placeholders
- **Login Tests**: [tests/UITests/LoginTest.cs](../tests/UITests/LoginTest.cs)
  - Uses credentials from `loginData.json`

