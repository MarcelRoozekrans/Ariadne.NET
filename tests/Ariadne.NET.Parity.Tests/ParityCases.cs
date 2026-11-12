using ArchUnitNET.Domain;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ariadne.NET.Parity.Tests;

/// <summary>Every parity case: one per in-scope catalogue row and polarity.</summary>
public static class ParityCases
{
    /// <summary>All cases, in catalogue order.</summary>
    public static IReadOnlyList<ParityCase> All { get; } =
    [
        new(
            "c-not-depend-on-any",
            "CNotDependOnAny",
            "Parity.CNotDependOnAny",
            "PARITY-CNOTDEPENDONANY",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CNotDependOnAny.Broken")
                .Or().ResideInNamespace("Parity.CNotDependOnAny.Clean")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.CNotDependOnAny.Target"))),
        new(
            "p-reside-in-namespace",
            "PResideInNamespace",
            "Parity.PResideInNamespace",
            "PARITY-PRESIDEINNAMESPACE",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.PResideInNamespace.Broken")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PResideInNamespace.Target"))),
        new(
            "p-reside-in-namespace:not",
            "PResideInNamespace",
            "Parity.PResideInNamespace",
            "PARITY-PRESIDEINNAMESPACE-NOT",
            Projection.Types,
            _ => Types().That().DoNotResideInNamespace("Parity.PResideInNamespace.NotClean")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PResideInNamespace.Target"))),
        new(
            "p-reside-in-assembly",
            "PResideInAssembly",
            "Parity.PResideInAssembly",
            "PARITY-PRESIDEINASSEMBLY",
            Projection.Types,
            a => Types().That().ResideInAssembly(AssemblyOf(a, OtherAssembly))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PResideInAssembly.Target"))),
        new(
            "p-reside-in-assembly:not",
            "PResideInAssembly",
            "Parity.PResideInAssembly",
            "PARITY-PRESIDEINASSEMBLY-NOT",
            Projection.Types,
            a => Types().That().DoNotResideInAssembly(AssemblyOf(a, OtherAssembly))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PResideInAssembly.Target"))),
        new(
            "p-implement-interface",
            "PImplementInterface",
            "Parity.PImplementInterface",
            "PARITY-PIMPLEMENTINTERFACE",
            Projection.Types,
            a => Types().That().ImplementInterface(InterfaceOf(a, "Parity.PImplementInterface.Contracts.IAuditable"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PImplementInterface.Target"))),
        new(
            "c-reside-in-namespace",
            "CResideInNamespace",
            "Parity.CResideInNamespace",
            "PARITY-CRESIDEINNAMESPACE",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CResideInNamespace.Broken")
                .Or().ResideInNamespace("Parity.CResideInNamespace.Clean")
                .Should().ResideInNamespace("Parity.CResideInNamespace.Clean")),
        new(
            "c-reside-in-assembly",
            "CResideInAssembly",
            "Parity.CResideInAssembly",
            "PARITY-CRESIDEINASSEMBLY",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CResideInAssembly.Broken")
                .Or().ResideInNamespace("Parity.CResideInAssembly.Clean")
                .Should().ResideInAssembly(AssemblyOf(a, "Parity.CResideInAssembly.Other"))),
        new(
            "c-implement-interface",
            "CImplementInterface",
            "Parity.CImplementInterface",
            "PARITY-CIMPLEMENTINTERFACE",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CImplementInterface.Broken")
                .Or().ResideInNamespace("Parity.CImplementInterface.Clean")
                .Should().ImplementInterface(InterfaceOf(a, "Parity.CImplementInterface.Target.IAuditable"))),
        new(
            "p-are",
            "PAre",
            "Parity.PAre",
            "PARITY-PARE",
            Projection.Types,
            a => Types().That().Are(TypeOf(a, "Parity.PAre.Broken.OrderService"), TypeOf(a, "Parity.PAre.Clean.Ledger"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAre.Target"))),
        new(
            "p-are:not",
            "PAre",
            "Parity.PAre",
            "PARITY-PARE-NOT",
            Projection.Types,
            a => Types().That().AreNot(TypeOf(a, "Parity.PAre.NotClean.ShipmentService"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAre.Target"))),
        new(
            "c-be-kind",
            "CBeKind",
            "Parity.CBeKind",
            "PARITY-CBEKIND",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeKind.Broken")
                .Or().ResideInNamespace("Parity.CBeKind.Clean")
                .Should().Be(Classes())),
        new(
            "c-be-internal",
            "CBeInternal",
            "Parity.CBeInternal",
            "PARITY-CBEINTERNAL",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeInternal.Broken")
                .Or().ResideInNamespace("Parity.CBeInternal.Clean")
                .Should().BeInternal()),
        new(
            "p-have-name",
            "PHaveName",
            "Parity.PHaveName",
            "PARITY-PHAVENAME",
            Projection.Types,
            _ => Types().That().HaveName("OrderService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveName.Target"))),
        new(
            "p-have-name:not",
            "PHaveName",
            "Parity.PHaveName",
            "PARITY-PHAVENAME-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveName("OrderService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveName.Target"))),
        new(
            "p-have-name-starting-with",
            "PHaveNameStartingWith",
            "Parity.PHaveNameStartingWith",
            "PARITY-PHAVENAMESTARTINGWITH",
            Projection.Types,
            _ => Types().That().HaveNameStartingWith("Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameStartingWith.Target"))),
        new(
            "p-have-name-starting-with:not",
            "PHaveNameStartingWith",
            "Parity.PHaveNameStartingWith",
            "PARITY-PHAVENAMESTARTINGWITH-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveNameStartingWith("Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameStartingWith.Target"))),
        new(
            "p-have-name-ending-with",
            "PHaveNameEndingWith",
            "Parity.PHaveNameEndingWith",
            "PARITY-PHAVENAMEENDINGWITH",
            Projection.Types,
            _ => Types().That().HaveNameEndingWith("Service")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameEndingWith.Target"))),
        new(
            "p-have-name-ending-with:not",
            "PHaveNameEndingWith",
            "Parity.PHaveNameEndingWith",
            "PARITY-PHAVENAMEENDINGWITH-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveNameEndingWith("Service")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameEndingWith.Target"))),
        new(
            "p-have-name-containing",
            "PHaveNameContaining",
            "Parity.PHaveNameContaining",
            "PARITY-PHAVENAMECONTAINING",
            Projection.Types,
            _ => Types().That().HaveNameContaining("Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameContaining.Target"))),
        new(
            "p-have-name-containing:not",
            "PHaveNameContaining",
            "Parity.PHaveNameContaining",
            "PARITY-PHAVENAMECONTAINING-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveNameContaining("Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveNameContaining.Target"))),
        new(
            "p-have-full-name",
            "PHaveFullName",
            "Parity.PHaveFullName",
            "PARITY-PHAVEFULLNAME",
            Projection.Types,
            _ => Types().That().HaveFullName("Parity.PHaveFullName.Broken.OrderService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullName.Target"))),
        new(
            "p-have-full-name:not",
            "PHaveFullName",
            "Parity.PHaveFullName",
            "PARITY-PHAVEFULLNAME-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveFullName("Parity.PHaveFullName.NotClean.PaymentService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullName.Target"))),
        new(
            "p-have-full-name-starting-with",
            "PHaveFullNameStartingWith",
            "Parity.PHaveFullNameStartingWith",
            "PARITY-PHAVEFULLNAMESTARTINGWITH",
            Projection.Types,
            _ => Types().That().HaveFullNameStartingWith("Parity.PHaveFullNameStartingWith.Broken.Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameStartingWith.Target"))),
        new(
            "p-have-full-name-starting-with:not",
            "PHaveFullNameStartingWith",
            "Parity.PHaveFullNameStartingWith",
            "PARITY-PHAVEFULLNAMESTARTINGWITH-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveFullNameStartingWith("Parity.PHaveFullNameStartingWith.NotCl")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameStartingWith.Target"))),
        new(
            "p-have-full-name-ending-with",
            "PHaveFullNameEndingWith",
            "Parity.PHaveFullNameEndingWith",
            "PARITY-PHAVEFULLNAMEENDINGWITH",
            Projection.Types,
            _ => Types().That().HaveFullNameEndingWith("Service")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameEndingWith.Target"))),
        new(
            "p-have-full-name-ending-with:not",
            "PHaveFullNameEndingWith",
            "Parity.PHaveFullNameEndingWith",
            "PARITY-PHAVEFULLNAMEENDINGWITH-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveFullNameEndingWith("Clean.PaymentService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameEndingWith.Target"))),
        new(
            "p-have-full-name-containing",
            "PHaveFullNameContaining",
            "Parity.PHaveFullNameContaining",
            "PARITY-PHAVEFULLNAMECONTAINING",
            Projection.Types,
            _ => Types().That().HaveFullNameContaining("Order")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameContaining.Target"))),
        new(
            "p-have-full-name-containing:not",
            "PHaveFullNameContaining",
            "Parity.PHaveFullNameContaining",
            "PARITY-PHAVEFULLNAMECONTAINING-NOT",
            Projection.Types,
            _ => Types().That().DoNotHaveFullNameContaining("NotClean")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveFullNameContaining.Target"))),
        new(
            "c-only-depend-on",
            "COnlyDependOn",
            "Parity.COnlyDependOn",
            "PARITY-CONLYDEPENDON",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.COnlyDependOn.Broken")
                .Or().ResideInNamespace("Parity.COnlyDependOn.Clean")
                .Should().OnlyDependOn(Types().That().ResideInNamespace("Parity.COnlyDependOn.Target"))),
        new(
            "p-have-any-attributes",
            "PHaveAnyAttributes",
            "Parity.PHaveAnyAttributes",
            "PARITY-PHAVEANYATTRIBUTES",
            Projection.Types,
            a => Types().That().HaveAnyAttributes(AttributeOf(a, "Parity.PHaveAnyAttributes.Contracts.AuditedAttribute"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveAnyAttributes.Target"))),
        new(
            "x-and",
            "XAnd",
            "Parity.XAnd",
            "PARITY-XAND",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.XAnd.Broken").And().HaveNameEndingWith("Service")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.XAnd.Target"))),
        new(
            "x-or-within-predicate",
            "XOrWithinPredicate",
            "Parity.XOrWithinPredicate",
            "PARITY-XORWITHINPREDICATE",
            Projection.Types,
            _ => Types().That().HaveName("OrderService").Or().HaveName("PaymentService")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.XOrWithinPredicate.Target"))),
    ];

    // The second assembly of the PResideInAssembly kind, by its simple name.
    private const string OtherAssembly = "Parity.PResideInAssembly.Other";

    /// <summary>The case with <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">No case has that id.</exception>
    public static ParityCase Get(string id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"No parity case has the id '{id}'.");

    /// <summary>Every case id, as xUnit member data.</summary>
    public static IEnumerable<object[]> Ids() => All.Select(c => new object[] { c.Id });

    // The loaded assembly with simple name name. ArchUnitNET's string overload of ResideInAssembly takes the full display
    // name, with version and culture, which the case would have to hard-code.
    private static Assembly AssemblyOf(Architecture architecture, string name) =>
        architecture.Assemblies.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException(
            $"The parity fixture loads no assembly '{name}'; it has {string.Join(", ", architecture.Assemblies.Select(x => x.Name))}.");

    // The loaded type with full name fullName, for the predicates that take types rather than names.
    private static IType TypeOf(Architecture architecture, string fullName) =>
        architecture.Types.FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"The parity fixture declares no type '{fullName}'.");

    // The loaded interface with full name fullName, for the rules that take an interface.
    private static Interface InterfaceOf(Architecture architecture, string fullName) =>
        architecture.Interfaces.FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"The parity fixture declares no interface '{fullName}'.");

    // The loaded attribute class with full name fullName, for the rules that take an attribute.
    private static ArchUnitNET.Domain.Attribute AttributeOf(Architecture architecture, string fullName) =>
        architecture.Attributes.FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"The parity fixture declares no attribute '{fullName}'.");
}
