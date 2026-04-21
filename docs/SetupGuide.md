# Setup Guide (Windows + macOS)

Use this guide if you are setting up the framework for the first time.

---

## What You Will Achieve

By the end of this guide, you will be able to:
- Install all required tools in the correct order.
- Clone and open the framework project.
- Configure test credentials safely.
- Run a first successful test execution.

---

## 1. Install Prerequisites in Sequence

Follow this exact order. Do not skip verification commands.

### Step 1: Install Visual Studio Code (IDE)

- Download and install VS Code: https://code.visualstudio.com/

Why this step: VS Code is the editor used to open, run, and troubleshoot this framework.

### Step 2: Install Git

- Download and install Git: https://git-scm.com/downloads

Verify installation:

**Windows (PowerShell):**
```powershell
git --version
```

**macOS (Terminal):**
```bash
git --version
```

Why this step: Git lets you download the repository and keep your local code updated.

### Step 3: Install .NET SDK (Required)

- Install .NET SDK 10.x from: https://dotnet.microsoft.com/download

Verify installation:

**Windows (PowerShell):**
```powershell
dotnet --version
dotnet --list-sdks
```

**macOS (Terminal):**
```bash
dotnet --version
dotnet --list-sdks
```

Why this step: The test framework is built on .NET and cannot run without the SDK.

### Step 4: Install Java (Required Before Allure CLI)

- Install Java 8+ (recommended: Temurin 17): https://adoptium.net/

Verify installation:

**Windows (PowerShell):**
```powershell
java -version
```

**macOS (Terminal):**
```bash
java -version
```

Why this step: Allure CLI depends on Java, so Java must be installed first.

### Step 5: Install a Browser for UI Tests

- Install at least one: Chrome, Edge, or Firefox (latest stable version).

Why this step: UI tests open a real browser to perform actions like login and navigation.

### Step 6: Install Allure CLI (After Java)

Version compatibility note:
- This framework is validated with **Allure CLI 2.x**.
- If your package manager installs **Allure 3.x** automatically, command behavior and report output can differ.
- If you see report generation issues, use a stable 2.x version (recommended: **2.38.1**).

**Windows option 1 (Scoop):**
```powershell
scoop install allure
```

**Windows option 2 (Chocolatey):**
```powershell
choco install allure
```

**macOS (Homebrew):**
```bash
brew install allure
```

Verify installation:

**Windows (PowerShell):**
```powershell
allure --version
```

**macOS (Terminal):**
```bash
allure --version
```

Expected:
- Version should be in the 2.x line for this framework setup.

If 3.x is installed, rollback to a compatible 2.x version:

Option A: Try package manager rollback first.

Windows (Scoop):
```powershell
scoop uninstall allure
scoop install allure
allure --version
```

Windows (Chocolatey):
```powershell
choco uninstall allure -y
choco install allure --version=2.38.1 -y
allure --version
```

macOS (Homebrew):
```bash
brew uninstall allure
brew install allure
allure --version
```

If package manager still installs 3.x, use manual install (guaranteed version control).

Option B: Manual install of a specific version (recommended fallback).

Windows (PowerShell):
```powershell
$ver = "2.38.1"
scoop uninstall allure
choco uninstall allure -y
Invoke-WebRequest "https://github.com/allure-framework/allure2/releases/download/$ver/allure-$ver.zip" -OutFile "$env:TEMP\allure-$ver.zip"
New-Item -ItemType Directory -Path "$env:USERPROFILE\tools" -Force | Out-Null
Expand-Archive "$env:TEMP\allure-$ver.zip" -DestinationPath "$env:USERPROFILE\tools" -Force
$allureBin = "$env:USERPROFILE\tools\allure-$ver\bin"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "User") + ";$allureBin", "User")
$env:Path = "$env:Path;$allureBin"
allure --version
Get-Command allure | Select-Object Source
```

macOS (bash/zsh):
```bash
VER=2.38.1
brew uninstall allure
curl -L -o /tmp/allure-$VER.tgz https://github.com/allure-framework/allure2/releases/download/$VER/allure-$VER.tgz
mkdir -p $HOME/tools
tar -xzf /tmp/allure-$VER.tgz -C $HOME/tools
echo 'export PATH="$HOME/tools/allure-'"$VER"'/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc
allure --version
which allure
```

After rollback, continue with report commands from this guide.

Why this step: Allure CLI generates and opens the HTML test report.

### Step 7: Install Useful VS Code Extensions (Recommended)

- C#
- C# Dev Kit
- .NET Test Explorer

Why this step: These extensions make test execution and troubleshooting easier for beginners.

### Step 8: First-Time Compatibility Check (Highly Recommended)

Before moving to repository setup, validate these compatibility points:

