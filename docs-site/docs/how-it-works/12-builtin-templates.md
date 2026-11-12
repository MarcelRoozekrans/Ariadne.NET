---
sidebar_position: 12
---

# Built-in Templates

Ten templates ship as embedded YAML resources, all in schema 2.

Every layer of a standalone template has an `id` with a pack prefix (`CA-` for clean-architecture, `ONION-`, `HEX-`, `LAY-`, `MS-`, `ED-`, `VS-` and `MM-` for the others, as in `CA-LAYER-DOMAIN`), a `because` written for that architecture, and a namespace selector of the form `**.<Segment>.**`. `Domain` in clean-architecture is `**.Domain.**`, which matches `Shop.Domain` and `Shop.Orders.Domain.Entities`. A solution whose namespaces do not follow that naming retargets the layers; see [Rule Schema](03-rule-schema.md#extends). Only `modular-monolith` defines `rules`: one slices rule that keeps modules out of each other's internals.

The templates fall into two categories:

## Standalone Templates

Standalone templates define their own layers and can be used independently.

### clean-architecture

4 layers, 19 roles. The classic Clean Architecture pattern (Robert C. Martin).

| Layer | Dependencies | Roles |
|-------|-------------|-------|
| Domain | *(none)* | Entity, ValueObject, DomainService, DomainServiceInterface, DomainEvent, Specification, RepositoryInterface |
| Application | Domain | CommandHandler, CommandHandlerInterface, QueryHandler, QueryHandlerInterface, Dto, Mapper, Validator, ExternalServiceInterface |
| Infrastructure | Domain, Application | Repository (internal), ExternalService (internal) |
| WebApi | Domain, Application, Infrastructure | Controller, Middleware |

```mermaid
graph LR
    WebApi --> Infrastructure
    WebApi --> Application
    WebApi --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    Application --> Domain
```

### onion-architecture

5 layers, 14 roles. Onion Architecture (Jeffrey Palermo).

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| DomainModel | *(none)* | Entity, ValueObject, DomainEnum |
| DomainServices | DomainModel | RepositoryInterface, DomainServiceInterface, DomainEvent |
| ApplicationServices | DomainModel, DomainServices | ApplicationService, ApplicationServiceInterface, Dto |
| Infrastructure | DomainModel, DomainServices, ApplicationServices | Repository (internal), DomainService (internal), DbContext |
| Presentation | DomainModel, DomainServices, ApplicationServices | Controller, ViewModel |

```mermaid
graph LR
    Presentation --> ApplicationServices
    Presentation --> DomainServices
    Presentation --> DomainModel
    Infrastructure --> ApplicationServices
    Infrastructure --> DomainServices
    Infrastructure --> DomainModel
    ApplicationServices --> DomainServices
    ApplicationServices --> DomainModel
    DomainServices --> DomainModel
```

### hexagonal-architecture

3 layers, 7 roles. Ports & Adapters pattern (Alistair Cockburn).

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Core | *(none)* | Entity, ValueObject, DomainService |
| Ports | Core | DrivingPort (Interface), DrivenPort (Interface) |
| Adapters | Core, Ports | PrimaryAdapter, SecondaryAdapter (internal) |

```mermaid
graph LR
    Adapters --> Ports
    Adapters --> Core
    Ports --> Core
```

### layered

3 layers, 11 roles. Traditional N-tier / layered architecture.

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Presentation | Business | Controller, ViewModel, ApiModel |
| Business | DataAccess | Service, ServiceInterface, Validator, BusinessModel |
| DataAccess | *(none)* | Repository, RepositoryInterface, DbContext, DataModel |

Note: dependency direction is **top-down** (Presentation → Business → DataAccess), unlike Clean Architecture where it flows inward.

```mermaid
graph TD
    Presentation --> Business
    Business --> DataAccess
```

### microservice

5 layers, 17 roles. Per-service microservice structure.

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Domain | *(none)* | Entity, Aggregate, ValueObject, DomainEvent, RepositoryInterface |
| Application | Domain, Contracts | Handler, ApplicationService, ApplicationServiceInterface |
| Infrastructure | Domain, Application, Contracts | Repository (internal), DbContext, MessagePublisher, MessageConsumer |
| Api | Domain, Application, Contracts | Controller, GrpcService |
| Contracts | *(none)* | IntegrationEvent, ApiRequest, ApiResponse |

Note: `Contracts` is a leaf layer with no dependencies — it contains shared DTOs and integration events that other services can consume.

```mermaid
graph TD
    Api --> Application
    Api --> Domain
    Api --> Contracts
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> Contracts
    Application --> Domain
    Application --> Contracts
```

### event-driven

5 layers, 9 roles. Event-driven architecture.

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Domain | *(none)* | *(inherits from composed packs)* |
| Events | Domain | Event, IntegrationEvent |
| Producers | Domain, Events | EventProducer, EventProducerInterface |
| Consumers | Domain, Events | EventConsumer, EventConsumerInterface, EventHandler |
| Infrastructure | Domain, Events, Producers, Consumers | MessageBrokerAdapter (internal), MessageBrokerInterface |

```mermaid
graph TD
    Infrastructure --> Producers
    Infrastructure --> Consumers
    Infrastructure --> Events
    Infrastructure --> Domain
    Producers --> Events
    Producers --> Domain
    Consumers --> Events
    Consumers --> Domain
    Events --> Domain
```

### vertical-slice

3 layers, 8 roles. Vertical slice / feature-based architecture.

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Feature | *(none)* | Request, Response, Handler, Validator, Endpoint |
| SharedKernel | *(none)* | SharedEntity, SharedDto |
| Infrastructure | Feature, SharedKernel | DbContext |

```mermaid
graph TD
    Infrastructure --> Feature
    Infrastructure --> SharedKernel
```

### modular-monolith

4 layers, 8 roles. Modular monolith pattern.

| Layer | Dependencies | Key Roles |
|-------|-------------|-----------|
| Module.Public | *(none)* | ModuleContract (Interface), ModuleEvent, ModuleDto |
| Module.Core | Module.Public | ModuleService (internal), ModuleEntity, ModuleRepositoryInterface |
| Module.Infrastructure | Module.Public, Module.Core | ModuleRepository (internal) |
| Host | Module.Public, Module.Core, Module.Infrastructure | ModuleRegistration |

```mermaid
graph TD
    Host --> Module.Infrastructure
    Host --> Module.Core
    Host --> Module.Public
    Module.Infrastructure --> Module.Core
    Module.Infrastructure --> Module.Public
    Module.Core --> Module.Public
```

The layers select `**.Public.**`, `**.Core.**`, `**.Infrastructure.**` and `**.Host.**`, so `Orders.Core` and `Billing.Core` are one layer, and the layer rules alone would let `Orders.Core` depend on `Billing.Core`. The pack's one rule, `MM-MODULES-INDEPENDENT`, keeps modules apart:

```yaml
rules:
  - id: MM-MODULES-INDEPENDENT
    because: A module may use another module only through that module's public API.
    allowEmpty: true
    slices:
      matching: ["**.(*).Public.**", "**.(*).Core.**", "**.(*).Infrastructure.**"]
      independent: true
      allow: { layers: [Module.Public] }
```

It is a [slices rule](rule-types.md#slices). The module is the namespace segment before `Public`, `Core` or `Infrastructure`, so `Shop.Orders.Core.OrderService` is in slice `Orders` and `Shop.Billing.Public.IInvoicing` in slice `Billing`. What it allows and what it reports:

| From | To | Result |
|------|----|--------|
| `Orders.Core` | `Orders.Public` (own module) | allowed: same slice |
| `Billing.Core` | `Orders.Public` (another module's public API) | allowed by `allow` |
| `Orders.Core` or `Orders.Infrastructure` | `Billing.Core` or `Billing.Infrastructure` | `SliceDependency` violation |
| `Host` | any module's `Public`, `Core` or `Infrastructure` | allowed: the host is in no slice |
| `Shop.SharedKernel` | anything | allowed: shared code outside the `Public`, `Core` and `Infrastructure` shape has no slice and is free to use |

Inside one module the layer rules still apply, so `Orders.Core` may not use `Orders.Infrastructure`. `allowEmpty: true` matches the pack's roles: a solution with no namespace in this shape gets no vacuity error for the rule. A team that has a type that must reach into another module adds it to the rule's `exceptions` in its own pack, with an entry that has only `id: MM-MODULES-INDEPENDENT` and `exceptions`. Extending the rule's `allow` from a child pack is not possible yet; it is tracked in #103.

Cycles between modules through their public APIs are not reported: `Orders.Core` may use `Billing.Public` while `Billing.Core` uses `Orders.Public`, because `allow` removes those edges before the check. A team that wants module dependencies to form no cycle adds its own rule with `freeOfCycles: true` over the same patterns, without `allow`.

## Composable Add-on Templates

Add-on templates define only component roles (no layers). They are designed to be composed with standalone templates via the `extends` field. Only role-only add-ons compose with a layer pack: two templates that both define layers fail to compose.

### cqrs

7 roles. Command/Query Responsibility Segregation.

| Role | Layer | TypeKind |
|------|-------|----------|
| Command | Application | Record |
| CommandHandler | Application | Class (requires interface) |
| CommandHandlerInterface | Application | Interface |
| Query | Application | Record |
| QueryHandler | Application | Class (requires interface) |
| QueryHandlerInterface | Application | Interface |
| QueryResult | Application | Record |

**Usage:** `extends: [builtin://clean-architecture, builtin://cqrs]`

### ddd-tactical

9 roles. Domain-Driven Design tactical patterns.

| Role | Layer | TypeKind |
|------|-------|----------|
| Aggregate | Domain | Class |
| AggregateRoot | Domain | Class |
| ValueObject | Domain | Record |
| DomainEvent | Domain | Record |
| DomainService | Domain | Class (requires interface) |
| DomainServiceInterface | Domain | Interface |
| Specification | Domain | Class |
| Factory | Domain | Class |
| DomainException | Domain | Class |

**Usage:** `extends: [builtin://clean-architecture, builtin://ddd-tactical]`

## Composing Templates

Templates compose via the merge described in [Rule Engine](05-rule-engine.md). A layer or rule can be defined by one pack only, so exactly one standalone template can be the base, and the add-ons come on top of it:

```yaml
# .architecture/rules.yaml
schema: 2
name: my-project
extends:
  - builtin://clean-architecture    # base: 4 layers, 19 roles
  - builtin://cqrs                  # add-on: +7 roles
  - builtin://ddd-tactical          # add-on: +9 roles (some overlap)

# Team-specific additions
componentRoles:
  - name: OutboxMessage
    layer: Infrastructure
    typeKind: Class
```

Naming two standalone templates fails the load. `clean-architecture+microservice` gives, for each layer both define, a message such as `packs 'clean-architecture' and 'microservice' both define layer 'Domain'`. Pick one base architecture and retarget its layers instead.

The `cqrs` and `ddd-tactical` roles name the Application and Domain layers but declare no layers of their own. They resolve on their own, but combine them with a base pack that declares those layers; once the merged pack has any layer, a role naming an undeclared layer is an Invalid error. Composing `ddd-tactical` with a base pack that has no `Domain` layer, such as `hexagonal-architecture`, fails for that reason.

The resolved pack will have:
- **Name:** `"clean-architecture+cqrs+ddd-tactical+my-project"`
- **Layers:** 4 (from clean-architecture, unmodified since add-ons define no layers)
- **Roles:** ~30 (19 + 7 + 9, minus overlaps where later packs override)
- **Rules:** none, because no built-in pack defines rules yet

---
