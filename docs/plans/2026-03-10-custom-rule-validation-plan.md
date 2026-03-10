# Custom Rule Validation — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend SourceValidator to enforce ComponentRoleDefinition constraints (type kind, accessibility, interface requirements) against source code using suffix-based role matching.

**Architecture:** Add role-based validation to existing `SourceValidator.ValidateFile`. Match type declarations to component roles by name suffix + layer, then check three constraints. No new services — just extending the existing validator.

**Tech Stack:** Microsoft.CodeAnalysis.CSharp (Roslyn) for type declaration inspection, existing SourceValidator infrastructure.

**Design doc:** `docs/plans/2026-03-10-custom-rule-validation-design.md`

---

### Task 1: Add TypeKindMismatch validation

**Files:**
- Modify: `src/ArchGov.Core/Validation/SourceValidator.cs`
- Modify: `tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs`

**Step 1: Write the failing test**

Add to `SourceValidatorTests.cs`. First update `CreateTestPack()` to include component roles, then add the test:

```csharp
private static RulePack CreateTestPackWithRoles()
{
    return new RulePack
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
            new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
        ],
        ComponentRoles =
        [
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class },
            new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
            new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
            new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface },
        ],
        NamingConventions = new NamingConventions
        {
            Namespace = "{SolutionName}.{Feature}.{Layer}",
            FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
        },
    };
}

[Fact]
public void Validate_CommandIsClass_ShouldReturnTypeKindMismatch()
{
    var appDir = Path.Combine(_tempDir, "MyApp.Orders.Application");
    Directory.CreateDirectory(appDir);
    File.WriteAllText(
        Path.Combine(appDir, "CreateOrderCommand.cs"),
        """
        namespace MyApp.Orders.Application;

        public class CreateOrderCommand { }
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.IsValid.Should().BeFalse();
    result.Violations.Should().ContainSingle(v => v.ViolationType == "TypeKindMismatch");
}

[Fact]
public void Validate_CommandIsRecord_ShouldBeValid()
{
    var appDir = Path.Combine(_tempDir, "MyApp.Orders.Application");
    Directory.CreateDirectory(appDir);
    File.WriteAllText(
        Path.Combine(appDir, "CreateOrderCommand.cs"),
        """
        namespace MyApp.Orders.Application;

        public record CreateOrderCommand;
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.Violations.Where(v => v.ViolationType == "TypeKindMismatch").Should().BeEmpty();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: FAIL — no TypeKindMismatch violations produced.

**Step 3: Implement role matching + type kind check**

In `SourceValidator.cs`, update `ValidateFile` signature to accept `IList<ComponentRoleDefinition> roles`, and add:

```csharp
// After existing dependency checks in ValidateFile, add:
ValidateTypeDeclarations(root, sourceLayer, roles, filePath, violations);
```

Add these helper methods:

```csharp
private static void ValidateTypeDeclarations(
    CompilationUnitSyntax root,
    string sourceLayer,
    IList<ComponentRoleDefinition> roles,
    string filePath,
    List<SourceViolation> violations)
{
    var typeDeclarations = root.DescendantNodes()
        .Where(n => n is TypeDeclarationSyntax)
        .Cast<TypeDeclarationSyntax>();

    foreach (var typeDecl in typeDeclarations)
    {
        var typeName = typeDecl.Identifier.Text;
        var matchedRole = MatchTypeToRole(typeName, sourceLayer, roles);
        if (matchedRole is null)
        {
            continue;
        }

        CheckTypeKind(typeDecl, matchedRole, filePath, violations);
    }
}

