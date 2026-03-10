# MCP Server Integration — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Expose the Architecture Governance Engine as an MCP server (stdio + HTTP) so AI agents can enforce architecture rules during code generation.

**Architecture:** New `ArchGov.Host.Mcp` console app with stdio transport + MCP HTTP transport added to existing `ArchGov.Host.Api`. Tools implemented as `[McpServerToolType]` classes with DI parameter injection. Resources expose built-in pack YAML content.

**Tech Stack:** ModelContextProtocol 1.1.0 (NuGet), ModelContextProtocol.AspNetCore 1.1.0, .NET 10

**Design doc:** `docs/plans/2026-03-10-mcp-server-integration-design.md`

---

### Task 1: Create ArchGov.Host.Mcp project scaffold

**Files:**
- Create: `src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj`
- Create: `src/ArchGov.Host.Mcp/Program.cs`
- Modify: `src/ArchGov.slnx` (add project)

**Step 1: Create project and add to solution**

```bash
cd src
dotnet new console -n ArchGov.Host.Mcp --framework net10.0
cd ..
dotnet sln src/ArchGov.slnx add src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj
```

**Step 2: Add NuGet packages and project reference**

```bash
dotnet add src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj package ModelContextProtocol --version 1.1.0
dotnet add src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj reference src/ArchGov.Core/ArchGov.Core.csproj
```

**Step 3: Write Program.cs**

```csharp
using ArchGov.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddArchGovCore();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();
```

**Step 4: Verify it builds**

Run: `dotnet build src/ArchGov.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

**Step 5: Commit**

```bash
git add src/ArchGov.Host.Mcp/ src/ArchGov.slnx
git commit -m "feat: scaffold ArchGov.Host.Mcp stdio server project"
```

---

### Task 2: Implement rule resolution helper

All MCP tools need to resolve rules from either an explicit template parameter or `.architecture/rules.yaml`. Extract this into a shared helper.

**Files:**
- Create: `src/ArchGov.Host.Mcp/RuleResolutionHelper.cs`

**Step 1: Write the helper**

```csharp
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Host.Mcp;

public sealed class RuleResolutionHelper
{
    private readonly IBuiltinPackRegistry _registry;
    private readonly IRulePackResolver _resolver;
    private readonly IRulePackLoader _loader;

    public RuleResolutionHelper(
        IBuiltinPackRegistry registry,
        IRulePackResolver resolver,
        IRulePackLoader loader)
    {
        _registry = registry;
        _resolver = resolver;
        _loader = loader;
    }

    public RulePack Resolve(string? template)
    {
        if (template is not null)
        {
            var userPack = new RulePack
            {
                Name = template,
                Extends = [$"builtin://{template}"],
            };
            return _resolver.Resolve(userPack);
        }

        var rulesPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".architecture",
            "rules.yaml");

        if (File.Exists(rulesPath))
        {
            var pack = _loader.LoadFromFile(rulesPath);
            return _resolver.Resolve(pack);
        }

        var defaultPack = new RulePack
        {
            Name = "clean-architecture",
            Extends = ["builtin://clean-architecture"],
        };
        return _resolver.Resolve(defaultPack);
    }
}
```

**Step 2: Register in DI — update Program.cs**

Add after `builder.Services.AddArchGovCore();`:
```csharp
builder.Services.AddSingleton<RuleResolutionHelper>();
```

**Step 3: Verify it builds**

Run: `dotnet build src/ArchGov.Host.Mcp`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/ArchGov.Host.Mcp/
git commit -m "feat: add RuleResolutionHelper for template auto-detection"
```

---

### Task 3: Implement list_templates and get_layers tools

**Files:**
- Create: `src/ArchGov.Host.Mcp/Tools/TemplateTools.cs`
- Create: `tests/ArchGov.Host.Mcp.Tests/ArchGov.Host.Mcp.Tests.csproj`
- Create: `tests/ArchGov.Host.Mcp.Tests/Tools/TemplateToolsTests.cs`

**Step 1: Create test project**

```bash
cd tests
dotnet new xunit -n ArchGov.Host.Mcp.Tests --framework net10.0
cd ..
dotnet sln src/ArchGov.slnx add tests/ArchGov.Host.Mcp.Tests/ArchGov.Host.Mcp.Tests.csproj
dotnet add tests/ArchGov.Host.Mcp.Tests reference src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj
dotnet add tests/ArchGov.Host.Mcp.Tests reference src/ArchGov.Core/ArchGov.Core.csproj
dotnet add tests/ArchGov.Host.Mcp.Tests package FluentAssertions
```