1. Use the correct shell for commands:
  - Windows commands in this guide are for **PowerShell**.
  - macOS commands are for **Terminal (bash/zsh)**.

2. Confirm Allure CLI major version:
  - Run `allure --version`
  - Prefer **2.x** for this framework (recommended 2.38.1).

3. Confirm Java is installed and visible in PATH:
  - Run `java -version`
  - If Allure is installed but fails at runtime, Java path is usually the cause.

4. Ensure internet/proxy access is available for first run:
  - `dotnet restore` needs NuGet access.
  - UI driver setup needs browser-driver downloads on first execution.

5. Confirm `.env` filename is exact:
  - File must be named `.env` (not `.env.txt`).
  - This is a common Windows first-time issue due to hidden extensions.

6. Run commands from solution root:
  - Folder containing `SeleniumAutomationFramework.sln`.
  - Running from another folder can make report and config paths look broken.

---

## 2. Repository Setup

### Step 1: Clone the Repository

**Windows (PowerShell):**
```powershell
git clone <repository-url>
cd SeleniumAutomationFramework
```

**macOS (Terminal):**
```bash
git clone <repository-url>
cd SeleniumAutomationFramework
```

Why this step: This creates your local project copy where you will run all commands.

### Step 2: Open the Project Folder in VS Code

**Windows (PowerShell):**
```powershell
code .
```

**macOS (Terminal):**
```bash
code .
```

Why this step: Opening the folder in VS Code lets you run tests and view reports from one place.

### Step 3: Restore Project Dependencies

**Windows (PowerShell):**
```powershell
dotnet restore
```

**macOS (Terminal):**
```bash
dotnet restore
```

Why this step: This downloads all required NuGet packages before build and test execution.

### Step 4: Build the Solution

**Windows (PowerShell):**
```powershell
dotnet build .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet build ./SeleniumAutomationFramework.sln
```

Why this step: Build confirms your environment is correct and the code compiles successfully.

---

## 3. Configure Test Credentials

Use this section to set login credentials required by test data.

### Step 1: Create a Local .env File

**Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

**macOS (Terminal):**
```bash
cp .env.example .env
```

Why this step: .env stores your local credentials without changing source code.

### Step 2: Add Your Test Credentials in .env

Update the file with the role credentials you plan to execute:

```bash
TEST_USER_EMAIL=your_test_email@example.com
TEST_USER_PASSWORD=your_test_password
TEST_ADMIN_EMAIL=your_admin_email@example.com
TEST_ADMIN_PASSWORD=your_admin_password
TEST_ORGANIZER_EMAIL=your_organizer_email@example.com
TEST_ORGANIZER_PASSWORD=your_organizer_password
TEST_VIEWER_EMAIL=your_viewer_email@example.com
TEST_VIEWER_PASSWORD=your_viewer_password
```

Why this step: tests validate credentials per resolved role, so only the role(s) you run must be configured.

Optional runtime role selector:

```bash
TEST_EXECUTION_ROLE=user
```

If not set, role can still be declared with `[TestRole("user")]` or `[TestRole("admin")]` in tests.

### Step 3: Understand Security Rule

- Never commit .env to source control.
- Keep only test credentials, never production credentials.

---

## 4. Role Execution Readiness

Before running role-based tests, verify:
- `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` are present for user-tagged tests.
- `TEST_ADMIN_EMAIL` and `TEST_ADMIN_PASSWORD` are present for admin-tagged tests.
- `TEST_ORGANIZER_EMAIL` and `TEST_ORGANIZER_PASSWORD` are present for organizer-tagged tests.
- `TEST_VIEWER_EMAIL` and `TEST_VIEWER_PASSWORD` are present for viewer-tagged tests.
- If you want one role across a full run, set `TEST_EXECUTION_ROLE` in the shell before `dotnet test`.

