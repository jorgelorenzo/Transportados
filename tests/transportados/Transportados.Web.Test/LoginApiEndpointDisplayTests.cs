using Transportados.Client.Services.Api;

namespace Transportados.Web.Test;

public sealed class LoginApiEndpointDisplayTests
{
    [Fact]
    public void BuildLabel_WhenDisplayFlagIsEnabled_ShouldReturnApiUrlLabel()
    {
        var label = LoginApiEndpointDisplay.BuildLabel(
            showApiBaseUrlOnLogin: true,
            apiBaseUrl: "https://transportados-api-staging.transportados.com/");

        Assert.Equal("API: https://transportados-api-staging.transportados.com/", label);
    }

    [Fact]
    public void BuildLabel_WhenDisplayFlagIsDisabled_ShouldReturnNull()
    {
        var label = LoginApiEndpointDisplay.BuildLabel(
            showApiBaseUrlOnLogin: false,
            apiBaseUrl: "https://transportados-api.transportados.com/");

        Assert.Null(label);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildLabel_WhenDisplayFlagIsEnabledButApiUrlIsMissing_ShouldReturnNull(string apiBaseUrl)
    {
        var label = LoginApiEndpointDisplay.BuildLabel(
            showApiBaseUrlOnLogin: true,
            apiBaseUrl: apiBaseUrl);

        Assert.Null(label);
    }
}
