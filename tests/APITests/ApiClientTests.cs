using Allure.NUnit;
using Allure.NUnit.Attributes;
using FluentAssertions;
using Framework.API;
using Framework.Core.Configuration;
using Framework.Reporting;

namespace APITests;

[AllureNUnit]
[AllureParentSuite("APITests")]
[AllureSuite("Api Client")]
[AllureFeature("HTTP")]
public class ApiClientTests : ApiTestBase
{
    [Test]
    [Priority(TestPriority.Medium)]
    [AllureStory("Successful GET request")]
    public async Task GetRequest_ShouldReturnSuccessfulResponse()
    {
        var apiBaseUrl = ConfigManager.GetString("Api:BaseUrl");
        using var client = new ApiClient(apiBaseUrl);
        var response = await client.SendAsync(
            new ApiRequestBuilder()
                .WithMethod(HttpMethod.Get)
                .WithEndpoint("get?framework=selenium")
                .Build());

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