**Step 2: Write the failing tests**

```csharp
using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class TemplateToolsTests
{
    private readonly BuiltinPackRegistry _registry;

    public TemplateToolsTests()
    {
        _registry = new BuiltinPackRegistry(new YamlRulePackLoader());
    }

    [Fact]
    public void ListTemplates_ShouldReturnAllBuiltinPacks()
    {
        var result = TemplateTools.ListTemplates(_registry);

        result.Should().Contain("clean-architecture");
        result.Should().Contain("hexagonal-architecture");
        result.Should().HaveCountGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void GetLayers_WithExplicitTemplate_ShouldReturnLayers()
    {
        var helper = new RuleResolutionHelper(
            _registry,
            new RulePackResolver(_registry, new RulePackMerger()),
            new YamlRulePackLoader());

        var result = TemplateTools.GetLayers(helper, "clean-architecture");

        result.Should().Contain(l => l.Name == "Domain");
        result.Should().Contain(l => l.Name == "Application");
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: FAIL — `TemplateTools` class does not exist.

**Step 4: Implement TemplateTools**

```csharp
using System.ComponentModel;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tools;

[McpServerToolType]
public static class TemplateTools
{
    [McpServerTool("list_templates")]
    [Description("List all available architecture templates (e.g. clean-architecture, hexagonal-architecture, cqrs).")]
    public static IReadOnlyList<string> ListTemplates(IBuiltinPackRegistry registry)
    {
        return registry.GetAvailablePackNames();
    }

