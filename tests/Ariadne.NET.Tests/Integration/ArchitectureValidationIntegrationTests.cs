using Ariadne.NET.Analysis;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.Integration;

/// <summary>
/// Validates realistic project structures against the real clean-architecture built-in pack.
/// Each test compiles a few in-memory source files and runs the full architecture validator.
/// </summary>
public sealed class ArchitectureValidationIntegrationTests
{
    private readonly ArchitectureValidator _validator = new();
    private readonly BuiltinPackRegistry _registry = new(new YamlRulePackLoader());

    [Fact]
    public void CleanArchProject_AllCorrect_ShouldPassValidation()
    {
        var report = Validate(
            ("Order.cs", """
                using System;
                namespace MyApp.Orders.Domain;

                public class Order
                {
                    public Guid Id { get; init; }
                    public string Description { get; init; } = "";
                }
                """),
            ("IOrderRepository.cs", """
                using System;
                namespace MyApp.Orders.Domain;

                public interface IOrderRepository
                {
                    Order? GetById(Guid id);
                }
                """),
            ("CreateOrderCommand.cs", """
                namespace MyApp.Orders.Application;

                public record CreateOrderCommand(string Description);
                """),
            ("CreateOrderHandler.cs", """
                using MyApp.Orders.Domain;

                namespace MyApp.Orders.Application;

                public class CreateOrderHandler
                {
                    private readonly IOrderRepository _repo;
                    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;
                }
                """),
            ("OrderRepository.cs", """
                using System;
                using MyApp.Orders.Domain;

                namespace MyApp.Orders.Infrastructure;

                internal class OrderRepository : IOrderRepository
                {
                    public Order? GetById(Guid id) => null;
                }
                """));

        report.IsValid.Should().BeTrue();
        report.Violations.Should().BeEmpty();
        report.TypesChecked.Should().Be(5);
    }

    [Fact]
    public void CleanArchProject_DomainDependsOnInfrastructure_ShouldReportLayerViolation()
    {
        var report = Validate(
            ("Order.cs", """
                using MyApp.Orders.Infrastructure;

                namespace MyApp.Orders.Domain;

                public class Order
                {
                    public OrderDbContext? Db { get; set; }
                }
                """),
            ("DbContext.cs", """
                namespace MyApp.Orders.Infrastructure;

                public class OrderDbContext { }
                """));

        report.IsValid.Should().BeFalse();
        report.Violations.Should().ContainSingle(v =>
            v.Kind == ViolationKind.DependencyNotAllowed
            && v.Message.Contains("Domain")
            && v.Message.Contains("Infrastructure"));
    }

    [Fact]
    public void CleanArchProject_DtoIsClass_ShouldReportTypeKindMismatch()
    {
        var report = Validate(("OrderDto.cs", """
            namespace MyApp.Orders.Application;

            public class OrderDto
            {
                public string Description { get; set; } = "";
            }
            """));

        report.Violations.Should().Contain(v =>
            v.Kind == ViolationKind.TypeKindMismatch
            && v.Message.Contains("OrderDto")
            && v.Message.Contains("Record"));
    }

    [Fact]
    public void CleanArchProject_PublicRepository_ShouldReportAccessibilityMismatch()
    {
        var report = Validate(
            ("IOrderRepository.cs", """
                namespace MyApp.Orders.Domain;

                public interface IOrderRepository { }
                """),
            ("OrderRepository.cs", """
                using MyApp.Orders.Domain;

                namespace MyApp.Orders.Infrastructure;

                public class OrderRepository : IOrderRepository { }
                """));

        report.Violations.Should().Contain(v =>
            v.Kind == ViolationKind.AccessibilityMismatch
            && v.Message.Contains("OrderRepository")
            && v.Message.Contains("internal"));
    }

    [Fact]
    public void CleanArchProject_RepositoryWithoutInterface_ShouldReportMissingInterface()
    {
        var report = Validate(("OrderRepository.cs", """
            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository { }
            """));

        report.Violations.Should().Contain(v =>
            v.Kind == ViolationKind.MissingInterface
            && v.Message.Contains("OrderRepository")
            && v.Message.Contains("RepositoryInterface"));
    }

    [Fact]
    public void CleanArchProject_MultipleViolations_ShouldReportAll()
    {
        var report = Validate(
            ("OrderCreatedDomainEvent.cs", """
                namespace MyApp.Orders.Domain;

                public class OrderCreatedDomainEvent { }
                """),
            ("OrderRepository.cs", """
                namespace MyApp.Orders.Infrastructure;

                public class OrderRepository { }
                """));

        report.IsValid.Should().BeFalse();
        report.Violations.Should().Contain(v => v.Kind == ViolationKind.TypeKindMismatch);
        report.Violations.Should().Contain(v => v.Kind == ViolationKind.AccessibilityMismatch);
        report.Violations.Should().Contain(v => v.Kind == ViolationKind.MissingInterface);
    }

    [Fact]
    public void CleanArchProject_ExternalServicePattern_ShouldValidateCorrectly()
    {
        var report = Validate(
            ("IPaymentGateway.cs", """
                namespace MyApp.Orders.Application;

                public interface IPaymentGateway
                {
                    bool Charge(decimal amount);
                }
                """),
            ("PaymentGateway.cs", """
                using MyApp.Orders.Application;

                namespace MyApp.Orders.Infrastructure;

                internal class PaymentGateway : IPaymentGateway
                {
                    public bool Charge(decimal amount) => true;
                }
                """));

        // IPaymentGateway matches the ExternalServiceInterface role through the alternative name ExternalService.
        report.Violations.Where(v => v.Message.Contains("PaymentGateway")).Should().BeEmpty();
    }

    [Fact]
    public void CleanArchProject_UnmatchedTypesAreIgnored_ShouldNotReportViolations()
    {
        var report = Validate(
            ("OrderStatus.cs", """
                namespace MyApp.Orders.Domain;

                public enum OrderStatus { Pending, Completed, Cancelled }
                """),
            ("OrderDto.cs", """
                using System;
                namespace MyApp.Orders.Application;

                public record OrderDto(Guid Id, string Description);
                """),
            ("DatabaseConfig.cs", """
                namespace MyApp.Orders.Infrastructure;

                public class DatabaseConfig
                {
                    public string ConnectionString { get; init; } = "";
                }
                """));

        report.IsValid.Should().BeTrue();
        report.Violations.Should().BeEmpty();
    }

    private ArchitectureReport Validate(params (string Path, string Source)[] files)
    {
        var pack = _registry.GetPack("clean-architecture").Value;
        return _validator.Validate(pack, TestCompilation.Create(files));
    }
}