private static ComponentRoleDefinition? MatchTypeToRole(
    string typeName,
    string layer,
    IList<ComponentRoleDefinition> roles)
{
    foreach (var role in roles)
    {
        if (!string.Equals(role.Layer, layer, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (typeName.EndsWith(role.Name, StringComparison.OrdinalIgnoreCase))
        {
            return role;
        }

        foreach (var alt in role.AlternativeNames)
        {
            if (typeName.EndsWith(alt, StringComparison.OrdinalIgnoreCase))
            {
                return role;
            }
        }
    }

    return null;
}

private static CSharpTypeKind GetActualTypeKind(TypeDeclarationSyntax typeDecl)
{
    return typeDecl switch
    {
        RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            => CSharpTypeKind.RecordStruct,
        RecordDeclarationSyntax => CSharpTypeKind.Record,
        InterfaceDeclarationSyntax => CSharpTypeKind.Interface,
        StructDeclarationSyntax => CSharpTypeKind.Struct,
        _ => CSharpTypeKind.Class,
    };
}

private static void CheckTypeKind(
    TypeDeclarationSyntax typeDecl,
    ComponentRoleDefinition role,
    string filePath,
    List<SourceViolation> violations)
{
    var actual = GetActualTypeKind(typeDecl);
    if (actual != role.TypeKind)
    {
        violations.Add(new SourceViolation
        {
            FilePath = filePath,
            ViolationType = "TypeKindMismatch",
            Message = $"Type '{typeDecl.Identifier.Text}' matches role '{role.Name}' and should be a '{role.TypeKind}' but is declared as a '{actual}'",
        });
    }
}
```

Update `Validate` method to pass `resolvedPack.ComponentRoles` through to `ValidateFile`, and update `ValidateFile`'s signature accordingly.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: ALL tests PASS.

**Step 5: Run full test suite**

Run: `dotnet build src/ArchGov.slnx && dotnet test src/ArchGov.slnx`
Expected: All tests pass, 0 warnings.

**Step 6: Commit**

```bash
git add src/ArchGov.Core/Validation/SourceValidator.cs tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs
git commit -m "feat: add type kind validation with suffix-based role matching"
```

---

### Task 2: Add AccessibilityMismatch validation

**Files:**
- Modify: `src/ArchGov.Core/Validation/SourceValidator.cs`
- Modify: `tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Validate_RepositoryIsPublic_ShouldReturnAccessibilityMismatch()
{
    var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
    Directory.CreateDirectory(infraDir);
    File.WriteAllText(
        Path.Combine(infraDir, "OrderRepository.cs"),
        """
        namespace MyApp.Orders.Infrastructure;

        public class OrderRepository { }
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.Violations.Should().Contain(v => v.ViolationType == "AccessibilityMismatch");
}

[Fact]
public void Validate_RepositoryIsInternal_ShouldNotReportAccessibilityMismatch()
{
    var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
    Directory.CreateDirectory(infraDir);
    File.WriteAllText(
        Path.Combine(infraDir, "OrderRepository.cs"),
        """
        namespace MyApp.Orders.Infrastructure;

        internal class OrderRepository { }
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.Violations.Where(v => v.ViolationType == "AccessibilityMismatch").Should().BeEmpty();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: FAIL — no AccessibilityMismatch violations produced.

**Step 3: Implement accessibility check**

Add to `ValidateTypeDeclarations` (after `CheckTypeKind`):

```csharp
CheckAccessibility(typeDecl, matchedRole, filePath, violations);
```

Add helper method:

```csharp
private static void CheckAccessibility(
    TypeDeclarationSyntax typeDecl,
    ComponentRoleDefinition role,
    string filePath,
    List<SourceViolation> violations)
{
    var expectedAccessibility = role.Accessibility;
    if (string.IsNullOrEmpty(expectedAccessibility) ||
        string.Equals(expectedAccessibility, "public", StringComparison.OrdinalIgnoreCase))
    {
        // Default is public — only report if role explicitly requires non-public
        return;
    }

    var isInternal = typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword));
    if (string.Equals(expectedAccessibility, "internal", StringComparison.OrdinalIgnoreCase) && !isInternal)
    {
        violations.Add(new SourceViolation
        {
            FilePath = filePath,
            ViolationType = "AccessibilityMismatch",
            Message = $"Type '{typeDecl.Identifier.Text}' matches role '{role.Name}' and should be '{expectedAccessibility}' but is declared 'public'",
        });
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: ALL tests PASS.

**Step 5: Run full test suite + build**

Run: `dotnet build src/ArchGov.slnx && dotnet test src/ArchGov.slnx`
Expected: All tests pass, 0 warnings.

**Step 6: Commit**

```bash
git add src/ArchGov.Core/Validation/SourceValidator.cs tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs
git commit -m "feat: add accessibility mismatch validation for role-matched types"
```

---

### Task 3: Add MissingInterface validation

**Files:**
- Modify: `src/ArchGov.Core/Validation/SourceValidator.cs`
- Modify: `tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Validate_RepositoryWithoutInterface_ShouldReturnMissingInterface()
{
    // Repository exists but no matching RepositoryInterface
    var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
    Directory.CreateDirectory(infraDir);
    File.WriteAllText(
        Path.Combine(infraDir, "OrderRepository.cs"),
        """
        namespace MyApp.Orders.Infrastructure;

        internal class OrderRepository { }
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.Violations.Should().Contain(v => v.ViolationType == "MissingInterface");
}

[Fact]
public void Validate_RepositoryWithInterface_ShouldNotReportMissingInterface()
{
    // Both Repository and RepositoryInterface exist
    var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
    Directory.CreateDirectory(infraDir);
    File.WriteAllText(
        Path.Combine(infraDir, "OrderRepository.cs"),
        """
        namespace MyApp.Orders.Infrastructure;

        internal class OrderRepository { }
        """);

    var domainDir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
    Directory.CreateDirectory(domainDir);
    File.WriteAllText(
        Path.Combine(domainDir, "IOrderRepository.cs"),
        """
        namespace MyApp.Orders.Domain;

        public interface IOrderRepository { }
        """);

    var validator = new SourceValidator();
    var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

    result.Violations.Where(v => v.ViolationType == "MissingInterface").Should().BeEmpty();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: FAIL — no MissingInterface violations produced.

**Step 3: Implement interface requirement check**

The interface check requires cross-file awareness — we need to know all type names across all scanned files. Refactor the validation to collect type declarations in a first pass, then check interface requirements in a second pass.

Update the `Validate` method to use a two-pass approach:

```csharp
public SourceValidationResult Validate(RulePack resolvedPack, string sourceDirectory)
{
    ArgumentNullException.ThrowIfNull(resolvedPack);
    ArgumentNullException.ThrowIfNull(sourceDirectory);

    var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
    var violations = new List<SourceViolation>();
    var knownLayers = new HashSet<string>(
        resolvedPack.Layers.Select(l => l.Name),
        StringComparer.OrdinalIgnoreCase);

    var allowedDependencies = BuildAllowedDependencies(resolvedPack);

    // Collect all type names across files (for interface requirement checks)
    var allTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var filePath in csFiles)
    {
        ValidateFile(filePath, knownLayers, allowedDependencies,
            resolvedPack.ComponentRoles, violations, allTypeNames);
    }

    // Second pass: check interface requirements
    CheckInterfaceRequirements(resolvedPack.ComponentRoles, allTypeNames, violations);

    return new SourceValidationResult
    {
        IsValid = violations.Count == 0,
        Violations = violations,
        FilesScanned = csFiles.Length,
    };
}
```

Update `ValidateFile` to collect type names and track matched roles:

In `ValidateTypeDeclarations`, add each type name to `allTypeNames`:

```csharp
// Inside the typeDeclarations loop, before role matching:
allTypeNames.Add(typeName);
```

Add the interface requirement check as a separate method that runs after all files are scanned:

```csharp
private static void CheckInterfaceRequirements(
    IList<ComponentRoleDefinition> roles,
    HashSet<string> allTypeNames,
    List<SourceViolation> violations)
{
    // Find all types that match roles requiring interfaces
    // For each, check if a type matching the interface role suffix exists
    foreach (var role in roles)
    {
        if (!role.RequiresInterface || string.IsNullOrEmpty(role.InterfaceRole))
        {
            continue;
        }

        var interfaceRole = roles.FirstOrDefault(r =>
            string.Equals(r.Name, role.InterfaceRole, StringComparison.OrdinalIgnoreCase));
        if (interfaceRole is null)
        {
            continue;
        }

        // Find types matching the implementation role
        var implementationTypes = allTypeNames
            .Where(t => t.EndsWith(role.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var implType in implementationTypes)
        {
            // Check if there's a matching interface type
            var hasInterface = allTypeNames.Any(t =>
                t.EndsWith(interfaceRole.Name, StringComparison.OrdinalIgnoreCase));

            if (!hasInterface)
            {
                violations.Add(new SourceViolation
                {
                    FilePath = "",
                    ViolationType = "MissingInterface",
                    Message = $"Type '{implType}' matches role '{role.Name}' which requires interface role '{role.InterfaceRole}', but no matching interface was found",
                });
            }
        }
    }
}
```

> **Note:** The `FilePath` for MissingInterface violations is empty since it's a cross-file check. The implementer may choose to track which file each implementation type came from if needed.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SourceValidatorTests"`
Expected: ALL tests PASS.

**Step 5: Run full test suite + build**

Run: `dotnet build src/ArchGov.slnx && dotnet test src/ArchGov.slnx`
Expected: All tests pass, 0 warnings.

**Step 6: Commit**

```bash
git add src/ArchGov.Core/Validation/SourceValidator.cs tests/ArchGov.Core.Tests/Validation/SourceValidatorTests.cs
git commit -m "feat: add missing interface validation for requiresInterface roles"
```

---

### Task 4: Verify all existing tests still pass + fix any warnings

**Step 1: Build and test entire solution**

Run: `dotnet build src/ArchGov.slnx && dotnet test src/ArchGov.slnx`
Expected: ALL tests pass (98+ existing + 6 new role validation tests), 0 warnings.

**Step 2: Fix any MA0051 or other analyzer warnings**

The SourceValidator may now be getting long. If `ValidateFile` or `ValidateTypeDeclarations` exceeds 60 lines, extract methods as needed to satisfy MA0051.

**Step 3: Commit if any fixes were needed**

```bash
git add -A
git commit -m "refactor: fix analyzer warnings in SourceValidator"
```

---

## Summary

| Task | What | New Tests |
|------|------|-----------|
| 1 | Suffix-based role matching + TypeKindMismatch | 2 |
| 2 | AccessibilityMismatch | 2 |
| 3 | MissingInterface (cross-file) | 2 |
| 4 | Full suite verification + cleanup | 0 |

**Total new tests:** 6
**Total tasks:** 4
**Files modified:** 2 (SourceValidator.cs, SourceValidatorTests.cs)