For complete role execution examples, see [ExecutionGuide.md](./ExecutionGuide.md#4a-role-based-execution).

Why this step: This prevents accidental credential exposure.

### Step 4: Optional Quick Check for Session Variables

If you use terminal-based environment variables instead of .env:

**Windows (PowerShell):**
```powershell
$env:TEST_USER_EMAIL = "your_test_email@example.com"
$env:TEST_USER_PASSWORD = "your_test_password"
```

**macOS (Terminal):**
```bash
export TEST_USER_EMAIL="your_test_email@example.com"
export TEST_USER_PASSWORD="your_test_password"
```

Why this step: This gives an alternate way to inject credentials for a single terminal session.

---

## 4. Configure Browser and Headless Mode

Default browser is Chrome if no override is given.

### Step 1: Keep Default Browser (No Change Needed)

- Do nothing to use Chrome.

Why this step: This is the fastest path for first execution.

### Step 2: Change Browser in .env (Recommended Method)

Add or update in .env:

```bash
TestSettings__Browser=edge
TestSettings__Headless=false
```

Allowed browser values:
- chrome
- edge
- firefox

Why this step: .env is simple and works the same for both Windows and macOS.

### Step 3: Optional Terminal Override (Current Session Only)

**Windows (PowerShell):**
```powershell
$env:TestSettings__Browser = "firefox"
$env:TestSettings__Headless = "true"
```

**macOS (Terminal):**
```bash
export TestSettings__Browser="firefox"
export TestSettings__Headless="true"
```

Why this step: Useful for quick trial runs without editing files.

### Step 4: Change Permanent Defaults in appsettings.json

Open [config/appsettings.json](../config/appsettings.json) in VS Code and edit the values directly:

```json
{
  "TestSettings": {
    "Browser": "chrome",
    "Headless": false,
    "ImplicitWaitSeconds": 5,
    "ExplicitWaitSeconds": 15
  }
}
```

Changes here become the new default for every run on your machine, without needing to set environment variables or edit `.env`.

Why this step: Use `appsettings.json` when you want a setting to persist across all terminal sessions and IDE restarts — for example, always defaulting to Edge or increasing wait times for a slow environment. Full reference of every setting is in [ExecutionGuide.md — Section 8](./ExecutionGuide.md#8-useful-configuration-settings).

---

## 5. First Setup Validation Run

Use this section to confirm setup is complete.

### Step 1: Run All Tests Once

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

Why this step: A full run validates tools, credentials, browser config, and framework wiring in one go.

### Step 2: Generate Allure HTML Report

**Windows (PowerShell):**
```powershell
allure generate .\reports\allure-results -o .\reports\allure-report --clean
```

**macOS (Terminal):**
```bash
allure generate ./reports/allure-results -o ./reports/allure-report --clean
```

Why this step: This converts raw test result files into a readable report.

Important:
- The command must start with `allure`.
- Running only `generate ...` or only a folder path will fail with `Unknown Syntax Error: Command not found`.

### Step 2.1: Verify Allure Results Are Actually Created

If report output looks generic or empty, first confirm result files exist.

**Windows (PowerShell):**
```powershell
Get-ChildItem .\reports\allure-results\*.json
```

**macOS (Terminal):**
```bash
ls ./reports/allure-results/*.json
```

Expected: multiple `*-result.json` files.

If empty, re-run tests fully and let execution complete:

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

Then run generate again.

### Step 3: Open Allure Report

**Windows (PowerShell):**
```powershell
allure open .\reports\allure-report
```

**macOS (Terminal):**
```bash
allure open ./reports/allure-report
```

Why this step: You can quickly review pass/fail details and evidence such as screenshots.

Fallback option:
- If `allure open` does not launch, open `reports/allure-report/index.html` directly in browser.

---

## 6. Quick Troubleshooting

| Problem | What to Check |
|---------|---------------|
| `dotnet` command not found | Reinstall .NET SDK and reopen terminal |
| `dotnet restore` fails in corporate network | Configure NuGet proxy/access and retry restore |
| `allure` command not found | Install Allure CLI, then verify PATH |
| Allure installed but report behavior is inconsistent | Check `allure --version`. If it is 3.x, switch to 2.x (recommended 2.38.1) for this framework |
| Need to force specific Allure version | Use the rollback steps in Step 6 and verify with `allure --version` and command source (`Get-Command allure` or `which allure`) |
| `Unknown Syntax Error: Command not found` during report generation | You likely ran `generate ...` without the `allure` prefix. Use: `allure generate .\reports\allure-results -o .\reports\allure-report --clean` |
| `allure-results` folder is empty | Ensure tests were executed to completion from solution root. Then validate with `Get-ChildItem .\reports\allure-results\*.json` |
| HTML report opens but looks generic | This framework also produces a separate execution HTML report. To view Allure, generate from `reports/allure-results` and open `reports/allure-report/index.html` |
| `java` command not found | Install Java first, then reinstall/verify Allure |
| Allure installed but fails to run | Verify `java -version` and ensure Java is on PATH |
| Login test fails with missing credentials | Confirm `.env` has the credential pair for the role under test (`TEST_USER_*`, `TEST_ADMIN_*`, `TEST_ORGANIZER_*`, or `TEST_VIEWER_*`) |
| Credentials still not loaded though `.env` exists | Ensure file is named exactly `.env` and saved in repository root |
| Wrong browser opens | Check `TestSettings__Browser` in `.env` and terminal overrides |
| Browser/driver startup fails on first run | Ensure browser is installed and internet access allows first-time driver download |
| Command works on one machine but fails on another | Verify shell type (PowerShell vs bash/zsh) and use the matching command block from this guide |

---

## 7. Next Document to Follow

After setup is complete, continue with execution steps in [ExecutionGuide.md](./ExecutionGuide.md).
