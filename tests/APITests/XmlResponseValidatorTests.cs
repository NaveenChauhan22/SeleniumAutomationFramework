using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using Framework.API;
using Framework.Reporting;

namespace APITests;

/// <summary>
/// Sample XML Response Validator Tests
/// 
/// This test suite demonstrates how to validate XML-formatted API responses.
/// While the current backend API returns JSON, this test file serves as a reference
/// for validating XML responses when working with XML-based APIs or services.
/// 
/// The XmlResponseValidator supports:
/// - Field existence validation
/// - Field value validation
/// - Type validation
/// - Substring matching
/// - XPath-style path notation (both "element/child" and "element.child" formats)
/// </summary>
[AllureNUnit]
[AllureParentSuite("APITests")]
[AllureSuite("Response Validators")]
[AllureFeature("XML Response Validation")]
public class XmlResponseValidatorTests
{
    private static string LoadXmlResponseFromTestData(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("XML file name cannot be null or empty.", nameof(fileName));
        }

        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "xml", fileName);
        Assert.That(File.Exists(path), Is.True, $"XML test data file not found: {path}");

        return File.ReadAllText(path);
    }

    private static void AttachValidationContext(string scenarioName, string xmlResponse, string validationPlan)
    {
        ReportHelper.AddStep($"{scenarioName}: attaching XML payload and validation plan.");
        ReportHelper.AttachContent($"{scenarioName} - XML Payload", "application/xml", xmlResponse, "xml");
        ReportHelper.AttachContent($"{scenarioName} - Validation Plan", "text/plain", validationPlan, "txt");
    }

    private static void AttachValidationResult(string scenarioName, string resultSummary)
    {
        ReportHelper.AddStep($"{scenarioName}: validation completed.");
        ReportHelper.AttachContent($"{scenarioName} - Validation Result", "text/plain", resultSummary, "txt");
    }

    [Test]
    [Category("High")]
    [AllureStory("XML Response Validation - Core Assertions")]
    [AllureSeverity(SeverityLevel.normal)]
    public void ValidateXmlResponse_CoreAssertions_WithDetailedLogging()
    {
        var xmlResponse = LoadXmlResponseFromTestData("xml-core-assertions-response.xml");

        const string validationPlan = "1) Validate required elements exist.\n2) Validate boolean and string field values.\n3) Validate email contains expected domain.";
        AttachValidationContext("XML Core Assertions", xmlResponse, validationPlan);

        AllureApi.Step("Execute XML core assertions", () =>
        {
            ResponseValidator
                .FromContent(xmlResponse, "application/xml")
                .ValidateFieldExists("ApiResponse/Success")
                .ValidateFieldExists("ApiResponse/Data/User/Id")
                .ValidateFieldExists("ApiResponse/Data/User/Email")
                .ValidateFieldExists("ApiResponse/Message")
                .Validate("ApiResponse/Success", true)
                .Validate("ApiResponse/Data/User/Name", "Test User")
                .ValidateContains("ApiResponse/Data/User/Email", "@test.example.com");
        });

        AttachValidationResult(
            "XML Core Assertions",
            "Passed: element-existence, exact-value, and contains checks for ApiResponse payload.");
    }

    [Test]
    [Category("High")]
    [AllureStory("XML Response Validation - Complex Paths and Types")]
    [AllureSeverity(SeverityLevel.normal)]
    public void ValidateXmlResponse_ComplexPathsAndTypes_WithDetailedLogging()
    {
        var xmlResponse = LoadXmlResponseFromTestData("xml-complex-paths-response.xml");

        const string validationPlan = "1) Validate indexed XPath-style element values.\n2) Validate integer type conversion on pagination and seat counts.\n3) Validate not-exists assertion for removed node.";
        AttachValidationContext("XML Complex Paths and Types", xmlResponse, validationPlan);

        AllureApi.Step("Execute XML complex-path and type assertions", () =>
        {
            ResponseValidator
                .FromContent(xmlResponse, "application/xml")
                .Validate("ApiResponse/Data/Events/Event[1]/Title", "Tech Conference 2026")
                .Validate("ApiResponse/Data/Events/Event[2]/Title", "React Workshop")
                .ValidateType("ApiResponse/Data/Pagination/Page", typeof(int))
                .ValidateType("ApiResponse/Data/Events/Event[1]/TotalSeats", typeof(int))
                .ValidateFieldNotExists("ApiResponse/Data/Events/Event[1]/DeletedAt");
        });

        AttachValidationResult(
            "XML Complex Paths and Types",
            "Passed: indexed-path, type, and not-exists assertions for nested ApiResponse/Data/Events payload.");
    }

    [Test]
    [Category("Medium")]
    [AllureStory("XML Response Validation - Auto Detection and Error Shape")]
    [AllureSeverity(SeverityLevel.normal)]
    public void ValidateXmlResponse_AutoDetectionAndErrorShape_WithDetailedLogging()
    {
        var xmlErrorResponse = LoadXmlResponseFromTestData("xml-error-shape-response.xml");

        const string validationPlan = "1) Validate XML auto-detection when content-type is omitted.\n2) Validate error payload fields and values.\n3) Validate raw content and content-type accessors.";
        AttachValidationContext("XML Auto Detection and Error Shape", xmlErrorResponse, validationPlan);

        AllureApi.Step("Execute XML auto-detection and error-shape assertions", () =>
        {
            ResponseValidator
                .FromContent(xmlErrorResponse)
                .Validate("ApiResponse/Success", false)
                .ValidateFieldExists("ApiResponse/Error/Code")
                .Validate("ApiResponse/Error/Code", "INVALID_PAYLOAD")
                .ValidateContains("ApiResponse/Error/Message", "Validation")
                .ValidateContains("ApiResponse/Error/Details/Issue", "required");

            var validator = ResponseValidator.FromContent(xmlErrorResponse, "application/xml");
            Assert.That(validator.GetRawContent(), Is.Not.Null.And.Not.Empty);
            Assert.That(validator.GetRawContent(), Contains.Substring("<ApiResponse>"));
            Assert.That(validator.GetContentType(), Is.EqualTo("application/xml"));
        });

        AttachValidationResult(
            "XML Auto Detection and Error Shape",
            "Passed: format auto-detection, error payload assertions, and raw content/content-type checks.");
    }
}
