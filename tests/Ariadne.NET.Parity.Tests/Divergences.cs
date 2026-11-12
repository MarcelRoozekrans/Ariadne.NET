namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// The cases where the tools disagree on purpose. The parity theory asserts they disagree on exactly the recorded names, so
/// an entry cannot outlive the difference it records.
/// </summary>
public static class Divergences
{
    private const string Arity =
        "ArchUnitNET reads the metadata name, which carries the arity, so OrderService<T> is named OrderService`1. Ariadne "
        + "matches 'types' and 'names' globs against the source name without arity, so OrderService<T> is OrderService.";

    private const string ArityExpected =
        "Ariadne's: in C# the type is named OrderService, and the arity is a compilation detail a developer does not write. "
        + "Neither tool can select OrderService and leave out OrderService<T> by name.";

    private const string Case =
        "ArchUnitNET 0.13.2 compares HaveName, HaveFullName and the HaveFullName prefix, suffix and part forms ignoring case, "
        + "but HaveNameStartingWith, HaveNameEndingWith and HaveNameContaining with case. Ariadne's 'types' and 'names' "
        + "globs always match with case.";

    private const string CaseExpected =
        "Ariadne's: C# names are case-sensitive, so orderService is another type than OrderService.";

    private const string StaticClass =
        "IL marks a static class abstract sealed, so ArchUnitNET's AreAbstract and BeAbstract hold it and AreNotAbstract "
        + "leaves it out. Ariadne follows C#, where a static class is not abstract, so 'abstract: true' leaves it out and "
        + "'abstract: false' selects it.";

    private const string StaticClassExpected =
        "Ariadne's: a static class cannot be declared abstract in C#, and no developer writes AreAbstract or BeAbstract to mean static. "
        + "Select static classes with 'static: true'.";

    /// <summary>All divergence entries.</summary>
    public static IReadOnlyList<Divergence> Entries { get; } =
    [
        new(
            "c-be-kind",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CBeKind.Broken.Ledger" },
            "A record class compiles to a class, so ArchUnitNET's Classes() holds it. Ariadne reads source, where record is its "
            + "own kind, so 'kind: class' rejects a record.",
            "ArchUnitNET's: a record class is a class in C#. 'is: { kinds: [class, record] }' expresses Be(Classes()) "
            + "exactly; the older 'kind' key takes one kind."),
        new(
            "c-only-depend-on",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.COnlyDependOn.Broken.AuditTrail" },
            new HashSet<string>(StringComparer.Ordinal),
            "OnlyDependOn fails a dependency on any type of the loaded assemblies outside the allowed set, while the layer check "
            + "is open-world: a dependency on a type no layer covers, here Helpers.Clock, is not checked. Framework types count "
            + "for neither tool.",
            "ArchUnitNET's, for a developer who wrote OnlyDependOn. In a pack, cover the namespace with a layer that is not in "
            + "allowedDependencies, as the fixture does for Forbidden, and Ariadne flags the dependency too."),
        new(
            "p-have-name",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveName.Broken.orderService" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveName.Broken.OrderService`1" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "p-have-name:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveName.Broken.OrderService`1" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveName.Broken.orderService" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "p-have-name-ending-with",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveNameEndingWith.Broken.OrderService`1" },
            Arity,
            ArityExpected),
        new(
            "p-have-name-ending-with:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveNameEndingWith.Broken.OrderService`1" },
            new HashSet<string>(StringComparer.Ordinal),
            Arity,
            ArityExpected),
        new(
            "p-have-full-name",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullName.Broken.orderService" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullName.Broken.OrderService`1", "Parity.PHaveFullName.Broken.Outer.OrderService" },
            Case + " " + Arity + " ArchUnitNET also joins a nested type to its outer type with '+', so the dotted name "
            + "Parity.PHaveFullName.Broken.Outer.OrderService names no type; Ariadne joins them with '.'.",
            CaseExpected + " " + ArityExpected + " For the nested type too: C# writes Outer.OrderService."),
        new(
            "p-have-full-name-starting-with",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullNameStartingWith.Broken.orderLine" },
            new HashSet<string>(StringComparer.Ordinal),
            Case,
            CaseExpected),
        new(
            "p-have-full-name-ending-with",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullNameEndingWith.Broken.Invoiceservice" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullNameEndingWith.Broken.OrderService`1" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "p-have-full-name-containing",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PHaveFullNameContaining.Broken.Backorder" },
            new HashSet<string>(StringComparer.Ordinal),
            Case,
            CaseExpected),
        new(
            "c-have-name",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveName.Broken.OrderService`1" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveName.Broken.orderService" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "c-have-name:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveName.NotBroken.orderService" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveName.NotBroken.OrderService`1" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "c-have-name-ending-with",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveNameEndingWith.Broken.OrderService`1" },
            new HashSet<string>(StringComparer.Ordinal),
            Arity,
            ArityExpected),
        new(
            "c-have-name-ending-with:not",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveNameEndingWith.NotBroken.OrderService`1" },
            Arity,
            ArityExpected),
        new(
            "c-have-full-name:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullName.NotBroken.paymentService" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullName.NotBroken.PaymentService`1" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "c-have-full-name-ending-with",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameEndingWith.Broken.OrderService`1" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameEndingWith.Broken.Invoiceservice" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "c-have-full-name-ending-with:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameEndingWith.NotBroken.Paymentservice" },
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameEndingWith.NotBroken.PaymentService`1" },
            Case + " " + Arity,
            CaseExpected + " " + ArityExpected),
        new(
            "c-have-full-name-containing",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameContaining.Broken.Backorder" },
            Case,
            CaseExpected),
        new(
            "c-have-full-name-containing:not",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CHaveFullNameContaining.NotBroken.paymentService" },
            new HashSet<string>(StringComparer.Ordinal),
            Case,
            CaseExpected),
        new(
            "p-are-abstract",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PAreAbstract.Broken.Clock" },
            new HashSet<string>(StringComparer.Ordinal),
            StaticClass,
            StaticClassExpected),
        new(
            "p-are-abstract:not",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.PAreAbstract.Broken.Clock" },
            StaticClass,
            StaticClassExpected),
        new(
            "c-be-abstract",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CBeAbstract.Broken.Clock" },
            StaticClass,
            StaticClassExpected),
    ];
}