    [McpServerTool("get_layers")]
    [Description("Get architecture layer definitions with allowed dependencies. If no template specified, uses .architecture/rules.yaml or defaults to clean-architecture.")]
    public static IList<LayerDefinition> GetLayers(
        RuleResolutionHelper helper,
        [Description("Template name (e.g. 'clean-architecture'). Optional — auto-detects from .architecture/rules.yaml if omitted.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.Layers;
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: PASS (2 tests).

**Step 6: Commit**

```bash
git add src/ArchGov.Host.Mcp/Tools/ tests/ArchGov.Host.Mcp.Tests/
git commit -m "feat: add list_templates and get_layers MCP tools"
```

---

### Task 4: Implement get_component_roles and get_rules tools

**Files:**
- Create: `src/ArchGov.Host.Mcp/Tools/RuleTools.cs`
- Create: `tests/ArchGov.Host.Mcp.Tests/Tools/RuleToolsTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleEngine;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class RuleToolsTests
{
    private readonly RuleResolutionHelper _helper;

    public RuleToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, new RulePackMerger()),
            loader);
    }

    [Fact]
    public void GetComponentRoles_ShouldReturnRolesForTemplate()
    {
        var result = RuleTools.GetComponentRoles(_helper, "clean-architecture");

        result.Should().Contain(r => r.Name == "Entity");
        result.Should().Contain(r => r.Name == "Repository");
    }

    [Fact]
    public void GetRules_ShouldReturnCodeFeatureRules()
    {
        var result = RuleTools.GetRules(_helper, "clean-architecture");

        result.Should().NotBeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: FAIL — `RuleTools` does not exist.

**Step 3: Implement RuleTools**

```csharp
using System.ComponentModel;
using ArchGov.Core.RuleSchema;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tools;

[McpServerToolType]
public static class RuleTools
{
    [McpServerTool("get_component_roles")]
    [Description("Get available component roles (Entity, Repository, Controller, etc.) with their layer, type kind, and accessibility. Use this to know what roles are valid for generate_component.")]
    public static IList<ComponentRoleDefinition> GetComponentRoles(
        RuleResolutionHelper helper,
        [Description("Template name. Optional — auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.ComponentRoles;
    }

    [McpServerTool("get_rules")]
    [Description("Get code feature rules that enforce structural constraints (e.g. interface requirements, accessibility rules).")]
    public static IList<CodeFeatureRuleDefinition> GetRules(
        RuleResolutionHelper helper,
        [Description("Template name. Optional — auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.CodeFeatureRules;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: PASS (4 tests).

**Step 5: Commit**

```bash
git add src/ArchGov.Host.Mcp/Tools/ tests/ArchGov.Host.Mcp.Tests/
git commit -m "feat: add get_component_roles and get_rules MCP tools"
```

---

### Task 5: Implement generate_component tool

**Files:**
- Create: `src/ArchGov.Host.Mcp/Tools/GenerateTools.cs`
- Create: `tests/ArchGov.Host.Mcp.Tests/Tools/GenerateToolsTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class GenerateToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly GenerateOrchestrator _orchestrator;

    public GenerateToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, new RulePackMerger()),
            loader);
        _orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());
    }

    [Fact]
    public void GenerateComponent_ShouldReturnGeneratedCode()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "Entity", name: "Order",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain("class Order");
        result.GeneratedFiles[0].Namespace.Should().Be("MyApp.Orders.Domain");
    }

    [Fact]
    public void GenerateComponent_UnknownRole_ShouldReturnError()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "NonExistent", name: "Foo",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: FAIL — `GenerateTools` does not exist.

**Step 3: Implement GenerateTools**

```csharp
using System.ComponentModel;
using ArchGov.Core.Orchestration;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tools;

[McpServerToolType]
public static class GenerateTools
{
    [McpServerTool("generate_component")]
    [Description("Generate a C# component (class, interface, record, etc.) that conforms to architecture rules. Returns the file path and generated source code.")]
    public static GenerateResult GenerateComponent(
        RuleResolutionHelper helper,
        IGenerateOrchestrator orchestrator,
        [Description("Component role (e.g. Entity, Repository, Controller, CommandHandler). Use get_component_roles to see available roles.")]
        string role,
        [Description("Component name (e.g. Order, OrderRepository, OrdersController).")]
        string name,
        [Description("Solution name (e.g. MyApp). Used for namespace generation.")]
        string solution,
        [Description("Feature name (e.g. Orders, Users). Used for namespace generation.")]
        string feature,
        [Description("Template name. Optional — auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);

        var request = new GenerateRequest
        {
            SolutionName = solution,
            Feature = feature,
            Components =
            [
                new ComponentRequest { RoleName = role, Name = name },
            ],
        };

        return orchestrator.Generate(resolved, request);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: PASS (6 tests).

**Step 5: Commit**

```bash
git add src/ArchGov.Host.Mcp/Tools/ tests/ArchGov.Host.Mcp.Tests/
git commit -m "feat: add generate_component MCP tool"
```

---

### Task 6: Implement validate_source and lint_source tools

**Files:**
- Create: `src/ArchGov.Host.Mcp/Tools/ValidationTools.cs`
- Create: `tests/ArchGov.Host.Mcp.Tests/Tools/ValidationToolsTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.Validation;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SourceValidator _validator;
    private readonly Linter _linter;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, new RulePackMerger()),
            loader);
        _validator = new SourceValidator();
        _linter = new Linter();
    }

    [Fact]
    public void ValidateSource_EmptyDir_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsValid.Should().BeTrue();
            result.FilesScanned.Should().Be(0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LintSource_EmptyDir_ShouldReturnClean()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.LintSource(
                _helper, _linter,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsClean.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: FAIL — `ValidationTools` does not exist.

**Step 3: Implement ValidationTools**

```csharp
using System.ComponentModel;
using ArchGov.Core.Validation;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tools;

[McpServerToolType]
public static class ValidationTools
{
    [McpServerTool("validate_source")]
    [Description("Validate existing source files against architecture dependency rules. Checks that using directives comply with layer dependency constraints (e.g. Domain cannot reference Infrastructure).")]
    public static SourceValidationResult ValidateSource(
        RuleResolutionHelper helper,
        ISourceValidator validator,
        [Description("Absolute or relative path to the source directory to validate.")]
        string sourceDirectory,
        [Description("Template name. Optional — auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return validator.Validate(resolved, sourceDirectory);
    }

    [McpServerTool("lint_source")]
    [Description("Lint source files for namespace-vs-directory layer mismatches. Lighter than validate — checks that namespace segments match the directory-based layer structure.")]
    public static LintResult LintSource(
        RuleResolutionHelper helper,
        ILinter linter,
        [Description("Absolute or relative path to the source directory to lint.")]
        string sourceDirectory,
        [Description("Template name. Optional — auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return linter.Lint(resolved, sourceDirectory);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: PASS (8 tests).

**Step 5: Commit**

```bash
git add src/ArchGov.Host.Mcp/Tools/ tests/ArchGov.Host.Mcp.Tests/
git commit -m "feat: add validate_source and lint_source MCP tools"
```

---

### Task 7: Implement MCP resources for rule packs

**Files:**
- Create: `src/ArchGov.Host.Mcp/Resources/PackResources.cs`
- Create: `tests/ArchGov.Host.Mcp.Tests/Resources/PackResourcesTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Host.Mcp.Resources;

namespace ArchGov.Host.Mcp.Tests.Resources;

public class PackResourcesTests
{
    private readonly BuiltinPackRegistry _registry;
    private readonly RuleResolutionHelper _helper;

    public PackResourcesTests()
    {
        var loader = new YamlRulePackLoader();
        _registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            _registry,
            new RulePackResolver(_registry, new RulePackMerger()),
            loader);
    }

    [Fact]
    public void GetPackContent_CleanArchitecture_ShouldReturnYaml()
    {
        var result = PackResources.GetPackContent(_registry, "clean-architecture");

        result.Should().Contain("name: clean-architecture");
        result.Should().Contain("layers:");
        result.Should().Contain("componentRoles:");
    }

    [Fact]
    public void GetPackContent_UnknownPack_ShouldThrow()
    {
        var act = () => PackResources.GetPackContent(_registry, "nonexistent");

        act.Should().Throw<KeyNotFoundException>();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: FAIL — `PackResources` does not exist.

**Step 3: Implement PackResources**

The `IBuiltinPackRegistry` returns parsed `RulePack` objects, not raw YAML. We need to read the embedded resource YAML directly. Add a method to get the raw YAML content.

First, check if `IBuiltinPackRegistry` or `BuiltinPackRegistry` exposes raw YAML. If not, read the embedded resource directly in the MCP resource class:

```csharp
using System.ComponentModel;
using System.Reflection;
using ArchGov.Core.RuleLoader;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Resources;

[McpServerResourceType]
public static class PackResources
{
    [McpServerResource(UriTemplate = "archgov://packs/{name}")]
    [Description("Get the full YAML content of a built-in architecture rule pack.")]
    public static string GetPackContent(
        IBuiltinPackRegistry registry,
        [Description("Pack name (e.g. clean-architecture, hexagonal-architecture, cqrs)")]
        string name)
    {
        // Validate pack exists (throws KeyNotFoundException if not found)
        registry.GetPack(name);

        // Read raw YAML from embedded resource
        var assembly = typeof(BuiltinPackRegistry).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r =>
                r.EndsWith($".{name}.yaml", StringComparison.OrdinalIgnoreCase)
                && r.Contains("BuiltinPacks", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource for pack '{name}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests`
Expected: PASS (10 tests).

**Step 5: Commit**

```bash
git add src/ArchGov.Host.Mcp/Resources/ tests/ArchGov.Host.Mcp.Tests/
git commit -m "feat: add MCP resources for built-in rule pack YAML content"
```

---

### Task 8: Add MCP HTTP transport to ArchGov.Host.Api

**Files:**
- Modify: `src/ArchGov.Host.Api/ArchGov.Host.Api.csproj`
- Modify: `src/ArchGov.Host.Api/Program.cs`

**Step 1: Add NuGet package and project reference**

The API needs the ASP.NET Core MCP package, plus a reference to the Mcp host project for the tool/resource classes:

```bash
dotnet add src/ArchGov.Host.Api/ArchGov.Host.Api.csproj package ModelContextProtocol.AspNetCore --version 1.1.0
dotnet add src/ArchGov.Host.Api/ArchGov.Host.Api.csproj reference src/ArchGov.Host.Mcp/ArchGov.Host.Mcp.csproj
```

**Step 2: Update Program.cs to add MCP endpoint**

Add after `builder.Services.AddArchGovCore();`:

```csharp
builder.Services.AddSingleton<ArchGov.Host.Mcp.RuleResolutionHelper>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(ArchGov.Host.Mcp.Tools.TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(ArchGov.Host.Mcp.Resources.PackResources).Assembly);
```

Add after `app.MapControllers();`:

```csharp
app.MapMcp();
```

**Step 3: Verify it builds**

Run: `dotnet build src/ArchGov.slnx`
Expected: Build succeeded, 0 warnings.

**Step 4: Commit**

```bash
git add src/ArchGov.Host.Api/
git commit -m "feat: add MCP HTTP transport to API host"
```

---

### Task 9: Add smoke test for stdio MCP server

**Files:**
- Create: `tests/ArchGov.Host.Mcp.Tests/McpServerSmokeTests.cs`

**Step 1: Write the smoke test**

This test starts the MCP server in-process using the `ModelContextProtocol` client SDK and verifies tools are listed:

```csharp
using FluentAssertions;
using ArchGov.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tests;

public class McpServerSmokeTests
{
    [Fact]
    public async Task Server_ShouldListAllTools()
    {
        await using var server = CreateInProcessServer();
        var tools = await server.ListToolsAsync();

        var toolNames = tools.Select(t => t.Name).ToList();
        toolNames.Should().Contain("list_templates");
        toolNames.Should().Contain("get_layers");
        toolNames.Should().Contain("get_component_roles");
        toolNames.Should().Contain("get_rules");
        toolNames.Should().Contain("generate_component");
        toolNames.Should().Contain("validate_source");
        toolNames.Should().Contain("lint_source");
    }

    [Fact]
    public async Task Server_ShouldCallListTemplates()
    {
        await using var server = CreateInProcessServer();
        var result = await server.CallToolAsync("list_templates");

        result.Content.Should().NotBeEmpty();
        var text = result.Content[0].Text;
        text.Should().Contain("clean-architecture");
    }

    private static IMcpClient CreateInProcessServer()
    {
        return McpClientFactory.CreateForServer(new McpServerConfig
        {
            Id = "archgov-test",
            Name = "ArchGov Test Server",
            TransportType = "stdio",
            Location = "dotnet",
            Arguments = ["run", "--project",
                Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "../../../../../../src/ArchGov.Host.Mcp"))],
        });
    }
}
```

> **Note:** The exact `McpClientFactory` API may differ. If the SDK uses a different pattern for in-process testing (e.g. `InProcessTransport`), adapt accordingly. Check the SDK docs for the recommended testing approach. An alternative is to use `WithInProcessClientTransport()` if available.

**Step 2: Run the smoke test**

Run: `dotnet test tests/ArchGov.Host.Mcp.Tests --filter "FullyQualifiedName~McpServerSmokeTests"`
Expected: PASS (2 tests). May need adjustment based on actual SDK client API.

**Step 3: Commit**

```bash
git add tests/ArchGov.Host.Mcp.Tests/
git commit -m "test: add MCP server smoke tests"
```

---

### Task 10: Update README and documentation

**Files:**
- Modify: `README.md`

**Step 1: Add MCP section to README**

Add a new section after the CLI section:

```markdown
### MCP Server

The engine runs as an MCP server for AI agent integration:

**Stdio (local — Claude Code, Cursor, etc.):**

Add to your `.mcp.json`:
```json
{
  "mcpServers": {
    "archgov": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/ArchGov.Host.Mcp"]
    }
  }
}
```

**HTTP (remote/team):**

Start the API server — MCP endpoint is available at `/mcp`:
```bash
dotnet run --project src/ArchGov.Host.Api
```

**Available MCP Tools:**

| Tool | Description |
|------|-------------|
| `list_templates` | List available architecture templates |
| `get_layers` | Get layer definitions with allowed dependencies |
| `get_component_roles` | Get available component roles |
| `get_rules` | Get code feature rules |
| `generate_component` | Generate a C# component |
| `validate_source` | Validate source files against rules |
| `lint_source` | Lint for namespace-layer mismatches |

**MCP Resources:**

| Resource | Description |
|----------|-------------|
| `archgov://packs/{name}` | Full YAML content of a built-in rule pack |
```

**Step 2: Update architecture diagram and test counts**

Update the project structure to include `ArchGov.Host.Mcp` and update test counts.

**Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add MCP server section to README"
```

---

## Summary

| Task | What | Tests |
|------|------|-------|
| 1 | Project scaffold + Program.cs | Build check |
| 2 | RuleResolutionHelper | Build check |
| 3 | list_templates + get_layers | 2 unit tests |
| 4 | get_component_roles + get_rules | 2 unit tests |
| 5 | generate_component | 2 unit tests |
| 6 | validate_source + lint_source | 2 unit tests |
| 7 | MCP resources (pack YAML) | 2 unit tests |
| 8 | HTTP transport in API | Build check |
| 9 | Smoke tests | 2 integration tests |
| 10 | README + docs | — |

**Total new tests:** ~12
**Total estimated tasks:** 10
