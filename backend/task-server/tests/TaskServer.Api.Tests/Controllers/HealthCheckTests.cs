using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskServer.Api.Tests.Controllers;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
