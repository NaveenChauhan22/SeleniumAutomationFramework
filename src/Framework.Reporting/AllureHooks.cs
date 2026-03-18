using NUnit.Framework;

/// <summary>
/// NUnit global setup fixture that bootstraps and finalises the Allure report run lifecycle.
/// This source file is compiled directly into each test project (UITests, APITests) via a
/// <c>&lt;Compile Include&gt;</c> link in the project file; it is excluded from the
/// Framework.Reporting library to avoid hooking into non-test assemblies.
/// </summary>
[SetUpFixture]
public sealed class AllureHooks
{
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        Framework.Reporting.AllureBootstrap.InitializeRun();
    }

    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        Framework.Reporting.AllureBootstrap.FinalizeRun();
    }
}