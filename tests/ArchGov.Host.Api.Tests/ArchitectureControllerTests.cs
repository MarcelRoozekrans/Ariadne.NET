using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArchGov.Host.Api.Tests;

public class ArchitectureControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ArchitectureControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTemplates_ShouldReturnBuiltinPacks()
    {
        var response = await _client.GetAsync("/v1.0/architecture/templates");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await response.Content.ReadFromJsonAsync<List<string>>();
        templates.Should().Contain("clean-architecture");
    }

    [Fact]
    public async Task GetLayers_ShouldReturnLayers()
    {
        var response = await _client.GetAsync("/v1.0/architecture/layers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetComponentRoles_ShouldReturnRoles()
    {
        var response = await _client.GetAsync("/v1.0/architecture/component-roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRules_ShouldReturnRules()
    {
        var response = await _client.GetAsync("/v1.0/architecture/rules");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
