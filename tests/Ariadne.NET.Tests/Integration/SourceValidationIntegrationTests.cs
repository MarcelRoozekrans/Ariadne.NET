using Ariadne.NET.RuleLoader;
using Ariadne.NET.Validation;
using FluentAssertions;

namespace Ariadne.NET.Tests.Integration;

/// <summary>
/// Integration tests that validate realistic project structures against the
/// real clean-architecture built-in pack. Each test scaffolds a mini project
/// in a temp directory and runs the full SourceValidator pipeline.
/// </summary>
public sealed class SourceValidationIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SourceValidator _validator;
    private readonly BuiltinPackRegistry _registry;

    public SourceValidationIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-inttest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _validator = new SourceValidator();
        _registry = new BuiltinPackRegistry(new YamlRulePackLoader());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void CleanArchProject_AllCorrect_ShouldPassValidation()
    {
        // Scaffold a correct clean-architecture project
        WriteFile("MyApp.Orders.Domain", "Order.cs", """
            namespace MyApp.Orders.Domain;

            public class Order
            {
                public Guid Id { get; init; }
                public string Description { get; init; } = "";
            }
            """);

        WriteFile("MyApp.Orders.Domain", "IOrderRepository.cs", """
            namespace MyApp.Orders.Domain;

            public interface IOrderRepository
            {
                Order? GetById(Guid id);
            }
            """);

        WriteFile("MyApp.Orders.Application", "CreateOrderCommand.cs", """
            namespace MyApp.Orders.Application;

            public record CreateOrderCommand(string Description);
            """);

        WriteFile("MyApp.Orders.Application", "CreateOrderHandler.cs", """
            using MyApp.Orders.Domain;

            namespace MyApp.Orders.Application;

            public class CreateOrderHandler
            {
                private readonly IOrderRepository _repo;
                public CreateOrderHandler(IOrderRepository repo) => _repo = repo;
            }
            """);

        WriteFile("MyApp.Orders.Infrastructure", "OrderRepository.cs", """
            using MyApp.Orders.Domain;

            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository : IOrderRepository
            {
                public Order? GetById(Guid id) => null;
            }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
        result.FilesScanned.Should().Be(5);
    }

    [Fact]
    public void CleanArchProject_DomainDependsOnInfrastructure_ShouldReportDependencyViolation()
    {
        WriteFile("MyApp.Orders.Domain", "Order.cs", """
            using MyApp.Orders.Infrastructure;

            namespace MyApp.Orders.Domain;

            public class Order { }
            """);

        WriteFile("MyApp.Orders.Infrastructure", "DbContext.cs", """
            namespace MyApp.Orders.Infrastructure;

            public class OrderDbContext { }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v =>
            v.ViolationType == "DependencyViolation"
            && v.Message.Contains("Domain")
            && v.Message.Contains("Infrastructure"));
    }

    [Fact]
    public void CleanArchProject_DtoIsClass_ShouldReportTypeKindMismatch()
    {
        // DTOs should be records in clean-architecture
        WriteFile("MyApp.Orders.Application", "OrderDto.cs", """
            namespace MyApp.Orders.Application;

            public class OrderDto
            {
                public string Description { get; set; } = "";
            }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.Violations.Should().Contain(v =>
            v.ViolationType == "TypeKindMismatch"
            && v.Message.Contains("OrderDto")
            && v.Message.Contains("Record"));
    }

    [Fact]
    public void CleanArchProject_PublicRepository_ShouldReportAccessibilityMismatch()
    {
        // Repositories should be internal in clean-architecture
        WriteFile("MyApp.Orders.Domain", "IOrderRepository.cs", """
            namespace MyApp.Orders.Domain;

            public interface IOrderRepository { }
            """);

        WriteFile("MyApp.Orders.Infrastructure", "OrderRepository.cs", """
            using MyApp.Orders.Domain;

            namespace MyApp.Orders.Infrastructure;

            public class OrderRepository : IOrderRepository { }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.Violations.Should().Contain(v =>
            v.ViolationType == "AccessibilityMismatch"
            && v.Message.Contains("OrderRepository")
            && v.Message.Contains("internal"));
    }

    [Fact]
    public void CleanArchProject_RepositoryWithoutInterface_ShouldReportMissingInterface()
    {
        // Repository exists but no matching interface in Domain
        WriteFile("MyApp.Orders.Infrastructure", "OrderRepository.cs", """
            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository { }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.Violations.Should().Contain(v =>
            v.ViolationType == "MissingInterface"
            && v.Message.Contains("OrderRepository")
            && v.Message.Contains("RepositoryInterface"));
    }

    [Fact]
    public void CleanArchProject_MultipleViolations_ShouldReportAll()
    {
        // DomainEvent as class (wrong kind) + public repo (wrong accessibility) + no interface
        WriteFile("MyApp.Orders.Domain", "OrderCreatedDomainEvent.cs", """
            namespace MyApp.Orders.Domain;

            public class OrderCreatedDomainEvent { }
            """);

        WriteFile("MyApp.Orders.Infrastructure", "OrderRepository.cs", """
            namespace MyApp.Orders.Infrastructure;

            public class OrderRepository { }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.ViolationType == "TypeKindMismatch");
        result.Violations.Should().Contain(v => v.ViolationType == "AccessibilityMismatch");
        result.Violations.Should().Contain(v => v.ViolationType == "MissingInterface");
    }

    [Fact]
    public void CleanArchProject_ExternalServicePattern_ShouldValidateCorrectly()
    {
        // ExternalService follows same pattern as Repository: internal + interface required
        WriteFile("MyApp.Orders.Application", "IPaymentGateway.cs", """
            namespace MyApp.Orders.Application;

            public interface IPaymentGateway
            {
                bool Charge(decimal amount);
            }
            """);

        WriteFile("MyApp.Orders.Infrastructure", "PaymentGateway.cs", """
            using MyApp.Orders.Application;

            namespace MyApp.Orders.Infrastructure;

            internal class PaymentGateway : IPaymentGateway
            {
                public bool Charge(decimal amount) => true;
            }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        // ExternalService role: internal + requiresInterface
        // IPaymentGateway matches ExternalServiceInterface role (via alternativeName "ExternalService")
        // PaymentGateway matches ExternalService role
        result.Violations.Where(v =>
            v.Message.Contains("PaymentGateway")).Should().BeEmpty();
    }

    [Fact]
    public void CleanArchProject_UnmatchedTypesAreIgnored_ShouldNotReportViolations()
    {
        // Types that don't match any role suffix should be silently ignored
        WriteFile("MyApp.Orders.Domain", "OrderStatus.cs", """
            namespace MyApp.Orders.Domain;

            public enum OrderStatus { Pending, Completed, Cancelled }
            """);

        WriteFile("MyApp.Orders.Application", "OrderDto.cs", """
            namespace MyApp.Orders.Application;

            public record OrderDto(Guid Id, string Description);
            """);

        WriteFile("MyApp.Orders.Infrastructure", "DatabaseConfig.cs", """
            namespace MyApp.Orders.Infrastructure;

            public class DatabaseConfig
            {
                public string ConnectionString { get; init; } = "";
            }
            """);

        var pack = _registry.GetPack("clean-architecture").Value;
        var result = _validator.Validate(pack, _tempDir);

        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    private void WriteFile(string projectDir, string fileName, string content)
    {
        var dir = Path.Combine(_tempDir, projectDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }
}
