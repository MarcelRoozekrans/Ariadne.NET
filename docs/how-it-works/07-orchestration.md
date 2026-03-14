# Orchestration

Orchestrators wire together the lower-level services into high-level workflows.

## GenerateOrchestrator

**Interface:** `IGenerateOrchestrator`
**Implementation:** `GenerateOrchestrator`

Generates code for one or more components in a single request.

```
IGenerateOrchestrator
  └── Generate(RulePack resolvedPack, GenerateRequest request) → GenerateResult
```

**Request/Response models:**

```
GenerateRequest
  ├── SolutionName: string        (e.g., "MyApp")
  ├── Feature: string             (e.g., "Orders")
  └── Components: IReadOnlyList<ComponentRequest>
        ├── RoleName: string      (e.g., "Entity")
        └── Name: string          (e.g., "Order")

GenerateResult
  ├── Success: bool               (true if no errors)
  ├── GeneratedFiles: IReadOnlyList<CodeGenerationResult>
  ├── Errors: IReadOnlyList<string>
  └── DependencyViolations: IReadOnlyList<DependencyViolation>
```

**Algorithm:**

```mermaid
flowchart TD
    A["For each component in request.Components"] --> B["1. Infer layer<br/>inferenceEngine.Infer(pack, roleName)"]
    B --> C{"Inference status?"}
    C -->|Ok| D["2. Generate code<br/>codeGenerator.Generate(pack, request)"]
    C -->|Ambiguous| E["Add error: ambiguous role<br/><i>Skip component</i>"]
    C -->|Invalid| F["Add error: invalid role<br/><i>Skip component</i>"]
    D --> G["Add to generatedFiles"]
    G --> H["Next component..."]
    E --> H
    F --> H
    H --> I["Return GenerateResult<br/><i>Success = errors.Count == 0</i>"]
```

## WorkspaceOrchestrator

**Interface:** `IWorkspaceOrchestrator`
**Implementation:** `WorkspaceOrchestrator`

Generates the full project directory and `.csproj` path structure for a solution based on the Cartesian product of features and layers.

```
IWorkspaceOrchestrator
  └── GenerateWorkspace(RulePack resolvedPack, WorkspaceGenerateRequest request) → WorkspaceGenerateResult
```

**Algorithm:**

```mermaid
flowchart TD
    A["WorkspaceGenerateRequest<br/><i>SolutionName, Features</i>"] --> B["Cartesian product:<br/>Features × Layers"]
    B --> C["For each (feature, layer) pair"]
    C --> D["Resolve projectName from<br/>NamingConventions.Namespace template"]
    D --> E["Add src/projectName to directories"]
    D --> F["Add src/projectName/projectName.csproj<br/>to projectPaths"]
```

**Example:**

```mermaid
graph TD
    subgraph Input
        S["SolutionName: MyApp"]
        FE["Features: Orders, Customers"]
        LA["Layers: Domain, Application, Infrastructure"]
    end

    subgraph Output["Output (Features x Layers)"]
        OD["src/MyApp.Orders.Domain/"]
        OA["src/MyApp.Orders.Application/"]
        OI["src/MyApp.Orders.Infrastructure/"]
        CD["src/MyApp.Customers.Domain/"]
        CA["src/MyApp.Customers.Application/"]
        CI["src/MyApp.Customers.Infrastructure/"]
    end

    S --> Output
    FE --> Output
    LA --> Output
```

---

Previous: [Code Generation](06-code-generation.md) | Next: [Validation and Linting](08-validation.md)
