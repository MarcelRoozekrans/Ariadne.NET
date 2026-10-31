using Ariadne.NET.CodeGeneration;
using Ariadne.NET.RuleSchema;
using FluentAssertions;

namespace Ariadne.NET.Tests.CodeGeneration;

public sealed class CodeGeneratorServiceTests
{
    private static RulePack CreateTestPack() => new()
    {
        Name = "test",
        ComponentRoles =
        [
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class },
            new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface },
            new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
            new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
        ],
        NamingConventions = new NamingConventions
        {
            Namespace = "{SolutionName}.{Feature}.{Layer}",
            FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
        },
    };

    private readonly CodeGeneratorService _sut = new();

    [Fact]
    public void Generate_Entity_ProducesPublicClass()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "Order",
            RoleName = "Entity",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.Code.Should().Contain("public class Order");
        result.TypeName.Should().Be("Order");
    }

    [Fact]
    public void Generate_RepositoryInterface_ProducesInterface()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "OrderRepository",
            RoleName = "RepositoryInterface",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.Code.Should().Contain("public interface IOrderRepository");
        result.TypeName.Should().Be("IOrderRepository");
    }

    [Fact]
    public void Generate_Command_ProducesRecord()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "CreateOrderCommand",
            RoleName = "Command",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.Code.Should().Contain("public record CreateOrderCommand");
        result.TypeName.Should().Be("CreateOrderCommand");
    }

    [Fact]
    public void Generate_Repository_ProducesInternalClassWithInterface()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "OrderRepository",
            RoleName = "Repository",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.Code.Should().Contain("internal class OrderRepository");
        result.Code.Should().Contain(": IOrderRepository");
        result.TypeName.Should().Be("OrderRepository");
    }

    [Fact]
    public void Generate_NamespaceFollowsConvention()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "Order",
            RoleName = "Entity",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.Namespace.Should().Be("MyApp.Orders.Domain");
        result.Code.Should().Contain("namespace MyApp.Orders.Domain");
    }

    [Fact]
    public void Generate_FilePathFollowsConvention()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "Order",
            RoleName = "Entity",
        };

        var result = _sut.Generate(CreateTestPack(), request);

        result.FilePath.Should().Be("src/MyApp.Orders.Domain/Order.cs");
    }

    [Fact]
    public void Generate_UnknownRole_ThrowsArgumentException()
    {
        var request = new CodeGenerationRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            ComponentName = "Order",
            RoleName = "NonExistentRole",
        };

        var act = () => _sut.Generate(CreateTestPack(), request);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("request");
    }
}
