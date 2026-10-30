# Dependency Injection

All core services are registered with a single extension method call:

```csharp
services.AddArchGovCoreServices();
```

The method is generated at compile time by [ZInject](https://www.nuget.org/packages/ZInject): every service class carries a `[Singleton]` attribute, and the source generator emits the registrations. The MCP host project gets its own `AddArchGovHostMcpServices()` the same way, which registers `RuleResolutionHelper`. Adding a service means adding the attribute; there is no registration list to maintain.

## Registration Table

All services use **Singleton** lifetime:

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IRulePackLoader` | `YamlRulePackLoader` | YAML → RulePack deserialization |
| `IBuiltinPackRegistry` | `BuiltinPackRegistry` | Embedded template discovery and loading |
| `IRulePackMerger` | `RulePackMerger` | Multi-pack composition |
| `IRulePackResolver` | `RulePackResolver` | Reference resolution (`builtin://`, `import://`) |
| `IDependencyValidator` | `DependencyValidator` | Layer dependency graph enforcement |
| `ILayerInferenceEngine` | `LayerInferenceEngine` | Role → layer mapping |
| `ICodeGeneratorService` | `CodeGeneratorService` | Roslyn-based C# generation |
| `IGenerateOrchestrator` | `GenerateOrchestrator` | Multi-component generation workflow |
| `IWorkspaceOrchestrator` | `WorkspaceOrchestrator` | Project structure scaffolding |
| `ISourceValidator` | `SourceValidator` | Deep source validation (two-pass) |
| `ILinter` | `Linter` | Shallow namespace/directory check |
| `IImportsLoader` | `ImportsYamlLoader` | Imports manifest parsing |
| `IGitSyncProvider` | `GitCliSyncProvider` | Git clone, tag resolution, caching |
| `ISyncOrchestrator` | `SyncOrchestrator` | Multi-import sync coordination |

## Why Singleton?

All services are stateless — they don't hold per-request state. The `BuiltinPackRegistry` caches loaded packs in a thread-safe dictionary, which benefits from Singleton lifetime. `CodeGeneratorService` creates a fresh `AdhocWorkspace` per call, avoiding shared mutable state.

---

Previous: [Host Applications](10-host-applications.md) | Next: [Built-in Templates](12-builtin-templates.md)
