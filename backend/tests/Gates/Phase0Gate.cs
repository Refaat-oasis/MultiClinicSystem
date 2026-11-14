using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gates;

public sealed class Phase0Gate : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public Phase0Gate(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Gate_01_BackendApi_HealthCheck_ReturnsHealthyAndRequestId()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Request-Id", out var requestIds).Should().BeTrue();
        requestIds.Should().NotBeNullOrEmpty();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Gate_02_HostBasedRouting_AdminRoutesOnClinicHost_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Host", "app.clinic.local");

        // Act
        var response = await client.GetAsync("/platform/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Gate_03_HostBasedRouting_ClinicTenantRoutesOnAdminHost_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Type", "admin");

        // Act
        var response = await client.GetAsync("/c/al-amal/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

