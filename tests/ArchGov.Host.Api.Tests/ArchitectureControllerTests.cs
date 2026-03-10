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

    [Fact]
    public async Task PostGenerate_ShouldReturnGeneratedCode()
    {
        var request = new
        {
            solutionName = "TestApp",
            components = new[]
            {
                new { componentRole = "Entity", name = "Order" },
            },
        };
        var response = await _client.PostAsJsonAsync("/v1.0/architecture/generate", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Order");
        content.Should().Contain("success");
    }

    [Fact]
    public async Task PostGenerateWorkspace_ShouldReturnProjectPaths()
    {
        var request = new
        {
            solutionName = "TestApp",
            features = new[] { "Orders" },
        };
        var response = await _client.PostAsJsonAsync("/v1.0/architecture/generate-workspace", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("TestApp.Orders.Domain");
    }

    [Fact]
    public async Task PostGenerateWorkspace_MultipleFeatures_ShouldReturnAllProjects()
    {
        var request = new
        {
            solutionName = "TestApp",
            features = new[] { "Orders", "Users" },
        };
        var response = await _client.PostAsJsonAsync("/v1.0/architecture/generate-workspace", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("TestApp.Orders.Domain");
        content.Should().Contain("TestApp.Users.Domain");
        content.Should().Contain("TestApp.Orders.Application");
        content.Should().Contain("TestApp.Users.Application");
    }

    [Fact]
    public async Task PostValidate_EmptyDirectory_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-apitest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var request = new
            {
                sourceDirectory = tempDir,
                template = "clean-architecture",
            };
            var response = await _client.PostAsJsonAsync("/v1.0/architecture/validate", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"isValid\":true");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PostValidate_WithViolation_ShouldReturnInvalid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-apitest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Domain referencing Infrastructure — a dependency violation
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Order.cs"), """
                using MyApp.Orders.Infrastructure;

                namespace MyApp.Orders.Domain;

                public class Order { }
                """);

            var infraDir = Path.Combine(tempDir, "MyApp.Orders.Infrastructure");
            Directory.CreateDirectory(infraDir);
            File.WriteAllText(Path.Combine(infraDir, "Db.cs"), """
                namespace MyApp.Orders.Infrastructure;

                public class Db { }
                """);

            var request = new
            {
                sourceDirectory = tempDir,
                template = "clean-architecture",
            };
            var response = await _client.PostAsJsonAsync("/v1.0/architecture/validate", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"isValid\":false");
            content.Should().Contain("DependencyViolation");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PostLint_EmptyDirectory_ShouldReturnClean()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-apitest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var request = new
            {
                sourceDirectory = tempDir,
                template = "clean-architecture",
            };
            var response = await _client.PostAsJsonAsync("/v1.0/architecture/lint", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"isClean\":true");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PostLint_WithMismatch_ShouldReturnViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-apitest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // File in Domain directory but namespace says Infrastructure
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Misplaced.cs"), """
                namespace MyApp.Orders.Infrastructure;

                public class Misplaced { }
                """);

            var request = new
            {
                sourceDirectory = tempDir,
                template = "clean-architecture",
            };
            var response = await _client.PostAsJsonAsync("/v1.0/architecture/lint", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"isClean\":false");
            content.Should().Contain("NamespaceMismatch");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetLayers_WithTemplate_ShouldReturnTemplateSpecificLayers()
    {
        var response = await _client.GetAsync("/v1.0/architecture/layers?template=hexagonal-architecture");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Core");
        content.Should().Contain("Ports");
        content.Should().Contain("Adapters");
    }
}
