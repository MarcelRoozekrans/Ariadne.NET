using ArchUnitNET.Domain;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

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
                .Or().HaveFullName("Parity.PHaveFullName.Broken.Outer.OrderService")
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

        // Phase 4.2 gaps: each has a KnownGaps entry and no rule in its kind's pack.
        new(
            "p-implement-interface:not",
            "PImplementInterface",
            "Parity.PImplementInterface",
            "PARITY-PIMPLEMENTINTERFACE-NOT",
            Projection.Types,
            a => Types().That().DoNotImplementInterface(InterfaceOf(a, "Parity.PImplementInterface.Contracts.IAuditable"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PImplementInterface.Target"))),
        new(
            "p-are-assignable-to",
            "PAreAssignableTo",
            "Parity.PAreAssignableTo",
            "PARITY-PAREASSIGNABLETO",
            Projection.Types,
            a => Types().That().AreAssignableTo(TypeOf(a, "Parity.PAreAssignableTo.Broken.OrderBase"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreAssignableTo.Target"))),
        new(
            "p-are-assignable-to:not",
            "PAreAssignableTo",
            "Parity.PAreAssignableTo",
            "PARITY-PAREASSIGNABLETO-NOT",
            Projection.Types,
            a => Types().That().AreNotAssignableTo(TypeOf(a, "Parity.PAreAssignableTo.Broken.OrderBase"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreAssignableTo.Target"))),
        new(
            "p-are-enums",
            "PAreEnums",
            "Parity.PAreEnums",
            "PARITY-PAREENUMS",
            Projection.Types,
            _ => Types().That().AreEnums()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreEnums.Target"))),
        new(
            "p-are-enums:not",
            "PAreEnums",
            "Parity.PAreEnums",
            "PARITY-PAREENUMS-NOT",
            Projection.Types,
            _ => Types().That().AreNotEnums()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreEnums.Target"))),
        new(
            "p-are-structs",
            "PAreStructs",
            "Parity.PAreStructs",
            "PARITY-PARESTRUCTS",
            Projection.Types,
            _ => Types().That().AreStructs()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreStructs.Target"))),
        new(
            "p-are-structs:not",
            "PAreStructs",
            "Parity.PAreStructs",
            "PARITY-PARESTRUCTS-NOT",
            Projection.Types,
            _ => Types().That().AreNotStructs()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreStructs.Target"))),
        new(
            "p-are-value-types",
            "PAreValueTypes",
            "Parity.PAreValueTypes",
            "PARITY-PAREVALUETYPES",
            Projection.Types,
            _ => Types().That().AreValueTypes()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreValueTypes.Target"))),
        new(
            "p-are-value-types:not",
            "PAreValueTypes",
            "Parity.PAreValueTypes",
            "PARITY-PAREVALUETYPES-NOT",
            Projection.Types,
            _ => Types().That().AreNotValueTypes()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreValueTypes.Target"))),
        new(
            "p-are-nested",
            "PAreNested",
            "Parity.PAreNested",
            "PARITY-PARENESTED",
            Projection.Types,
            _ => Types().That().AreNested()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreNested.Target"))),
        new(
            "p-are-nested:not",
            "PAreNested",
            "Parity.PAreNested",
            "PARITY-PARENESTED-NOT",
            Projection.Types,
            _ => Types().That().AreNotNested()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreNested.Target"))),
        new(
            "p-are-nested-in",
            "PAreNestedIn",
            "Parity.PAreNestedIn",
            "PARITY-PARENESTEDIN",
            Projection.Types,
            a => Types().That().AreNestedIn(TypeOf(a, "Parity.PAreNestedIn.Broken.Order"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreNestedIn.Target"))),
        new(
            "p-have-any-attributes:not",
            "PHaveAnyAttributes",
            "Parity.PHaveAnyAttributes",
            "PARITY-PHAVEANYATTRIBUTES-NOT",
            Projection.Types,
            a => Types().That().DoNotHaveAnyAttributes(AttributeOf(a, "Parity.PHaveAnyAttributes.Contracts.AuditedAttribute"))
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PHaveAnyAttributes.Target"))),
        new(
            "p-are-abstract",
            "PAreAbstract",
            "Parity.PAreAbstract",
            "PARITY-PAREABSTRACT",
            Projection.Types,
            _ => Classes().That().AreAbstract()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreAbstract.Target"))),
        new(
            "p-are-abstract:not",
            "PAreAbstract",
            "Parity.PAreAbstract",
            "PARITY-PAREABSTRACT-NOT",
            Projection.Types,
            _ => Classes().That().AreNotAbstract()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreAbstract.Target"))),
        new(
            "p-are-sealed",
            "PAreSealed",
            "Parity.PAreSealed",
            "PARITY-PARESEALED",
            Projection.Types,
            _ => Classes().That().AreSealed()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreSealed.Target"))),
        new(
            "p-are-sealed:not",
            "PAreSealed",
            "Parity.PAreSealed",
            "PARITY-PARESEALED-NOT",
            Projection.Types,
            _ => Classes().That().AreNotSealed()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreSealed.Target"))),
        new(
            "p-are-record",
            "PAreRecord",
            "Parity.PAreRecord",
            "PARITY-PARERECORD",
            Projection.Types,
            _ => Classes().That().AreRecord()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreRecord.Target"))),
        new(
            "p-are-record:not",
            "PAreRecord",
            "Parity.PAreRecord",
            "PARITY-PARERECORD-NOT",
            Projection.Types,
            _ => Classes().That().AreNotRecord()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreRecord.Target"))),
        new(
            "p-attribute-are-abstract",
            "PAttributeAreAbstract",
            "Parity.PAttributeAreAbstract",
            "PARITY-PATTRIBUTEAREABSTRACT",
            Projection.Types,
            _ => Attributes().That().AreAbstract()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAttributeAreAbstract.Target"))),
        new(
            "p-attribute-are-abstract:not",
            "PAttributeAreAbstract",
            "Parity.PAttributeAreAbstract",
            "PARITY-PATTRIBUTEAREABSTRACT-NOT",
            Projection.Types,
            _ => Attributes().That().AreNotAbstract()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAttributeAreAbstract.Target"))),
        new(
            "p-attribute-are-sealed",
            "PAttributeAreSealed",
            "Parity.PAttributeAreSealed",
            "PARITY-PATTRIBUTEARESEALED",
            Projection.Types,
            _ => Attributes().That().AreSealed()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAttributeAreSealed.Target"))),
        new(
            "p-attribute-are-sealed:not",
            "PAttributeAreSealed",
            "Parity.PAttributeAreSealed",
            "PARITY-PATTRIBUTEARESEALED-NOT",
            Projection.Types,
            _ => Attributes().That().AreNotSealed()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAttributeAreSealed.Target"))),
        new(
            "p-are-public",
            "PArePublic",
            "Parity.PArePublic",
            "PARITY-PAREPUBLIC",
            Projection.Types,
            _ => Types().That().ArePublic()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePublic.Target"))),
        new(
            "p-are-public:not",
            "PArePublic",
            "Parity.PArePublic",
            "PARITY-PAREPUBLIC-NOT",
            Projection.Types,
            _ => Types().That().AreNotPublic()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePublic.Target"))),
        new(
            "p-are-internal",
            "PAreInternal",
            "Parity.PAreInternal",
            "PARITY-PAREINTERNAL",
            Projection.Types,
            _ => Types().That().AreInternal()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreInternal.Target"))),
        new(
            "p-are-internal:not",
            "PAreInternal",
            "Parity.PAreInternal",
            "PARITY-PAREINTERNAL-NOT",
            Projection.Types,
            _ => Types().That().AreNotInternal()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreInternal.Target"))),
        new(
            "p-are-protected",
            "PAreProtected",
            "Parity.PAreProtected",
            "PARITY-PAREPROTECTED",
            Projection.Types,
            _ => Types().That().AreProtected()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreProtected.Target"))),
        new(
            "p-are-protected:not",
            "PAreProtected",
            "Parity.PAreProtected",
            "PARITY-PAREPROTECTED-NOT",
            Projection.Types,
            _ => Types().That().AreNotProtected()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreProtected.Target"))),
        new(
            "p-are-private",
            "PArePrivate",
            "Parity.PArePrivate",
            "PARITY-PAREPRIVATE",
            Projection.Types,
            _ => Types().That().ArePrivate()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePrivate.Target"))),
        new(
            "p-are-private:not",
            "PArePrivate",
            "Parity.PArePrivate",
            "PARITY-PAREPRIVATE-NOT",
            Projection.Types,
            _ => Types().That().AreNotPrivate()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePrivate.Target"))),
        new(
            "p-are-protected-internal",
            "PAreProtectedInternal",
            "Parity.PAreProtectedInternal",
            "PARITY-PAREPROTECTEDINTERNAL",
            Projection.Types,
            _ => Types().That().AreProtectedInternal()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreProtectedInternal.Target"))),
        new(
            "p-are-protected-internal:not",
            "PAreProtectedInternal",
            "Parity.PAreProtectedInternal",
            "PARITY-PAREPROTECTEDINTERNAL-NOT",
            Projection.Types,
            _ => Types().That().AreNotProtectedInternal()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PAreProtectedInternal.Target"))),
        new(
            "p-are-private-protected",
            "PArePrivateProtected",
            "Parity.PArePrivateProtected",
            "PARITY-PAREPRIVATEPROTECTED",
            Projection.Types,
            _ => Types().That().ArePrivateProtected()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePrivateProtected.Target"))),
        new(
            "p-are-private-protected:not",
            "PArePrivateProtected",
            "Parity.PArePrivateProtected",
            "PARITY-PAREPRIVATEPROTECTED-NOT",
            Projection.Types,
            _ => Types().That().AreNotPrivateProtected()
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.PArePrivateProtected.Target"))),
        new(
            "c-reside-in-namespace:not",
            "CResideInNamespace",
            "Parity.CResideInNamespace",
            "PARITY-CRESIDEINNAMESPACE-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CResideInNamespace.NotBroken")
                .Or().ResideInNamespace("Parity.CResideInNamespace.NotClean")
                .Should().NotResideInNamespace("Parity.CResideInNamespace.NotBroken")),
        new(
            "c-reside-in-assembly:not",
            "CResideInAssembly",
            "Parity.CResideInAssembly",
            "PARITY-CRESIDEINASSEMBLY-NOT",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CResideInAssembly.NotBroken")
                .Or().ResideInNamespace("Parity.CResideInAssembly.NotClean")
                .Should().NotResideInAssembly(AssemblyOf(a, "Parity.CResideInAssembly.Other"))),
        new(
            "c-implement-interface:not",
            "CImplementInterface",
            "Parity.CImplementInterface",
            "PARITY-CIMPLEMENTINTERFACE-NOT",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CImplementInterface.NotBroken")
                .Or().ResideInNamespace("Parity.CImplementInterface.NotClean")
                .Should().NotImplementInterface(InterfaceOf(a, "Parity.CImplementInterface.Target.IAuditable"))),
        new(
            "c-be-kind:not",
            "CBeKind",
            "Parity.CBeKind",
            "PARITY-CBEKIND-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeKind.NotBroken")
                .Or().ResideInNamespace("Parity.CBeKind.NotClean")
                .Should().NotBe(Interfaces())),
        new(
            "c-be-internal:not",
            "CBeInternal",
            "Parity.CBeInternal",
            "PARITY-CBEINTERNAL-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeInternal.NotBroken")
                .Or().ResideInNamespace("Parity.CBeInternal.NotClean")
                .Should().NotBeInternal()),
        new(
            "c-be-assignable-to",
            "CBeAssignableTo",
            "Parity.CBeAssignableTo",
            "PARITY-CBEASSIGNABLETO",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CBeAssignableTo.Broken")
                .Or().ResideInNamespace("Parity.CBeAssignableTo.Clean")
                .Should().BeAssignableTo(TypeOf(a, "Parity.CBeAssignableTo.Target.AuditedBase"))),
        new(
            "c-be-assignable-to:not",
            "CBeAssignableTo",
            "Parity.CBeAssignableTo",
            "PARITY-CBEASSIGNABLETO-NOT",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CBeAssignableTo.NotBroken")
                .Or().ResideInNamespace("Parity.CBeAssignableTo.NotClean")
                .Should().NotBeAssignableTo(TypeOf(a, "Parity.CBeAssignableTo.Target.AuditedBase"))),
        new(
            "c-be-enums",
            "CBeEnums",
            "Parity.CBeEnums",
            "PARITY-CBEENUMS",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeEnums.Broken")
                .Or().ResideInNamespace("Parity.CBeEnums.Clean")
                .Should().BeEnums()),
        new(
            "c-be-enums:not",
            "CBeEnums",
            "Parity.CBeEnums",
            "PARITY-CBEENUMS-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeEnums.NotBroken")
                .Or().ResideInNamespace("Parity.CBeEnums.NotClean")
                .Should().NotBeEnums()),
        new(
            "c-be-structs",
            "CBeStructs",
            "Parity.CBeStructs",
            "PARITY-CBESTRUCTS",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeStructs.Broken")
                .Or().ResideInNamespace("Parity.CBeStructs.Clean")
                .Should().BeStructs()),
        new(
            "c-be-structs:not",
            "CBeStructs",
            "Parity.CBeStructs",
            "PARITY-CBESTRUCTS-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeStructs.NotBroken")
                .Or().ResideInNamespace("Parity.CBeStructs.NotClean")
                .Should().NotBeStructs()),
        new(
            "c-be-value-types",
            "CBeValueTypes",
            "Parity.CBeValueTypes",
            "PARITY-CBEVALUETYPES",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeValueTypes.Broken")
                .Or().ResideInNamespace("Parity.CBeValueTypes.Clean")
                .Should().BeValueTypes()),
        new(
            "c-be-value-types:not",
            "CBeValueTypes",
            "Parity.CBeValueTypes",
            "PARITY-CBEVALUETYPES-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeValueTypes.NotBroken")
                .Or().ResideInNamespace("Parity.CBeValueTypes.NotClean")
                .Should().NotBeValueTypes()),
        new(
            "c-be-nested",
            "CBeNested",
            "Parity.CBeNested",
            "PARITY-CBENESTED",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeNested.Broken")
                .Or().ResideInNamespace("Parity.CBeNested.Clean")
                .Should().BeNested()),
        new(
            "c-be-nested:not",
            "CBeNested",
            "Parity.CBeNested",
            "PARITY-CBENESTED-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeNested.NotBroken")
                .Or().ResideInNamespace("Parity.CBeNested.NotClean")
                .Should().NotBeNested()),
        new(
            "c-be-nested-in",
            "CBeNestedIn",
            "Parity.CBeNestedIn",
            "PARITY-CBENESTEDIN",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CBeNestedIn.Broken")
                .Or().ResideInNamespace("Parity.CBeNestedIn.Clean")
                .Should().BeNestedIn(TypeOf(a, "Parity.CBeNestedIn.Broken.Order"))),
        new(
            "c-be-abstract",
            "CBeAbstract",
            "Parity.CBeAbstract",
            "PARITY-CBEABSTRACT",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeAbstract.Broken")
                .Or().ResideInNamespace("Parity.CBeAbstract.Clean")
                .Should().BeAbstract()),
        new(
            "c-be-abstract:not",
            "CBeAbstract",
            "Parity.CBeAbstract",
            "PARITY-CBEABSTRACT-NOT",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeAbstract.NotBroken")
                .Or().ResideInNamespace("Parity.CBeAbstract.NotClean")
                .Should().NotBeAbstract()),
        new(
            "c-be-sealed",
            "CBeSealed",
            "Parity.CBeSealed",
            "PARITY-CBESEALED",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeSealed.Broken")
                .Or().ResideInNamespace("Parity.CBeSealed.Clean")
                .Should().BeSealed()),
        new(
            "c-be-sealed:not",
            "CBeSealed",
            "Parity.CBeSealed",
            "PARITY-CBESEALED-NOT",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeSealed.NotBroken")
                .Or().ResideInNamespace("Parity.CBeSealed.NotClean")
                .Should().NotBeSealed()),
        new(
            "c-be-record",
            "CBeRecord",
            "Parity.CBeRecord",
            "PARITY-CBERECORD",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeRecord.Broken")
                .Or().ResideInNamespace("Parity.CBeRecord.Clean")
                .Should().BeRecord()),
        new(
            "c-be-record:not",
            "CBeRecord",
            "Parity.CBeRecord",
            "PARITY-CBERECORD-NOT",
            Projection.Types,
            _ => Classes().That().ResideInNamespace("Parity.CBeRecord.NotBroken")
                .Or().ResideInNamespace("Parity.CBeRecord.NotClean")
                .Should().NotBeRecord()),
        new(
            "c-attribute-be-abstract",
            "CAttributeBeAbstract",
            "Parity.CAttributeBeAbstract",
            "PARITY-CATTRIBUTEBEABSTRACT",
            Projection.Types,
            _ => Attributes().That().ResideInNamespace("Parity.CAttributeBeAbstract.Broken")
                .Or().ResideInNamespace("Parity.CAttributeBeAbstract.Clean")
                .Should().BeAbstract()),
        new(
            "c-attribute-be-abstract:not",
            "CAttributeBeAbstract",
            "Parity.CAttributeBeAbstract",
            "PARITY-CATTRIBUTEBEABSTRACT-NOT",
            Projection.Types,
            _ => Attributes().That().ResideInNamespace("Parity.CAttributeBeAbstract.NotBroken")
                .Or().ResideInNamespace("Parity.CAttributeBeAbstract.NotClean")
                .Should().NotBeAbstract()),
        new(
            "c-attribute-be-sealed",
            "CAttributeBeSealed",
            "Parity.CAttributeBeSealed",
            "PARITY-CATTRIBUTEBESEALED",
            Projection.Types,
            _ => Attributes().That().ResideInNamespace("Parity.CAttributeBeSealed.Broken")
                .Or().ResideInNamespace("Parity.CAttributeBeSealed.Clean")
                .Should().BeSealed()),
        new(
            "c-attribute-be-sealed:not",
            "CAttributeBeSealed",
            "Parity.CAttributeBeSealed",
            "PARITY-CATTRIBUTEBESEALED-NOT",
            Projection.Types,
            _ => Attributes().That().ResideInNamespace("Parity.CAttributeBeSealed.NotBroken")
                .Or().ResideInNamespace("Parity.CAttributeBeSealed.NotClean")
                .Should().NotBeSealed()),
        new(
            "c-be",
            "CBe",
            "Parity.CBe",
            "PARITY-CBE",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CBe.Broken")
                .Or().ResideInNamespace("Parity.CBe.Clean")
                .Should().Be(TypeOf(a, "Parity.CBe.Clean.Ledger"), TypeOf(a, "Parity.CBe.Clean.InvoiceService"))),
        new(
            "c-be:not",
            "CBe",
            "Parity.CBe",
            "PARITY-CBE-NOT",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CBe.NotBroken")
                .Or().ResideInNamespace("Parity.CBe.NotClean")
                .Should().NotBe(TypeOf(a, "Parity.CBe.NotBroken.Shipment"))),
        new(
            "c-not-exist",
            "CNotExist",
            "Parity.CNotExist",
            "PARITY-CNOTEXIST",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CNotExist.Broken")
                .Should().NotExist()),
        new(
            "c-be-public",
            "CBePublic",
            "Parity.CBePublic",
            "PARITY-CBEPUBLIC",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePublic.Broken")
                .Or().ResideInNamespace("Parity.CBePublic.Clean")
                .Should().BePublic()),
        new(
            "c-be-public:not",
            "CBePublic",
            "Parity.CBePublic",
            "PARITY-CBEPUBLIC-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePublic.NotBroken")
                .Or().ResideInNamespace("Parity.CBePublic.NotClean")
                .Should().NotBePublic()),
        new(
            "c-be-protected",
            "CBeProtected",
            "Parity.CBeProtected",
            "PARITY-CBEPROTECTED",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeProtected.Broken")
                .Or().ResideInNamespace("Parity.CBeProtected.Clean")
                .Should().BeProtected()),
        new(
            "c-be-protected:not",
            "CBeProtected",
            "Parity.CBeProtected",
            "PARITY-CBEPROTECTED-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeProtected.NotBroken")
                .Or().ResideInNamespace("Parity.CBeProtected.NotClean")
                .Should().NotBeProtected()),
        new(
            "c-be-private",
            "CBePrivate",
            "Parity.CBePrivate",
            "PARITY-CBEPRIVATE",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePrivate.Broken")
                .Or().ResideInNamespace("Parity.CBePrivate.Clean")
                .Should().BePrivate()),
        new(
            "c-be-private:not",
            "CBePrivate",
            "Parity.CBePrivate",
            "PARITY-CBEPRIVATE-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePrivate.NotBroken")
                .Or().ResideInNamespace("Parity.CBePrivate.NotClean")
                .Should().NotBePrivate()),
        new(
            "c-be-protected-internal",
            "CBeProtectedInternal",
            "Parity.CBeProtectedInternal",
            "PARITY-CBEPROTECTEDINTERNAL",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeProtectedInternal.Broken")
                .Or().ResideInNamespace("Parity.CBeProtectedInternal.Clean")
                .Should().BeProtectedInternal()),
        new(
            "c-be-protected-internal:not",
            "CBeProtectedInternal",
            "Parity.CBeProtectedInternal",
            "PARITY-CBEPROTECTEDINTERNAL-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBeProtectedInternal.NotBroken")
                .Or().ResideInNamespace("Parity.CBeProtectedInternal.NotClean")
                .Should().NotBeProtectedInternal()),
        new(
            "c-be-private-protected",
            "CBePrivateProtected",
            "Parity.CBePrivateProtected",
            "PARITY-CBEPRIVATEPROTECTED",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePrivateProtected.Broken")
                .Or().ResideInNamespace("Parity.CBePrivateProtected.Clean")
                .Should().BePrivateProtected()),
        new(
            "c-be-private-protected:not",
            "CBePrivateProtected",
            "Parity.CBePrivateProtected",
            "PARITY-CBEPRIVATEPROTECTED-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CBePrivateProtected.NotBroken")
                .Or().ResideInNamespace("Parity.CBePrivateProtected.NotClean")
                .Should().NotBePrivateProtected()),
        new(
            "c-have-name",
            "CHaveName",
            "Parity.CHaveName",
            "PARITY-CHAVENAME",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveName.Broken")
                .Or().ResideInNamespace("Parity.CHaveName.Clean")
                .Should().HaveName("OrderService")),
        new(
            "c-have-name:not",
            "CHaveName",
            "Parity.CHaveName",
            "PARITY-CHAVENAME-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveName.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveName.NotClean")
                .Should().NotHaveName("OrderService")),
        new(
            "c-have-name-starting-with",
            "CHaveNameStartingWith",
            "Parity.CHaveNameStartingWith",
            "PARITY-CHAVENAMESTARTINGWITH",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameStartingWith.Broken")
                .Or().ResideInNamespace("Parity.CHaveNameStartingWith.Clean")
                .Should().HaveNameStartingWith("Order")),
        new(
            "c-have-name-starting-with:not",
            "CHaveNameStartingWith",
            "Parity.CHaveNameStartingWith",
            "PARITY-CHAVENAMESTARTINGWITH-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameStartingWith.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveNameStartingWith.NotClean")
                .Should().NotHaveNameStartingWith("Order")),
        new(
            "c-have-name-ending-with",
            "CHaveNameEndingWith",
            "Parity.CHaveNameEndingWith",
            "PARITY-CHAVENAMEENDINGWITH",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameEndingWith.Broken")
                .Or().ResideInNamespace("Parity.CHaveNameEndingWith.Clean")
                .Should().HaveNameEndingWith("Service")),
        new(
            "c-have-name-ending-with:not",
            "CHaveNameEndingWith",
            "Parity.CHaveNameEndingWith",
            "PARITY-CHAVENAMEENDINGWITH-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameEndingWith.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveNameEndingWith.NotClean")
                .Should().NotHaveNameEndingWith("Service")),
        new(
            "c-have-name-containing",
            "CHaveNameContaining",
            "Parity.CHaveNameContaining",
            "PARITY-CHAVENAMECONTAINING",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameContaining.Broken")
                .Or().ResideInNamespace("Parity.CHaveNameContaining.Clean")
                .Should().HaveNameContaining("Order")),
        new(
            "c-have-name-containing:not",
            "CHaveNameContaining",
            "Parity.CHaveNameContaining",
            "PARITY-CHAVENAMECONTAINING-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveNameContaining.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveNameContaining.NotClean")
                .Should().NotHaveNameContaining("Order")),
        new(
            "c-have-full-name",
            "CHaveFullName",
            "Parity.CHaveFullName",
            "PARITY-CHAVEFULLNAME",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullName.Broken")
                .Or().ResideInNamespace("Parity.CHaveFullName.Clean")
                .Should().HaveFullName("Parity.CHaveFullName.Clean.OrderService")),
        new(
            "c-have-full-name:not",
            "CHaveFullName",
            "Parity.CHaveFullName",
            "PARITY-CHAVEFULLNAME-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullName.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveFullName.NotClean")
                .Should().NotHaveFullName("Parity.CHaveFullName.NotBroken.PaymentService")),
        new(
            "c-have-full-name-starting-with",
            "CHaveFullNameStartingWith",
            "Parity.CHaveFullNameStartingWith",
            "PARITY-CHAVEFULLNAMESTARTINGWITH",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameStartingWith.Broken")
                .Or().ResideInNamespace("Parity.CHaveFullNameStartingWith.Clean")
                .Should().HaveFullNameStartingWith("Parity.CHaveFullNameStartingWith.Clean.Order")),
        new(
            "c-have-full-name-starting-with:not",
            "CHaveFullNameStartingWith",
            "Parity.CHaveFullNameStartingWith",
            "PARITY-CHAVEFULLNAMESTARTINGWITH-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameStartingWith.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveFullNameStartingWith.NotClean")
                .Should().NotHaveFullNameStartingWith("Parity.CHaveFullNameStartingWith.NotB")),
        new(
            "c-have-full-name-ending-with",
            "CHaveFullNameEndingWith",
            "Parity.CHaveFullNameEndingWith",
            "PARITY-CHAVEFULLNAMEENDINGWITH",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameEndingWith.Broken")
                .Or().ResideInNamespace("Parity.CHaveFullNameEndingWith.Clean")
                .Should().HaveFullNameEndingWith("Service")),
        new(
            "c-have-full-name-ending-with:not",
            "CHaveFullNameEndingWith",
            "Parity.CHaveFullNameEndingWith",
            "PARITY-CHAVEFULLNAMEENDINGWITH-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameEndingWith.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveFullNameEndingWith.NotClean")
                .Should().NotHaveFullNameEndingWith("Broken.PaymentService")),
        new(
            "c-have-full-name-containing",
            "CHaveFullNameContaining",
            "Parity.CHaveFullNameContaining",
            "PARITY-CHAVEFULLNAMECONTAINING",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameContaining.Broken")
                .Or().ResideInNamespace("Parity.CHaveFullNameContaining.Clean")
                .Should().HaveFullNameContaining("Order")),
        new(
            "c-have-full-name-containing:not",
            "CHaveFullNameContaining",
            "Parity.CHaveFullNameContaining",
            "PARITY-CHAVEFULLNAMECONTAINING-NOT",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CHaveFullNameContaining.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveFullNameContaining.NotClean")
                .Should().NotHaveFullNameContaining("NotBroken.Pay")),
        new(
            "c-have-any-attributes",
            "CHaveAnyAttributes",
            "Parity.CHaveAnyAttributes",
            "PARITY-CHAVEANYATTRIBUTES",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CHaveAnyAttributes.Broken")
                .Or().ResideInNamespace("Parity.CHaveAnyAttributes.Clean")
                .Should().HaveAnyAttributes(AttributeOf(a, "Parity.CHaveAnyAttributes.Target.AuditedAttribute"))),
        new(
            "c-have-any-attributes:not",
            "CHaveAnyAttributes",
            "Parity.CHaveAnyAttributes",
            "PARITY-CHAVEANYATTRIBUTES-NOT",
            Projection.Types,
            a => Types().That().ResideInNamespace("Parity.CHaveAnyAttributes.NotBroken")
                .Or().ResideInNamespace("Parity.CHaveAnyAttributes.NotClean")
                .Should().NotHaveAnyAttributes(AttributeOf(a, "Parity.CHaveAnyAttributes.Target.AuditedAttribute"))),

        // Phase 4.3 gaps: each has a KnownGaps entry and no rule in its kind's pack. The pattern spans the kind, so every
        // namespace below Parity.<Kind> is its own slice and slice names start with the Broken or Clean namespace.
        new(
            "s-free-of-cycles",
            "SFreeOfCycles",
            "Parity.SFreeOfCycles",
            "PARITY-SFREEOFCYCLES",
            Projection.Slices,
            _ => Slices().Matching("Parity.SFreeOfCycles.(**)").Should().BeFreeOfCycles(),
            "Parity.SFreeOfCycles.(**)"),
        new(
            "s-not-depend-on-each-other",
            "SNotDependOnEachOther",
            "Parity.SNotDependOnEachOther",
            "PARITY-SNOTDEPENDONEACHOTHER",
            Projection.Slices,
            _ => Slices().Matching("Parity.SNotDependOnEachOther.(**)").Should().NotDependOnEachOther(),
            "Parity.SNotDependOnEachOther.(**)"),
        new(
            "s-matching-with-packages",
            "SMatchingWithPackages",
            "Parity.SMatchingWithPackages",
            "PARITY-SMATCHINGWITHPACKAGES",
            Projection.Slices,
            _ => Slices().MatchingWithPackages("Parity.SMatchingWithPackages.(**)").Should().NotDependOnEachOther(),
            "Parity.SMatchingWithPackages.(**)"),
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
