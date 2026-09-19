# Backend World Catalog — what exists in .NET

*Last updated: 2026-05-04*

> Companion: [`targets.md`](./targets.md) — what *we'll* implement, prioritized.
>
> **Purpose**: enumerate the universe of .NET backend tech, libraries, runtime APIs, patterns, and conventions. **No verdicts.** Source of ideas, not a roadmap.
>
> **Target package**: `wow-two-sdk.backend.beta` — single big NuGet bundling everything. Dependency bloat is explicitly accepted; the goal is one-import, batteries-included.
>
> **Scope today**: web/API backends (HTTP, gRPC, GraphQL, real-time), data access (relational, NoSQL, event store), cross-cutting concerns (DI, config, logging, validation, mapping, resilience, observability, security, caching, rate limiting, jobs, health, feature flags, multi-tenancy, i18n), messaging (in-process + distributed), search, storage, AI/LLM/vector, time, files, comms (email/sms/push), workflow/state machines, BCL/runtime APIs, testing surface, tooling. **Future scope**: client SDK generation, source-only deliverables, AOT-only branch — sketched in §10.

---

## 0. Reading guide

| § | Chapter | Read when… |
|---|---|---|
| 1 | Architectural philosophies | Choosing how the package fits together (Clean / VS / DDD / CQRS / actor / reactive) |
| 2 | Reference architectures | Looking for templates / sample code / existing distillations |
| 3 | Frameworks survey (compact matrix) | Picking what to wrap or borrow |
| 4 | Cross-cutting vectors | Designing any abstraction (DI, logging, validation, …) |
| 5 | Domain matrices | Speccing a single area (HTTP, EF, messaging, …) |
| 6 | Delegate / extension API surface | Designing customization hooks |
| 7 | Configuration leak point inventory | Speccing what consumers must supply |
| 8 | .NET runtime / BCL encyclopedia | Wrapping a lower-level API |
| 9 | Tooling ecosystem | Build, pack, ship, observe |
| 10 | Future scope notes | When considering scope expansion |

---

## 1. Architectural philosophies

The schools dominating .NET backend design. Each has a mental model, a set of strengths, and a typical cost.

| School | Mental model | Representatives / canonical refs | Strengths | Cost |
|---|---|---|---|---|
| **Clean Architecture** | Concentric rings; deps point inward; domain at the core | Uncle Bob; `ardalis/CleanArchitecture`; Jason Taylor template | Decoupled domain; easy to test core | Verbose for small apps; many projects |
| **Onion** | Variant of Clean — domain center, infra outermost | Jeffrey Palermo | Same as Clean | Same as Clean |
| **Hexagonal / Ports & Adapters** | Domain in the middle, ports as interfaces, adapters as impls | Alistair Cockburn; many .NET refs | Swap external systems trivially | Naming overhead, indirection |
| **Vertical Slice** | One feature = handler + DTOs + data access stacked top-to-bottom | Jimmy Bogard; `JimmyBogard/ContosoUniversityCore`; many MediatR samples | Low cross-feature coupling, file-locality | Cross-cutting concerns get duplicated |
| **Modular Monolith** | One deployable, multiple modules with private DBs | Kamil Grzybek `ModularMonolith.Sample` | Clear boundaries pre-microservices | Some boilerplate per module |
| **Microservices** | Each service own DB + lifecycle | eShopOnContainers, Steeltoe, Dapr | Independent deploy, tech freedom | Operational + distributed-systems cost |
| **CQRS** | Commands ≠ Queries; separate models | MediatR + EF; Marten + Postgres | Read perf optimization, intent clarity | Two models to keep in sync |
| **Event Sourcing** | State = fold of events | EventStoreDb (Kurrent), Marten, EventFlow, NEventStore | Audit, replay, projections | Schema evolution complexity |
| **DDD (tactical)** | Entity / Value Object / Aggregate / Domain Event / Service / Repository | Eric Evans + Vaughn Vernon books; many .NET samples | Ubiquitous language, behavior-first | Steeper team learning curve |
| **DDD (strategic)** | Bounded Contexts, Context Maps, Subdomains | Same | Macro decomposition | Hard with green field unclear domain |
| **Actor model** | State + behavior + mailbox; single-threaded per actor | Microsoft Orleans, Akka.NET, Proto.Actor | Stateful workloads, IoT, gaming, sims | Mental shift, less ORM-friendly |
| **Reactive (Rx)** | Streams of events; back-pressure; functional ops | System.Reactive, RxNET, Akka.Streams | Event flows, complex event processing | Debugging, learning curve |
| **Pipeline (mediator)** | Request → behaviors → handler | MediatR, Brighter, Wolverine, Mediator (sg) | Cross-cutting via behaviors, easy | Coupling to mediator lib |
| **Saga / Process Manager** | Long-running coordination across messages | MassTransit Saga, NServiceBus Saga, Marten Saga | Encapsulates multi-step flows | State persistence + idempotency required |
| **Outbox / Inbox** | Transactional messaging without 2PC | DotNetCore.CAP, MassTransit Outbox, Wolverine TX | Reliable cross-bus delivery | Schema + dispatcher overhead |
| **API Gateway / BFF** | Edge layer aggregates downstream APIs | YARP, Ocelot, Duende.BFF | Auth concentrating, response shaping | Extra hop, ownership of edge |
| **Service Mesh** | Sidecar handles cross-cutting infra | Istio, Linkerd (config'd via .NET configs) | Lang-agnostic, network policy | Operational complexity |
| **Strangler Fig** | Replace legacy by routing slices to new system | Routing + ProxyKit / YARP rules | Incremental migration | Long parallel period |
| **Anti-Corruption Layer** | Translate alien model at the boundary | Pure pattern, no specific lib | Keeps domain clean | Mapping layer to maintain |
| **12-Factor App** | Build, config, processes, port-binding, stateless | Docker, Aspire, k8s deployments | Cloud-native portability | Some constraints awkward locally |
| **Serverless / FaaS** | Functions react to events | Azure Functions, AWS Lambda, Google Cloud Run | Scale-to-zero, op simplicity | Cold start, vendor coupling |
| **Polyrepo vs Monorepo** | One repo per package vs one big | Both seen in .NET; we use polyrepo at workspace level | Coupling control vs unified change | Tooling differs |
| **Polyglot persistence** | Right DB per workload | Postgres + Redis + Elastic + S3 + Mongo combos | Workload-fit | Ops sprawl |
| **Event-driven** | Producers don't know consumers | Kafka, NATS, RMQ, ASB, EventBridge | Decoupling, replay | Eventual consistency |
| **Pipes & Filters** | Stages compose into pipelines | TPL Dataflow, Channels, Akka.Streams, Rx | Streaming/transform graphs | Less natural for request/response |

### Distribution variants (orthogonal to school)

| Variant | Description | Examples |
|---|---|---|
| **Single big package** | One NuGet, all features | `ServiceStack`, our `wow-two-sdk.backend.beta` |
| **Per-concern packages** | Many small NuGets per feature | `Microsoft.Extensions.*`, `Polly.*` |
| **Meta-package** | Empty package depending on a curated set | `Microsoft.AspNetCore.App` (framework ref), `Polly.Extensions.Http` |
| **Source-only** | Code shipped as `.cs` files compiled into consumer | Source generators, T4 templates, shadcn-style copy |
| **Source generator** | Roslyn analyzer emitting code at compile | `MapsterMapper`, `Mediator`, `Mapperly`, `Vogen` |
| **Project template** | `dotnet new` template | `Ardalis.CleanArchitecture.Solution.Template`, `Aspire.Starter` |
| **Reference architecture** | Sample repo to fork or read | eShop, eShopOnDapr, Modular Monolith Saas |
| **Distillation framework** | Opinionated full-stack abstraction | ABP Framework, OrchardCore, ServiceStack, Suave (F#) |

---

## 2. Reference architectures (the ".NET design systems")

Repos and templates that codify a way of building. Treat these as competitors / inspirations / sources of distillation.

| Reference | Owner | Philosophy nucleus | Signature ideas |
|---|---|---|---|
| **eShopOnContainers** | Microsoft | Microservices reference | DDD + CQRS + Event Bus + Docker Compose + Identity |
| **eShopOnDapr** | Microsoft | Microservices on Dapr | Service invocation, pub/sub, state, secrets via Dapr building blocks |
| **eShopOnWeb** | Microsoft (Steve Smith) | Modular monolith | Razor + MVC + Specs + Clean Arch lite |
| **eShop** (latest) | Microsoft | Aspire + AI showcase | .NET Aspire orchestration, semantic search, OpenTelemetry |
| **Ardalis Clean Architecture** | Steve Smith | Clean Arch template | Web/Core/Infra split; UseCases; SmartEnum; Specifications |
| **Jason Taylor Clean Architecture** | Jason Taylor | Clean Arch + DDD + CQRS | MediatR + FluentValidation + EF + Identity + Angular FE |
| **Modular Monolith Sample** | Kamil Grzybek | Modular Monolith | Module per bounded context, private schema |
| **Modular Monolith SaaS** | Kamil Grzybek | Modular SaaS template | Tenancy + Modules + Domain Events |
| **DDD Forum** | Khalil Stemmler | Tactical DDD | Aggregate, repo, dispatcher (TS, but often referenced) |
| **Vertical Slice Architecture template** | Jimmy Bogard et al. | VS Architecture | Slice = Endpoint+Handler+Validator+DTO+Persistence in one folder |
| **ABP Framework** | Volosoft | Opinionated full-stack DDD framework | Modular + Tenancy + DDD + UI; commercial extension |
| **OrchardCore** | OrchardCore community | Modular CMS framework | Tenants, modules, tenants-per-shell, Microsoft.AspNetCore |
| **ServiceStack** | ServiceStack Inc | Type-safe service framework | DTO-first, multi-format auto-serialization, AutoQuery |
| **Steeltoe** | VMware/Tanzu | Spring Cloud for .NET | Eureka, Config Server, Hystrix-like, OAuth |
| **Microsoft Aspire** | Microsoft | Orchestration host + integrations | AppHost project, ServiceDefaults, OTel out-of-box, dev dashboard |
| **Brighter** | Iain Couzens et al. | Command Processor + Outbox | Pipeline behaviors, transactional outbox, sagas |
| **Wolverine (JasperFx)** | Jeremy D. Miller | Runtime mediator + messaging | Conventional handler discovery, transactional inbox/outbox, codegen |
| **Marten** | JasperFx | Postgres as document DB + event store | LINQ-to-jsonb, projections, async daemon |
| **NetCorePal** | NetCorePal community | Chinese DDD framework | Modular DDD, source-gen for boilerplate |
| **NServiceBus / Particular** | Particular | Commercial messaging + sagas | Industrial-strength messaging, ServiceControl, ServicePulse |
| **MassTransit** | Chris Patterson | Open messaging abstraction | Consumers, sagas, state machines, multi-broker |
| **Microsoft.SemanticKernel** | Microsoft | LLM orchestration framework | Plugins, planners, memory, connectors |
| **Microsoft.KernelMemory** | Microsoft | Vector memory pipeline | Ingest → embed → query, multi-store |
| **DotNet Boxed templates** | Muhammad Rehan Saeed | Hardened API templates | OpenTelemetry, security, perf tuned defaults |
| **DotnetTemplates by NimblePros** | NimblePros | Modern starter set | Aspire + Clean Arch combos |
| **Reqnroll** | Reqnroll team | BDD reborn (post-SpecFlow) | Gherkin-driven test runner |
| **smartstore** | SmartStore team | E-commerce platform | Module-based, plugins |
| **nopCommerce** | nopCommerce | E-commerce platform | Plugins, multi-store |
| **DotNetNuke (DNN)** | DNN community | CMS platform | Skin/module model |
| **Umbraco** | Umbraco HQ | CMS / DXP | Headless + traditional, content tree |
| **Sitefinity** | Progress (commercial) | Enterprise DXP | WCM + commerce + analytics |

### "Reference architectures" of the actor / reactive worlds

| Reference | Owner | Niche |
|---|---|---|
| **Microsoft Orleans samples** | Microsoft | Virtual actor patterns, streams, reminders |
| **Akka.NET samples** | Akka.NET | Persistent actors, sharding, event sourcing |
| **Proto.Actor samples** | Asynkron | Lightweight actors, gRPC remoting |
| **Dapr quickstarts** | CNCF / Microsoft | Building blocks: state, pubsub, secrets, bindings, actors |

---

## 3. Frameworks survey (compact matrix)

Compact axes per framework. ✓ strong · ◐ partial · ○ weak / none.

### 3.1 HTTP / API frameworks

| Framework | Routing model | DI | OpenAPI | Validation | Auth | Streaming | gRPC | GraphQL | Real-time | TS/Spec gen | Bundle |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **ASP.NET Core MVC (Controllers)** | Attribute / convention | ✓ MS DI | ✓ (Swashbuckle/NSwag/MS) | ✓ DataAnnotations / FluentValidation | ✓ all schemes | ✓ | (separate) | (HotChoc/GraphQL.NET) | ✓ SignalR | ✓ NSwag/Kiota | mature |
| **Minimal APIs** | Lambda map | ✓ | ✓ MS native + Swashbuckle | ◐ FluentValidation manual | ✓ | ✓ TypedResults | (separate) | (HotChoc) | ✓ | ✓ | mature, idiomatic |
| **Carter** | Module classes | ✓ | ✓ (Swashbuckle) | ◐ via FluentValidation | ✓ | ✓ | ○ | ○ | ✓ | ✓ | small |
| **FastEndpoints** | Endpoint-per-class (REPR) | ✓ | ✓ | ✓ FluentValidation native | ✓ | ✓ | ○ | ○ | ✓ | ✓ generated | fast |
| **ApiEndpoints (Ardalis)** | Endpoint-per-class | ✓ | ✓ | ✓ | ✓ | ✓ | ○ | ○ | ✓ | ✓ | small |
| **Razor Pages** | Page model | ✓ | (less common) | ✓ | ✓ | ✓ | ○ | ○ | ✓ | ◐ | mature |
| **Blazor Server** | Component routing | ✓ | n/a | ✓ | ✓ | n/a | n/a | n/a | ✓ (SignalR) | n/a | render |
| **Blazor WASM** | Component routing | ✓ | n/a | ✓ | ✓ | n/a | n/a | n/a | n/a | n/a | ◐ AOT |
| **Blazor United (.NET 8+)** | Mixed render modes | ✓ | n/a | ✓ | ✓ | n/a | n/a | n/a | ✓ | n/a | hybrid |
| **Grpc.AspNetCore** | Proto-defined | ✓ | (see Protobuf descriptors) | n/a (proto types) | ✓ | ✓ bidi | ✓ | ○ | ✓ | ✓ codegen | mature |
| **HotChocolate** | Schema/code-first | ✓ | (no — GraphQL spec) | ✓ via FV | ✓ | ✓ subscriptions | ○ | ✓ | ✓ | ✓ Strawberry Shake | feature-rich |
| **GraphQL.NET** | Schema-first | ✓ | ○ | ◐ | ✓ | ✓ | ○ | ✓ | ✓ | ◐ | older |
| **Dapr SDK** | Service invocation building blocks | n/a (sidecar) | n/a | n/a | ✓ via sidecar | ✓ pubsub | ✓ underlying | ○ | ✓ | n/a | sidecar |
| **Microsoft Orleans** | Grain interfaces | grain DI | n/a | ◐ | ◐ | ✓ streams | ✓ underlying | ○ | ✓ Reminders | ◐ codegen | actor model |
| **Akka.NET** | Actor messaging | ✓ Akka DI | n/a | ○ | ◐ | ✓ Akka.Streams | ◐ remoting | ○ | ✓ | n/a | actor model |
| **ServiceStack** | Service classes | ✓ Funq | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ multi-format | commercial-friendly |
| **Nancy** (legacy) | Module-based | ✓ | ◐ | ◐ | ✓ | ✓ | ○ | ○ | ◐ | ◐ | OWIN |
| **Suave (F#)** | WebPart composition | ✓ | ◐ | ○ | ◐ | ✓ | ○ | ○ | ○ | ○ | small |
| **Giraffe (F#)** | HttpHandler composition | ✓ | ◐ | ◐ | ✓ | ✓ | ○ | ○ | ◐ | ◐ | F#-idiomatic |
| **Saturn (F#)** | Convention-on-Giraffe | ✓ | ◐ | ◐ | ✓ | ✓ | ○ | ○ | ◐ | ◐ | F# Rails-y |
| **OWIN / Katana** (legacy) | Middleware pipeline | ◐ | n/a | ◐ | ✓ | ◐ | ○ | ○ | ◐ | ○ | legacy |

### 3.2 Persistence / data access

| Lib | Style | Async | LINQ | Migrations | Bulk | Multi-DB | Multi-tenant | OSS license |
|---|---|---|---|---|---|---|---|---|
| **EF Core** | ORM | ✓ | ✓ | ✓ | ◐ ExecuteUpdate / EFCore.BulkExtensions | ✓ providers | ◐ via filters | MIT |
| **Dapper** | Micro-ORM | ✓ | ○ (raw SQL) | n/a | ◐ MultiExec | ✓ ADO.NET | n/a | Apache |
| **NHibernate** | ORM (full) | ✓ | ✓ | ✓ FluentMig | ◐ Stateless | ✓ | ◐ | LGPL |
| **RepoDb** | Hybrid | ✓ | ◐ Fluent | ◐ | ✓ Bulk | ✓ | ◐ | Apache |
| **linq2db** | LINQ-to-DB | ✓ | ✓✓ | ✓ | ✓ Bulk | ✓ | ◐ | MIT |
| **ServiceStack.OrmLite** | Lite ORM | ✓ | ◐ | ✓ | ✓ | ✓ | ◐ | dual |
| **PetaPoco** | Micro-ORM | ✓ | ○ | ✓ | ◐ | ✓ | ◐ | Apache |
| **Marten** | DocDB + ES on Postgres | ✓ | ✓ jsonb LINQ | ✓ | ✓ | Postgres only | ✓ tenancy | MIT |
| **MongoDB.Driver** | NoSQL doc | ✓ | ✓ Mongo LINQ | n/a | ✓ Bulk | n/a | ✓ | Apache |
| **Cosmos SDK** | NoSQL | ✓ | ✓ subset | n/a | ✓ TX-batch | n/a | ✓ partitions | MIT |
| **RavenDB.Client** | NoSQL doc + indexes | ✓ | ✓ | ✓ index defs | ✓ | n/a | ✓ | AGPL/comm |
| **LiteDB** | Embedded NoSQL | ◐ | ✓ | n/a | ◐ | n/a | ◐ | MIT |
| **Cassandra C# driver** | Wide-column | ✓ | ✓ LINQ | ✓ schema | ✓ | n/a | n/a | Apache |
| **Elastic.Clients.Elasticsearch** | Search | ✓ | ◐ NEST-like | ✓ index ops | ✓ Bulk | n/a | ◐ | Elastic |
| **Pgvector for Npgsql** | Vector ext | ✓ | ✓ via EF | ✓ | n/a | n/a | n/a | PG |

### 3.3 Messaging / eventing

| Lib | Brokers | In-mem | Sagas | Outbox | Schedule | Compensation | License |
|---|---|---|---|---|---|---|---|
| **MediatR** | none (in-process) | ✓ | ○ | ○ | ○ | ○ | Apache |
| **Mediator (martinothamar)** | none | ✓ | ○ | ○ | ○ | ○ | MIT |
| **MassTransit** | RMQ, ASB, Kafka, SQS, ActiveMQ, GRPC, in-mem | ✓ | ✓ state-machine | ✓ | ✓ Quartz/Hangfire | ✓ Routing slip | Apache |
| **NServiceBus** (commercial) | RMQ, ASB, SQS, MSMQ, SQL | ✓ | ✓✓ | ✓ | ✓ | ✓ | commercial |
| **Brighter** | RMQ, Kafka, ASB, SQS, MQTT, Redis | ✓ | ✓ | ✓ | ✓ | ◐ | BSD |
| **Wolverine** | RMQ, ASB, Kafka, SQS, MQTT, in-mem | ✓ | ✓ | ✓ codegen | ✓ | ◐ | MIT |
| **Rebus** | RMQ, ASB, SQL, file, in-mem | ✓ | ✓ | ◐ | ✓ | ◐ | MIT |
| **DotNetCore.CAP** | RMQ, Kafka, ASB, NATS, Redis | ◐ | ◐ | ✓✓ | ✓ | ◐ | MIT |
| **Silverback** | Kafka, RMQ, MQTT | ✓ | ◐ | ◐ | ◐ | ◐ | MIT |
| **EasyNetQ** | RMQ only | ○ | ◐ | ◐ | ◐ | ◐ | MIT |
| **KafkaFlow** | Kafka only | ◐ | ◐ | ✓ | ◐ | ◐ | Apache |
| **Microsoft Orleans Streams** | Orleans-managed | ✓ | n/a | n/a | ✓ Reminders | n/a | MIT |

### 3.4 Cross-cutting libs (the ones consumed in nearly every backend)

| Lib | Concern | License | Status |
|---|---|---|---|
| **FluentValidation** | Validation | Apache | de-facto std |
| **AutoMapper** | Mapping | MIT | de-facto (now commercial maint sponsorship) |
| **Mapster** / **Mapperly** | Mapping (perf / sg) | MIT | growing |
| **Polly** + **Microsoft.Extensions.Resilience** | Resilience | BSD-3 | de-facto |
| **Serilog** | Structured logging | Apache | de-facto |
| **NLog** | Structured logging (alt) | BSD | mature |
| **OpenTelemetry .NET** | Observability | Apache | std |
| **Hangfire** | Background jobs | LGPL/comm | mature |
| **Quartz.NET** | Scheduling | Apache | mature |
| **Coravel** | Lightweight scheduler+jobs | MIT | growing |
| **Microsoft.SemanticKernel** | LLM orchestration | MIT | active |
| **MediatR** | Mediator/CQRS | Apache | de-facto (now commercial license shift) |
| **Refit** | HTTP client gen | MIT | de-facto |
| **NodaTime** | Time/dates | Apache | de-facto for serious time |
| **Ardalis.GuardClauses** | Pre-condition guards | MIT | popular |
| **Vogen** | Value objects | MIT | growing |
| **Stripe.net** | Payments | Apache | de-facto |
| **Stripe-like** alternatives | Payments | varies | … |
| **MailKit / MimeKit** | Email | MIT | de-facto |
| **Spectre.Console** | CLI / pretty output | MIT | de-facto |
| **Bogus** | Fake data | MIT | de-facto |
| **Verify** | Snapshot tests | MIT | growing |
| **xUnit / NUnit / MSTest** | Test runners | varies | std |
| **Moq / NSubstitute / FakeItEasy** | Mocking | varies | std |
| **Testcontainers** | Test infra | MIT | growing |
| **WireMock.Net** | HTTP fake | Apache | popular |

> Detailed inventories by area live in §4 (cross-cutting) and §5 (per-domain).

---

## 4. Cross-cutting vectors

The "everywhere" axes — every backend touches these.

### 4.1 Dependency Injection

| Sub-vector | Detail |
|---|---|
| Container | Microsoft.Extensions.DependencyInjection (built-in), Autofac, SimpleInjector, Lamar, DryIoc, LightInject, Castle.Windsor (legacy), Ninject, StructureMap (deprecated), Stashbox, Grace, Pure.DI (sg), Jab (sg), StrongInject (sg), Funq (ServiceStack), Unity (legacy) |
| Lifetimes | Singleton, Scoped, Transient + per-request, per-tenant, per-conversation, custom scopes |
| Registration patterns | Direct (`AddSingleton<T>`), assembly scanning, conventions, Scrutor, decorator pattern, factory pattern, named/keyed (built-in `[FromKeyedServices]` since .NET 8) |
| Validation | `BuildServiceProvider().Validate()`, MS DI scope validation in dev, `IValidatableObject` for options |
| Disposal | Sync `IDisposable`, async `IAsyncDisposable`, scope cascade |
| Decorators | Scrutor `Decorate<T>`, custom open-generic decorators, behavior pipelines (MediatR/Wolverine/Brighter) |
| Open generics | `IRequestHandler<,>`, `IRepository<>`, `IValidator<>`, generic decorators |
| Named registrations | Keyed (.NET 8+), `IEnumerable<T>`, manual factories |
| AOP / interception | Castle.DynamicProxy, Microsoft.Extensions.Resilience pipeline, source-gen proxies (Pure.DI), Fody plugins |
| Source-gen DI | Pure.DI, Jab, StrongInject — compile-time wiring, no reflection at runtime, AOT-friendly |
| Modular registration | Per-feature `IServiceCollection` extensions, Carter `ICarterModule`, ABP modules, Orleans grains |
| Lazy | `Lazy<T>` registration, `Func<T>` factory |
| Service discovery (cross-process) | Microsoft.Extensions.ServiceDiscovery (Aspire), Steeltoe Discovery, Consul.NET |

### 4.2 Configuration & Options

| Sub-vector | Detail |
|---|---|
| Sources | JSON, XML, INI, YAML (community), TOML (community), env vars, command line, in-memory, user-secrets, key-per-file (Docker/k8s), Azure App Configuration, Azure Key Vault, AWS Secrets Manager, AWS AppConfig, GCP Secret Manager, Vault (Hashi), Consul KV, etcd, GitHub Action secrets, .env files (DotNetEnv) |
| Provider chain | Last wins; `IConfigurationBuilder` chain; reload-on-change |
| Hot reload | `IOptionsMonitor<T>`, `IConfiguration.GetReloadToken()`, file watcher providers |
| Binding | `Get<T>()`, `Bind()`, `[Bind*]`, source-gen binding (.NET 8+), record types, immutable config |
| Validation | `IValidateOptions<T>`, DataAnnotations, FluentValidation, Microsoft.Extensions.Options.DataAnnotations, MS source-gen validator (.NET 8+), startup validation (`ValidateOnStart()`) |
| Options pattern | `IOptions<T>` (singleton snapshot), `IOptionsSnapshot<T>` (scoped), `IOptionsMonitor<T>` (live) |
| Named options | Multiple instances by name |
| Post-config | `Configure<T>()`, `PostConfigure<T>()` ordered hooks |
| Secret rotation | Provider-driven refresh; ChangeToken propagation |
| Strongly typed | `record TOptions(...)`, init-only props, source-gen binding |
| Per-tenant override | Stack tenant config provider on top |
| Encryption at rest | Data Protection encrypts secrets in protected sources |
| Schema export | JsonSchema generation from option types (NJsonSchema) |
| Feature flags | Microsoft.FeatureManagement, FeatureManagement.AspNetCore (filters: percentage, time-window, targeting, custom), LaunchDarkly, ConfigCat, GrowthBook, Unleash, Esquio, Flagsmith |

### 4.3 Logging

| Sub-vector | Detail |
|---|---|
| Provider abstraction | `Microsoft.Extensions.Logging.ILogger<T>`, `ILoggerFactory`, scopes |
| Sinks/providers | Console, Debug, EventLog, EventSource, ApplicationInsights, Serilog (200+ sinks), NLog (many), log4net, ZLogger, Datadog, Splunk, NewRelic, Honeycomb, Elastic, Loki, Seq, Sentry, OpenTelemetry, AWS CloudWatch, GCP Cloud Logging, Azure Monitor |
| Structured logging | Message template + property bag (`logger.LogInformation("User {UserId} logged in", userId)`); avoid string interp |
| Source-gen | `[LoggerMessage]` attribute, free-allocation |
| Performance | High-perf logger generation, disabled-state cost, `LogTrace`/`LogDebug` guarding |
| Scopes | `BeginScope` for per-request enrichment |
| Enrichment | LogContext (Serilog), Activity tag attach (OTel), exception decomposition (Destructurama) |
| Log levels | Trace, Debug, Information, Warning, Error, Critical, None — filtering per-category |
| Filtering | Per-category, per-provider, programmatic |
| Sampling | OTel sampler / Serilog filter-by-rate |
| Redaction | Microsoft.Extensions.Compliance (Data Classification + Redaction) (.NET 8+) |
| Sensitive-data | Tag with `[DataClassification]`; auto-redact; PII detection plugins |
| Log routing | Different sinks for different categories (security, audit, debug) |
| Correlation | `traceId` / `spanId` via Activity; `requestId`; OTel propagation |
| Audit log | Audit.NET — separate channel, append-only |
| ULIDs / KSUIDs | Sortable IDs with embedded timestamp |

### 4.4 Validation

| Sub-vector | Detail |
|---|---|
| Validation engines | DataAnnotations, FluentValidation, MiniValidation, Vogen (value-object validation), JsonSchema.Net, NJsonSchema, custom (`IValidatableObject`) |
| Scopes | DTO validation, command validation, query validation, options validation, model binding validation, business invariant |
| Timing | Pre-handler (filter / behavior), inline guard, post-creation invariant |
| Pipeline integration | MediatR `IPipelineBehavior<,>`, Wolverine middleware, FastEndpoints validators, Carter validators, MinimalApis filters |
| Cross-field | FluentValidation custom rules, condition chains, `When` / `Unless` |
| Async | Async validators (DB lookup, HTTP); cancellation |
| Schema-driven | JSON Schema, Avro, Protobuf reflection, OpenAPI schema |
| Localization | Resource-based messages, ICU MessageFormat (community), per-culture override |
| Error format | RFC 7807 ProblemDetails with `errors` extension; ValidationProblemDetails |
| Guard clauses | Ardalis.GuardClauses, Throw, EnsureThat, Dawn.Guard |
| Pre-condition libs | Code Contracts (legacy), Throw, GuardAgainst |
| Soft vs hard | Warnings vs errors; severity per rule |
| Server-side hardening | Always re-validate; never trust client |
| Compiled validators | FluentValidation pre-compiled; minimal allocation |
| Extension hooks | Custom property validators, custom rule sets, conditional rules |

### 4.5 Mapping / projection

| Sub-vector | Detail |
|---|---|
| Engines | AutoMapper, Mapster (runtime + sg), Mapperly (sg only), AgileMapper, ExpressMapper, EmitMapper, TinyMapper, NextGenMapper, manual hand-written |
| Source-gen | Mapperly (Roslyn), Mapster.SourceGenerator, NextGenMapper |
| Profile / config | Per-profile registration, fluent rules, conventions |
| Projection-to-LINQ | EF projection (`.ProjectTo<TDto>()`); flatten complex graphs to SQL columns |
| Reverse mapping | DTO ↔ Domain via attribute or reverse map |
| Custom value converters | Per-property converters; type converters |
| Null handling | Default, null-substitute, null-throw |
| Polymorphism | Inheritance maps; discriminator; type tag |
| Validation pre-mapping | Validate then map (or vice versa) |
| AOT-friendliness | sg engines only (Mapperly, NextGen); reflection mappers fail trim/AOT |
| Bench | Mapperly ≈ manual; AutoMapper has runtime cost |

### 4.6 Resilience & fault tolerance

| Sub-vector | Detail |
|---|---|
| Strategies | Retry (constant/linear/exponential/jitter), Circuit Breaker, Timeout, Hedging, Fallback, Rate Limiter, Bulkhead, Cache (cacheable response) |
| Engines | Polly v8 (pipelines), Microsoft.Extensions.Resilience (built on Polly), Microsoft.Extensions.Http.Resilience (HttpClient handler) |
| Pipelines | Composed strategy chain; per-route policies |
| Standard handlers | Standard, Standard-Hedging |
| Failure detection | Status code predicates, exception predicates, content predicates, response analysis |
| Telemetry | Polly metric/event; OTel metric integration |
| Idempotency keys | Per-request unique key; server-side dedup |
| Retry budget | Cap retry rate (token bucket); avoid retry storm |
| Cancellation | CT honored throughout pipeline |
| State storage | Circuit-breaker state (in-memory; distributed via Redis for multi-instance) |
| Chaos | Polly Chaos extension; Simmy (legacy); fault injection |
| Compensation | Saga compensating actions; non-Polly orchestration |
| Backoff strategies | Decorrelated jitter, full jitter, equal jitter, no jitter |
| Per-dependency | Different policies per downstream (db vs cache vs http) |

### 4.7 Observability — traces, metrics, logs

| Sub-vector | Detail |
|---|---|
| Pillars | Logs, Traces, Metrics — and emerging: Profiles (continuous profiling), Events |
| OpenTelemetry .NET | Tracer, Meter, Logger; SDK + auto-instrumentation; OTLP exporter |
| Auto-instrumentation | OTel.Instrumentation.AspNetCore, .Http, .SqlClient, .EntityFrameworkCore, .StackExchangeRedis, .Quartz, .Hangfire, .GrpcNetClient, .MongoDb, .Wcf, .Elasticsearch, .Kafka, .MassTransit, .RabbitMq, .Cassandra, .ServiceBus |
| Exporters | OTLP (gRPC/HTTP), Jaeger, Zipkin, Prometheus, AzureMonitor, Datadog, New Relic, Honeycomb, Splunk, AWS X-Ray, Tempo, Loki |
| Activity API | `System.Diagnostics.ActivitySource` / `Activity` — native trace |
| Meter API | `System.Diagnostics.Metrics.Meter` — counters, gauges, histograms, observable callbacks |
| EventSource / EventCounters | `EventSource` for high-perf events; `EventCounter` for runtime metrics; viewable via `dotnet-counters` |
| dotnet diagnostics CLI | dotnet-counters, dotnet-trace, dotnet-dump, dotnet-monitor, EventPipe |
| APMs | Application Insights, Datadog APM, New Relic, Dynatrace, Honeycomb, Elastic APM, AppDynamics, Lightstep |
| Profile / continuous | Pyroscope, Parca, dotnet-counters, JetBrains dotTrace, JetBrains dotMemory |
| Sampling | Always-on, ratio, tail-based (collector), parent-based, custom |
| Baggage | Propagated cross-service kv, user-tag, tenant tag |
| Health checks | AspNetCore.HealthChecks (Xabaril) — 50+ checks; Microsoft.Extensions.Diagnostics.HealthChecks |
| SLO / SLI | Burn-rate alerts, multi-window, error budgets — usually backend code surfaces metric |
| Dashboards | Grafana, Aspire dashboard, Datadog, Application Insights, Power BI |
| Alerting | Prometheus AlertManager, Datadog Monitors, Azure Alert Rules, PagerDuty integrations |
| Audit observability | Audit.NET as separate channel from app logs |

### 4.8 Security & Authentication / Authorization

| Sub-vector | Detail |
|---|---|
| Authn schemes | Cookies, JWT bearer, OpenIdConnect, OAuth 2.x, WS-Federation, Negotiate (Kerberos), Certificate (mTLS), API key, Basic (rare), Custom |
| OIDC providers | Microsoft Entra ID (AAD), Auth0, Okta, Keycloak, Azure AD B2C, AWS Cognito, GCP Identity Platform, Duende IdentityServer, OpenIddict |
| Token types | JWT (compact, signed/encrypted), JWE, Reference tokens, Paseto |
| Token validation | Issuer, audience, signing key, lifetime, NotBefore, audience match, key rotation via JWKS |
| Cookie auth | Slide expiration, sliding cookie, ticket store, redirect URIs |
| Authz | Role-based (RBAC), Claims-based (CBAC), Policy-based (`AddPolicy`), Resource-based (`IAuthorizationHandler`), Attribute-based (ABAC), ReBAC, Permission-based via FineGrained |
| Policy engines | ASP.NET Core authorization policies, OPA (Open Policy Agent), Casbin.NET, Authzed (SpiceDB), Ory Keto |
| Permissions | Permission tree, scopes, claims, roles → permissions mapping |
| OAuth flows | Authorization Code + PKCE, Client Credentials, Device Code, Refresh, Token Exchange (RFC 8693), Resource Owner (legacy) |
| Identity stores | ASP.NET Core Identity, Identity API endpoints (.NET 8+ minimal), Duende IdentityServer, OpenIddict, Auth0, custom stores |
| MFA / 2FA | TOTP (Otp.NET), WebAuthn/FIDO2 (Fido2.AspNetCore), SMS, email, hardware keys |
| Session | Cookie + ticket, distributed cache backend (Redis), sticky-session caveats |
| Logout | Cookie signout, OIDC end-session, back-channel logout, single-sign-out |
| BFF pattern | Duende.BFF or Microsoft.Identity.Web.Bff; cookie at edge, token inside |
| Crypto | AES-GCM, ChaCha20-Poly1305, Argon2 (Konscious), bcrypt, scrypt, PBKDF2, RSA, ECDsa, EdDSA, Curve25519, post-quantum (.NET 9 ML-KEM) |
| Hashing | SHA256/384/512, BLAKE2/3, xxHash, MurmurHash |
| Secrets storage | Microsoft DataProtection (encrypt local), KeyVault, AWS SM, GCP SM, HashiCorp Vault, dotnet user-secrets (dev) |
| Anti-XSS | `System.Text.Encodings.Web.HtmlEncoder` |
| Anti-CSRF | `Microsoft.AspNetCore.Antiforgery`, double-submit, SameSite cookies |
| CORS | `AddCors`, per-policy origins, headers, credentials |
| HSTS | `UseHsts`, preload list, max-age tuning |
| HTTPS redirect | `UseHttpsRedirection`, port mapping |
| TLS | Cert mgmt (KeyVault, ACME via Lego), pinning, mTLS |
| OWASP top-10 mitigations | Injection (param SQL), broken authn, exposure, XXE (XML entities), broken access ctrl, misconfig, XSS, deserialization, vuln deps, logging |
| Secure headers | Strict-Transport-Security, X-Frame-Options, X-Content-Type-Options, Content-Security-Policy, Referrer-Policy, Permissions-Policy, Cache-Control |
| Content-Security-Policy | Strict CSP with nonces / hashes |
| Vulnerability scanning | dotnet list package --vulnerable, GitHub Dependabot, Snyk, OWASP Dependency-Check |
| SBOM | CycloneDX, SPDX generators (CycloneDX.NetCore, dotnet-sbom-tool) |
| Static analysis | Microsoft.CodeAnalysis.NetAnalyzers (security rules), SonarAnalyzer.CSharp, Roslynator.Security |
| Runtime hardening | UseExceptionHandler, RFC 7807 ProblemDetails (don't leak stack), structured 4xx/5xx |

### 4.9 Caching

| Sub-vector | Detail |
|---|---|
| Tiers | L0 (CPU) → L1 (in-process / `IMemoryCache`) → L2 (distributed / Redis, Memcached, NCache) → L3 (CDN / Output cache / response cache) |
| In-process | Microsoft.Extensions.Caching.Memory, BitFaster.Caching, LazyCache, Foundatio.Caching, Carbon.Cache |
| Distributed | Microsoft.Extensions.Caching.Distributed (interface), StackExchange.Redis, NCache, Memcached (EnyimMemcachedCore), FreeRedis, Couchbase |
| Hybrid (L1+L2) | Microsoft.Extensions.Caching.Hybrid (.NET 9), FusionCache (ZiggyCreatures) — most feature-rich, EasyCaching |
| Invalidation strategies | TTL, sliding, manual, tag-based, event-driven (cache-aside, write-through, write-behind) |
| Stampede protection | Lock-and-load (FusionCache), single-flight, value-coalescing |
| Stale-while-revalidate | FusionCache, custom |
| Backplane | Redis pub/sub (FusionCache, NCache), in-mem dev backplane |
| Output cache | `Microsoft.AspNetCore.OutputCaching` (.NET 7+), per-route policy, vary-by, eviction, custom store |
| Response cache | `Microsoft.AspNetCore.ResponseCaching` (older, header-driven) |
| Browser cache | `Cache-Control`, `ETag`, `If-None-Match`, `If-Modified-Since` headers |
| CDN | Cloudflare, Akamai, Fastly, AWS CloudFront, Azure CDN — config typically YAML/Terraform |
| EF query cache | EFCore.SecondLevelCache.Interceptor, ZZZ Projects |
| Negative caching | Cache misses, expired sentinel |
| Tag eviction | Cache regions, key prefixes |
| Compression | gzip / brotli / zstd (`Microsoft.AspNetCore.ResponseCompression`) |
| Serialization for cache | MessagePack, MemoryPack, JSON, Protobuf — affect L2 perf |

### 4.10 Rate limiting / throttling / quotas

| Sub-vector | Detail |
|---|---|
| Built-in | `System.Threading.RateLimiting` (Concurrency, Token Bucket, Fixed Window, Sliding Window, Chained limiter), `Microsoft.AspNetCore.RateLimiting` middleware |
| Algorithms | Fixed Window, Sliding Window, Token Bucket, Leaky Bucket, Concurrency, GCRA |
| Scopes | Global, per-IP, per-user, per-tenant, per-API key, per-route |
| Distributed | Redis-backed limiter (custom), Envoy limiter, NGINX limit_req, Kong/Tyk gateway level |
| Communication | 429 Too Many Requests + `Retry-After` header; ProblemDetails; quota headers (`X-RateLimit-*`) |
| Abuse defense | IP allowlist/denylist, captcha challenge, exponential lockout |
| Per-endpoint | Different limits per endpoint sensitivity |
| Bypass | Internal callers, admin override |
| Cooperative back-pressure | TPL Channels, Dataflow, Akka.Streams demand signaling |

### 4.11 Background jobs / scheduling

| Sub-vector | Detail |
|---|---|
| Engines | Hangfire (Pro/OSS), Quartz.NET, Coravel, NCronJob, FluentScheduler, Wolverine local queue, MassTransit Quartz integration, Microsoft.DurableTask (Azure Functions Durable Tasks), Tewr.Coravel |
| Hosted service base | `BackgroundService`, `IHostedService`, hosted lifecycle |
| Scheduling | Cron (Cronos parser), interval, calendar (Quartz), one-shot, recurring |
| Persistence | SQL (Hangfire SqlServer, MySQL, Postgres), Redis (Hangfire.Redis), MongoDB, MSMQ (legacy) |
| Distributed coordination | Multi-instance leader election (DistributedLock libs), Hangfire built-in |
| Job state | Pending, processing, succeeded, failed, deleted, awaiting (continuations) |
| Continuations | Hangfire `ContinueJobWith` |
| Retries | Built-in with exponential backoff |
| Idempotency | Job key dedupe; per-job replay-safe |
| Parallelism | Per-queue worker count |
| Dashboard | Hangfire Dashboard, Coravel Pro (commercial), custom UI |
| Cluster | Hangfire (multi-instance pool), Quartz cluster (DB-backed), Coravel single-process |
| Cron library | Cronos (parses any cron, including 6-field with seconds); Quartz expressions |
| Time zone | Per-job TZ; DST-safe |
| Cancellation | CT propagated; long-running task cancellation |
| Outbox-as-job | Common pattern: outbox row → dispatcher job |

### 4.12 Health checks

| Sub-vector | Detail |
|---|---|
| Engine | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| UI | `AspNetCore.HealthChecks.UI` (Xabaril) |
| Probes | Liveness, Readiness, Startup (Kubernetes-aligned) |
| Built-in checks | DBContext check (EFCore) |
| Xabaril checks (50+) | SqlServer, Npgsql, MySql, Sqlite, Oracle, Redis, RabbitMQ, AzureServiceBus, AzureBlobStorage, AzureQueueStorage, AzureFiles, AzureKeyVault, AzureCosmosDb, AzureSearch, AwsS3, AwsDynamoDb, AwsSqs, AwsSecretsManager, MongoDb, Elasticsearch, Solr, OpenSearch, Kafka, IbmMq, Nats, EventStore, Hangfire, Quartz, Network (DNS, Tcp, Ping, Smtp, Imap, Ftp, Sftp, ImapPop3), Uris, Kubernetes, Consul, ArangoDb, RethinkDb, Couchbase, OpenIdConnect, OAuth2, IdentityServer, AzureIotHub, AzureNotificationHubs, Gremlin, Influxdb, Cassandra, Sap, Sharepoint, Stackexchange.Redis advanced, custom |
| Composition | Multiple checks per app, tags filter |
| Reporting | JSON output, OTel emit, Prometheus exporter |
| Startup gating | Don't accept traffic until ready |

### 4.13 Feature flags / experimentation

| Sub-vector | Detail |
|---|---|
| Engines | Microsoft.FeatureManagement, LaunchDarkly.ServerSdk, Unleash.Client, GrowthBook.NET, ConfigCat.Client, Esquio, Flagsmith, Optimizely, Statsig, Eppo, Hypertune |
| Filters | Percentage, time-window, targeting (user/tenant/region), custom |
| A/B testing | Variant assignment, exposure event, conversion metric |
| Kill switches | Boolean flag for emergency disable |
| Roll-out | Gradual percentage ramp, ring-based |
| Local override | Dev-time forced value |
| Segmentation | User attributes, tenant attributes, geo |
| Ed-time | Compile-time conditional vs runtime; trunk-based dev |
| Audit | Change log of who toggled what |
| Schema | TS interface generation; Hypertune type-safe flags |

### 4.14 Multi-tenancy

| Sub-vector | Detail |
|---|---|
| Engines | Finbuckle.MultiTenant, ABP.MultiTenancy, OrchardCore tenants, custom |
| Isolation strategies | Per-DB (silo), Per-schema, Per-row (shared), Per-shard, Per-cluster |
| Tenant resolution | Subdomain, path, header, JWT claim, query string, default |
| Tenant store | In-memory, EF, JSON, custom store, Microsoft Entra B2C (per-tenant) |
| Per-tenant config | Override settings per tenant |
| Per-tenant DI | Tenant-scoped services |
| Per-tenant cache | Tenant-key prefix; invalidate per tenant |
| EF Core query filters | Auto-apply `tenantId == current` |
| Multi-tenant migrations | Apply per-tenant DB schema updates |
| Multi-tenant background jobs | Per-tenant queue / per-tenant job |

### 4.15 Localization / i18n

| Sub-vector | Detail |
|---|---|
| Engines | `Microsoft.Extensions.Localization`, `IStringLocalizer<T>`, `IHtmlLocalizer<T>`, `IViewLocalizer`, OrchardCore.Localization, Fluent.Net (Mozilla Fluent), I18Next.Net, NGettext |
| Resource format | .resx, .json, .po, .yml, ICU MessageFormat (community), Fluent (FTL) |
| Culture providers | Query, cookie, accept-language, route, custom |
| Pluralization | ICU MessageFormat, Microsoft fallback (limited) |
| Fallback chain | en-GB → en → invariant |
| Number / date / currency | `Intl`-style via `CultureInfo`, `NumberFormatInfo`, `DateTimeFormatInfo` |
| Timezones | TimeZoneInfo (Win/IANA via TimeZoneConverter) |
| Calendars | GregorianCalendar, HijriCalendar, BuddhistCalendar, JapaneseCalendar, PersianCalendar, etc. |
| RTL | UI concern primarily (see UI lib); backend forwards `dir` if computed |
| Pseudo-localization | Tooling pseudo-loc for QA |
| Lazy load | Per-locale resource bundles |

### 4.16 Time / clock / culture

| Sub-vector | Detail |
|---|---|
| Abstraction | `TimeProvider` (.NET 8+), `IClock` (NodaTime), custom `ISystemClock` |
| NodaTime | Instant, ZonedDateTime, LocalDateTime, Period, Duration, IClock — domain-grade |
| Conversions | TimeZoneInfo, TimeZoneConverter (Win↔IANA), Cronos (cron parser), Humanizer (phrase output) |
| Storage | UTC (recommended), local time + offset, store TZ name separately when policy-driven |
| Clock skew | NTP, monotonic clocks (Stopwatch.GetTimestamp); leap seconds |
| Test fakes | `FakeTimeProvider` (.NET 8+ in MS.Extensions.TimeProvider.Testing), Microsoft.Bcl.TimeProvider, NodaTime FakeClock |
| Cron | Cronos, NCrontab, Quartz CronExpression |

### 4.17 Error handling / Result types / discriminated unions

| Sub-vector | Detail |
|---|---|
| Exception strategy | Throw for exceptional only; results for expected; document on contract |
| Global handler | `UseExceptionHandler`, `IExceptionHandler` (.NET 8+), `ExceptionFilters` |
| ProblemDetails | `Microsoft.AspNetCore.Mvc.ProblemDetails`, Hellang.Middleware.ProblemDetails |
| Result types | OneOf, ErrorOr, FluentResults, CSharpFunctionalExtensions, LanguageExt.Either, Optional, Maybe |
| Discriminated unions | OneOf, language proposal (C# 13+), F# DUs interop, ErrorOr |
| Error catalog | Enum + factory pattern; SmartEnum (Ardalis); typed errors |
| HTTP status mapping | 400/401/403/404/409/422/429/500 — opinionated mapper |
| Don't-leak | Hide stack from prod, show typed problem; correlation ID for support |
| Retry vs fail | Caller decides; idempotent retry vs non-retryable |
| Result-bearing pipeline | MediatR `Result<T>` (community), Wolverine codegen results |
| Logging on error | Single-source-of-truth: throw or log, not both |

### 4.18 Serialization

| Sub-vector | Detail |
|---|---|
| Engines | System.Text.Json (default), Newtonsoft.Json (legacy), MessagePack-CSharp, MemoryPack, protobuf-net, Google.Protobuf, Apache.Avro, Bond, FlatBuffers, Cap'n Proto, Bebop, ServiceStack.Text, Jil, NetJson, Utf8Json (legacy), Bond |
| Source-gen | STJ source-gen (.NET 6+), MemoryPack, Mapperly transit, MessagePack source-gen |
| Polymorphism | `[JsonPolymorphic]` + `[JsonDerivedType]` (.NET 7+); custom resolver |
| Property naming | camelCase, snake_case, kebab-case via naming policies |
| Date/time | ISO 8601 default; NodaTime serializers (NodaTime.Serialization.SystemTextJson) |
| Decimal precision | Currency exact via decimal; JSON number tolerance |
| Streaming | `JsonSerializer.DeserializeAsyncEnumerable<T>` |
| JsonNode / JsonElement | DOM access; mutable JsonNode (.NET 6+) |
| JsonSchema | NJsonSchema, JsonSchema.Net, generation from types |
| JsonPath | JsonPath.Net |
| JsonPatch | Microsoft.AspNetCore.JsonPatch (Newtonsoft-based), JsonPatch.Net |
| JMESPath | JmesPath.Net |
| Compression | Brotli, gzip, zstd (Iddle), LZ4 (K4os.Compression.LZ4) |
| Binary perf | MemoryPack > MessagePack > Protobuf > STJ for size+speed |
| Schema evolution | Avro (full), Protobuf (good), MessagePack (with care), JSON (manual) |
| AOT-friendliness | STJ source-gen, MemoryPack, Mapperly — yes; reflection-only — no |

### 4.19 Real-time / streaming / events

| Sub-vector | Detail |
|---|---|
| Engines | SignalR (server-sent + WebSocket + Long-poll fallback), Microsoft.Azure.SignalR, gRPC streaming, Server-Sent Events (Lib.AspNetCore.ServerSentEvents), raw WebSockets (`Microsoft.AspNetCore.WebSockets`), MQTTnet, NATS, Kafka streams, EventStore subscriptions, Marten async daemon |
| Transport modes | WebSocket, SSE, gRPC stream, HTTP long-poll, MQTT |
| Backpressure | gRPC flow control, RxNET back-pressure ops, TPL Channels bounded |
| Hub abstractions | SignalR Hub, MagicOnion (gRPC + RPC) |
| Auth | Per-connection JWT, per-message claims, hub authorization filters |
| Scale-out | Redis backplane, Azure SignalR Service, custom backplane |
| Reconnection | Built-in client retry with exponential; user-level state recovery |
| Throughput | MessagePack hub protocol (built-in option) |
| Server-to-client | Push notifications (FCM, APNs), Web Push, Email, SMS |
| Client-to-server | Hub method invoke, request/response, cancellation |
| Streaming methods | `IAsyncEnumerable<T>` over hub, channel reader |

### 4.20 Async data / pipelines / dataflow

| Sub-vector | Detail |
|---|---|
| Engines | `IAsyncEnumerable<T>`, System.Threading.Channels, TPL Dataflow, Reactive Extensions (Rx.NET), Akka.Streams, KafkaFlow, Streamiz |
| Cancellation | `CancellationToken`, `CancellationTokenSource`, linked tokens, timeout |
| Coordination | `SemaphoreSlim`, `Lock` (.NET 9 `System.Threading.Lock`), `AsyncLock`, `AsyncManualResetEvent`, `AsyncCountdownEvent` |
| Task helpers | `Task.WhenAll`, `Task.WhenAny`, `Task.WhenEach` (.NET 9), `Parallel.ForEachAsync` |
| Ratelimit | Channels with bounded capacity, RateLimiting library |
| Pipelines | System.IO.Pipelines for high-perf parsing |
| Async streams | `await foreach`, `WithCancellation`, `ConfigureAwait(false)` |
| Parallel | `Parallel.For`, `Parallel.ForEachAsync`, PLINQ |

### 4.21 Performance / runtime

| Sub-vector | Detail |
|---|---|
| AOT | Native AOT (.NET 8+); requires trim-friendly libs and source-gen for JSON/Mapping/DI |
| Trimming | Linker; trim warnings; `[UnconditionalSuppressMessage]` |
| ReadyToRun | Pre-jitted images |
| Tiered compilation | Default since .NET 6+ |
| Object pooling | `Microsoft.Extensions.ObjectPool`, ArrayPool, MemoryPool |
| Spans / Memory | `Span<T>`, `Memory<T>`, `ReadOnlySpan`, `ReadOnlyMemory` |
| Buffers | System.Buffers (`ArrayPool<T>.Shared`) |
| Numerics | System.Numerics.Vectors, Tensors, BigInteger |
| SIMD | Vector / Avx (Intrinsics) |
| GC modes | Server GC, Workstation GC, Background, low-latency hint |
| Memory hints | `RuntimeHelpers.IsKnownConstant`, generic specialization |
| ValueTask | Hot-path returns |
| Unsafe | `Unsafe.As`, `Unsafe.Add`, ref structs |
| Source generators | Roslyn IIncrementalGenerator |
| Profilers | dotTrace, dotMemory, PerfView, JetBrains Rider profiler, BenchmarkDotNet |

### 4.22 Idempotency & dedupe

| Sub-vector | Detail |
|---|---|
| Idempotency keys | Header (`Idempotency-Key`), command id, message id |
| Storage | Redis SETNX, EF dedup table, Outbox row uniqueness |
| Window | Per-key TTL of dedup |
| Retry-safety | Side-effect once; downstream-safe |
| Inbox pattern | Process message ID dedupe |
| Outbox dedup | Send-once guarantee per outbox row |

### 4.23 Distributed transactions / outbox / inbox

| Sub-vector | Detail |
|---|---|
| Engines | DotNetCore.CAP, MassTransit Outbox, Wolverine TX, NServiceBus Outbox, Brighter Outbox, Marten Outbox |
| Patterns | Outbox (publish reliably from your DB), Inbox (consume idempotently), Saga (long-running orchestration) |
| Transaction scope | DB transaction wraps state + outbox row |
| Dispatcher | Background hosted service drains outbox |
| Polling vs CDC | Polling outbox table vs CDC (Debezium) |
| 2PC | XA/MSDTC discouraged; outbox replaces |
| Compensation | Saga compensating actions |
| Two-DB scenarios | Per-service DB; cross-service via outbox + event |

### 4.24 Audit / change tracking

| Sub-vector | Detail |
|---|---|
| Engines | Audit.NET (large family of integrations: AzureStorage, EntityFramework, HttpClient, Mvc, NLog, Polly, Redis, SignalR, Sql, Wcf, File), EntityFrameworkCore.Triggered, EFCore temporal tables, custom interceptor |
| Event types | INSERT/UPDATE/DELETE, READ (sensitive), AUTH (login/logout), ADMIN (config change) |
| Storage | Append-only DB, blob storage, log sink, separate index |
| User context | "Who changed what when, with which fields" |
| Soft delete | `IsDeleted` flag + EF query filter |
| Temporal tables | SQL Server temporal, history table |
| Compliance | GDPR, HIPAA, SOC2, PCI-DSS — audit immutability requirements |
| Retention | Per-policy purge / archive |

### 4.25 Testing surface

(See §5.* per-domain test surface details and §9 tooling for runners.)

| Sub-vector | Detail |
|---|---|
| Test types | Unit, integration, contract (PactNet), property (FsCheck/CsCheck/Hedgehog), mutation (Stryker), snapshot (Verify), performance (BenchmarkDotNet, NBomber, Microsoft.Crank), end-to-end |
| Web testing | `WebApplicationFactory<TProgram>`, `Microsoft.AspNetCore.TestHost` |
| Container fixtures | Testcontainers (Postgres, Mongo, Redis, RabbitMQ, Kafka, Elastic, Azure Storage Emulator, LocalStack, Wiremock) |
| HTTP fakes | WireMock.Net, MockHttp.NetCore, JustEat HttpClientInterception, RichardSzalay.MockHttp, Spectre.HttpMock |
| Time fakes | `FakeTimeProvider` (.NET 8+), NodaTime FakeClock |
| In-memory infra | EF InMemory, EF Sqlite memory, in-memory message bus (MassTransit, Wolverine, Brighter) |
| Assertion | FluentAssertions, Shouldly, NFluent, native xUnit Assert |
| Mocking | Moq, NSubstitute, FakeItEasy, JustMock (commercial), Telerik Mocking |
| Data | AutoFixture, Bogus, Faker.Net, NBomber data generators |
| Property-based | FsCheck, CsCheck, Hedgehog |
| Mutation | Stryker.NET |
| Snapshot | Verify (with EF/Cosmos/JSON/HTTP integrations), Snapshooter, Approvals.NET |
| BDD | Reqnroll (post-SpecFlow), LightBDD |
| Coverage | Coverlet (XPlat), ReportGenerator, JetBrains dotCover |
| Benchmarks | BenchmarkDotNet, Microsoft.Crank |
| Load | NBomber, k6 (external), Apache JMeter, Gatling, Bombardier (external) |
| Code analysis | Roslyn analyzers, SonarAnalyzer, Roslynator |
| Dev DB | EF Core migrations + Testcontainers boot |

### 4.26 AOP / interception / source-gen

| Sub-vector | Detail |
|---|---|
| Runtime proxy | Castle.DynamicProxy, AutoFac.Extras.DynamicProxy, ImpromptuInterface |
| Source gen | Mediator.SourceGenerator (martinothamar), Mapperly, Vogen, Microsoft source generators (logger, RegEx, JSON, Configuration), Pure.DI, Jab, StrongInject |
| Fody plugins | MethodBoundaryAspect.Fody, NullGuard.Fody, PropertyChanged.Fody, Anotar.Fody, Costura.Fody |
| PostSharp / Metalama | Metalama (commercial), PostSharp (legacy) — declarative aspects |
| Behaviors | MediatR `IPipelineBehavior<,>`, Wolverine middleware, FastEndpoints pre/post processors |
| Interceptors (C# 12+) | `[InterceptsLocation]` — call rewriting (Roslyn experimental) |
| Endpoint filters | `IEndpointFilter` (.NET 7+) for Minimal APIs |

### 4.27 Modularity

| Sub-vector | Detail |
|---|---|
| Engines | OrchardCore tenants/modules, ABP modules, Carter `ICarterModule`, FastEndpoints groups, MEF (legacy), Microsoft.Extensions.DependencyModel |
| Module loading | Static (referenced), dynamic (assembly load), tenant-scoped |
| Inter-module | Events, Mediator, public API contract |
| Dependency direction | Module references only abstractions, never sibling concretes |
| Independent deploy | Plug-in style (rare) vs single deployable |

### 4.28 Documentation surface

| Sub-vector | Detail |
|---|---|
| API docs | OpenAPI (Microsoft.AspNetCore.OpenApi, Swashbuckle, NSwag), AsyncAPI (Saunter), gRPC reflection, GraphQL Playground / Banana Cake Pop |
| XML docs | `<summary>`, `<param>`, `<returns>`, `<exception>`; `Compile.GenerateDocumentationFile` |
| Doc site generators | DocFX, Statiq, MkDocs (non-.NET), Astro Starlight, VitePress (non-.NET) |
| Tutorials | Sample projects, Aspire-orchestrated demos |

---

## 5. Domain matrices (per-area hidden complexity)

Per category, the hidden complexity beyond "make it work."

### 5.1 HTTP API surface

| Concern | Hidden vectors |
|---|---|
| Routing | Attribute, conventional, minimal, slug params, route constraints, transformer, parameter binding, area/group, route precedence |
| Model binding | Form, body (JSON/XML/form-urlencoded/multipart), query, route, header, services, FromKeyed, custom binder, type converters |
| Content negotiation | `Accept`, `Content-Type`, formatters, custom output formatter, problem-details |
| Versioning | URL segment (`/v1/`), header (`api-version`), query, media-type — Asp.Versioning |
| Auth | Per-endpoint policy, attribute, fallback policy, anonymous opt-out |
| Caching | Output cache, response cache, ETag/304 |
| Compression | gzip, brotli, zstd; per-route opt-in/out |
| Decompression | Request decompression (.NET 7+) |
| Error response | ProblemDetails, ValidationProblemDetails, custom envelope (avoid), correlation ID |
| Streaming response | `IResult` streaming, `IAsyncEnumerable<T>`, gRPC stream, SSE |
| File response | `FileResult`, `PhysicalFile`, range requests, Content-Disposition |
| File upload | Multipart parsing, large-file streaming, antiforgery, MIME validation, virus scan integration, S3 pre-signed URL |
| Headers | Forwarded headers (proxy chain), Server-Timing, RateLimit, custom |
| OpenAPI | Spec generation, schema customization, example generators, schema filter, document filter, security scheme |
| AsyncAPI | Saunter for event-driven APIs |
| API contract testing | PactNet (consumer-driven), Postman/Newman runs, Karate (external), Schemathesis |
| BFF aggregation | Multi-call aggregator endpoint, per-tenant tailoring |
| Server-Sent Events | One-way stream from server; reconnection w/ Last-Event-ID |
| Long-polling | Hold connection until event; backoff |
| Webhooks | Outbound (your → external), inbound (external → you); signature verification (HMAC), retry policy, dead-letter |
| Idempotency | Header-based key, server-side store with TTL |
| Pagination | Offset (`page`/`limit`), cursor (`next`), keyset (token), total-count optional |
| Filtering | Operators (eq, neq, in, contains, between), Sieve, Gridify, OData-style |
| Sorting | Multi-key with priority; default order |
| Search | Full-text via index; query parser |
| Bulk operations | Batch endpoint, partial-success reporting |
| OData | `Microsoft.AspNetCore.OData` for full $filter/$select/$expand |
| GraphQL | HotChocolate / GraphQL.NET — schema, resolvers, dataloader |
| gRPC | Service per .proto, server streaming, client streaming, bidi streaming, gRPC-Web for browsers, gRPC reflection |

### 5.2 HTTP client / outbound integration

| Concern | Hidden vectors |
|---|---|
| Engines | `HttpClient` (with `IHttpClientFactory`), Refit (typed clients), RestSharp, Flurl.Http, Microsoft.Extensions.Http.Resilience standard handlers, Kiota generated clients, NSwag generated clients |
| Lifetimes | Named clients, typed clients, transient pitfalls, SocketsHttpHandler config |
| DNS | TTL refresh, PooledConnectionLifetime, DNS rotation |
| Connection pooling | Per-handler, per-client, max connections per server |
| TLS | Cert store, custom validation, mTLS, SNI, ALPN |
| Resilience | Polly handlers, retry, breaker, hedging, timeout, fallback, rate-limit |
| Circuit-breaker state | Per-host, per-route, per-method |
| Logging | Request/response logging (BodyLogger middleware), redact secrets |
| Tracing | OTel HttpClient instrumentation; W3C tracecontext propagation |
| Auth | Bearer, mTLS cert, OAuth client credentials (Microsoft.Identity.Web for downstream API) |
| Retries | Idempotent only; respect Retry-After |
| Compression | Auto-decompress; Accept-Encoding |
| Streaming | Stream upload/download, ranged downloads, multipart streaming |
| Polly v8 pipelines | Composed strategies, telemetry, predicates, contexts |
| Test fakes | WireMock.Net, MockHttp, JustEat HttpClientInterception, custom DelegatingHandler |
| Code-gen clients | Refit (interface), NSwag (OpenAPI), Kiota (OpenAPI) |
| GraphQL client | Strawberry Shake, GraphQL.Client |
| gRPC client | Grpc.Net.Client, ClientFactory, codegen from .proto |
| WebSocket client | ClientWebSocket, Marfusios Websocket.Client |
| Binary protocols | Custom over Sockets, MQTT (MQTTnet), MessagePack RPC |

### 5.3 Data persistence (relational)

| Concern | Hidden vectors |
|---|---|
| Provider | SQL Server, PostgreSQL (Npgsql), MySQL (Pomelo), SQLite, Oracle (Devart/Oracle), Cosmos, Firebird, DB2 (IBM) |
| Migrations | EF Migrations, FluentMigrator, DbUp, Liquibase (external), Flyway (external) |
| Transactions | `IDbContextTransaction`, ambient via TransactionScope, isolation levels (RC/RR/Serializable/Snapshot), retryable transactions |
| Concurrency | Optimistic via RowVersion / xmin / version columns; pessimistic locks (`SELECT FOR UPDATE`) |
| Bulk ops | EFCore.BulkExtensions, ExecuteUpdate/ExecuteDelete (.NET 7+), provider-specific copy (`COPY` for Postgres, `BULK INSERT` for SQL Server) |
| Query splitting | `AsSplitQuery()` for include cardinality blowup |
| Compiled queries | EF.CompileAsyncQuery |
| JSON columns | `OwnsOne`, `HasJsonConversion`, JSON path queries |
| Owned types | Aggregates as nested-without-id types |
| Inheritance | TPH, TPT, TPC strategies |
| Soft delete | Query filter, restore op |
| Auditing | `SaveChangesInterceptor`, EFCore.Triggered |
| Multi-tenancy | Query filter on tenant key; per-DB; per-schema |
| Query interceptors | `IDbCommandInterceptor`, `ISaveChangesInterceptor`, `IMaterializationInterceptor` |
| Connection resiliency | EnableRetryOnFailure (Sql/Npgsql) |
| Read replicas | Connection routing (manual or policy) |
| CQRS reads | Direct Dapper / linq2db for query side; EF for write side |
| Specifications | Ardalis.Specification, NSpecifications |
| Event sourcing | Marten, EventStore, Aggregates.NET, EventFlow |
| Sharding | Manual (custom router), ShardingCore (Chinese), provider-specific (Citus for Postgres) |
| Performance | Tracking off for read paths; projection to DTO; AsNoTracking; AsNoTrackingWithIdentityResolution |
| Compiled models | EF compiled model gen for fast startup |
| AOT-friendly | EF AOT in preview .NET 9 |
| Connection mgmt | DbContext pooling (`AddDbContextPool`), per-request lifetime |
| Database first / Code first / Reverse engineer | dotnet ef dbcontext scaffold |

### 5.4 Data persistence (NoSQL / specialty)

| Concern | Hidden vectors |
|---|---|
| Document DBs | MongoDB.Driver (full LINQ), MongoFramework (EF-like), Marten (Postgres-as-doc), RavenDB.Client, LiteDB (embedded), Couchbase.NetClient, ArangoDB.NET |
| Key-Value | Redis (StackExchange.Redis, NRedisStack for modules), Memcached, Etcd, Consul.NET |
| Wide-column | Cassandra C# driver, ScyllaDB, Microsoft Azure Data Tables |
| Graph | Neo4j.Driver, Gremlin via Microsoft.Azure.Cosmos, ArangoDB.NET, EmbeddedDB |
| Time-series | InfluxDB.Client, QuestDB, Prometheus (read-only), TimescaleDB (Postgres ext) |
| Wide-column / OLAP | ClickHouse.Client, Apache Druid, Apache Pinot |
| Vector | Pinecone, Weaviate, Qdrant, Milvus, Chroma, Pgvector, NRedisStack vector, Microsoft.SemanticKernel.Memory.* |
| Object storage | Azure.Storage.Blobs, AWSSDK.S3, Google.Cloud.Storage.V1, MinIO, Storage.Net, FluentStorage |
| Embedded | LiteDB, SQLite, RocksDB.NET, LevelDb |
| Event store | EventStore.Client (Kurrent), Marten, EventStoreDb, Apache Kafka (used as ES) |
| Indices / search | Lucene.Net, Elasticsearch, OpenSearch, Solr, Algolia, Meilisearch, Typesense, Manticore |
| Session store | Redis, SQL, Distributed memory cache |

### 5.5 Messaging / integration

| Concern | Hidden vectors |
|---|---|
| Brokers | RabbitMQ (RabbitMQ.Client), Kafka (Confluent.Kafka), Azure Service Bus, Azure Event Hubs, Azure Event Grid, AWS SQS, AWS SNS, AWS Kinesis, GCP Pub/Sub, NATS / JetStream, MQTT brokers (Mosquitto, EMQX, HiveMQ), ActiveMQ Artemis, IBM MQ, MSMQ (legacy) |
| Abstractions | MassTransit, NServiceBus, Brighter, Wolverine, Rebus, EasyNetQ, Silverback, KafkaFlow, Streamiz, Foundatio.Messaging |
| Patterns | Pub/Sub, Request/Response, Send/Receive, Saga, Routing slip, Choreography, Orchestration |
| Schemas | Protobuf, Avro (+ Confluent Schema Registry), JSON, MessagePack, MemoryPack, custom |
| Idempotency | Inbox table, message ID dedup |
| Outbox | DotNetCore.CAP, MassTransit Outbox, Wolverine TX, NServiceBus Outbox, Brighter Outbox, Marten Outbox |
| Sagas / state machines | MassTransit Automatonymous (built-in), NServiceBus, Brighter, Wolverine, Stateless, Appccelerate.StateMachine, Daenet.Stateless, WorkflowCore, Elsa, Optimajet.Workflow, Microsoft DurableTask |
| Dead-letter | Per-queue DLQ, retry then DLQ, manual reprocess |
| Poison message | Detection + DLQ + alarm |
| Message versioning | Schema evolution; backward/forward compat |
| Routing | Topic, exchange, partition key, header-based |
| Ordering | Partition key (Kafka), single consumer (RMQ), session ID (ASB) |
| Compaction | Kafka compacted topics |
| Throttling | Per-consumer concurrency, per-tenant rate limit |
| Consumer scaling | Competing consumers, partition assignment |
| Tracing | OTel propagation across messages |
| At-least-once vs exactly-once | Default at-least-once + idempotent; ASB has optional EOI; Kafka EOS via TX |
| Webhooks | Outbound HTTP push with retry; signature verification; replay protection |
| AsyncAPI | Saunter for AsyncAPI doc generation |

### 5.6 Real-time / push

| Concern | Hidden vectors |
|---|---|
| Engines | SignalR (Hub), Microsoft.Azure.SignalR (managed), MagicOnion (gRPC RPC), MQTTnet, raw WebSockets |
| Backplane | Redis (StackExchange.Redis), Azure SignalR Service, NATS-bridged |
| Auth | JWT bearer in connect; per-method auth filter |
| State recovery | SignalR connection-level state restore |
| Reconnection | Built-in client retry policy |
| Streams | `IAsyncEnumerable` over hub, `ChannelReader<T>` |
| Server push | FCM (Firebase Admin), APNs, Web Push (`WebPush`), Pushover |
| Email push | SendGrid, Mailgun, Postmark, Amazon SES, Azure Comm Email, MailKit + SMTP |
| SMS | Twilio, Vonage, Plivo, MessageBird, Azure Comm SMS |
| Chat | Twilio Conversations, Stream Chat, Azure Comm Chat |

### 5.7 Storage / files / media

| Concern | Hidden vectors |
|---|---|
| Object stores | Azure.Storage.Blobs, AWSSDK.S3, Google.Cloud.Storage.V1, MinIO.SDK, Backblaze B2, Wasabi, DigitalOcean Spaces |
| Abstractions | Storage.Net (aloneguid), FluentStorage |
| Local file | `System.IO`, `IFileProvider`, PhysicalFileProvider |
| Pre-signed URLs | Generate, expiry, content-type lock |
| Multi-part upload | Engine-specific (S3 multi-part) |
| Large file streaming | `Stream` upload/download, range, resume |
| Compression | gzip, brotli, zstd, LZ4, 7z (SevenZip), zip (System.IO.Compression, SharpZipLib) |
| Encryption | AES-GCM at rest, KMS-managed keys, client-side vs server-side |
| Image | ImageSharp, ImageSharp.Web (cache+resize), SkiaSharp, Magick.NET |
| Video | FFMpegCore, Xabe.FFmpeg, NReco.VideoConverter |
| PDF | QuestPDF (modern), DinkToPdf, PdfSharp, iText7 (AGPL), SelectPdf (commercial), Aspose.PDF (commercial) |
| Office docs | OpenXml SDK, ClosedXML, EPPlus, NPOI, DocX |
| CSV | CsvHelper, ServiceStack.Text.Csv |
| Parquet | Parquet.Net, Apache.Arrow |
| HTML parsing | HtmlAgilityPack, AngleSharp |
| MIME | MimeKit |
| Content-Disposition | Inline vs attachment, filename encoding |
| Virus scan | ClamAV via clamd client, Microsoft Defender for Storage |
| Hashing | SHA256, BLAKE3, xxHash for dedup |

### 5.8 AI / ML / LLM / vector

| Concern | Hidden vectors |
|---|---|
| Orchestration | Microsoft.SemanticKernel, Microsoft.KernelMemory, LangChain.NET, LangChainSharp, Microsoft.AutoGen |
| LLM providers | OpenAI (Official, OpenAI-DotNet community), Anthropic.SDK / AnthropicClient, Azure OpenAI, AWS Bedrock (AmazonBedrockRuntime), Google Vertex / Gemini, OpenRouter, Ollama (OllamaSharp), LLamaSharp (local), Mistral, Cohere |
| Vector DBs | Pinecone, Weaviate, Qdrant, Milvus, Chroma, Pgvector (Npgsql.Pgvector), NRedisStack vectors, Microsoft.SemanticKernel.Connectors.* |
| Embedding models | text-embedding-3-small/large, ada-002, Cohere Embed, Voyage, BGE, Microsoft.Extensions.AI |
| RAG | Ingest → chunk → embed → upsert → retrieve → rerank → augment-prompt; Microsoft.KernelMemory shape |
| Tools / Function calling | OpenAI tools, Anthropic tools, MS SK plugins, MCP (Model Context Protocol) — anthropics' standard |
| Streaming | SSE / chunked stream of LLM tokens |
| Cost tracking | Token-usage counters; OTel custom meter |
| Prompt mgmt | Versioned prompts, Prompty file, Microsoft.Extensions.AI prompts |
| Eval | LLM-as-judge, ground truth set, Promptfoo (external), Microsoft.AI.Evaluation (.NET 9) |
| Caching | Embedding cache (hash → vector), response cache (semantic similar), prompt cache |
| Local inference | LLamaSharp (LLaMA cpp binding), ONNXRuntime, TorchSharp |
| ML.NET | Classical ML — classification, regression, clustering, recommendation |
| Vision | Microsoft.Extensions.AI Image, Azure.AI.Vision, OpenCV (Emgu.CV), ONNX models |
| Speech | Whisper.net (Whisper.cpp binding), Azure Speech, AWS Transcribe, GCP Speech-to-Text, Coqui TTS |
| Document AI | Azure.AI.DocumentIntelligence (Form Recognizer), Aspose.OCR, Tesseract.NET |

### 5.9 Authentication / Authorization (deep)

| Concern | Hidden vectors |
|---|---|
| Identity store | ASP.NET Core Identity, Identity API endpoints, Duende IdentityServer (commercial), OpenIddict, custom |
| External login | Google, Microsoft, Apple, Twitter, Facebook, LinkedIn, GitHub, GitLab, Bitbucket, generic OIDC |
| MFA | TOTP (Otp.NET), WebAuthn/FIDO2 (Fido2.AspNetCore), SMS, email |
| Password policy | Length, complexity, breach check (HaveIBeenPwned via "Pwned Passwords"), Argon2 (Konscious), bcrypt, scrypt, PBKDF2 |
| Account lockout | Attempts threshold, duration, sliding |
| Account recovery | Email reset, security question (deprecated), backup codes, identity-proof |
| Session mgmt | Cookie, refresh tokens, token rotation, revocation |
| Authorization | Role, claim, policy, resource (handlers), permission (Permission package), ABAC, ReBAC (SpiceDB / Authzed) |
| OPA integration | OPA sidecar via REST or embed |
| RBAC vs ABAC vs ReBAC | Choose by relationship complexity |
| API key | Hash + storage, rotation, scope |
| mTLS | Cert auth scheme, X-Forwarded-Client-Cert |
| Audit | Login/logout/MFA events; failed attempts |

### 5.10 Workflow / state machines / process

| Concern | Hidden vectors |
|---|---|
| State machines | Stateless (Nicholas Blumhardt), Appccelerate.StateMachine, Daenet.Stateless, MassTransit Automatonymous |
| Workflow engines | Elsa Workflows, WorkflowCore, Optimajet.Workflow, Camunda (.NET via REST), Conductor Netflix (.NET via REST), Temporal.io (.NET SDK), Zeebe |
| Durable tasks | Microsoft.DurableTask, Azure Durable Functions |
| Process modeling | BPMN (Camunda), state-chart (XState — JS), code-first (Stateless, MassTransit) |
| Compensation | Saga pattern, undo step |
| Persistence | DB-backed (Marten, SQL, Cosmos), redis, EventStore |
| Versioning | Long-lived workflows must handle code change |
| Visualization | DOT graph, mermaid, Camunda modeler |
| Suspend/resume | Pause for human approval; resume on event |

### 5.11 Documents / reports / PDFs / Excel

| Concern | Hidden vectors |
|---|---|
| PDF gen | QuestPDF (modern, dual-license), DinkToPdf (HTML→PDF wkhtmltopdf), PdfSharp, iText7 (AGPL/commercial), SelectPdf (commercial), Aspose.PDF, GemBox.Pdf |
| Excel | ClosedXML, EPPlus, NPOI, OpenXml SDK, ExcelDataReader, Aspose.Cells, Spire.XLS |
| Word | DocX, OpenXml SDK, Aspose.Words, Spire.Doc |
| PowerPoint | OpenXml SDK, Aspose.Slides |
| HTML→PDF | DinkToPdf, SelectPdf, Aspose, Microsoft Edge headless via Puppeteer-Sharp |
| HTML rendering | HtmlAgilityPack, AngleSharp, RazorEngine.NetCore |
| Templating | Razor (Microsoft.AspNetCore.Razor), Scriban, Fluid (DotLiquid), Handlebars.NET |
| Image / chart embedding | OxyPlot, LiveCharts2, ScottPlot, ImageSharp.Drawing |
| OCR | Tesseract.NET, Azure Document Intelligence, Aspose.OCR |
| Reports | FastReport (commercial), Reports.NET, Microsoft Reporting Services (legacy) |

### 5.12 Geo / IP / maps

| Concern | Hidden vectors |
|---|---|
| Geometry | NetTopologySuite (NTS), ProjNet (projections), GeoAPI |
| Geocoding | Geocoding.Net, GoogleApi (via Maps), Bing.Maps, Nominatim.NET |
| IP geolocation | MaxMind.GeoIP2, IPInfo.io client, ip-api, ipdata |
| Distance / routing | OSRM (external), GraphHopper (external), Microsoft Maps Routes, Mapbox |
| Spatial queries | Postgres + PostGIS via Npgsql.NetTopologySuite, SqlServer.Spatial |
| Reverse geo | Geocoding.Net, MaxMind reverse |

### 5.13 Email / SMS / Push notifications

| Concern | Hidden vectors |
|---|---|
| SMTP | MailKit + MimeKit, SmtpClient (.NET legacy), FluentEmail (high-level) |
| Transactional providers | SendGrid, Mailgun, Postmark, Amazon SES, Azure Comm Email, Resend |
| Templating | MJML (via mjml.net), Razor templates, Handlebars, Liquid |
| Bounce / complaint handling | Provider webhook ingest, suppression list |
| SMS | Twilio (Twilio.AspNet), Vonage, Plivo, MessageBird, Azure Comm SMS |
| Push | Firebase Admin SDK, APNs (P8 key), OneSignal, Web Push (vapidkeys) |
| Inbound | Mailgun routes, SendGrid Inbound Parse, Postmark inbound, Azure Comm receive |
| Anti-spam | DKIM, SPF, DMARC config; reputation monitoring |

### 5.14 Payments / billing

| Concern | Hidden vectors |
|---|---|
| Providers | Stripe.net, Paddle.NET, BraintreeSDK, Adyen.NET, Square.Connect, Klarna, ChargeBee, Recurly |
| Subscriptions | Recurring, trial, proration, upgrade/downgrade |
| Webhooks | Signature verification (Stripe sig), replay protection, idempotency |
| Tax | Stripe Tax, Avalara (Avatax.SDK), TaxJar, MOSS (EU), reverse charge |
| Invoicing | Provider invoices vs custom (QuestPDF) |
| Compliance | PCI-DSS scope (use provider Elements), SCA / 3DS, AML |
| Reconciliation | Daily settle, dispute handling, refund flow |

### 5.15 Search

| Concern | Hidden vectors |
|---|---|
| Engines | Lucene.Net, Elasticsearch (Elastic.Clients.Elasticsearch / NEST legacy), OpenSearch.Client, Solr.NET, Meilisearch, Typesense.Client, Algolia.Search, Manticore.Client, Azure.Search.Documents, Postgres FTS (tsvector via Npgsql), SQLite FTS5, Redis RediSearch (NRedisStack), Marten FTS |
| Indexing | Push (manual), pull (DB connector), CDC (Debezium → ES) |
| Schema | Mappings, analyzers (lowercase, stemmer, ngram, edge_ngram, synonym), tokenizers (standard, whitespace, custom) |
| Multi-field | Full-text + keyword + completion |
| Synonyms | Per-language synonym files |
| Ranking | BM25 (default), TF-IDF, custom function score, learning-to-rank |
| Vector search | k-NN, ANN (HNSW, IVF), hybrid (BM25 + vector) |
| Faceting | Aggregations, terms, ranges, date histograms |
| Highlighting | Snippet generation |
| Suggestions / autocomplete | Completion suggester, edge-ngram |
| Multi-language | Per-field language analyzer |
| Sharding / replication | Index sharding, replica count |
| Reindexing | Zero-downtime via alias swap |
| Security | TLS, basic auth, API key, role mapping |

### 5.16 Configuration & secrets (deep)

| Concern | Hidden vectors |
|---|---|
| Sources | env, JSON, YAML (community provider), TOML (community), CommandLine, UserSecrets, AzureAppConfig, KeyVault, AWS SecretsManager, AWS AppConfig, GCP Secret Manager, Vault (Hashi), Consul KV, etcd, file watcher provider |
| Reload | `IOptionsMonitor`, ChangeToken, polling, push (Vault) |
| Encryption | DataProtection layer, KMS unwrap |
| Secrets rotation | Provider-driven; cache TTL; refresh on token expiry |
| Validation | Startup-time, post-config, DataAnnotations, FluentValidation |
| Environment-aware | `IHostEnvironment.EnvironmentName`, per-env JSON file, override chain |
| Hierarchy | Section binding, named options, IConfigurationSection |
| Per-tenant | Layer tenant config provider on top |

### 5.17 Realtime metrics / dashboards / SLO

| Concern | Hidden vectors |
|---|---|
| Engines | OpenTelemetry Metrics, Prometheus.NET, App.Metrics, Microsoft.Extensions.Diagnostics |
| Exporters | OTLP, Prometheus scrape, Datadog, AzureMonitor, NewRelic |
| Dashboards | Grafana, Aspire Dashboard, Datadog, App Insights Workbooks |
| SLO/SLI | Burn rate calculation, multi-window/multi-burn-rate alerts, error budgets |
| Tracing | Activity, ActivitySource, baggage, sampling |
| Logs | Serilog/NLog/MEL, OTel logs, structured fields |
| Profiling | dotnet-counters, dotnet-trace, JetBrains dotTrace, JetBrains dotMemory, PerfView |
| Continuous profile | Pyroscope.NET, Parca, Datadog Continuous Profiler |

### 5.18 Multi-tenancy (deep)

| Concern | Hidden vectors |
|---|---|
| Resolution | Subdomain, path, header, JWT claim, query, default tenant |
| Storage isolation | Per-DB (silo), per-schema, per-row, per-shard |
| Per-tenant cache | Key-prefix; cross-tenant invalidation isolation |
| Per-tenant config | Settings overlay |
| Per-tenant DB | Migrations: per-tenant runner, parallel apply |
| Per-tenant background jobs | Tenant-scoped queues |
| Tenant onboarding / offboarding | Provisioning, deprovisioning, data export, GDPR delete |
| Cross-tenant features | Aggregated reports, admin views, careful access control |
| Per-tenant feature flags | Override per tenant |

### 5.19 Internationalization (deep)

| Concern | Hidden vectors |
|---|---|
| String layer | `IStringLocalizer<T>`, OrchardCore.Localization, Fluent.Net |
| Resource format | .resx, .json, .po, .ftl |
| Plural | Microsoft fallback (limited), ICU MessageFormat (community), Fluent (full) |
| Number / date | `CultureInfo`, `NumberFormatInfo`, `DateTimeFormatInfo` |
| Calendar | Gregorian / Hijri / Buddhist / Japanese / Persian / etc. |
| Timezone | TimeZoneInfo + TimeZoneConverter (Win↔IANA) |
| RTL | UI mostly; backend forwards `dir` if needed |
| Pseudo-localization | QA tooling |
| Per-locale fallback chain | en-GB → en → invariant |
| Per-tenant locale | Resolution chain: user pref → tenant default → app default |

### 5.20 Static analysis / QA / dev experience

| Concern | Hidden vectors |
|---|---|
| Analyzers | Microsoft.CodeAnalysis.NetAnalyzers, StyleCop.Analyzers, SonarAnalyzer.CSharp, Roslynator, Meziantou.Analyzer, ErrorProne.NET, AsyncFixer, Threading.Analyzers |
| Editor configs | `.editorconfig` rules; severity by-rule |
| Style enforcement | dotnet format, Prettier (non-.NET) |
| Code review | GitHub PR templates, Reviewable, Conventional Comments |
| Pre-commit | Husky.Net, dotnet format pre-commit, lint-staged (npm) |
| Build | dotnet build, Cake, Nuke, FAKE (F#), MSBuild custom targets |
| Pack | dotnet pack, NuGet config, source link, deterministic builds |
| Repo | Git, Git LFS, Git submodule, Git worktree |
| CI/CD | GitHub Actions, Azure DevOps, GitLab CI, Jenkins, Bamboo, TeamCity, CircleCI, Buildkite, Octopus Deploy |
| Container | Docker, Buildah, Podman, multi-stage, distroless base, Aspire publishing |
| K8s | kubectl, helm, kustomize, Skaffold, Tilt, Kubernetes Operator (KubeOps for .NET) |
| Cloud | Azure (App Service, Container Apps, AKS), AWS (ECS, EKS, App Runner, Lambda), GCP (Cloud Run, GKE, Functions), DO, Linode, Vultr |
| IaC | Terraform, Pulumi (.NET), Bicep, ARM, CloudFormation, AWS CDK (.NET), GCP Deployment Manager |

---

## 6. Delegate / extension API surface

The hooks consumers pass in to customize behavior. Roughly grouped by shape.

| Shape | Representative use | Example libs |
|---|---|---|
| `Func<T, TResult>` | Pure transform | LINQ, EF projection, Mapper config |
| `Func<T, CancellationToken, ValueTask<TResult>>` | Async transform | Polly, MediatR, Wolverine |
| `Action<TBuilder>` | Configure-by-builder | `services.AddX(opts => …)`, MS DI, Polly v8 |
| `Action<TOptions>` | Configure options | `services.Configure<TOptions>(o => …)` |
| `IPipelineBehavior<TIn, TOut>` | Cross-cutting on mediator | MediatR, Brighter, Wolverine, NServiceBus |
| `IInterceptor` | Method intercept | Castle.DynamicProxy |
| `IEndpointFilter` | HTTP endpoint cross-cut | Minimal API (.NET 7+) |
| `IExceptionHandler` | Centralized exception | .NET 8+ |
| `IAuthorizationHandler` | Resource auth | ASP.NET Core authz |
| `IMiddleware` / `RequestDelegate` | HTTP pipeline | ASP.NET Core middleware |
| `IDbCommandInterceptor` / `ISaveChangesInterceptor` | EF interception | EF Core |
| `IRetryStrategy` / `Predicate<...>` | Polly predicate | Polly v8 |
| `Func<HttpRequestMessage, …, ResiliencePipelineBuilder>` | Resilience config | Microsoft.Extensions.Http.Resilience |
| `IConfigureOptions<T>` | Late options config | Microsoft.Extensions.Options |
| `IStartupFilter` | Pipeline modification at startup | ASP.NET Core |
| `IApplicationLifetime` hooks | App lifecycle events | Hosting |
| `IHealthCheck` | Custom health check | HealthChecks |
| `Func<T, Task<bool>>` predicates | Filter | LINQ-async, EF |
| Comparer<T> / IEqualityComparer<T> | Equality / sort delegation | LINQ, dictionaries, hash |
| `IObservable<T>` / `IObserver<T>` | Reactive | Rx.NET |
| `Action<ILoggerBuilder>` | Logging config | Microsoft.Extensions.Logging |
| `Action<IConfigurationBuilder>` | Config source addition | Microsoft.Extensions.Configuration |
| `Func<IServiceProvider, T>` factory | DI factory | Microsoft DI |
| `IValidator<T>` | Validation | FluentValidation, custom |
| `IMessageHandler<T>` / `IConsumer<T>` | Message consumption | MassTransit, NServiceBus |
| `IEventHandler<TEvent>` | Domain event handling | Custom + MediatR |
| `IRequestHandler<TReq,TRes>` | Mediator handler | MediatR |
| `ICommandHandler<TCmd>` / `IQueryHandler<TQry,TRes>` | CQRS | Brighter, Wolverine, Custom |
| Source-gen attribute hooks | Compile-time customization | Mapperly, Vogen, Mediator |

---

## 7. Configuration leak point inventory

Config that *must* be supplied at composition (or have sensible defaults). Leak per area.

| Vector | Config the consumer must supply / can override |
|---|---|
| **Hosting** | Environment name, content root, web root, URLs, port, protocols (HTTP/1, HTTP/2, HTTP/3), ListenOptions |
| **Auth** | Issuer, audience, signing key (or JWKS), required scopes, redirect URIs, post-logout URI, client ID, secret/cert, OIDC discovery URL |
| **DB** | Connection string, credentials, command timeout, retry count, pool size, multi-AZ replica strings |
| **Cache** | Redis connection, region prefix, default TTL, hybrid L1 size cap |
| **Messaging** | Broker URL, vhost / namespace, credentials, topic/queue name, partitioning key, consumer group, prefetch count |
| **Object storage** | Endpoint, region, bucket/container, credential mode (managed identity vs key) |
| **Email** | SMTP host/port, API key (SendGrid etc.), from address, reply-to, default templates |
| **SMS / Push** | Provider keys, sender ID, sandbox mode |
| **Search** | Endpoint, API key, index name, default analyzer |
| **Vector store** | Endpoint, API key, namespace, embedding model name + dim |
| **LLM provider** | API key, base URL, default model, default temperature, max tokens, organization ID |
| **Telemetry** | OTLP endpoint, service name, deployment env, sampling ratio, exporter selection |
| **Logging sinks** | Connection strings (Seq, Splunk, Datadog), token, level overrides per category |
| **Health checks** | Tags, timeouts, failure thresholds |
| **Rate limiting** | Per-policy limits, window, per-IP / per-user partition keys |
| **Background jobs** | Worker count, queue names, retention, dashboard auth |
| **Feature flags** | Provider keys, environment, default fallback |
| **Multi-tenancy** | Tenant resolution strategy, default tenant, per-tenant secrets store |
| **Localization** | Default culture, supported cultures, fallback chain, resource path |
| **CORS** | Allowed origins, methods, headers, credentials, max-age |
| **Compression** | Schemes, minimum size, level |
| **Anti-forgery** | Cookie name, header name, suppress on safe methods |
| **DataProtection** | Key ring storage, application name, key lifetime |
| **HSTS** | max-age, preload, include-subdomains |
| **Static files** | Roots, MIME types, default file, redirect, caching headers |
| **OpenAPI** | Title, version, description, security schemes, server URLs |
| **gRPC** | Max message size, compression, deadline propagation |
| **GraphQL** | Schema-first vs code-first, introspection on/off in prod |
| **SignalR** | Backplane (Redis or Azure SR), keepalive, max message size, transport whitelist |
| **Resilience** | Per-route policies, retry counts, backoff, timeout |
| **Webhook delivery** | Secret signing key, retry count, dead-letter sink |

---

## 8. .NET runtime / BCL APIs encyclopedia

The "MDN equivalent" — every namespace a backend lib might wrap or expose.

### 8.1 Memory & buffers

| API | Niche |
|---|---|
| `System.Span<T>` / `System.ReadOnlySpan<T>` | Stack-only window over contiguous memory |
| `System.Memory<T>` / `System.ReadOnlyMemory<T>` | Heap-storable window |
| `System.Buffers.ArrayPool<T>` | Pooled array rent/return |
| `System.Buffers.MemoryPool<T>` | Pooled `IMemoryOwner<T>` |
| `System.Buffers.ReadOnlySequence<T>` | Multi-segment buffer chain |
| `System.IO.Pipelines` | High-perf parsing/writing pipeline |
| `System.Buffers.Binary.BinaryPrimitives` | Endian-aware read/write |
| `System.Buffers.Text.Utf8Parser` / `Utf8Formatter` | Lossless utf8 numeric IO |
| `System.Runtime.InteropServices.MemoryMarshal` | Unsafe Span↔T conversions |
| `System.GC` | GC.Collect, GetTotalMemory, AllocateUninitializedArray |
| `System.GCSettings` | LatencyMode, IsServerGC |

### 8.2 Concurrency primitives

| API | Niche |
|---|---|
| `System.Threading.Tasks.Task` / `Task<T>` / `ValueTask` / `ValueTask<T>` | Asynchrony |
| `System.Threading.Tasks.Parallel` / `Parallel.ForEachAsync` | Parallel loops |
| `System.Threading.Channels` | MPMC bounded/unbounded channel |
| `System.Threading.Tasks.Dataflow` | Block-based pipeline |
| `System.Threading.Lock` (.NET 9) | Lightweight monitor primitive |
| `System.Threading.SemaphoreSlim` | Async-friendly semaphore |
| `System.Threading.Interlocked` | Atomics |
| `System.Threading.CountdownEvent` / `ManualResetEvent` / `AutoResetEvent` | Sync events |
| `System.Threading.AsyncLocal<T>` | Implicit context |
| `System.Threading.CancellationToken` / `CancellationTokenSource` | Cancellation |
| `System.Threading.Timer` / `PeriodicTimer` | Scheduled callbacks |
| `System.Threading.RateLimiting` | Token bucket / sliding / fixed / concurrency |

### 8.3 Reactive

| API | Niche |
|---|---|
| `System.Reactive.Linq.Observable` | LINQ-over-events |
| `System.Reactive.Disposables.CompositeDisposable` | Resource bookkeeping |
| `System.Reactive.Subjects.*` | Subject, BehaviorSubject, ReplaySubject |
| `System.Linq.Async` | Async LINQ over IAsyncEnumerable |
| `System.IObservable<T>` / `System.IObserver<T>` | Push streams |

### 8.4 Diagnostics

| API | Niche |
|---|---|
| `System.Diagnostics.Activity` / `ActivitySource` | Native trace span |
| `System.Diagnostics.ActivityListener` | Subscribe to activities |
| `System.Diagnostics.Metrics.Meter` | Counters, gauges, histograms |
| `System.Diagnostics.Tracing.EventSource` / `EventListener` | High-perf events |
| `System.Diagnostics.Stopwatch` | High-res elapsed |
| `System.Diagnostics.Debug` / `Trace` | Debug-only |
| `System.Diagnostics.Process` | OS process control |
| `System.Diagnostics.DiagnosticSource` / `DiagnosticListener` | Lib-to-host events |

### 8.5 Network / IO

| API | Niche |
|---|---|
| `System.Net.Http.HttpClient` / `SocketsHttpHandler` | HTTP client |
| `System.Net.Http.Json.HttpClientJsonExtensions` | JSON helpers |
| `System.Net.Sockets.Socket` / `TcpClient` / `UdpClient` | Sockets |
| `System.Net.Quic.QuicConnection` (preview) | QUIC client/server |
| `System.Net.WebSockets.ClientWebSocket` / `WebSocket` | WebSocket |
| `System.Net.NetworkInformation` | Pings, interfaces |
| `System.Net.Mail.SmtpClient` (legacy) | SMTP |
| `System.Net.Dns` | DNS resolve |
| `System.Net.IPAddress` / `IPEndPoint` | Endpoints |
| `System.IO.FileSystem` | File ops |
| `System.IO.MemoryStream` / `FileStream` / `BufferedStream` | Streams |
| `System.IO.Compression` (gzip, deflate, brotli, ZipFile) | Compression |
| `System.IO.Hashing` (CRC32/64, XXHash32/64/128) | Non-crypto hashing |
| `System.IO.Pipes` | Named pipes IPC |

### 8.6 Cryptography

| API | Niche |
|---|---|
| `System.Security.Cryptography.Aes` (CBC), `AesGcm`, `AesCcm` | AES modes |
| `System.Security.Cryptography.ChaCha20Poly1305` | ChaCha AEAD |
| `System.Security.Cryptography.SHA*` | Hashing |
| `System.Security.Cryptography.HMACSHA*` | Keyed MACs |
| `System.Security.Cryptography.RandomNumberGenerator` | CSPRNG |
| `System.Security.Cryptography.Rfc2898DeriveBytes` (PBKDF2) | Key derivation |
| `System.Security.Cryptography.RSA` / `ECDsa` / `ECDiffieHellman` | Asymmetric |
| `System.Security.Cryptography.X509Certificates` | Certs |
| `System.Security.Cryptography.MLKem` (.NET 9 preview) | Post-quantum KEM |
| `System.Security.Cryptography.MLDsa` (.NET 9 preview) | Post-quantum signing |

### 8.7 Reflection / metaprogramming

| API | Niche |
|---|---|
| `System.Reflection.Assembly` / `Type` / `MethodInfo` etc. | Runtime introspection |
| `System.Reflection.Emit` | Dynamic code (less AOT-friendly) |
| `System.Linq.Expressions.Expression<T>` | Expression trees |
| `System.Runtime.CompilerServices.RuntimeHelpers` | Low-level helpers |
| `System.Runtime.CompilerServices.Unsafe` | Unsafe casts |
| `Microsoft.CodeAnalysis.*` (Roslyn) | Source generators / analyzers |

### 8.8 Globalization / culture

| API | Niche |
|---|---|
| `System.Globalization.CultureInfo` | Culture context |
| `System.Globalization.RegionInfo` | Region info |
| `System.Globalization.NumberFormatInfo` / `DateTimeFormatInfo` | Format providers |
| `System.Globalization.Calendar` (Gregorian/Hijri/Buddhist/Japanese/Persian/etc.) | Calendar systems |
| `System.Globalization.StringInfo` | Grapheme iteration |
| `System.Globalization.CompareInfo` | Locale-aware compare |
| `System.Globalization.Plural` / `PluralRules` (community) | Plural rules |

### 8.9 Time

| API | Niche |
|---|---|
| `System.DateTime` / `DateTimeOffset` | Builtin time types |
| `System.TimeProvider` (.NET 8+) | Time abstraction |
| `System.TimeZoneInfo` | Time zones |
| `System.Threading.PeriodicTimer` | Async tick |
| `System.Diagnostics.Stopwatch.GetTimestamp()` | Monotonic ticks |
| `System.DateOnly` / `TimeOnly` (.NET 6+) | Date/time-only types |

### 8.10 Numerics / SIMD

| API | Niche |
|---|---|
| `System.Numerics.BigInteger` | Arbitrary precision int |
| `System.Numerics.Complex` | Complex numbers |
| `System.Numerics.Vector<T>` / `Vector2/3/4` / `Matrix4x4` | SIMD |
| `System.Runtime.Intrinsics.*` (Avx, Avx2, Avx512, Sse, Arm.AdvSimd) | Hardware intrinsics |
| `System.Numerics.Tensors` | Tensor primitives + ML helpers |

### 8.11 Text / regex / formatting

| API | Niche |
|---|---|
| `System.Text.StringBuilder` | Mutable strings |
| `System.Text.Encoding` (UTF-8/16/32, ASCII) | Encoding |
| `System.Text.RegularExpressions.Regex` | Regex (compiled, source-gen) |
| `System.Text.Json.*` | JSON (DOM, source-gen, polymorphism) |
| `System.Text.Encodings.Web.HtmlEncoder` / `JavaScriptEncoder` / `UrlEncoder` | Anti-XSS |
| `System.Globalization.IdnMapping` | IDN domain names |
| `System.Buffers.Text.*` (Base64Url, Utf8Parser) | Low-level utf8 |
| `string.Create<TState>` | Allocation-free fluent |
| `System.Text.Unicode.UnicodeRanges` | Range constants |

### 8.12 Formats

| API | Niche |
|---|---|
| `System.Formats.Asn1.AsnReader` / `AsnWriter` | ASN.1 |
| `System.Formats.Cbor.CborReader` / `CborWriter` | CBOR |
| `System.Formats.Tar.TarFile` / `TarReader` / `TarWriter` | TAR archives |
| `System.Formats.Nrbf.NrbfDecoder` (.NET 9) | NRBF (legacy BinaryFormatter replacement) |
| `System.Xml.*` / `System.Xml.Linq.*` | XML / LINQ-to-XML |
| `System.Xml.XPath.*` | XPath |
| `System.Xml.Schema.*` | XSD |

### 8.13 Hosting / lifecycle

| API | Niche |
|---|---|
| `Microsoft.Extensions.Hosting.HostBuilder` / `Host.CreateApplicationBuilder` | Generic host |
| `Microsoft.Extensions.Hosting.IHostedService` / `BackgroundService` | Long-running |
| `Microsoft.Extensions.Hosting.IHostApplicationLifetime` | Stop, started, stopping events |
| `Microsoft.AspNetCore.Builder.WebApplication` | Minimal host (.NET 6+) |
| `Microsoft.Extensions.Hosting.Systemd` / `Microsoft.Extensions.Hosting.WindowsServices` | OS service host |
| `Microsoft.AspNetCore.Hosting.WebApplicationBuilder.Services` | DI registration |

### 8.14 Configuration

| API | Niche |
|---|---|
| `Microsoft.Extensions.Configuration.IConfiguration` / `IConfigurationRoot` / `IConfigurationSection` | Read tree |
| Source providers: `Microsoft.Extensions.Configuration.{Json,Xml,Ini,EnvironmentVariables,CommandLine,UserSecrets,KeyPerFile,Memory,Binder}` | Sources |
| `Microsoft.Extensions.Options.IOptions<T>` / `IOptionsMonitor<T>` / `IOptionsSnapshot<T>` | Options |
| `Microsoft.Extensions.Configuration.Binder.SourceGeneration` (.NET 8+) | AOT-safe binding |
| Cloud providers: `Microsoft.Extensions.Configuration.AzureAppConfiguration`, `Aspire.Azure.Security.KeyVault` etc. | Cloud config |

### 8.15 Logging

| API | Niche |
|---|---|
| `Microsoft.Extensions.Logging.ILogger<T>` | Log entry |
| `Microsoft.Extensions.Logging.LoggerMessage.Define` / `[LoggerMessage]` | Source-gen high-perf log |
| `ILogger.BeginScope` | Scoped enrichment |
| `Microsoft.Extensions.Logging.Console`/`.Debug`/`.EventLog`/`.EventSource` | Built-in providers |
| `Microsoft.Extensions.Compliance.{Classification,Redaction}` (.NET 8+) | PII redaction |

### 8.16 DI

| API | Niche |
|---|---|
| `Microsoft.Extensions.DependencyInjection.IServiceCollection` / `IServiceProvider` | Container |
| Built-in registration helpers: `AddSingleton`, `AddScoped`, `AddTransient`, `AddKeyedSingleton`, etc. | Registrations |
| `IServiceProviderFactory<T>` | Container swap (Autofac, Lamar) |

### 8.17 ASP.NET Core

| API | Niche |
|---|---|
| `Microsoft.AspNetCore.Builder.WebApplication` | Minimal host |
| `Microsoft.AspNetCore.Mvc.*` | Controllers, model binding, formatters |
| `Microsoft.AspNetCore.Authorization.*` / `AspNetCore.Authentication.*` | Authn / Authz |
| `Microsoft.AspNetCore.Antiforgery` / `Cors` / `DataProtection` / `OutputCaching` / `RateLimiting` / `RequestDecompression` / `ResponseCompression` / `ResponseCaching` / `Routing` / `Cookies` / `Session` / `StaticFiles` / `WebSockets` / `HostFiltering` / `HttpsPolicy` / `Hsts` | Middleware grab-bag |
| `Microsoft.AspNetCore.SignalR` | Real-time hubs |
| `Microsoft.AspNetCore.OpenApi` (.NET 9+) | OpenAPI built-in |
| `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<T>` | In-process integration tests |
| `Microsoft.AspNetCore.Components` | Blazor |
| `Microsoft.AspNetCore.Identity` | User store + endpoints |
| `Microsoft.AspNetCore.JsonPatch` | RFC 6902 |
| `Microsoft.AspNetCore.Razor.*` | Razor engine |
| `Microsoft.AspNetCore.Hosting.WindowsServices` / `Systemd` | Host modes |
| `Microsoft.AspNetCore.Diagnostics.HealthChecks` | Health checks |
| Endpoint filter / Middleware abstraction | `IEndpointFilter`, `IMiddleware`, `RequestDelegate` |

### 8.18 EF Core

| API | Niche |
|---|---|
| `Microsoft.EntityFrameworkCore.DbContext` / `DbSet<T>` | Aggregate root |
| `Microsoft.EntityFrameworkCore.Metadata.*` | Model metadata |
| `Microsoft.EntityFrameworkCore.Diagnostics.*` (interceptors) | Hooks |
| `Microsoft.EntityFrameworkCore.SqlServer` / `Npgsql.EntityFrameworkCore.PostgreSQL` / `Pomelo.EntityFrameworkCore.MySql` / `Microsoft.EntityFrameworkCore.Sqlite` / `Microsoft.EntityFrameworkCore.Cosmos` / `Microsoft.EntityFrameworkCore.InMemory` | Providers |
| `EFCore.NamingConventions` | Snake-case et al. |
| `EFCore.BulkExtensions` | Bulk |
| `EntityFrameworkCore.Triggered` | Triggers |
| `EFCore.Projectables` | Computed properties as expressions |

### 8.19 Microsoft.Extensions.AI (preview / .NET 9)

| API | Niche |
|---|---|
| `Microsoft.Extensions.AI.IChatClient` | Chat-completion abstraction |
| `Microsoft.Extensions.AI.IEmbeddingGenerator<TInput,TEmbedding>` | Embedding abstraction |
| `Microsoft.Extensions.AI.OpenAI` / `.AzureAIInference` / `.Ollama` | Provider impls |
| `Microsoft.Extensions.AI.Evaluation` | Eval framework |
| `Microsoft.SemanticKernel.*` | LLM orchestration framework |
| `Microsoft.KernelMemory.*` | RAG pipeline |

### 8.20 Microsoft.Extensions.* misc

| API | Niche |
|---|---|
| `Microsoft.Extensions.FileProviders.*` | File abstractions |
| `Microsoft.Extensions.Caching.{Memory,Distributed,Hybrid,StackExchangeRedis}` | Caching |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | Health |
| `Microsoft.Extensions.Resilience` / `Microsoft.Extensions.Http.Resilience` | Resilience |
| `Microsoft.Extensions.ObjectPool` | Pooling |
| `Microsoft.Extensions.Identity.*` | Identity primitives |
| `Microsoft.Extensions.Localization.*` | i18n |
| `Microsoft.Extensions.Compliance.{Classification,Redaction}` | PII |
| `Microsoft.Extensions.ServiceDiscovery` | Aspire service discovery |
| `Microsoft.Extensions.TimeProvider.Testing` | Fake time |
| `Microsoft.Bcl.AsyncInterfaces` / `TimeProvider` | netstandard polyfills |

---

## 9. Tooling ecosystem

### 9.1 Build / pack / publish

| Tool | Niche |
|---|---|
| `dotnet` CLI | Core build, run, test, pack, publish |
| MSBuild | Underlying engine; custom targets |
| Cake (`Cake.Frosting`) | Build orchestration in C# |
| Nuke (`Nuke.Common`) | Build orchestration with type-safe pipelines |
| FAKE (F#) | F#-based build |
| NuGet | Package store + signing + symbols |
| Source Link | Map symbols to GitHub |
| Deterministic Builds | Reproducible binaries |
| Native AOT publish | `dotnet publish -c Release -p:PublishAot=true` |
| Trim | Linker; warning analysis |
| ReadyToRun | Pre-jitted images |
| `dotnet pack` | Build NuGet pkg |
| `dotnet nuget push` | Publish |
| Symbol packages | snupkg |
| MinVer | Semver from tags |
| GitVersion | Version from git history |

### 9.2 Testing tooling

(See §4.25 for libs.) Core runners: `dotnet test`, xUnit, NUnit, MSTest, TUnit. Coverage: Coverlet + ReportGenerator. Mutation: Stryker.NET. Snapshot: Verify. Property: FsCheck/CsCheck.

### 9.3 Local dev / orchestration

| Tool | Niche |
|---|---|
| **.NET Aspire** | App composition, orchestration, dashboard, integrations |
| **Docker Compose** | Local container compose |
| **k3d / kind / minikube** | Local k8s |
| **Tilt / Skaffold** | Inner loop dev |
| **Tye** (legacy MS) | Replaced by Aspire |
| **Project Tye** (archived) | Microservice dev |
| **dotnet user-secrets** | Dev secrets |
| **dotnet dev-certs** | HTTPS dev cert |
| **Bombardier / k6 / wrk / hey** | Load test (external) |

### 9.4 CI/CD

| Provider | Niche |
|---|---|
| GitHub Actions | Default; .NET-friendly via `actions/setup-dotnet` |
| Azure DevOps | First-class .NET |
| GitLab CI | Self-host friendly |
| Jenkins | Legacy / on-prem |
| TeamCity | JetBrains commercial |
| CircleCI / Buildkite / Drone | SaaS CI |
| Octopus Deploy | Release orchestration (.NET-friendly) |

### 9.5 Container / runtime

| Tool | Niche |
|---|---|
| Docker | de-facto |
| Podman / Buildah | rootless container |
| chiseled images | Microsoft 8.0-jammy-chiseled — small + secure |
| distroless | Smaller surface |
| Aspire publishing | Generates manifest for Compose / k8s / Bicep |
| Kubernetes | k8s manifests, helm, kustomize |
| Helm | Pkg manager for k8s |
| Kustomize | Templating-free overlay |
| KubeOps (.NET) | Build operators in C# |
| ArgoCD / Flux | GitOps |

### 9.6 IaC

| Tool | Niche |
|---|---|
| Terraform | Multi-cloud |
| Pulumi (.NET) | IaC in C# |
| Bicep | Azure-native ARM |
| ARM templates | Azure JSON |
| AWS CDK (.NET) | IaC in C# for AWS |
| Crossplane | k8s-native cloud provisioning |

### 9.7 Documentation tooling

| Tool | Niche |
|---|---|
| DocFX | .NET docs site generator |
| Statiq | Modern static gen in .NET |
| Wyam | DocFX-era tool (legacy) |
| Sandcastle | Legacy XML→HTML |
| Microsoft.AspNetCore.OpenApi + Swashbuckle / NSwag | Built-in OpenAPI |
| AsyncAPI generators (Saunter) | Event-driven API docs |

---

## 10. Future-scope notes (sketch)

Not enumerated in depth yet — flag for later expansion.

| Area | Hooks |
|---|---|
| Client SDK generation | Refit-style + Kiota + NSwag generation patterns; TypeScript / Python / Go targets |
| Native AOT branch | trim-friendly subset of the lib; source-gen-only mappers, validators, JSON |
| Source-only deliverables | Companion `wow-two-sdk.backend.beta.source` or `.templates` |
| F# friendliness | Computation expressions, options, async workflows |
| WASM backend | Wasi.NET, Spin, Fermyon Spin .NET — explore as deployment target |
| Mobile / Maui backend integration | n/a yet — backend-side only |
| Game / sim | Orleans + custom pipelines (LATER) |
| Frontend integration | TypeScript client gen via Kiota + OpenAPI (LATER) |
| Schema first | Protobuf / GraphQL SDL / OpenAPI as source-of-truth flow |
| Edge runtime | Cloudflare Workers (.NET), Vercel functions, Lambda@Edge |
| Federation / GraphQL Hive | Federated GraphQL gateway |
| Service mesh | Istio / Linkerd config awareness |
| WebAssembly plugins | Extism .NET — hot-loadable lib plugins |

---

## §A. Survey deep-dive — MS-built / official inventory

> Detailed enumeration. See §4 / §5 for cross-cutting / per-domain summaries.

Status legend: `mature` = stable, recommended; `preview` = .NET 9/10 preview or marked preview attribute; `legacy` = still supported but superseded; `archive` = source-available, no active development; `community` = not Microsoft-built but stewarded / ubiquitous.

Bundle cost legend: `tiny` (< 50 KB), `small` (50–250 KB), `medium` (250 KB–1 MB), `large` (> 1 MB), `framework` (in-box, no extra), `transitive` (always pulled by ASP.NET Core).

### A.1 ASP.NET Core — hosting & app model

| Lib/Namespace | Niche | Status | Bundle/dep |
|---|---|---|---|
| `Microsoft.AspNetCore.App` (shared framework) | Umbrella metapackage installed with the ASP.NET Core runtime | mature | framework |
| `Microsoft.AspNetCore` | Top-level WebHost / WebApplication entry types | mature | framework |
| `Microsoft.AspNetCore.Hosting` | `IHostingEnvironment`, `IWebHostBuilder`, lifecycle | mature | framework |
| `Microsoft.AspNetCore.Builder` | `WebApplicationBuilder`, `WebApplication`, `IApplicationBuilder` | mature | framework |
| `Microsoft.AspNetCore.Hosting.WindowsServices` | Run as Windows Service | mature | tiny |
| `Microsoft.Extensions.Hosting.Systemd` | Run as systemd unit on Linux | mature | tiny |
| `Microsoft.AspNetCore.Server.Kestrel` | Cross-platform default HTTP server (HTTP/1, /2, /3 via QUIC) | mature | framework |
| `Microsoft.AspNetCore.Server.Kestrel.Transport.Quic` | HTTP/3 transport over MsQuic | mature | framework |
| `Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes` | Named-pipes transport for Windows IPC | mature | framework |
| `Microsoft.AspNetCore.Server.IIS` | Out-of-process IIS module | mature | framework |
| `Microsoft.AspNetCore.Server.IISIntegration` | In-process / out-of-process IIS integration | mature | framework |
| `Microsoft.AspNetCore.Server.HttpSys` | Windows HTTP.sys server (Kerberos, port sharing) | mature | framework |

### A.2 Routing, endpoints, HTTP plumbing

| Lib/Namespace | Niche | Status | Bundle |
|---|---|---|---|
| `Microsoft.AspNetCore.Routing` | Endpoint routing core | mature | framework |
| `Microsoft.AspNetCore.Http` | `HttpContext`, results, headers | mature | framework |
| `Microsoft.AspNetCore.Http.Extensions` | Helpers (`GetEncodedUrl`, `WriteAsJsonAsync`); Minimal API surface (`MapGet/Post/...`) | mature | framework |
| `Microsoft.AspNetCore.Http.Features` | Feature collection (`IHttpRequestFeature`, etc.) | mature | framework |
| `Microsoft.AspNetCore.Http.Connections` | Long-running connections (used by SignalR) | mature | framework |
| `Microsoft.AspNetCore.Http.Results` | `Results.Ok()`, `TypedResults.*` builders | mature | framework |
| `Microsoft.AspNetCore.HttpOverrides` | `ForwardedHeaders` middleware | mature | framework |
| `Microsoft.AspNetCore.HostFiltering` | Host header allow-list middleware | mature | framework |

### A.3 Controllers / MVC / Razor / Blazor

| Lib/Namespace | Niche | Status |
|---|---|---|
| `Microsoft.AspNetCore.Mvc` / `.Mvc.Core` / `.Mvc.Abstractions` | Full MVC stack | mature |
| `Microsoft.AspNetCore.Mvc.ApiExplorer` | Endpoint metadata for OpenAPI generation | mature |
| `Microsoft.AspNetCore.Mvc.NewtonsoftJson` | Newtonsoft serializer for MVC | legacy |
| `Microsoft.AspNetCore.Mvc.Formatters.Xml` | XML in/out formatters | mature |
| `Microsoft.AspNetCore.Mvc.Razor` / `.Mvc.RazorPages` | Razor / Razor Pages | mature |
| `Microsoft.AspNetCore.Mvc.ViewFeatures` / `.TagHelpers` / `.DataAnnotations` / `.Cors` | Misc MVC infra | mature |
| `Asp.Versioning.Mvc` / `.Http` / `.Mvc.ApiExplorer` | API versioning (Microsoft-stewarded) | mature |
| `Microsoft.AspNetCore.Components` (+`.Web`, `.Forms`, `.Routing`, `.Server`, `.WebAssembly`, `.Authentication`, `.QuickGrid`, `.Endpoints`, `.WebView.{Maui,Wpf,WindowsForms}`) | Blazor stack — Server, WASM, Hybrid, Endpoints (United render modes) | mature |

### A.4 Real-time & RPC

| Lib/Namespace | Niche | Status |
|---|---|---|
| `Microsoft.AspNetCore.SignalR` (+`.Core`, `.Common`, `.Protocols.{Json,MessagePack,NewtonsoftJson}`, `.StackExchangeRedis`, `.Client`, `.Specification.Tests`) | Real-time hub server + clients + backplanes | mature |
| `Microsoft.Azure.SignalR` / `Microsoft.Azure.SignalR.Management` | Azure SignalR Service backplane + serverless REST | mature |
| `Grpc.AspNetCore` / `.Server` / `.Web` / `.Server.ClientFactory` / `.HealthChecks` / `.Server.Reflection` | gRPC server + reflection + health + gRPC-Web | mature |
| `Grpc.Net.Client` / `.ClientFactory` / `Grpc.Tools` / `Google.Protobuf` | .NET gRPC client + tooling + Protobuf runtime | mature |
| `Microsoft.AspNetCore.Grpc.JsonTranscoding` / `.Grpc.Swagger` | gRPC ↔ HTTP/JSON transcoding + OpenAPI for transcoded gRPC | mature |

### A.5 OData / OpenAPI

| Lib/Namespace | Niche | Status |
|---|---|---|
| `Microsoft.AspNetCore.OData` | OData v4 endpoints, $filter/$select/$expand | mature |
| `Microsoft.OData.Core` / `.Edm` / `.Client` / `Microsoft.Spatial` | OData protocol + EDM model + spatial | mature |
| `Microsoft.AspNetCore.OpenApi` | First-party OpenAPI generation (.NET 9 GA) | mature |
| `Microsoft.Extensions.ApiDescription.Server` / `.Client` | Build-time OpenAPI doc + client gen tooling | mature |
| `Swashbuckle.AspNetCore` / `NSwag.AspNetCore` | Community OpenAPI gen + Swagger UI / TS+C# client codegen | community |

### A.6 Authentication & authorization

Schemes: Cookies, JwtBearer, OAuth, OpenIdConnect, WsFederation, Negotiate (Kerberos/NTLM), Certificate (mTLS), BearerToken (Identity API endpoints, .NET 8+), Google/MicrosoftAccount/Facebook/Twitter (legacy).

| Lib/Namespace | Niche | Status |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.*` | All scheme handlers | mature |
| `Microsoft.AspNetCore.Authorization` / `.Policy` | Policy-based authorization | mature |
| `Microsoft.AspNetCore.Identity` (+`.UI`, `.EntityFrameworkCore`, `.Specification.Tests`) | ASP.NET Core Identity (cookies + scaffolding + EF stores) | mature |
| `Microsoft.Identity.Web` (+`.UI`, `.MicrosoftGraph`, `.DownstreamApi`, `.TokenAcquisition`) | OIDC / Microsoft Entra integration | mature |
| `Microsoft.Identity.Client` (MSAL) (+`.Extensions.Msal`) | Token broker + cross-platform secure cache | mature |
| `Microsoft.IdentityModel.Tokens` / `.JsonWebTokens` / `.Protocols.{OpenIdConnect,WsFederation}` / `.Logging` / `.Validators` / `.LoggingExtensions` / `.Tokens.Saml` / `.Xml` / `.Abstractions` | JWT / SAML / OIDC primitives | mature |
| `System.IdentityModel.Tokens.Jwt` | Legacy JWT handler | legacy |
| `Duende.IdentityServer` / `OpenIddict` | Community OIDC servers | community |
| `Microsoft.AspNetCore.Cryptography.KeyDerivation` | PBKDF2 helpers | mature |

### A.7 DataProtection / Antiforgery / encoders

| Lib | Niche | Status |
|---|---|---|
| `Microsoft.AspNetCore.DataProtection` (+`.Abstractions`, `.Extensions`, `.AzureKeyVault`, `.AzureStorage`, `.StackExchangeRedis`, `.EntityFrameworkCore`) | Key management for cookies/tokens; persistence + KV-wrap | mature |
| `Microsoft.AspNetCore.Antiforgery` | CSRF tokens for forms + minimal API | mature |
| `System.Text.Encodings.Web` | HTML/JavaScript/URL encoders against XSS | mature |

### A.8 Middleware / cross-cutting

`Microsoft.AspNetCore.{HttpsPolicy, Cors, ResponseCaching, OutputCaching, OutputCaching.StackExchangeRedis, ResponseCompression, RequestDecompression, RateLimiting, ConcurrencyLimiter, RequestTimeouts, HttpLogging, MiddlewareAnalysis, Diagnostics, Diagnostics.HealthChecks, Session, Localization, Localization.Routing, SpaServices.Extensions, SpaProxy, StaticFiles, WebSockets, HostFiltering, HeaderPropagation, JsonPatch, JsonPatch.SystemTextJson, WebUtilities, Authentication.BearerToken, SystemWebAdapters, SystemWebAdapters.SessionState, SystemWebAdapters.CoreServices}`

`Microsoft.Extensions.Diagnostics.HealthChecks` (+`.Abstractions`, `.EntityFrameworkCore`); community: `AspNetCore.HealthChecks.*` (Xabaril — 70+ provider checks).

### A.9 EF Core core + abstractions

`Microsoft.EntityFrameworkCore` (+`.Abstractions`, `.Relational`, `.Design`, `.Tools`, `.Proxies`, `.Analyzers`).

Providers: `Microsoft.EntityFrameworkCore.{SqlServer, SqlServer.NetTopologySuite, SqlServer.HierarchyId, Sqlite, Sqlite.Core, Sqlite.NetTopologySuite, InMemory, Cosmos}` + community `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pomelo.EntityFrameworkCore.MySql`, `MySql.EntityFrameworkCore` (Oracle), `Oracle.EntityFrameworkCore`, `Devart.Data.*.EFCore`, `IBM.EntityFrameworkCore`, `EntityFrameworkCore.FirebirdSql`.

Feature surface: change tracker · model customizer · migrations · compiled queries · query splitting · interceptors (`IDbCommand`, `ISaveChanges`, `IMaterialization`) · `ExecuteUpdate/DeleteAsync` · `FromSqlInterpolated` / `SqlQuery` · JSON columns · Owned types · TPH/TPT/TPC · Keyless via `HasNoKey().ToView/Function/SqlQuery` · `EF.Functions` · `AsAsyncEnumerable` · `EnableRetryOnFailure` · `EF.Property<T>` shadow props · `IDesignTimeDbContextFactory<T>`. EF6 legacy (`EntityFramework`, `EntityFramework.SqlServer`, `EntityFramework6.Npgsql`, `EntityFramework.SqlServerCompact`).

### A.10 Microsoft.Extensions.* — host / DI / config / options

`Microsoft.Extensions.Hosting` (+`.Abstractions`, `.WindowsServices`, `.Systemd`); `Microsoft.Extensions.DependencyInjection` (+`.Abstractions`, `.Specification.Tests`); `Microsoft.Extensions.Configuration` (+`.Abstractions`, `.Binder`, `.{CommandLine, EnvironmentVariables, FileExtensions, Json, Xml, Ini, UserSecrets, KeyPerFile, AzureAppConfiguration, AzureKeyVault}`); `Microsoft.Extensions.Options` (+`.ConfigurationExtensions`, `.DataAnnotations`); `Microsoft.Extensions.Primitives`; `Microsoft.Extensions.FileProviders.{Abstractions,Composite,Embedded,Physical}`; `Microsoft.Extensions.FileSystemGlobbing`; `Microsoft.Extensions.ObjectPool`; `Azure.Extensions.AspNetCore.Configuration.Secrets` (modern Key Vault provider).

### A.11 Logging

`Microsoft.Extensions.Logging` (+`.Abstractions`, `.Configuration`, `.Console`, `.Debug`, `.EventLog`, `.EventSource`, `.TraceSource`, `.AzureAppServices`); `Microsoft.Extensions.Telemetry` (+`.Abstractions`); `Microsoft.Extensions.Compliance.{Redaction,Classification}`; `Microsoft.Extensions.AmbientMetadata.Application`. Source-gen: `[LoggerMessage]`. Community: Serilog, NLog, log4net, ZLogger.

### A.12 Caching / Resilience / HTTP

`Microsoft.Extensions.Caching.{Memory, Abstractions, SqlServer, StackExchangeRedis, Cosmos, Hybrid, Hybrid.Internal}`; `Microsoft.Extensions.Http` (+`.Polly` legacy, `.Resilience`, `.Diagnostics`, `.Telemetry`, `.Logging.Abstractions`); `Microsoft.Extensions.Resilience`; `Polly` v8 + `.Extensions`, `.Caching.Memory`; `Microsoft.Extensions.AsyncState`.

### A.13 Diagnostics / metrics / health

`Microsoft.Extensions.Diagnostics` (+`.Abstractions` w/ `IMeterFactory`); `Microsoft.Extensions.Diagnostics.HealthChecks` (+`.Abstractions`); `Microsoft.Extensions.Diagnostics.{ResourceMonitoring, Probes, Testing}`.

### A.14 Identity / localization / AI extensions

`Microsoft.Extensions.Identity.{Core, Stores}`; `Microsoft.Extensions.Localization` (+`.Abstractions`); `Microsoft.Extensions.AI` (+`.Abstractions`); `Microsoft.Extensions.VectorData.Abstractions` (preview); `Microsoft.Extensions.AI.{OpenAI, AzureAIInference, Ollama}` (preview).

### A.15 System.* runtime — serialization / text

`System.Text.Json` (+`.Serialization`, `.Nodes`, `.Schema` w/ `JsonSchemaExporter`); `System.Text.RegularExpressions` w/ `[GeneratedRegex]`; `System.Text.Unicode`; `System.Text.Encoding.CodePages`; `System.Text.Encodings.Web`; `System.Xml` / `.Linq` / `.XPath`; `System.Xml.XmlSerializer`; `System.Runtime.Serialization` (+`.Json`); `System.Formats.{Asn1, Cbor, Tar, Nrbf}`.

### A.16 System.* runtime — concurrency / async / IO

`System.Threading.Tasks` (+`.Parallel`, `.Extensions` for `ValueTask`/`IAsyncDisposable`); `System.Threading.Channels`; `System.Threading.Tasks.Dataflow`; `System.Threading.RateLimiting`; `System.Threading.{AccessControl, ThreadPool, Lock (.NET 9), Interlocked}`; `System.Diagnostics.AsyncLocal<T>`; `System.IO.Pipelines`; `System.IO.{Hashing, Compression, Compression.Brotli, Compression.ZipFile, Compression.ZLib, Compression.Zstd (.NET 10 preview), MemoryMappedFiles, Ports}`; `System.Buffers` (+`.Binary`, `.Text`); `System.Memory`; `Microsoft.Bcl.{AsyncInterfaces, HashCode, Numerics, TimeProvider, Memory, Cryptography}`.

### A.17 System.* — reactive / LINQ / reflection / numerics

`System.Linq` (+`.Async`, `.Expressions`, `.Queryable`, `.Parallel`); `System.Interactive` (+`.Async`); `System.Reactive` (+`.Linq`, `.Core`, `.PlatformServices`); `Microsoft.CodeAnalysis.CSharp.Scripting`; `System.Reflection` (+`.Emit`, `.Metadata`, `.MetadataLoadContext`, `.DispatchProxy`); `System.ComponentModel` (+`.Annotations`, `.DataAnnotations`); `System.Dynamic`; `Microsoft.CSharp`; `System.Numerics` (+`.Vectors`, `.Tensors`); `System.Runtime.Intrinsics.*` (Sse, Avx, Avx2, Avx512, AdvSimd, Wasm Packed); `System.Half`.

### A.18 System.* — networking / crypto / globalization

`System.Net.Http` (+`.Json`, `.Headers`); `System.Net.{Sockets, Security, Quic, WebSockets, WebSockets.Client, Mail (legacy), NetworkInformation, NameResolution, ServerSentEvents (.NET 10 preview), ClientModel}`; `System.Diagnostics` (+`.DiagnosticSource` w/ `Activity`, `.Metrics` w/ `Meter`, `.PerformanceCounter (legacy)`, `.Tracing` w/ `EventSource`/`EventCounter`/`EventListener`, `.Debug`, `.Process`, `.TraceSource`, `.StackTrace`, `.FileVersionInfo`); `System.Security.Cryptography` (+`.Algorithms`, `.Cng`, `.X509Certificates`, `.OpenSsl`, `.Pkcs`, `.Xml`, `.ProtectedData`, `.Cose`, `.AesGcm`, `.MLKem` / `.MLDsa` / `.SlhDsa` (.NET 10 preview)); `System.Security.{AccessControl, Permissions (legacy), Principal}`; `System.IdentityModel (legacy)`; `System.DirectoryServices` (+`.AccountManagement`, `.Protocols`); `System.Globalization` (+`.ICU`); `System.Resources` (+`.Extensions`).

### A.19 .NET Aspire — orchestration & integrations

`Aspire.AppHost.Sdk`; `Aspire.Hosting` (+`.AppHost`, `.Testing`); `Aspire.Dashboard`; `Aspire.ServiceDefaults` template; `Microsoft.Extensions.ServiceDiscovery` (+`.Yarp`, `.Dns`).

Hosting integrations: `Aspire.Hosting.{Redis, Garnet, Valkey, PostgreSQL, MySql, SqlServer, Oracle, MongoDB, RabbitMQ, Kafka, NATS, Milvus, Qdrant, Elasticsearch, Seq, Grafana, Prometheus, Keycloak, Ollama, NodeJs, Python, Docker, Yarp, Azure.{AppContainers, AppService, AppConfiguration, ApplicationInsights, CosmosDB, EventHubs, Functions, KeyVault, OpenAI, Search, ServiceBus, SignalR, Sql, Storage, WebPubSub}}`.

Client integrations: `Aspire.{StackExchange.Redis, StackExchange.Redis.OutputCaching, StackExchange.Redis.DistributedCaching, Microsoft.EntityFrameworkCore.SqlServer, Npgsql.EntityFrameworkCore.PostgreSQL, Pomelo.EntityFrameworkCore.MySql, Microsoft.Data.SqlClient, Npgsql, MongoDB.Driver, Confluent.Kafka, RabbitMQ.Client, NATS.Net, Seq, OpenAI, Azure.AI.OpenAI, Azure.Search.Documents, Azure.Storage.Blobs, Azure.Data.Tables, Azure.Messaging.{ServiceBus, EventHubs, WebPubSub}, Azure.Security.KeyVault, Microsoft.Azure.Cosmos, Elastic.Clients.Elasticsearch, Milvus.Client, Qdrant.Client, OllamaSharp}`.

### A.20 OpenTelemetry .NET

`OpenTelemetry` (SDK core); `OpenTelemetry.Api`; `OpenTelemetry.Extensions.Hosting`. Auto-instrumentation: `OpenTelemetry.Instrumentation.{AspNetCore, Http, GrpcNetClient, SqlClient, EntityFrameworkCore, StackExchangeRedis, Runtime, Process, EventCounters}`. Exporters: `OpenTelemetry.Exporter.{Console, OpenTelemetryProtocol (OTLP), Prometheus.AspNetCore, Zipkin, Jaeger (legacy)}`; `Azure.Monitor.OpenTelemetry.{Exporter, AspNetCore}`.

### A.21 Azure SDKs (track 2)

Core/Identity: `Azure.Core` (+`.Amqp`); `Azure.Identity` (+`.Broker`); `Azure.Extensions.AspNetCore.{Configuration.Secrets, DataProtection.Keys, DataProtection.Blobs}`.

Storage/Data: `Azure.Storage.{Blobs, Blobs.ChangeFeed, DataMovement, DataMovement.Blobs, Queues, Files.DataLake, Files.Shares}`; `Azure.Data.Tables`; `Microsoft.Azure.Cosmos` (+`.Encryption`); `Azure.Search.Documents`.

Messaging/Events: `Azure.Messaging.{ServiceBus, ServiceBus.Administration, EventHubs, EventHubs.Processor, EventGrid, EventGrid.Namespaces, WebPubSub, WebPubSub.Client}`; `Azure.Communication.{Email, Sms, Identity, Chat, Calling.WindowsClient, PhoneNumbers, JobRouter, Rooms}`.

Security/KV: `Azure.Security.KeyVault.{Secrets, Keys, Certificates, Administration}`; `Azure.Security.{Attestation, ConfidentialLedger}`.

AI/ML: `Azure.AI.{OpenAI, Inference, Translation.Text, Translation.Document, TextAnalytics, Language.{Conversations, QuestionAnswering, Text}, Vision.{ImageAnalysis, Face}, FormRecognizer (legacy), DocumentIntelligence, ContentSafety, Personalizer (retiring 2026), MetricsAdvisor (legacy), AnomalyDetector (legacy), Projects (preview), Agents.Persistent (preview)}`; `OpenAI` (official base); `Microsoft.SemanticKernel` (+`.Connectors.{OpenAI, AzureOpenAI, MistralAI, Google, Onnx, HuggingFace, AzureAISearch, Qdrant, Postgres, Redis, Sqlite, MongoDB}`, `.Plugins.*`, `.Memory`, `.Agents.Core`, `.Process.Core` preview); `Microsoft.ML` (+`.AutoML`, `.OnnxRuntime`, `.OnnxRuntime.{Gpu, DirectML}`, `.OnnxRuntimeGenAI`, `.Tokenizers`, `.Trainers.LightGbm`, `.TimeSeries`, `.Vision`); `TorchSharp`.

Other: `Azure.IoT.{DeviceUpdate, Hub.Service, Operations.* (preview)}`; `Microsoft.Azure.Devices` (+`.Client`, `.Provisioning.Client`); `Microsoft.Azure.Functions.Worker` (+`.Sdk`, `.Extensions.{Http, Timer, ServiceBus, Storage, EventHubs, EventGrid, Cosmos, SignalR, Tables, Kafka, Dapr, Sql}`); `Microsoft.Azure.WebJobs.*` (legacy); `Azure.Maps.*`; `Microsoft.Graph` (+`.Beta`, `.Core`); `Microsoft.PowerBI.Api`; `Microsoft.Extensions.Azure` (DI extensions for Azure clients).

### A.22 DB / data / messaging clients (Microsoft + ubiquitous)

Microsoft.Data.SqlClient (+`.SNI.runtime`); `Microsoft.SqlServer.Server`; `Microsoft.Data.Sqlite` (+`.Core`) + `SQLitePCLRaw.bundle_e_sqlite3`; `System.Data.{SqlClient (legacy), OleDb, Odbc, Common}`; `Microsoft.Azure.Cosmos` (+`.Table` legacy); `Microsoft.SqlServer.{TransactSql.ScriptDom, SqlManagementObjects, DacFx, Types}`. Ubiquitous community: `Dapper` (+`.SqlBuilder`, `.Contrib`); `Npgsql`; `MySql.Data`/`MySqlConnector`; `Cassandra.Datastax`; `MongoDB.Driver`; `StackExchange.Redis`; `Garnet` (Microsoft Redis-compatible server); `LinqToDB`; `RepoDB`.

### A.23 Orleans & messaging

`Microsoft.Orleans.{Core, Core.Abstractions, Server, Client, Sdk, Streaming, Streaming.{EventHubs, SQS}, Reminders, Reminders.{AzureStorage, AdoNet}, Persistence.{AzureStorage, AdoNet, Cosmos, DynamoDB, Redis}, Clustering.{AzureStorage, Consul, Kubernetes, Redis, AdoNet}, Hosting.{AzureAppServices, Kubernetes}, Transactions, Serialization, Serialization.SystemTextJson, OrleansCodeGenerator}`.

Dapr: `Dapr.{Client, AspNetCore, Actors, Workflow, Extensions.Configuration}`.

gRPC alts: `Grpc.Core.Api` / `.Core (legacy C-core)`; `Google.Protobuf` (+`.Tools`); `Grpc.Tools`; `protobuf-net` (+`.Grpc`, `.Grpc.AspNetCore`); `MessagePack` (+`.AspNetCoreMvcFormatter`).

Brokers: `RabbitMQ.Client`; `Confluent.Kafka`; `NATS.Client.Core`; `MassTransit` (+`.{RabbitMQ, AzureServiceBus, Kafka, AmazonSqs}`); `NServiceBus`; `Wolverine`; `MQTTnet`; `Microsoft.Azure.Devices.Client.Mqtt`. YARP: `Yarp.{ReverseProxy, ReverseProxy.Tunnels, Telemetry.Consumption, Kubernetes.Controller}`.

### A.24 Testing official + standard

`Microsoft.NET.Test.Sdk`; `Microsoft.Testing.Platform` (+`.MSBuild`, `.Extensions.{CodeCoverage, Retry, Telemetry}`); `MSTest.{TestFramework, TestAdapter, Sdk}`; `xunit`/`xunit.v3`/`xunit.runner.visualstudio`; `NUnit`/`NUnit3TestAdapter`. Web/EF/SignalR/gRPC: `Microsoft.AspNetCore.Mvc.Testing`; `Microsoft.AspNetCore.TestHost`; `Microsoft.AspNetCore.SignalR.Specification.Tests`/`.Client.Core`; `Grpc.AspNetCore.Server.Tests.Common`; `Microsoft.EntityFrameworkCore.{InMemory, Sqlite, Relational.Specification.Tests}`. Fakes: `Microsoft.Extensions.{TimeProvider.Testing, Logging.Testing, Diagnostics.Testing, Caching.Hybrid.Testing, DependencyInjection.Specification.Tests}`. Standard community: `FluentAssertions`, `Shouldly`, `NSubstitute`, `Moq`, `AutoFixture`, `Bogus`, `Verify` (+integrations), `Testcontainers` (+`.PostgreSql`, `.Redis`, `.Kafka`, `.RabbitMQ`, `.Azurite`, `.MsSql`, `.MongoDb`, `.Elasticsearch`), `WireMock.Net`, `BenchmarkDotNet` (+`.Diagnostics.Windows`), `NBomber`, `Stryker.NET`, `JustMock`/`TypeMock` (commercial), `coverlet.collector`/`.msbuild`, `ReportGenerator`.

### A.25 Tooling / pack / generators

CLI: `dotnet`; SDKs: `Microsoft.NET.Sdk` (+`.Web`, `.Worker`, `.BlazorWebAssembly`, `.WindowsDesktop`, `.Razor`, `.Functions` legacy); `Microsoft.NET.ILLink.Tasks`; `Microsoft.DotNet.ILCompiler` (Native AOT); `NuGet.{CommandLine, Build.Tasks.Pack, Protocol, Versioning, Client}`. Roslyn: `Microsoft.CodeAnalysis` (+`.CSharp`, `.CSharp.Workspaces`, `.Analyzers`, `.NetAnalyzers`, `.PublicApiAnalyzers`, `.BannedApiAnalyzers`); `StyleCop.Analyzers`; `SonarAnalyzer.CSharp`; `Roslynator.Analyzers`; `Microsoft.VisualStudio.Threading.Analyzers`; `Meziantou.Analyzer`; `EditorConfig.Core`. OpenAPI: `Microsoft.Extensions.ApiDescription.{Server, Client}`; `NSwag.MSBuild`; `Swashbuckle.AspNetCore.Cli`; `Microsoft.OpenApi` (+`.Readers`, `.OData`); `OpenAPI.NET.Hidi`. Other CLIs: `dotnet-{format, aspnet-codegenerator, ef, watch, user-secrets, user-jwts, dev-certs, trace, counters, dump, gcdump, monitor, symbol, stack, sos}`; `Microsoft.Extensions.SecretManager.Tools`. Source generators: `[LoggerMessage]`, `JsonSerializable`, `[GeneratedRegex]`, `[LibraryImport]` / `[GeneratedComInterface]`, ConfigurationBindingGenerator, `[OptionsValidator]`, `Microsoft.AspNetCore.Http.RequestDelegateGenerator`, Native-AOT type-name SG, `Microsoft.Extensions.Telemetry` SG (`[LogProperties]`, `[TagName]`), `Microsoft.Interop.JavaScript.JSImportGenerator`, `Microsoft.Extensions.AI` function-calling SG (preview).

### A.26 Specialized / less-known MS packages

`Microsoft.FeatureManagement` (+`.AspNetCore`, `.Telemetry.ApplicationInsights`); `Microsoft.AspNetCore.JsonPatch`; `Microsoft.AspNetCore.Cryptography.KeyDerivation`; `Microsoft.AspNetCore.WebUtilities`; `Microsoft.AspNetCore.Authentication.BearerToken`; `Microsoft.AspNetCore.{HostFiltering, MiddlewareAnalysis, HeaderPropagation, Hosting.WindowsServices}`; `Microsoft.Extensions.Hosting.Systemd`; `Microsoft.Web.Xdt`; `Microsoft.PowerShell.SDK`; `Microsoft.Management.Infrastructure` (CIM/WMI); `Microsoft.Win32.{Registry, SystemEvents}`; `Microsoft.Bcl.{AsyncInterfaces, HashCode, TimeProvider, Cryptography, Numerics, Memory}`; `Microsoft.Extensions.{Compliance.Redaction, Compliance.Classification, AmbientMetadata.Application, AsyncState, ContextualOptions (.NET 8), ExtraAnalyzers, LocalAnalyzers, Telemetry.Abstractions}`; `Microsoft.AspNetCore.{HeaderPropagation, OAuth.Claims, SystemWebAdapters, SystemWebAdapters.SessionState, SystemWebAdapters.CoreServices}`; `Microsoft.Extensions.PlatformAbstractions` (legacy); `Microsoft.Recognizers.Text`; `Microsoft.MixedReality.WebRTC` (archive); `Microsoft.Tye` (archive — replaced by Aspire).

### A.27 Windows-only / interop addenda

`Microsoft.Windows.Compatibility`; `Microsoft.Win32.{Registry, SystemEvents}`; `Microsoft.WindowsAPICodePack-*`; `System.ServiceProcess.ServiceController`; `System.IO.Pipes` (+`.AccessControl`); `System.Configuration.ConfigurationManager` (legacy); `System.Drawing.Common` (mature, Win-only post-.NET 6); `System.Diagnostics.PerformanceCounter` (legacy); `System.Management` (WMI); `System.Speech`; `System.Web.Services.Description` (legacy); `System.ServiceModel.*` / `CoreWCF.{Primitives, Http, NetTcp, WebHttp}` (community-stewarded WCF for .NET 8/9).

### A.28 .NET 9 / .NET 10 — additions, removals, deprecations

**.NET 9 (GA Nov 2024):** HybridCache (L1+L2 + stampede + tags); first-party OpenAPI (`Microsoft.AspNetCore.OpenApi`); `JsonSchemaExporter`; `Microsoft.Extensions.AI` abstractions + function calling; `Microsoft.Extensions.VectorData.Abstractions` (preview); Blazor United (`Microsoft.AspNetCore.Components.Endpoints`); JWT-handler enhancements (faster `JsonWebTokens`); HealthCheck publisher improvements; `BackgroundService` failures fatal by default; new `System.Threading.Lock`; STJ polymorphism enhancements; chained rate limiters; LINQ `CountBy/AggregateBy/Index`; SignalR stateful reconnect; OutputCaching tag-eviction stable; rate-limit metric tags; Native AOT now stable for Worker / gRPC / Minimal APIs (still no MVC controllers). Removed/deprecated: `Microsoft.Extensions.Caching.Memory.MemoryCacheOptions.SizeLimit` semantics changed for HybridCache; Azure Personalizer / MetricsAdvisor / AnomalyDetector retired.

**.NET 10 (preview):** First-class `System.Net.ServerSentEvents`; `Microsoft.AspNetCore.OpenApi` → OpenAPI 3.1 + YAML + XML doc parse; `Microsoft.AspNetCore.JsonPatch.SystemTextJson` (no Newtonsoft); Blazor reconnect UX, page-level error boundaries, persistent component state on enhanced nav; `Microsoft.Extensions.Validation` (source-gen, AOT-friendly); FIPS 203/204/205 PQC (`MLKem`, `MLDsa`, `SlhDsa`); `System.IO.Compression.Zstd`; expanded `System.Numerics.Tensors` + `Tensor<T>`; keyed-services improvements (async factories); `Microsoft.Extensions.Configuration` better trim/AOT; Razor Components SSR AOT (preview); `dotnet run app.cs` (file-based apps); more `Microsoft.Extensions.AI` providers GA. Twitter handler effectively unused after API v1.1 sunset; Newtonsoft-bridging packages gradually deprioritized.

### A.29 Suggested aggregation grouping (from MS-research view)

| Bundle | Composition |
|---|---|
| `wow-two-sdk.backend.beta.web` | ASP.NET Core core + Routing + Endpoints + Auth/AuthZ + DataProtection + ProblemDetails + RateLimiting + RequestTimeouts + Response{Compression,Decompression} + OutputCaching + Cors + StaticFiles + HealthChecks + OpenAPI |
| `…api` | Minimal API conventions + endpoint filters + FluentValidation interop + Asp.Versioning + JsonPatch + ProblemDetails enrichers |
| `…realtime` | SignalR Server + Redis backplane + gRPC server + JsonTranscoding + WebPubSub + SSE |
| `…data` | EF Core + relational + Sqlite + SqlServer + Postgres + Cosmos + Dapper + interceptors (audit, soft-delete, multitenancy, outbox) |
| `…cache` | HybridCache + Memory + Redis (StackExchangeRedis) + OutputCaching adapters |
| `…messaging` | MassTransit / Wolverine adapters + Service Bus + RabbitMQ + Kafka + outbox |
| `…identity` | Identity API endpoints + JWT + OIDC + Microsoft.Identity.Web + Duende/OpenIddict adapters |
| `…observability` | OpenTelemetry distro + Aspire ServiceDefaults + Health Checks + log enrichers + dotnet-monitor profile |
| `…cloud.azure` | Azure SDK clients via `Microsoft.Extensions.Azure` + Aspire integrations |
| `…ai` | `Microsoft.Extensions.AI` + Semantic Kernel + ML.NET + ONNX Runtime + Tokenizers |
| `…testing` | WebApplicationFactory harness + Testcontainers fixtures + FakeTimeProvider/FakeLogger + Verify + Bogus + WireMock |
| `…tools` | Source generators, analyzers (CA, VSTHRD, Meziantou, Roslynator), banned/public-API trackers |

---

## §B. Survey deep-dive — Web/API/Comms community inventory

### B.1 HTTP clients / API consumption

The core split: declarative type-safe clients (Refit, RestSharp v110+), fluent builders (Flurl), resilience overlays (Polly + `Microsoft.Extensions.Http.Resilience`), and code generators (NSwag, Kiota, OpenAPI Generator).

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Refit | Attribute-driven type-safe REST client | Reactive UI / community (Paul Betts orig.) | Active | MIT | SG since v6, AOT since v7. De facto for typed HTTP. Integrates `IHttpClientFactory` |
| RestSharp | Imperative REST client, batteries included | RestSharp org (John Sheehan) | Active | Apache-2.0 | v110+ rewrote on `HttpClient` |
| Flurl / Flurl.Http | URL builder + fluent HTTP | Todd Menier | Active | MIT | Excellent test harness via `HttpTest` |
| Polly + Polly.Contrib.{Simmy, MutableBulkhead, DuplicateRequestCollapser, LoggingPolicy, AzureKeyVaultJitter} | Resilience pipelines + chaos / collapsing / KV-jitter | App vNext | Active | BSD-3 | v8 ResiliencePipeline; Simmy = chaos engineering |
| NSwag | OpenAPI tooling — server doc gen + C#/TS client gen | Rico Suter | Active | MIT | Both runtime middleware and CLI/MSBuild |
| OpenAPI Generator (`openapi-generator-cli`) | Multi-language client gen incl. C# (`generichost`, `csharp`, `csharp-netcore` templates) | OpenAPITools | Active | Apache-2.0 | Use `generichost` for modern .NET |
| Kiota | OpenAPI → fluent client SDK gen, multi-language | Microsoft | Active | MIT | Competes with NSwag/OpenAPI Generator |
| WireMock.Net | Stubbing & contract testing port of WireMock (Java) | Stef Heyenrath | Active | Apache-2.0 | Heavy-feature server-side mock |
| RichardSzalay.MockHttp | `HttpMessageHandler` mocking for client tests | Richard Szalay | Active | MIT | Pairs with Refit/Flurl |
| MockHttp.NetCore | Fluent HTTP mocking, strict expectations | Skwas | Active | MIT | Richer matchers |
| Spectre.HttpMock | Lightweight HTTP server-side mocking for tests | Spectre.Console org | Active | MIT | Sibling to Spectre.Console |
| Stubbery | In-process HTTP stub server | Mark Vincze | Maintenance | MIT | Simple stub-only |
| HttpClient.Interception (JustEat) | Bait-and-switch interception | Martin Costello | Active | Apache-2.0 | Bundle-of-rules approach |
| HttpClientToCurl | Generates `curl` from `HttpRequestMessage` | Mahdi Hamzeh | Active | MIT | Debug helper |
| HttpRecorder / Vcr.NetClient | VCR-style record/replay | community | Stale | MIT | Concept worth noting |
| Snapper.Http | Snapshot tests for HTTP responses | community | Active | MIT | Pairs with Snapper |

### B.2 HTTP servers / Minimal API extensions

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Carter | Module-based composition for Minimal APIs | Jonathan Channon / community | Active | MIT | Group endpoints into `ICarterModule` |
| FastEndpoints | Convention-based REPR framework | dj-nitehawk (Anthony Crystal) | Active | Apache-2.0 | Performance-oriented; opinionated structure |
| Ardalis.ApiEndpoints | Endpoint-per-class on top of MVC controllers | Steve Smith | Active | MIT | Pre-Minimal API origin; MVC-based |
| MinimalApis.Extensions | Helpers for Minimal APIs (TypedResults, OpenAPI) | Damian Edwards | Active | MIT | Many features upstreamed |
| MinimalApis.FluentValidation | FluentValidation for Minimal endpoints | community | Active | MIT | Replaces hand-rolled validation filters |
| MinimalValidation | DataAnnotations for Minimal APIs | Damian Edwards | Active | MIT | When FluentValidation feels heavy |
| Saunter | AsyncAPI document generation | Tim Burgess / community | Maintenance | MIT | Event-driven APIs |
| Ardalis.Result | Result/Either for endpoint return | Steve Smith | Active | MIT | Avoid throw-based control flow |
| Immediate.Apis | Source-gen endpoint registration | Immediate Platform | Active | MIT | Source-generator-first competitor to FastEndpoints |
| Wolverine.HTTP | Endpoint module of Wolverine messaging framework | Critter Stack (Jeremy Miller) | Active | MIT | Endpoint-as-handler |
| Dunet | Discriminated unions / OneOf-style return types | Domn Werner | Active | - | Pairs with FastEndpoints |

### B.3 gRPC ecosystem

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Grpc.AspNetCore | Official ASP.NET Core gRPC server | Microsoft | Active | Apache-2.0 | Bedrock |
| Grpc.Net.Client | Official .NET gRPC client | Microsoft | Active | Apache-2.0 | Used by all gRPC clients |
| protobuf-net.Grpc | Code-first gRPC (no .proto needed) | Marc Gravell | Active | Apache-2.0 | Excellent for C#-only systems |
| protobuf-net | High-performance Protobuf serializer | Marc Gravell | Active | Apache-2.0 | Battle-tested |
| MagicOnion | gRPC + MessagePack RPC + StreamingHub | Cysharp (Yoshifumi Kawai) | Active | MIT | Unity-friendly, server filters |
| Google.Protobuf | Official Google Protobuf runtime | Google | Active | BSD-3 | Standard for `.proto` |
| Grpc.Tools | MSBuild integration for `.proto` compilation | Google / gRPC team | Active | Apache-2.0 | Build-time codegen |
| Grpc.HealthCheck | gRPC health checking protocol | gRPC team | Active | Apache-2.0 | Standard for service health probes |
| Grpc.Reflection | gRPC server reflection | gRPC team | Active | Apache-2.0 | Required for `grpcurl` |
| Twirp.NET / TwirpDotNet | .NET implementation of Twitch's Twirp | various community forks | Mixed | - | RPC over HTTP/JSON or Protobuf |
| Cap'n Proto for .NET (`capnproto-dotnetcore`) | RPC + zero-copy serialization | Christian Köllner | Active (modest) | MIT | Capability-based RPC |
| FlatBuffers (`Google.FlatBuffers`) | Zero-copy serialization | Google | Active | Apache-2.0 | Sometimes paired with custom RPC |
| Calzolari.Grpc.AspNetCore.Validation | FluentValidation interceptor for gRPC | Enrico Calzolari | Active | - | Drop-in validation |

### B.4 GraphQL

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| HotChocolate | Code-first GraphQL with subscriptions, Apollo Federation v2, persisted queries | ChilliCream | Active | MIT | Industry standard for new .NET GraphQL |
| Strawberry Shake | Type-safe GraphQL client gen | ChilliCream | Active | MIT | Used in Blazor / RN / MAUI |
| HotChocolate.Subscriptions.{InMemory, Redis, RabbitMQ, NATS} | Subscription transports | ChilliCream | Active | MIT | Multi-transport pub/sub |
| HotChocolate.Fusion | Distributed GraphQL gateway — Apollo Federation-style | ChilliCream | Active | MIT | Stitching successor |
| GraphQL.NET (`GraphQL`) | Original .NET GraphQL implementation | graphql-dotnet org | Active | MIT | Schema-first or programmatic |
| GraphQL.Server.Transports.* / GraphQL.Client | Transports + client | graphql-dotnet | Active | MIT | Modular transports |
| EntityGraphQL | GraphQL schema from EF Core models | Luke Murray | Active | MIT | Quick-path EF→GraphQL |
| GraphQL.AspNet | Code-first server framework, MS-pattern | Kevin Carroll | Active | MIT | Less popular alternative |
| Tanka GraphQL | Modular GraphQL server | Pekka Heikura | Active (modest) | MIT | Smaller schemas |
| AutoFilterer.GraphQL | Generic filter operators | Enisn | Maintenance | - | EF Core-backed schemas |
| Banana Cake Pop / Nitro | GraphQL IDE component | ChilliCream | Active | MIT | Embedded GraphQL IDE |

### B.5 OData

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Microsoft.AspNetCore.OData | Official OData server for ASP.NET Core | Microsoft | Active | MIT | Bedrock |
| Microsoft.OData.Client | Strongly typed OData client | Microsoft | Active | MIT | |
| Simple.OData.Client | Lightweight dynamic OData client (no codegen) | Vagif Abilov | Active | Apache-2.0 | Loose-typed, ergonomic |
| Microsoft.Spatial | Spatial data types for OData | Microsoft | Active | MIT | Geography/Geometry |
| Microsoft.OData.{Edm, Core} | Low-level OData parsing | Microsoft | Active | MIT | Building blocks |
| AspNetCore.OData.Versioning.ApiExplorer | API versioning + OData | Asp.Versioning team | Active | - | |
| RESTier (legacy) | OData service framework | Microsoft (archived) | Archive | MIT | |

### B.6 Real-time / WebSockets / chat

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Microsoft.AspNetCore.SignalR | Official server real-time | Microsoft | Active | MIT | Bedrock |
| Microsoft.Azure.SignalR | Azure-managed scale-out | Microsoft | Active | MIT | |
| Microsoft.AspNetCore.SignalR.Protocols.MessagePack | Binary protocol | Microsoft | Active | MIT | Smaller payloads |
| TypedSignalR.Client | Source generator for typed hub clients | Nemesis-Soft | Active | MIT | Faster, AOT-friendly |
| SignalR.Strong | Strongly typed SignalR client (interface-based) | community | Active | - | Compile-time safety |
| Websocket.Client (Marfusios) | High-performance reactive WebSocket client | Marfusios | Active | MIT | Reactive, auto-reconnect |
| Fleck | Lightweight WebSocket server | statianzo | Active | MIT | Stand-alone WS server |
| SuperWebSocket | WebSocket server on SuperSocket | Kerry Jiang | Maintenance | Apache-2.0 | Older, stable |
| SuperSocket | TCP/UDP socket server framework | Kerry Jiang | Active | Apache-2.0 | Lower-level building block |
| vtortola.WebSockets | Self-hosted WebSocket server | vtortola | Maintenance | MIT | Non-ASP.NET hosting |
| EmbedIO | Tiny self-hosted web server with WS | Unosquare | Active | BSD-2 | Embedded scenarios |
| NetCoreServer | Multi-protocol async TCP/UDP/HTTP/WS server | chronoxor | Active | MIT | High performance, non-ASP.NET |
| MagicOnion (real-time hub) | StreamingHub bidi RPC + MessagePack | Cysharp | Active | MIT | Game/Unity scenarios |
| SignalR.Orleans | Orleans backplane for SignalR | OrleansContrib | Active | MIT | Replaces Redis backplane |
| Soenneker.SignalR.Client.* | Modern wrapper packages | Jake Soenneker | Active | MIT | Productivity wrappers |
| Bytewizer.TinyCLR.WebSockets | Embedded/IoT WebSockets | Bytewizer | Active | - | IoT |

### B.7 Streaming / SSE / Webhooks

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Lib.AspNetCore.ServerSentEvents | SSE server middleware | Tomasz Pęczek | Active | MIT | The standard SSE lib |
| LaunchDarkly.EventSource | Robust SSE client (used by LaunchDarkly SDK) | LaunchDarkly | Active | Apache-2.0 | Production-grade SSE *client* |
| AspNetCore.WebHooks | Webhook receivers + senders (legacy MS) | Microsoft | Maintenance | Apache-2.0 | Aging |
| EventStore.Client (gRPC) | Event-sourcing streaming client | EventStore Ltd | Active | Apache-2.0 | HTTP and gRPC |
| Octokit.Webhooks | GitHub webhook handlers | Octokit | Active | MIT | Vendor-specific |
| Streamiz.Kafka.Net | Kafka Streams API for .NET | LGouellec | Active | MIT | Streaming data |
| Akka.Streams | Reactive Streams implementation | Akka.NET team | Active | Apache-2.0 | Streaming dataflow |

### B.8 API gateways / reverse proxies

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| YARP | High-performance reverse proxy library + product | Microsoft | Active | MIT | Embeddable; community-friendly |
| Ocelot | API gateway for .NET microservices | ThreeMammals | Active | MIT | Older than YARP; configuration-driven via `ocelot.json` |
| Ocelot.Provider.Polly | Resilience integration | ThreeMammals | Active | MIT | |
| AspNetCore.Proxy | Lightweight in-process HTTP/WebSocket proxy middleware | twitchax | Active | MIT | Small proxy inside an app |
| ProxyKit | Composable HTTP proxy middleware (archived) | Damian Hickey | Archive | MIT | Concept absorbed by YARP |
| Yarp.Telemetry.Consumption | Telemetry for YARP | Microsoft | Active | MIT | Diagnostics surface |
| Steeltoe.Discovery + LoadBalancer | Client-side load balancing acting as soft gateway | Steeltoe | Active | Apache-2.0 | Overlaps gateway responsibilities |

### B.9 BFF / authentication brokering

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Duende.BFF | BFF security framework with anti-forgery, session mgmt | Duende Software | Active | Commercial / RPL | Free for small companies; paid for larger |
| Duende IdentityServer | OAuth2/OIDC server (commercial fork of IdentityServer4) | Duende Software | Active | RPL / Commercial | License — not free for production at scale |
| Microsoft.Identity.Web | AAD/Entra ID auth helpers | Microsoft | Active | MIT | Includes BFF helpers for AAD-only |
| OpenIddict | Free, community OIDC server framework | Kévin Chalet | Active | Apache-2.0 | Replacement for IdentityServer4 |
| OpenIddict.Validation.AspNetCore | Token validation middleware | Kévin Chalet | Active | Apache-2.0 | |
| IdentityModel | OAuth2/OIDC client primitives | IdentityModel team (Duende-adjacent) | Active | Apache-2.0 | Foundation |
| IdentityModel.AspNetCore.OAuth2Introspection | Opaque token validation | IdentityModel | Active | Apache-2.0 | |
| IdentityModel.OidcClient | Native OIDC client (mobile/desktop) | IdentityModel | Active | Apache-2.0 | |
| AspNet.Security.OAuth.Providers | 50+ OAuth provider middlewares | Kévin Chalet + community | Active | Apache-2.0 | GitHub, Twitter, etc. |
| ITfoxtec.Identity.Saml2 | SAML 2.0 protocol | ITfoxtec | Active | LGPL | License worth noting |
| Sustainsys.Saml2 | SAML 2.0 alternative | Sustainsys | Active | LGPL | |
| Auth0.AspNetCore.Authentication | Auth0 BFF/web integration | Auth0 | Active | MIT | |
| Okta.AspNetCore | Okta integration | Okta | Active | Apache-2.0 | |
| Aguafrommars/TheIdServer | Open-source IdentityServer4-like fork | Aguafrommars | Active | Apache-2.0 | Free Duende alt |
| Volo.Abp.Account / Volo.Abp.IdentityServer | ABP framework's auth layer | ABP team | Active | LGPL | Heavy-framework path |

### B.10 API versioning / docs / spec

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Asp.Versioning.{Mvc, Http, Mvc.ApiExplorer} | API versioning for MVC + Minimal APIs | dotnet/aspnet-api-versioning | Active | MIT | Modern path; replaces older `Microsoft.AspNetCore.Mvc.Versioning` |
| NSwag.AspNetCore | OpenAPI doc + Swagger UI for ASP.NET Core | Rico Suter | Active | - | Tighter integration with NSwag client gen |
| Swashbuckle.AspNetCore | Swagger / OpenAPI doc gen | domaindrivendev / Microsoft (now) | Active | - | Default; .NET 9 ships without by default |
| Swashbuckle.AspNetCore.Annotations | Attribute-driven Swagger metadata | Same | Active | - | Inline summaries/descriptions |
| Swashbuckle.AspNetCore.Filters | Examples, polymorphism support | Mattfrear | Active | - | Standard companion |
| Microsoft.AspNetCore.OpenApi | New OpenAPI doc generator (.NET 9+) | Microsoft | Active | MIT | Replaces Swashbuckle for many MS-default scenarios |
| Hellang.Middleware.ProblemDetails | RFC 7807 error responses | Kristian Hellang | Active | - | Bridges exceptions → problem+json |
| Pact.NET (Pact-net) | Consumer-driven contract testing | Pact Foundation | Active | MIT | Critical for inter-service comms |
| Refitter | OpenAPI → Refit interface generator | Christian Resma Helle | Active | MIT | Bypass NSwag client; emit Refit interfaces |
| Microsoft.OpenApi.* | Low-level OpenAPI document model + Readers | Microsoft | Active | MIT | Build custom doc tooling |

### B.11 Hypermedia / REST conventions

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| RiskFirst.Hateoas | Convention-based HATEOAS link generation | RiskFirst | Maintenance | MIT | Cleaner HATEOAS lib |
| Riok.HateoasNet (`HateoasNet`) | HATEOAS link generation | community | Maintenance | MIT | Multiple forks |
| JsonApiDotNetCore | JSON:API server framework over EF Core | JADNC contributors | Active | MIT | The .NET JSON:API standard |
| Halibut (Octopus Deploy) | HTTP-over-WebSocket secure RPC | Octopus Deploy | Active | Apache-2.0 | Secure RPC over outbound-only firewalls |
| Halcyon | HAL-formatted hypermedia responses | community | Maintenance | - | |
| Microsoft.AspNet.WebApi.HelpPage | Legacy MVC 5 help page generator | Microsoft (legacy) | Archive | - | |

### B.12 HTTP middleware grab-bag

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Sieve | Filter/sort/pagination from query strings | Bilal Fakhouri | Active | Apache-2.0 | Pragmatic alternative to OData |
| AutoFilterer.Mvc | Auto-generates filter expressions from query DTOs | Enisn | Maintenance | MIT | Annotation-driven |
| Gridify | LINQ filter/sort/pagination via string syntax | Alireza Sabouri | Active | MIT | Strong dynamic filtering DSL |
| LinqKit / DynamicLinq | Predicate building for filters | Joseph Albahari / dynamic-linq team | Active | LGPL / Apache-2.0 | License (LGPL) of LinqKit |
| Microsoft.AspNetCore.JsonPatch | RFC 6902 JSON Patch support | Microsoft | Active | MIT | Newtonsoft-only |
| SystemTextJsonPatch (Havunen) | Standalone STJ-compatible JSON Patch | Tuomo Havunen | Active | MIT | Drop-in replacement |
| Hellang.Middleware.SpaFallback | SPA fallback routing | Kristian Hellang | Maintenance | - | |
| MiniProfiler | Lightweight request profiler | Stack Exchange | Active | MIT | Endpoint timing dashboards |
| Westwind.AspNetCore.Markdown | Markdown content rendering middleware | Rick Strahl | Active | MIT | Embedded docs/help pages |
| AspNetCore.Authentication.{ApiKey, Basic} | API key / Basic auth middleware | Mihir Sutariya | Active | MIT | Common in inter-service |
| AspNetCoreRateLimit | Older rate-limit middleware | Stefan Prodan | Maintenance | Apache-2.0 / MIT | Pre-.NET 7 |
| AspNetCore.Diagnostics.HealthChecks (Xabaril) | Comprehensive health-check pack + UI | Xabaril | Active | Apache-2.0 | 70+ provider checks |
| Carter.OpenApi | OpenAPI module for Carter | community | Active | MIT | Pairs with Carter |
| ZymLabs.NSwag.FluentValidation | FluentValidation rules in NSwag | ZymLabs | Active | MIT | Carries validation into OpenAPI |
| Microsoft.IO.RecyclableMemoryStream | Buffer pooling | Microsoft | Active | MIT | Often pulled by middleware logging libs |
| HotChocolate.PersistedOperations.* | Persisted query libraries | ChilliCream | Active | MIT | |

### B.13 Service discovery / config

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Steeltoe.{Discovery.{Eureka, Consul}, Configuration.{ConfigServer, Cloud, Kubernetes, CloudFoundry}, CircuitBreaker.Hystrix, LoadBalancer, Connectors} | Spring-Cloud-style libs | Steeltoe (Broadcom/VMware) | Active | Apache-2.0 | Closest to Spring ecosystem |
| Consul.NET | Direct Consul API client | PlayFab/community | Active | Apache-2.0 | |
| dnsclient.net (DnsClient) | DNS client library | MichaCo | Active | Apache-2.0 | SRV record discovery |
| etcd-cs (`dotnet-etcd`) | etcd v3 client | shubhamranjan | Active | Apache-2.0 | Service discovery + KV |
| zookeeper.net | Apache ZooKeeper client | shayhatsor | Maintenance | Apache-2.0 | |
| Microsoft.Extensions.ServiceDiscovery | Standardized service discovery (Aspire) | Microsoft | Active | MIT | Pluggable model — DNS, Consul, etc. |
| Microsoft.Extensions.Configuration.AzureAppConfiguration | Azure App Configuration | Microsoft | Active | MIT | |
| AWSSDK.Extensions.NETCore.Setup + AppConfig | AWS AppConfig | AWS | Active | Apache-2.0 | |
| Vault.NET / VaultSharp | HashiCorp Vault clients | Various / Raja Nadar | Active | Apache-2.0 | Secrets discovery |
| OpenFeature.NET | Vendor-neutral feature flag standard | OpenFeature org | Active | Apache-2.0 | |

### B.14 Feature flags (community)

| Lib | Niche | Maintainer | License |
|---|---|---|---|
| LaunchDarkly.ServerSdk + Client | LaunchDarkly server + client SDK | LaunchDarkly | Apache-2.0 |
| Unleash.Client / Unleash.FeatureManagement | Unleash OSS flag SDK + bridge | Unleash + community | Apache-2.0 |
| GrowthBook.NET | GrowthBook A/B SDK | GrowthBook | MIT |
| ConfigCat.SDK / OpenFeature.Provider | ConfigCat + OpenFeature bridge | ConfigCat | MIT |
| Esquio (+`.AspNetCore`, `.Toggles.*`) | Open-source feature toggle framework with UI | Xabaril | MIT |
| Flagsmith.NET | Flagsmith SDK | Flagsmith | BSD-3 |
| Featurevisor (.NET SDK) | GitOps-style flag mgmt | Featurevisor | MIT |
| Optimizely.SDK | Optimizely Full Stack | Optimizely | Apache-2.0 |
| Split.io SDK | Split (Harness) SDK | Split | Apache-2.0 |
| OpenFeature provider impls | Pluggable providers under OpenFeature | OpenFeature + vendors | Apache-2.0 |
| Microsoft.FeatureManagement | Microsoft's feature flag abstraction | Microsoft | MIT |
| FeatureToggle (Jason Roberts) | Classic .NET feature toggle library | Jason Roberts | - |
| FF4J.NET | Port of Java FF4J | community | - |

### B.15 Inter-service / RPC alternatives

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| MagicOnion | gRPC + MessagePack RPC + StreamingHub | Cysharp | Active | MIT | Already covered |
| Microsoft Orleans | Virtual actor RPC via grain interfaces | Microsoft | Active | MIT | Cluster-aware actor RPC |
| Microsoft Service Fabric | Legacy actor/services platform | Microsoft | Maintenance | MIT | |
| Akka.NET (+`.Remote`, `.Cluster.*`) | Akka port — actors + clustering + remoting | Akka.NET team | Active | Apache-2.0 | |
| Proto.Actor (+`.Cluster`) | Lightweight actor framework | AsynkronIT (Roger Johansson) | Active | Apache-2.0 | Faster than Akka.NET; smaller surface |
| Wolverine | Mediator + messaging framework with HTTP/RPC modules | Critter Stack | Active | MIT | RPC-via-message paradigm |
| MassTransit | Distributed app framework — saga, request/response | Chris Patterson | Active | Apache-2.0 | Request/response = RPC pattern |
| NServiceBus | Mature messaging with request/response | Particular Software | Active | Commercial / RPL | RPC-via-messages; commercial license |
| Rebus | Lightweight service bus with RPC | Rebus team | Active | Apache-2.0 | Smaller than MassTransit |
| DotNetty | Netty-port low-level networking | Microsoft Azure / community | Maintenance | MIT | Building block; used by Akka.NET, Orleans |
| ZeroMQ.NET / NetMQ | ZeroMQ patterns in .NET (req/rep, pub/sub) | NetMQ team | Active | LGPL | License worth noting |
| Apache Thrift | Cross-language RPC | Apache | Maintenance | Apache-2.0 | Mostly legacy |
| Bedrock.Framework | Custom transport framework on Kestrel | David Fowler / Microsoft (community) | Active (low) | MIT | For bespoke wire protocols |
| Foundatio | Distributed primitives (queues, locks, RPC patterns) | Foundatio | Active | Apache-2.0 | Fits where MassTransit is overkill |
| Dapr.{AspNetCore, Client} | Dapr sidecar — service invocation, pub/sub, state | Dapr (CNCF) | Active | Apache-2.0 | Service-to-service RPC abstraction over a sidecar |
| .NET Aspire | Cloud-ready stack incl. service discovery + RPC composition | Microsoft | Active | MIT | Opinionated RPC/orchestration |

---

## §C. Survey deep-dive — Data/Persistence/Messaging inventory

### C.1 Relational ORMs and data access

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Microsoft.EntityFrameworkCore | Core EF runtime, change tracking, LINQ | Microsoft | Active | MIT | Default ORM |
| Microsoft.EntityFrameworkCore.SqlServer / Sqlite / Cosmos / InMemory | Microsoft EF providers | Microsoft | Active | MIT | First-class |
| Npgsql.EntityFrameworkCore.PostgreSQL | EF Core for Postgres | Shay Rojansky / npgsql org | Active | PostgreSQL | JSONB, arrays, ranges, pgvector |
| Pomelo.EntityFrameworkCore.MySql | EF Core for MySQL/MariaDB | Pomelo Foundation | Active | MIT | Broader feature coverage than MySql.Data |
| MySql.EntityFrameworkCore | Oracle's MySQL provider | Oracle | Active | GPL-2.0 with FOSS Exception | Slower release cadence |
| Oracle.EntityFrameworkCore | Oracle DB provider | Oracle | Active | Oracle license | Enterprise stacks |
| FirebirdSql.EntityFrameworkCore.Firebird | Firebird provider | FirebirdSQL Foundation | Active | LGPL-style | Niche |
| Devart.Data.{MySql,PostgreSql,Oracle}.EFCore | Commercial providers | Devart | Active | Commercial | Performance and feature add-ons |
| Dapper (+`.Contrib`, `.SqlBuilder`, `.FluentMap`, `.Plus`) | Micro-ORM | Stack Exchange / .NET Foundation | Active | Apache-2.0 / Commercial (Plus) | Industry standard for fast read paths |
| RepoDb | Hybrid micro/macro ORM | Michael Pendon | Active | Apache-2.0 | Performant alt to Dapper + EF |
| NHibernate (+ FluentNHibernate) | Mature full ORM, Hibernate port | NHibernate Community | Active | LGPL-2.1 / BSD-3 | Heavy but battle-tested |
| linq2db | Lightweight LINQ-to-SQL with high SQL fidelity | linq2db community | Active | MIT | Strong for complex queries |
| BLToolkit | Predecessor of linq2db | linq2db team | Legacy | MIT | Deprecated |
| ServiceStack.OrmLite | Code-first POCO ORM | ServiceStack | Active | Dual (AGPL/commercial) | Schema-from-types |
| PetaPoco | Tiny micro-ORM with T4 templates | community | Slow | Apache-2.0 | Lightweight alt |
| Massive | Single-file dynamic micro-ORM | Rob Conery | Legacy | New BSD | Historical |
| FluentMigrator | Fluent C# DB migrations | FluentMigrator team | Active | Apache-2.0 | Provider-agnostic |
| DbUp | SQL-script migration runner | DbUp team | Active | MIT | Idempotent execution |
| Evolve | DB migration tool inspired by Flyway | Philippe Etiévant | Active | MIT | Pure SQL |
| RoundhousE | Convention-based DB migration | Chuck Norris organization | Slow | Apache-2.0 | Legacy in many shops |
| EFCore.BulkExtensions | Bulk ops for EF Core | borisdj | Active | MIT | De facto bulk choice in OSS |
| Z.EntityFramework.{Extensions, Plus} | Commercial EF bulk + utilities | ZZZ Projects | Active | Commercial / MIT-ish | Widest provider/feature surface |
| EFCore.NamingConventions | Snake_case / lower_case naming | Shay Rojansky | Active | MIT | Crucial for Postgres |
| EntityFrameworkCore.Triggered | Trigger pipeline (before/after save) | Joris Dries | Active | MIT | Declarative triggers via DI |
| EFCore.Projectables | Project methods/properties into SQL | koenbeuk | Active | MIT | Eliminates client-side eval |
| Linq.Translations | Translate computed properties | Damien Guard | Legacy | MIT | Predecessor to Projectables |
| NeinLinq | Reusable LINQ predicates and translations | axelheer | Active | LGPL-3.0 | Predicate composition |
| LinqKit (+`.Microsoft.EntityFrameworkCore`) | PredicateBuilder, AsExpandable | Joseph Albahari / community | Active | MS-PL / LGPL-2.1 | License caveats |
| System.Linq.Dynamic.Core | String-based dynamic LINQ | StefH | Active | Apache-2.0 | Runtime query composition |
| EFCore.SecondLevelCache.Core | L2 cache for EF Core | VahidN | Active | Apache-2.0 | Memory/Redis L2 |
| EntityFrameworkCore.Exceptions | Strongly-typed DB exception mapping | Giorgi Dalakishvili | Active | MIT | Map SQL errors to typed exceptions |
| EntityFrameworkCore.Sqlite.NodaTime | NodaTime support | Khalid Abuhakmeh | Active | MIT | |
| Npgsql / Microsoft.Data.SqlClient / Microsoft.Data.Sqlite | Raw ADO.NET drivers | npgsql org / Microsoft | Active | PostgreSQL / MIT | Used directly by Dapper |
| MySqlConnector | Async-first MySQL/MariaDB driver | Bradley Grainger | Active | MIT | Underlies Pomelo |
| Oracle.ManagedDataAccess.Core | Managed Oracle ADO.NET | Oracle | Active | Oracle license | |
| FirebirdSql.Data.FirebirdClient | Firebird ADO.NET | FirebirdSQL Foundation | Active | Public Domain | |
| IBM.Data.DB2.Core | IBM DB2 ADO.NET | IBM | Active | IBM proprietary | Mainframe / enterprise |

### C.2 NoSQL — document, graph, time-series, wide-column

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| MongoDB.Driver (+`.EntityFrameworkCore`, MongoFramework) | Mongo client + EF + EF-like wrapper | MongoDB Inc. + community | Active | Apache-2.0 / MIT | LINQ provider, GridFS |
| CassandraCSharpDriver / ScyllaDB CSharp Driver | Cassandra / ScyllaDB | DataStax / ScyllaDB | Active | Apache-2.0 | LINQ provider available |
| ArangoDB.NET (`arangodb-net-standard`) | ArangoDB multi-model | community | Active | Apache-2.0 | Docs, graphs, KV |
| RavenDB.Client | Official RavenDB client | Hibernating Rhinos | Active | Commercial / AGPL | Distributed by default |
| LiteDB | Embedded NoSQL .NET file DB | Mauricio David | Active | MIT | Single-file, no server |
| MyCouch / CouchDB.NET | CouchDB clients | community | Slow / Active | MIT | LINQ-friendly variant |
| Elastic.Clients.Elasticsearch (+`.Net`) | Modern ES client (ES 8+) | Elastic | Active | Apache-2.0 | Replaces NEST |
| NEST / Elasticsearch.Net | Legacy ES 7 clients | Elastic | Legacy | Apache-2.0 | Compatible with ES 7 |
| OpenSearch.{Client, Net} | OpenSearch fork (NEST-derived) | OpenSearch project / AWS | Active | Apache-2.0 | |
| Marten | Postgres as docDB + event store | JasperFx (Jeremy D. Miller) | Active | MIT | DocDB + ES + CQRS-friendly |
| EventStore.Client (Kurrent) | Client for EventStoreDB / KurrentDB | Kurrent / EventStore Ltd | Active | Apache-2.0 | |
| InfluxDB.Client (+`.v1`) | InfluxDB v2/v3 (and v1 legacy) | InfluxData | Active | MIT | Flux/InfluxQL |
| QuestDB.Client | Time-series client | QuestDB | Active | Apache-2.0 | InfluxLineProtocol |
| ClickHouse.{Client, Ado} / Octonica.ClickHouseClient | ClickHouse drivers | DarkWanderer / killwort / Octonica | Active / Slow / Active | MIT / LGPL-3.0 / MIT | Modern + legacy + high-perf |
| StackExchange.Redis (+`.Extensions.Core`) | Redis client (+ helpers) | Marc Gravell / Stack Exchange / Imperugo | Active | MIT | Foundation for many distributed-cache stacks |
| NRedisStack | Redis Stack module access (Search/JSON/Graph/TS) | Redis Inc. | Active | MIT | Wraps StackExchange.Redis |
| Couchbase.{NetClient, Lite.NET} | Couchbase Server + embedded sync | Couchbase Inc. | Active | Apache-2.0 | KV + N1QL + FTS |
| Neo4j.Driver / Neo4jClient | Bolt driver + fluent client | Neo4j Inc. / Readify | Active / Slow | Apache-2.0 / MS-PL | |
| AWSSDK.DynamoDBv2 (+`.DataModel`) | Dynamo SDK + high-level mapper | AWS | Active | Apache-2.0 | |
| Azure.Data.Tables | Azure Table / Cosmos Table client | Microsoft | Active | MIT | Replaces older WindowsAzure.Storage |
| dotnet-etcd | etcd v3 gRPC client | shubhamranjan | Active | Apache-2.0 | Distributed config / leader election |
| FoundationDB.NET (community wrappers) | FoundationDB bindings | community | Niche | Apache-2.0 | Edge use |
| Aerospike.Client | Aerospike KV/document | Aerospike Inc. | Active | Apache-2.0 | Sub-ms KV |
| FreeRedis / BeetleX.Redis | Alternative Redis clients | nicye / beetlex-io | Active | MIT / Apache-2.0 | Sync-first / perf-tuned |
| Hazelcast.NET | Hazelcast IMDG client | Hazelcast Inc. | Active | Apache-2.0 | Cluster-wide maps, queues, locks |
| Apache.Ignite | Apache Ignite .NET client | Apache | Active | Apache-2.0 | Distributed cache + compute |

### C.3 Caching (hybrid + in-process + distributed)

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Microsoft.Extensions.Caching.{Memory, Distributed, StackExchangeRedis, SqlServer, Cosmos, Hybrid} | First-party caches + Hybrid (.NET 9) | Microsoft | Active | MIT | HybridCache: opinionated tag-based invalidation |
| ZiggyCreatures.FusionCache (+`.Backplane.StackExchangeRedis`) | L1+L2 with stampede protection, fail-safe, soft/hard timeouts | Jody Donetti | Active | MIT | Best-in-class OSS hybrid |
| CacheManager.Core | Provider-agnostic cache abstraction | MichaCo | Slow | Apache-2.0 | Historically widely used |
| EasyCaching | Cache abstraction with many providers | Catcher Wong | Active | MIT | Memcached, Redis, LiteDB, in-mem |
| Foundatio.Caching | Cache abstraction in Foundatio | Exceptionless | Active | Apache-2.0 | |
| LazyCache | Memory cache with `Lazy<T>` | Alastair Crabtree | Active | MIT | Simple wrapper |
| BitFaster.Caching | High-perf concurrent caches (LRU, LFU) | Alex Peck | Active | MIT | Lock-free where possible |
| Carbon.Cache | Lightweight cache helpers | Carbon Aware project | Slow | MIT | |
| EFCore.SecondLevelCache.Core | EF L2 cache (memory or Redis) | VahidN | Active | Apache-2.0 | |
| Microsoft.AspNetCore.OutputCaching / ResponseCaching | HTTP output cache + response cache | Microsoft | Active | MIT | Framework-level |
| ImageSharp.Web Cache | Cached image transformation pipeline | Six Labors | Active | Apache-2.0 + commercial tier | Output cache for image responses |
| MemoryPack-based caches | High-perf binary serialization for cache values | Cysharp | Active | MIT | Compact entries |
| Akavache | Async key-value store | ReactiveUI team | Slow | MIT | Mostly client-side |
| CacheCow | HTTP caching for HttpClient and WebAPI | Aliostad | Slow | MIT | Outgoing HTTP cache |
| MonkeyCache | Lightweight key-value store | James Montemagno | Slow | MIT | Mobile-oriented |

### C.4 Distributed cache / KV stores

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| StackExchange.Redis (+`.Extensions.Core`) | Foundational Redis client + helpers | Stack Exchange / Imperugo | Active | MIT | Most-used .NET Redis |
| RedLock.net | Distributed lock (Redlock algorithm) | Sam Cook | Active | MIT | Multi-master locks |
| NCache.Client | NCache distributed cache | Alachisoft | Active | Commercial | High-perf .NET-native |
| EnyimMemcachedCore | Memcached client | cnblogs.com community | Active | Apache-2.0 | Modern .NET Core binding |
| Memcached.ClientLibrary | Older Memcached client | Memcached community | Legacy | Apache-2.0 | |
| FreeRedis / BeetleX.Redis | Alternative Redis clients | nicye / beetlex-io | Active | MIT / Apache-2.0 | |
| Couchbase.NetClient | Couchbase as KV/Document | Couchbase Inc. | Active | Apache-2.0 | Multi-purpose |
| Hazelcast.NET / Apache.Ignite / Aerospike.Client | Distributed in-mem grids | Vendors | Active | Apache-2.0 | Cluster-wide ops |
| dotnet-etcd | etcd v3 gRPC | shubhamranjan | Active | Apache-2.0 | Distributed config |
| Consul.NET | HashiCorp Consul client | PlayFab/community | Slow | Apache-2.0 | Service discovery + KV |
| FoundationDB.NET | FoundationDB bindings | community | Niche | Apache-2.0 | |

### C.5 Search

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Lucene.Net | Embedded full-text search engine | Apache | Active | Apache-2.0 | The .NET port of Lucene |
| Elastic.Clients.Elasticsearch / NEST / Elasticsearch.Net | Elasticsearch clients (modern + legacy) | Elastic | Active / Legacy | Apache-2.0 / Elastic | Modern required for ES 8+ |
| OpenSearch.{Client, Net} | OpenSearch clients | OpenSearch / AWS | Active | Apache-2.0 | NEST fork |
| Algolia.Search | Hosted search service client | Algolia | Active | MIT | As-you-type search |
| Meilisearch.Client | Hosted/self-hosted search | Meili | Active | MIT | Lightweight typo-tolerant |
| Typesense.Client | Hosted/self-hosted typo-tolerant | Typesense | Active | Apache-2.0 | Schema-first |
| SolrNet | Apache Solr client | Mauricio Scheffer | Slow | Apache-2.0 | Mature; less new use |
| Azure.Search.Documents | Azure Cognitive Search SDK | Microsoft | Active | MIT | Hosted vector + lexical, semantic ranker |
| NRedisStack (RediSearch module) | Redis-based search | Redis Inc. | Active | MIT | FT.SEARCH on JSON/Hash |
| SQL FullTextSearch (vendor features) | Built-in DB engine FTS | DB vendors | n/a | DB license | SQL Server contains/freetext, Postgres tsvector |
| sqlite-fts (FTS5) | SQLite full-text extension | SQLite team | Active | Public Domain | Embedded |
| Postgres tsvector via EF | Postgres FTS through EF Core | Npgsql team | Active | PostgreSQL | GIN-indexed columns |
| ManticoreSearch.Client | Manticore Search SDK | Manticore Software | Active | GPL-2.0 | Sphinx successor |
| Lifti | Embedded in-memory full-text indexer | Mike Goatly | Active | MIT | Minimal-footprint |
| AWSSDK.OpenSearchService / AWSSDK.CloudSearch | Cloud search mgmt SDKs | AWS | Active | Apache-2.0 | Cluster mgmt |

### C.6 Storage abstractions / files / blobs / media

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Azure.Storage.{Blobs, Files.Shares, Files.DataLake, Queues} | Azure Storage SDKs | Microsoft | Active | MIT | Standard for Azure |
| AWSSDK.{S3, Glacier, Transfer} | AWS object storage + transfer manager | AWS | Active | Apache-2.0 | Multi-region, multipart |
| MinIO.SDK | S3-compatible object storage | MinIO | Active | Apache-2.0 | Self-hosted MinIO |
| Google.Cloud.Storage.V1 | GCS SDK | Google | Active | Apache-2.0 | First-class GCP blob |
| Microsoft.Extensions.FileProviders.{Physical, Embedded, Composite} | File providers | Microsoft | Active | MIT | Static-content serving |
| Storage.Net (aloneguid) / FluentStorage | Multi-cloud storage abstraction | aloneguid / community | Active | Apache-2.0 | Single API across clouds |
| FileSystemAbstractions / TestableIO/System.IO.Abstractions | IFileSystem mockable | Tatham Oddie / community | Active | MIT | Mock filesystem in tests |
| DiskQueue | Persistent disk-backed queue | i-e-b / community | Slow | MIT | File-based reliable queue |
| SharpCompress | Multi-format archive read/write | adamhathcock | Active | MIT | Zip, Tar, Rar, 7z, GZip |
| ICSharpCode.SharpZipLib | Long-lived archive library | community | Active | MIT | Zip, Tar, GZip, BZip2 |
| SevenZipNETStandard | 7-Zip wrapper | community | Slow | LGPL-2.1 | Native lib needed |
| MimeKit | MIME parsing and creation | Jeffrey Stedfast | Active | MIT | Foundation for MailKit |
| Xabe.FFmpeg / FFMpegCore | FFmpeg wrappers | Xabe / rosenbjerg | Active | Commercial-LGPL / MIT | Sync + async transcoding |
| ImageSharp (+`.Web`) | Cross-platform image processing + web middleware | Six Labors | Active | Apache-2.0 (commercial tier) | System.Drawing replacement |
| SkiaSharp | Skia 2D graphics binding | Microsoft / community | Active | MIT | Cross-platform |
| Magick.NET | ImageMagick binding | Dirk Lemstra | Active | Apache-2.0 | Wide format support |
| Emgu.CV | OpenCV binding | Emgu | Active | Commercial / GPL | Computer vision |
| Tesseract OCR (Tesseract .NET wrapper) | OCR engine | Charles Weld | Slow | Apache-2.0 | |
| Aspose.Storage / Aspose.Total | Commercial multi-format storage | Aspose | Active | Commercial | Enterprise |
| BlobHelper | Multi-cloud blob abstraction | Joel Christner | Slow | MIT | Lightweight Storage.Net alt |
| Azure.Storage.DataMovement | Azure data movement library | Microsoft | Active | MIT | Resumable copies |

### C.7 Messaging / pub-sub / queues

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| MassTransit (+`.RabbitMQ`, `.AzureServiceBus.Core`, `.Kafka`, `.AmazonSQS`, `.ActiveMQ`) | Multi-broker messaging framework | MassTransit / Chris Patterson | Active | Apache-2.0 (commercial v9+ announced) | RabbitMQ, ASB, Kafka, SQS, ActiveMQ Artemis, in-mem |
| NServiceBus (+`.RabbitMQ`, `.AzureServiceBus`) | Commercial enterprise messaging | Particular Software | Active | Commercial / royalty-free dev | Sagas, outbox, recoverability |
| Brighter | Command processor + broker abstraction | Iain Wilson / brighter org | Active | BSD-3 | Pipelines, sagas, outbox |
| Rebus | Lean messaging framework | Mogens Heller Grabe | Active | MIT | Friendly DI, many transports |
| EasyNetQ | High-level RabbitMQ API | EasyNetQ org | Active | MIT | Convention-driven RabbitMQ |
| RabbitMQ.Client | Official low-level RabbitMQ AMQP client | RabbitMQ team / VMware | Active | Apache-2.0 / MPL-2.0 | Used by EasyNetQ, MT, Wolverine |
| Confluent.Kafka | Kafka client | Confluent | Active | Apache-2.0 | Canonical .NET Kafka |
| AWSSDK.{SQS, SNS} | AWS SQS/SNS SDKs | AWS | Active | Apache-2.0 | |
| Azure.Messaging.{ServiceBus, EventHubs, EventGrid} | Azure messaging SDKs | Microsoft | Active | MIT | |
| NATS.Client | NATS / JetStream client | Synadia / NATS | Active | Apache-2.0 | High-perf pub-sub |
| Apache.NMS | JMS-like ActiveMQ access | Apache | Slow | Apache-2.0 | |
| MQTTnet (+`.Extensions.Hosting`) | MQTT broker + client | Christian Kratky | Active | MIT | Production-grade MQTT |
| Silverback | Multi-broker messaging with sagas | Sergio Aquilini | Active | MIT | Kafka, RabbitMQ, MQTT |
| Foundatio.Messaging | Pluggable messaging | Exceptionless | Active | Apache-2.0 | |
| MediatR | In-process pub-sub / mediator | Jimmy Bogard | Maintenance / Commercial transition | Apache-2.0 / commercial license shift announced | De facto in-process |
| Mediator (martinothamar) | Source-gen mediator | Martin Othamar | Active | MIT | Compile-time pipelines |
| Wolverine | Modern message + command framework | JasperFx | Active | MIT | Code-first, transactional outbox built-in |
| DotNetCore.CAP | Distributed transaction outbox | Yang Zheng | Active | MIT | Bridges DB tx and broker publishes |
| KafkaFlow | Kafka consumer/producer framework | farfetch | Active | MIT | Pipelines, middleware, retries |
| Apache.Pulsar.Client / DotPulsar | Pulsar client | Apache Pulsar / dotpulsar | Active | Apache-2.0 | |
| AWSSDK.MQ / AmazonMQ | Hosted ActiveMQ/RabbitMQ | AWS | Active | Apache-2.0 | |
| Tibco.EMS / Solace.Messaging | Commercial broker SDKs | Vendors | Active | Commercial | Enterprise brokers |
| RawRabbit | RabbitMQ wrapper | pardahlman | Legacy | MIT | Predecessor |

### C.8 Event sourcing / CQRS frameworks

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| EventStore.Client (Kurrent) | EventStoreDB / KurrentDB client | Kurrent | Active | Apache-2.0 | Append-only log + projections |
| Marten | Postgres event store + docDB | JasperFx | Active | MIT | First-class projections + aggregates |
| EventFlow | CQRS + ES framework | rasmus | Slow | Apache-2.0 | Aggregates, sagas, snapshots |
| Aggregates.NET | NServiceBus-based ES | Charles Solar | Slow | MIT | |
| NEventStore | Long-running ES infrastructure | NEventStore community | Slow | MIT | Pluggable persistence |
| SqlStreamStore | Append-only event stream over SQL | Damian Hickey | Slow | MIT | Postgres / SQL Server / MySQL |
| Akka.Persistence (+`.Sql.Common`) | Event-sourced actor persistence | Akka.NET | Active | Apache-2.0 | Akka actor state persistence |
| Orleans Streams + EventSourcing | Virtual actor streams + event-sourced grains | Microsoft | Active | MIT | |
| Eventuous (EventuousFx) | Lightweight ES toolkit | Alexey Zimarev | Active | Apache-2.0 | EventStoreDB / Postgres |
| Particular.{ServiceControl, ServicePulse} | Operational tooling for NServiceBus | Particular Software | Active | Commercial | |
| Proto.Persistence | Event-sourced actors for Proto.Actor | Asynkron | Active | Apache-2.0 | Snapshot + journal |
| Equinox.NET | F#-leaning ES library | Jet.com | Active | Apache-2.0 | Stream-based ES |
| MartenDB.AspNetCore | ASP.NET Core integration | JasperFx | Active | MIT | Wires Marten + Wolverine |

### C.9 Workflow / sagas / state machines

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| MassTransit Sagas | Saga / state machine atop MassTransit | MassTransit | Active | Apache-2.0 | Automatonymous successor |
| NServiceBus Sagas | Built-in long-running process model | Particular Software | Active | Commercial | Persistent w/ timeouts |
| Brighter Sagas | Saga orchestration in Brighter | brighter org | Active | BSD-3 | Coordinated outbox |
| WorkflowCore | DSL-based workflow engine | Daniel Gerlag | Active | MIT | Persistent codified workflows |
| Elsa Workflows | Declarative + visual workflow engine | Elsa Workflows | Active | MIT | Designer-friendly, versioned |
| Stateless | Lightweight hierarchical state machine | dotnet-state-machine | Active | Apache-2.0 | Most-used .NET state machine |
| Appccelerate.StateMachine | State machine framework | Appccelerate | Slow | Apache-2.0 | Older alt to Stateless |
| Daenet.Stateless | Daenet's state machine | Daenet | Niche | MIT | |
| Optimajet.Workflow | Commercial workflow engine | Optimajet | Active | Commercial | Designer + persistence |
| Microsoft.DurableTask | Durable task framework, Durable Functions | Microsoft | Active | MIT | Backbone of Durable Functions |
| Microsoft Orleans Sagas | Orchestration via Orleans grains + timers | Microsoft | Active | MIT | Patterns rather than first-class |
| Marten Sagas | Saga state in Marten | JasperFx | Active | MIT | Use with Wolverine command bus |
| Cadence / Temporal SDK (.NET) | Durable workflow engines | community / Temporal Tech | Active | MIT | Distributed workflows |
| Saga via ABP Framework | ABP-integrated sagas | Volosoft | Active | LGPL-3.0 | Module within ABP |

### C.10 Background jobs / scheduling

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| Hangfire | Persistent background jobs (SQL/Redis storage, dashboard) | Sergey Odinokov | Active | LGPL-3.0 (Pro: commercial) | SQL Server, Postgres, Redis, MongoDB backends |
| Hangfire.Pro | Commercial features | Sergey Odinokov | Active | Commercial | Batches, throttling, advanced UI |
| Hangfire.PostgreSQL / .Redis.StackExchange / .MemoryStorage / .MongoDB | Storage providers | Various | Active | LGPL-3.0 | Production-ready alts |
| Quartz.NET | Cron-based scheduler | Quartz.NET community | Active | Apache-2.0 | Persistent, clustered |
| Coravel (+`.Mail`, `.Pro`, `.Cache`, `.Queue`, `.Cli`) | In-process scheduler + jobs + utilities | James Hickey | Active | MIT (Pro commercial) | Lightweight monolith fit |
| FluentScheduler | Fluent scheduling library | community | Slow | New BSD | Predates Coravel |
| NCronJob | Modern minimal API scheduler | Linkdotnet / NCronJob org | Active | MIT | DI-first cron jobs |
| Microsoft.Extensions.Hosting.BackgroundService | IHostedService base class | Microsoft | Active | MIT | Foundation |
| Topshelf | Windows service hosting | Topshelf community | Slow | Apache-2.0 | Console → Windows service |
| Cronos | Cron-string parsing library | HangfireIO | Active | MIT | Used by many schedulers |
| Quartzmin | Web admin for Quartz.NET | jlucansky | Slow | Apache-2.0 | Hangfire-style UI for Quartz |
| Wolverine.Scheduled | Scheduled message support | JasperFx | Active | MIT | Time-based dispatch |
| Tewr.Coravel | Coravel community extensions | Tewr | Niche | MIT | |
| ServiceStack.Jobs | ServiceStack-aligned jobs | ServiceStack | Active | Dual (AGPL/commercial) | |
| RecurrentTasks | Simple recurring task runner | community | Active | MIT | Basic timer scheduler |
| CrystalQuartz | Quartz UI dashboard | Aliaksei Bulhak | Active | MIT | |

### C.11 Distributed transactions / outbox

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| DotNetCore.CAP | Distributed transaction + outbox | Yang Zheng | Active | MIT | Multi-broker outbox + retries + inbox |
| Brighter Outbox | Outbox for Brighter pipelines | brighter org | Active | BSD-3 | DB ↔ broker coordination |
| MassTransit Transactional Outbox | EF Core / NHibernate outbox | MassTransit | Active | Apache-2.0 | First-class in MT v8 |
| NServiceBus Outbox | Built-in outbox feature | Particular Software | Active | Commercial | Persistence-aware dedup |
| Wolverine Transactional Outbox | Wolverine's built-in outbox | JasperFx | Active | MIT | EF/Marten integration |
| EFCore Outbox helpers (manual + community) | Custom interceptors + dispatch loops | community | Active | MIT | Roll-your-own |
| Marten Outbox | Built-in outbox in Marten | JasperFx | Active | MIT | Uses Wolverine for dispatch |

### C.12 Streaming / dataflow

| Lib | Niche | Maintainer | License |
|---|---|---|---|
| System.Threading.Channels | Producer/consumer channels | Microsoft | MIT |
| TPL Dataflow | Block-based dataflow | Microsoft | MIT |
| Reactive Extensions (Rx.NET) | Observables / operators | dotnet/reactive | MIT |
| System.Linq.Async | Async LINQ over IAsyncEnumerable | Microsoft / community | MIT |
| Akka.Streams | Reactive streams over Akka.NET | Akka.NET | Apache-2.0 |
| Streamiz.Kafka.Net | Kafka Streams implementation | LGouellec | Apache-2.0 |
| KafkaFlow | Kafka consumer/producer framework | farfetch | MIT |
| Apache.Pulsar / DotPulsar | Pulsar streaming | dotpulsar | Apache-2.0 |
| RxSocket / SocketIOClient | Reactive socket libs | community | MIT |
| Spectre.Pipelines | Pipeline composition | community | MIT |
| ZeroMQ via NetMQ | ZeroMQ binding | NetMQ community | LGPL-3.0 |
| RabbitMQ.Stream.Client | RabbitMQ stream protocol | RabbitMQ team | Apache-2.0 / MPL-2.0 |

### C.13 Specialty data / file formats

| Lib | Niche | License |
|---|---|---|
| CsvHelper | Industry-standard CSV reader/writer | MS-PL / Apache-2.0 |
| Sylvan.Data.Csv | High-perf CSV reader | MIT |
| Sep (nietras) | Cutting-edge CSV throughput | MIT |
| Parquet.Net | Apache Parquet reader/writer | Apache-2.0 |
| Apache.Arrow | Apache Arrow C# implementation | Apache-2.0 |
| EPPlus | XLSX manipulation | Polyform Noncommercial / Commercial |
| ClosedXML | OpenXML wrapper for XLSX | MIT |
| NPOI | OpenXML and HSSF for XLS/XLSX | Apache-2.0 |
| OpenXML SDK (DocumentFormat.OpenXml) | Microsoft's OpenXML SDK | MIT |
| ExcelDataReader | Lightweight read-only XLS/XLSX | MIT |
| FastMember | Fast member access | Apache-2.0 |
| iText 7 / iTextSharp | PDF generation | AGPL / Commercial |
| PdfSharp / MigraDoc.Foundation | OSS PDF | MIT |
| QuestPDF | Layout-based PDF generator | MIT (commercial above revenue threshold) |
| DinkToPdf | HTML→PDF via wkhtmltopdf | MIT |
| SelectPdf | Commercial HTML-to-PDF | Commercial / community |
| Aspose.{Cells, Words, Pdf, Slides, Total} | Comprehensive Office/PDF | Commercial |
| Spire.Office | Commercial Office tooling | Commercial |
| ChoEtl | ETL library for files | Apache-2.0 |
| YamlDotNet | YAML parser/emitter | MIT |
| Tomlyn | TOML parser | BSD-2 |
| MessagePack-CSharp / MemoryPack | High-perf binary serializers | MIT |
| Bond | Cross-language MS serializer | MIT |
| protobuf-net | Code-first protobuf | Apache-2.0 |
| Google.Protobuf | Official protobuf | BSD-3 |
| Apache.Avro | Avro serialization | Apache-2.0 |

### C.14 AI memory / vector DBs (data-side)

| Lib | Niche | License |
|---|---|---|
| Pinecone.NET | Pinecone vector DB SDK | Apache-2.0 |
| Weaviate.NET (via REST) | Weaviate client | BSD-3 |
| Qdrant.Client | Qdrant SDK (gRPC + REST) | Apache-2.0 |
| Milvus.Client | Milvus SDK | Apache-2.0 |
| Chroma.NET (community) | Chroma client | Apache-2.0 |
| Npgsql.Pgvector | pgvector EF Core / Npgsql | PostgreSQL |
| Redis Vector via NRedisStack | Redis vector search | MIT |
| Microsoft.SemanticKernel (+`.Memory.*`) | LLM orchestration + memory abstractions | MIT |
| Microsoft.KernelMemory | Hosted RAG memory service | MIT |
| LangChain.NET (tryAGI) | LangChain port | MIT |
| OpenAI.NET / OpenAI-DotNet | OpenAI SDKs (official + community) | MIT |
| Anthropic.NET | Claude API client | MIT |
| OllamaSharp | Local Ollama LLM client | MIT |
| LlamaSharp | LLaMA inference in-process | MIT |
| Azure.AI.OpenAI / AmazonBedrockRuntime / Google.Cloud.Vertex.AI | Hosted model SDKs | varies |
| ML.NET / TensorFlow.NET / TorchSharp / Microsoft.ML.OnnxRuntime | Classic + deep ML inference | MIT / Apache-2.0 |
| Microsoft.Extensions.AI | Unified AI abstractions (.NET 9) | MIT |
| LM.Kit | Commercial LLM SDK | Commercial |
| Cohere.NET / Mistral.NET / Replicate.NET | Provider-specific clients | MIT |

---

## §D. Survey deep-dive — Cross-cutting concerns inventory

### D.1 Validation — schema / fluent

| Lib | Niche | Maintainer | Active? | License | Notes |
|---|---|---|---|---|---|
| FluentValidation | Fluent rule-builder for POCOs/DTOs/commands | Jeremy Skinner | Active | Apache-2.0 | De-facto industry std. Rule chains, conditionals, async, child collections. AspNetCore + MediatR + MinimalAPI integrations |
| System.ComponentModel.DataAnnotations | Attribute-based validation | Microsoft | Active | MIT | `[Required]`, `[Range]`, `[StringLength]`, `[RegularExpression]`, `[EmailAddress]` |
| Microsoft.Extensions.Options.DataAnnotations | Bridges DA to `IValidateOptions<T>` | Microsoft | Active | MIT | `ValidateDataAnnotations()`, `ValidateOnStart()` |
| MiniValidation | Tiny single-file DA runner for Minimal APIs | Damian Edwards (MS) | Active | MIT | `MiniValidator.TryValidate(obj, out errors)` |
| MinimalApis.{Extensions, FluentValidation} | FV hooked into Minimal API endpoint filters | Damian Edwards / community | Active | MIT | `WithValidation<T>()` |
| Vogen (+`.Validation`) | Source-gen value objects with built-in `Validate()` | Steve Dunn | Active | Unlicense | No primitive obsession |
| JsonSchema.Net | JSON Schema 2020-12 / draft-07 validator | Greg Dennis | Active | MIT | OpenAPI flavored schemas |
| NJsonSchema | JSON Schema generation + validation | Rico Suter | Active | MIT | Used by NSwag |
| Json.Schema.Generation | Schema code-first generator | Greg Dennis | Active | MIT | Companion to JsonSchema.Net |
| Manatee.Json.Schema (legacy) | Older JSON Schema validator | Greg Dennis | Archived | MIT | Superseded |
| FlexValidator | Niche fluent alt | community | Stale | MIT | |
| Niqlin.Validation / ValidationFlow | Niche fluent alts | community | Stale / Niche | MIT | |

Guard clauses: **Ardalis.GuardClauses** (Steve Smith, MIT, popular), **EnsureThat** (Daniel Wertheim, MIT), **Dawn.Guard** (Şafak Gür, Apache-2.0), **Throw** (Amichai Mantinband, MIT, modern fluent), **GuardAgainst**, **LightGuard**, **AnyofThem**.

ASP.NET pipelines: **Hellang.Middleware.ProblemDetails** (RFC 7807 mapping + FV integration), **MediatR.Extensions.FluentValidation** (`ValidationBehavior<TRequest,TResponse>`), **Sieve** / **AutoFilterer** (filter validation), **FluentValidation.AspNetCore** (legacy / deprecated).

### D.2 Object mapping

Reflection/runtime: **AutoMapper** (Jimmy Bogard / LuckyPenny, **commercial license from 2025+**), **AgileObjects.AgileMapper** (MIT), **ExpressMapper** (stale, MIT), **EmitMapper** (stale, LGPL), **TinyMapper** (stale, MIT).

Source-gen: **Mapster** (Chaowlert Chaisrichalermpol, MIT, hybrid), **Riok.Mapperly** (Apache-2.0, pure source-gen — generally fastest, AOT-safe), **NextGenMapper** (Anton Krasilov, MIT, simpler API).

Manual: hand-written `ToDto()`/`ToEntity()` extension methods, `IEnumerable.ToDictionary/ToList/ConvertAll`, **Microsoft.OpenApi** DOM mapping.

### D.3 Resilience / fault tolerance

| Lib | Niche | License | Notes |
|---|---|---|---|
| Polly | Resilience policies — retry, breaker, bulkhead, timeout, fallback, hedging | BSD-3 | v8 ResiliencePipeline; foundation for `Microsoft.Extensions.Http.Resilience` |
| Polly.Contrib.{WaitAndRetry, MutableBulkheadPolicy, DuplicateRequestCollapser, LoggingPolicy, SimmyChaos, AzureKeyVaultJitter} | Add-ons (chaos, collapsing, jitter) | BSD-3 | Many superseded by v8 built-ins |
| Polly.Caching.Memory / Polly.Caching.Distributed | In-memory / IDistributedCache cache policy | BSD-3 | Redis/SQL via IDistributedCache |
| Polly.RateLimit | Polly v7 rate-limit (replaced) | BSD-3 | Replaced by `System.Threading.RateLimiting` |
| Microsoft.Extensions.Resilience | MS resilience packaging on Polly v8 | MIT | Standardized pipelines + telemetry |
| Microsoft.Extensions.Http.Resilience | HttpClientFactory resilience handlers | MIT | `AddStandardResilienceHandler()`, `AddStandardHedgingHandler()` |
| Steeltoe.CircuitBreaker.Hystrix (legacy) | Hystrix-style breaker | Apache-2.0 | Largely displaced by Polly |
| Akka.Util retry helpers | Resilience inside Akka | Apache-2.0 | Actor-supervision-based |
| App.Metrics | Resilience metrics + quotas | Apache-2.0 | Mostly historical |
| Hyperion (Akka) | Akka serialization w/ retry semantics | Apache-2.0 | |
| Resilience4Net / Open.Resilience | Niche helpers | MIT | Rare |

### D.4 Logging — providers + sinks

Core: `Microsoft.Extensions.Logging` family (see §A.11).

**Serilog ecosystem** (Apache-2.0): `Serilog`, `Serilog.AspNetCore`, `Serilog.Settings.Configuration`. Sinks: `Console`, `File`, `Async`, `Seq`, `Elasticsearch`, `Elastic.Serilog.Sinks` (modern), `Grafana.Loki`, `Splunk`, `Datadog.Logs`, `NewRelic.Logs`, `Sentry`, `GoogleCloudLogging`, `ApplicationInsights`, `AzureAnalytics`, `AwsCloudWatch`, `AmazonKinesis`, `MSSqlServer`, `Postgresql`, `MongoDB`, `Email`, `RabbitMQ`, `Network` (TCP/UDP/Syslog). Enrichers: `Environment`, `Process`, `Thread`, `Span` (OTel), `CorrelationId`. Destructuring: `Destructurama.Attributed`, `Destructurama.JsonNet`, `Destructurama.ByIgnoringProperties`. Plus `Serilog.Expressions` for filtering/templating.

Other frameworks: **NLog** (+`.Web.AspNetCore`, `.Targets.Seq`) — BSD-3; **log4net** (Apache-2.0, legacy); **Loupe.Agent** (commercial); **Lurgle** (Apache-2.0, simpler Serilog setup); **ZLogger** (Cysharp, MIT, zero-allocation, interpolated handlers); **MEL.Source.Generator** `[LoggerMessage]` (Microsoft, MIT, high-perf static log methods); **Sentry.Extensions.Logging** (MIT); **Octolog** (niche).

### D.5 Observability — OpenTelemetry + APMs

OpenTelemetry .NET (Apache-2.0): `OpenTelemetry`, `.Api`, `.Extensions.Hosting`. Auto-instrumentation: `OpenTelemetry.Instrumentation.{AspNetCore, Http, GrpcNetClient, SqlClient, EntityFrameworkCore, StackExchangeRedis, Runtime, Process, EventCounters, MongoDB.Driver, Confluent.Kafka, Quartz, Hangfire, Elasticsearch, Wcf, MassTransit}`. Exporters: `OpenTelemetry.Exporter.{OpenTelemetryProtocol (OTLP), Console, Prometheus.AspNetCore, Zipkin, Jaeger (legacy)}`; `Azure.Monitor.OpenTelemetry.{Exporter, AspNetCore}`; `Datadog.Trace.OpenTelemetry`; `Honeycomb.OpenTelemetry`; `Splunk.OpenTelemetry.AutoInstrumentation`; `Elastic.OpenTelemetry`; Grafana distro.

Other observability: **App.Metrics** (+`.AspNetCore`, Apache-2.0, stale); **prometheus-net** (+`.AspNetCore`, `.DotNetRuntime`, MIT); **Datadog.Trace** (Apache-2.0, native APM); **Elastic.Apm** (+`.AspNetCore`, Apache-2.0); **NewRelic.Agent.Api** (Apache-2.0); **Sentry** (+`.AspNetCore`, MIT); **Microsoft.ApplicationInsights** (+`.AspNetCore`, `.WorkerService`, MIT, maintenance); **Microsoft.Diagnostics.Tracing.TraceEvent** (MIT); built-in `Activity`, `ActivitySource`, `Meter`, `EventSource`, `EventCounter`. CLIs: `dotnet-counters`, `dotnet-trace`, `dotnet-monitor`, `dotnet-dump`, `dotnet-gcdump`, `dotnet-stack`.

### D.6 Health checks (community Xabaril pack)

Microsoft core: `Microsoft.{Extensions, AspNetCore}.Diagnostics.HealthChecks`, `.HealthChecks.EntityFrameworkCore`.

Xabaril `AspNetCore.HealthChecks.*` (Apache-2.0, 70+ providers): UI (+`.Client`, `.InMemory.Storage`, `.{SqlServer,PostgreSQL,MySql,SQLite}.Storage`); DB checks (`SqlServer`, `Npgsql`, `MySql`, `Sqlite`, `Oracle`); cache/messaging (`Redis`, `Stackexchange.Redis`, `RabbitMQ`, `Kafka`, `IbmMQ`, `Nats`, `EventStore`); Azure (`AzureServiceBus`, `AzureStorage`, `AzureKeyVault`, `AzureCosmosDb`, `AzureSearch`, `AzureApplicationInsights`, `AzureDigitalTwin`, `AzureIoTHub`); AWS (`Aws.S3`, `.SecretsManager`, `.SystemsManager`, `.SQS`, `.SNS`, `.DynamoDb`); Kubernetes; Network (TCP/Ping/SMTP/IMAP/DNS); MongoDb; OpenIdConnect / IdentityServer; Hangfire; Elasticsearch; Solr; Uris; SendGrid; SignalR; ArangoDb; RavenDB; CosmosDb; OpenTelemetry; System (disk/mem/process); Publishers (Prometheus pushgateway, ApplicationInsights, Datadog, Seq, CloudWatch).

### D.7 Security / cryptography / secrets

DataProtection (MS): `Microsoft.AspNetCore.DataProtection` + Azure KV / AzureStorage / Redis / EF Core / StackExchangeRedis persistence. Antiforgery, KeyDerivation (PBKDF2).

Crypto libs: **NSec.Cryptography** (libsodium-based, MIT — Ed25519, X25519, AEAD, HKDF); **BouncyCastle.Cryptography** (modern official) + legacy **BouncyCastle.NetCore**; **libsodium-net** (ISC, stale); **Konscious.Security.Cryptography** (Argon2 + Blake2, MIT); **Microsoft.IdentityModel.Tokens** + `System.IdentityModel.Tokens.Jwt` (MIT, JWT); **paseto-dotnet** (MIT, alt to JWT); built-in `ChaCha20Poly1305`, `X509Chain`; **NIST-PQC** experimental via BC or NSec.

Secret stores: `Azure.Security.KeyVault.{Secrets, Keys, Certificates}` + `Azure.Extensions.AspNetCore.Configuration.Secrets`; `AWSSDK.SecretsManager` + `Kralizek.Extensions.Configuration.AWSSecretsManager`; `Google.Cloud.SecretManager.V1`; **VaultSharp** (Apache-2.0); HashiCorp `Vault.Provider` for IConfiguration; `Aliyun.Acs.KMS`.

Authn/Identity: `Microsoft.Identity.Web` (+`.MicrosoftGraph`); **IdentityModel.OidcClient** (Duende, Apache-2.0); **IdentityModel** (foundation); **Auth0.AspNetCore.Authentication**; **Okta.AspNetCore**; **Sustainsys.Saml2** + **ITfoxtec.Identity.Saml2** (LGPL); **OpenIddict** (Apache-2.0); **Duende.IdentityServer** (commercial); **IdentityServer4** (legacy, Apache-2.0). MFA: **Otp.NET** (TOTP/HOTP RFC 6238/4226), **QRCoder**, **Fido2.AspNetCore** (`Fido2NetLib`, MIT, WebAuthn / passkeys), **FidoBlazor**.

### D.8 Rate limiting / throttling

Built-in (Microsoft, MIT): `System.Threading.RateLimiting` (FixedWindow, SlidingWindow, TokenBucket, Concurrency), `Microsoft.AspNetCore.RateLimiting` middleware, `Microsoft.AspNetCore.ConcurrencyLimiter` (legacy).

Community: **AspNetCoreRateLimit** (Stefan Prodan, MIT, maintenance), **Polly.RateLimit** (BSD-3, stale), **RateLimiter** (David Desmaisons, MIT, stale), **Redis.RateLimit** / custom Lua, **FusionCache** (compose for distributed rate-limit pattern), **HybridRateLimit** (memory + Redis).

### D.9 Configuration / options (community)

**NetEscapades.Configuration.Yaml** (Andrew Lock, MIT); **Tomlyn.Extensions.Configuration** (Alexandre Mutel, BSD-2); **Configuration.Validation**; **FluentValidation.Options**; **Spectre.Console.Cli** (CLI config); **Steeltoe.Configuration.{CloudFoundry, ConfigServer, Kubernetes}** (Apache-2.0); **VaultSharp.Extensions.Configuration**; **Aws.AppConfig.Extensions.Configuration**; **Google.Cloud.Configuration**.

Feature flags: see §B.14 + § Feature flags.

### D.10 Errors / Result types / DUs

ProblemDetails: **Hellang.Middleware.ProblemDetails** (Henrik Lau Eriksson, MIT — exception → status mapping with FV integration); built-in `Microsoft.AspNetCore.Mvc.ProblemDetails` (`AddProblemDetails()`, .NET 7+).

Result types: **OneOf** (+`.SourceGenerator`, +`OneOfFx`, MIT, Harry McIntyre — DU via `OneOf<T1..Tn>`); **ErrorOr** (Amichai Mantinband, MIT — Result + error union, MediatR-friendly); **FluentResults** (Michael Altmann, MIT — Result with reasons hierarchy); **CSharpFunctionalExtensions** (Vladimir Khorikov, MIT — `Result<T>`, `Maybe<T>`, value object base, DDD-friendly); **LanguageExt.Core** (Paul Louth, MIT — Either/Option/Validation, monads); **Optional** (stale, MIT); **Maybe.Net** (niche); **Ardalis.Result** (MIT, Steve Smith).

Strongly-typed primitives: **Vogen** (SteveDunn, Unlicense); **StronglyTypedId** (Andrew Lock, MIT); **ValueOf** (Mehdi El Gueddari, stale); **Ardalis.SmartEnum** + **NickStrupat.EFCore.SmartEnum** + **NetCore.Enum.SmartEnum.Generators**; idiomatic Enumeration class.

### D.11 Serialization (extra to STJ)

Already covered extensively in §A.15, §C.13, §4.18. Headlines: **Newtonsoft.Json** (MIT, maintenance); **MessagePack-CSharp** (Cysharp, MIT, fast binary, source-gen); **MemoryPack** (Cysharp, MIT, fastest .NET serializer); **protobuf-net** (Apache-2.0, code-first protobuf); **Google.Protobuf** (BSD-3); **ServiceStack.Text** (commercial/AGPL); **Jil**, **Utf8Json** (legacy/EOL). JSON path/patch/query: **JsonPath.Net**, **JsonPatch.Net**, **JsonPointer.Net**, **JmesPath.Net**, **Json.Diff** (all Greg Dennis, MIT). NodaTime serialization: `NodaTime.Serialization.SystemTextJson` and `.JsonNet`.

### D.12 DDD / DI / mediator / AOP

Mediator/CQRS: **MediatR** (Active, **commercial 12.0+**, Apache-2.0 older); **Mediator** (martinothamar, MIT, source-gen); **Brighter** (BSD-3, Command Processor + messaging); **Wolverine** (MIT, codegen mediator + messaging); **MassTransit.Mediator** (Apache-2.0); **Ardalis.{Specification, ApiEndpoints, Result, SmartEnum}**; **FastEndpoints**; **OpenCqrs** (niche).

DI containers (community): **Autofac** (+`.Extensions.DependencyInjection`, MIT); **SimpleInjector** (Steven van Deursen, MIT — strict graph verification); **Lamar** (Jeremy Miller, MIT — StructureMap successor); **DryIoc** (MIT — high-perf, advanced lifetimes); **LightInject** (Bernhard Richter, MIT — small footprint); **Castle.Windsor** (Apache-2.0, legacy but active); **Ninject** (Apache-2.0, maintenance); **StructureMap** (EOL); **Stashbox** (Peter Csajtai, MIT); **Grace** (Ian Johnson, MIT); **ZenInject/Zenject/Extenject** (game-engine focused); **Jab** (Anton Korotkov, Apache-2.0, source-gen, AOT-friendly); **StrongInject** (YairHalberstadt, MIT, source-gen, stale); **Pure.DI** (DevTeam, MIT, source-gen graph generation, no container).

AOP/interception: **Castle.{DynamicProxy, Core}** (Apache-2.0); **System.Reflection.DispatchProxy** (built-in); **Autofac.Extras.DynamicProxy**; **MethodBoundaryAspect.Fody** (compile-time advice); **Fody** (IL weaver platform, MIT, many plugins); **Aspectize** (stale); **NConcern** (LGPL); **Metalama** / **PostSharp Technologies** (commercial / free tier — modern AOP via source-gen); **PostSharp** (commercial, legacy); **AutoCtor**, **AutoConstructor** (source-gen ctor boilerplate); **Polly.Roslyn** analyzers; **Microsoft.CodeAnalysis** (Roslyn — IIncrementalGenerator).

Modular monolith helpers: **MicroElements.Modules**, **Modules.Core** (community, often hand-rolled).

### D.13 Time / date

**NodaTime** (+`.Serialization.SystemTextJson`, `.JsonNet`, `.Testing` — Jon Skeet, Apache-2.0); **Microsoft.Bcl.TimeProvider** (polyfill); **System.TimeProvider** (.NET 8+); **Microsoft.Extensions.TimeProvider.Testing** (`FakeTimeProvider`); **TimeZoneConverter** (Matt Johnson-Pint, MIT — Win↔IANA); **TimeZoneNames** (MIT); **Cronos** (HangfireIO, MIT); **Quartz CronExpression**; **NCrontab** (Atif Aziz, Apache-2.0); **Humanizer** (+`.Core`, Mehdi Khalili, MIT — date phrases, ordinals, byte sizes); **Iso8601DurationHelper**.

### D.14 Specifications / dynamic queries / filters

**Ardalis.Specification** (+`.EntityFrameworkCore`, Steve Smith, MIT — composable specs, paging, includes); **NSpecifications** (stale); **Sieve** (Bilal Fakhouri, Apache-2.0); **Gridify** (Alirezanet, MIT — dynamic filtering DSL); **AutoFilterer** (Enisn, Apache-2.0); **System.Linq.Dynamic.Core** (ZZZ Projects, Apache-2.0); **DynamicQueryable** (legacy); **linq2db.Linq**; **LinqKit** (+`.Microsoft.EntityFrameworkCore`, LGPL-2.1).

### D.15 AOP / source-gen continued

**Castle.DynamicProxy** (most-consumed runtime AOP); **Microsoft.CodeAnalysis.Analyzers**; **IIncrementalGenerator** (Roslyn modern source-gen); **AutoCtor**, **AutoConstructor** (Lombok-style); **MethodDecorator** (Fody); **Fody**, **Fody.PropertyChanged**; **Metalama / PostSharp.Metalama** (commercial/Free tier); **Aspectize**, **NConcern** (stale/niche); **Mediator** (martinothamar, source-gen mediator); **Polly.Roslyn**; **Mapperly**, **Vogen** (compile-time mappers / value objects).

### D.16 Audit / change tracking — Audit.NET ecosystem

Federico Colombo's family (MIT): `Audit.NET`, `Audit.NET.{SqlServer, PostgreSql, MySql, MongoDB, AzureCosmos, AzureStorage, AzureStorageBlobs, AzureStorageTables, AmazonQLDB, DynamoDB, Redis, Elasticsearch, RavenDB, Files, NLog, Log4net, Serilog, Kafka, RabbitMQ}`, `Audit.{EntityFramework, EntityFramework.Core, EntityFramework.Identity, WebApi, WebApi.Core, Mvc, Mvc.Core, SignalR, HttpClient, WCF, WCF.Client, FileSystem}`.

EF Core change-tracking / temporal / soft-delete: **EntityFrameworkCore.Triggered** (MIT), **Z.EntityFramework.Plus** (Commercial/Free tiered), **EFCore.AuditBase**, **EFCoreSecondLevelCacheInterceptor**, **SoftDeleteServices** (Jon P Smith, MIT), **EntityFramework.Filters** (MS-PL, stale), built-in EF Temporal Tables (.NET 6+), **SqlServerTemporalSupport**, idiomatic AuditableEntities pattern.

### D.17 Validation pipelines / contract enforcement

`System.ComponentModel.DataAnnotations.Validator`; FluentValidation `IPipelineBehavior<,>` pattern; MediatR `RequestPreProcessorBehavior`; **MinimalApis.FluentValidation** + **MinimalApis.Extensions.Validation**; **Ardalis.GuardClauses**; **FastEndpoints** built-in validators; FluentValidation.AspNetCore.Filters (manual); built-in `Microsoft.AspNetCore.Mvc.ModelStateValidation` + `ApiBehaviorOptions`; OpenAPI request validator (NSwag/Swashbuckle.Schemas).

---

## §E. Survey deep-dive — Architecture/Testing/AI/Tooling inventory

### E.1 Architectural philosophies — concept summary

Already enumerated structurally in §1; agent-5's contribution adds these mental-model headlines for fast lookup:

- **Clean / Onion / Hexagonal** — concentric domain at center, deps pointing inward (Uncle Bob / Palermo / Cockburn).
- **Vertical Slice** — feature-folders with handler+validator+DTO+tests co-located (Bogard).
- **Modular Monolith vs Microservices** — single deployable with internal module boundaries vs independently deployable services.
- **DDD strategic** (bounded contexts, ubiquitous language, context maps) + **DDD tactical** (aggregates, entities, VOs, repos).
- **CQRS / Event Sourcing / Domain Events / Integration Events / Saga / Outbox-Inbox** — separation, persistence-as-events, cross-service notifications, long-running coordination, transactional messaging.
- **REST / REST+HATEOAS / GraphQL (schema-first vs code-first) / gRPC / SignalR / SSE / Pub-sub / Actor model / Reactive / FaaS** — communication styles.
- **BFF / API Gateway / Service Mesh / Polyrepo vs Monorepo / 12-Factor / Strangler Fig / ACL** — topology / migration patterns.
- **Resilience patterns**: Pipes & Filters, Bulkhead, Circuit Breaker, Retry, Timeout, Hedging, Outbox, Inbox, Idempotency keys.

### E.2 Reference architectures & templates

| Reference | Owner | What it shows | Status |
|---|---|---|---|
| eShopOnWeb | Microsoft (Steve Smith) | Monolithic Clean Architecture | Active |
| eShopOnContainers | Microsoft | Microservices w/ Docker, polyglot persistence, EventBus | Archived (superseded) |
| eShop (new) | Microsoft | Modern .NET Aspire reference for distributed apps | Active |
| eShopOnDapr | Microsoft / community | eShop on Dapr building blocks | Active |
| .NET Microservices Architecture Reference | Cesar de la Torre | Free e-book + sample for .NET microservices | Active |
| .NET Aspire starter templates | Microsoft | Distributed-app orchestration with OTel out-of-box | Active |
| ardalis/CleanArchitecture | Steve Smith | Most-used Clean Arch template | Very active |
| jasontaylordev/CleanArchitecture | Jason Taylor | CQRS + MediatR + Clean Arch + Identity + Angular FE | Very active |
| Modular Monolith with DDD | Kamil Grzybek | Banking sample showing module boundaries | Active reference |
| Modular Monolith Saas | Kamil Grzybek | Multi-tenant modular monolith | Active reference |
| Brighter samples | Brighter project | Command Processor + Outbox tutorial | Active |
| Steeltoe samples | VMware Tanzu | Spring Cloud parity for .NET | Active |
| Orleans Samples | Microsoft | Virtual actor model demos | Active |
| NetCorePal | Chinese OSS community | Opinionated DDD framework with code generators | Active |
| ABP Framework / abp.io | Volosoft | Full app framework: tenancy, identity, audit, modules | Commercial + OSS |
| OrchardCore | OrchardCMS team | Modular CMS engine, multi-tenant by default | Active |
| SmartStore.NET / nopCommerce | E-commerce platforms | Plugin-based commerce | Active |
| DotNetNuke (DNN) / Umbraco / Sitefinity | CMS / DXP | Long-running portal/CMS engines | Active |
| dotnet-architecture/eShop | Microsoft | Modern reference replacing eShopOnContainers | Active |
| VerticalSliceArchitecture template | Jimmy Bogard | Slice-based code organization | Active |
| DotnetDevops template | Various | GH Actions + Bicep/Terraform + .NET | Mixed |
| DDDForum (Khalil Stemmler) | Khalil Stemmler | TS but conceptual canon for tactical DDD | Inactive but referenced |

### E.3 Test frameworks & utilities

| Lib | Niche | Active? | License | Notes |
|---|---|---|---|---|
| xUnit.net (+ v3, .runner.visualstudio) | De-facto modern unit framework | Very active | Apache-2.0 | v3 in active dev |
| NUnit (+`NUnit3TestAdapter`) | Classic attribute-rich framework | Active | MIT | SetUp/TearDown semantics |
| MSTest (+`MSTest.{TestFramework, TestAdapter, Sdk}`) | Microsoft's unit framework | Active | MIT | Competitive after v3 rewrite |
| TUnit | New source-generated tester | Active | MIT | Compile-time discovery, async-first, faster than xUnit |
| Microsoft.NET.Test.Sdk + Microsoft.Testing.Platform (+`.MSBuild`, `.Extensions.{CodeCoverage, Retry, Telemetry}`) | Test host runner + new unified platform | Active | MIT | dotnet test integration |
| LightBDD | Lightweight structured BDD | Active | MIT | No Gherkin |
| SpecFlow | Cucumber/Gherkin BDD | **Discontinued 2024** | Apache-2.0 | Replaced by Reqnroll |
| Reqnroll | SpecFlow successor (community fork) | Very active | BSD-3 | Drop-in replacement |
| xBehave.net | xUnit-flavored BDD | Maintenance | MIT | Niche |

Assertions/snapshots: **FluentAssertions** (Dennis Doomen — license **changed to Xceed/paid for v8+** in 2025; v7 free); **Shouldly** (BSD-2, friendlier alt); **NFluent** (Apache-2.0); **AwesomeAssertions** (FluentAssertions OSS fork after license change); **Verify** (Simon Cropp, MIT, dominant snapshot tool — `VerifyXunit/NUnit/MSTest/TUnit` + `.EntityFramework`, `.Http`, `.AspNetCore`); **Snapshooter** (SwissLife OSS); **ApprovalTests.NET** (Kent Beck approval testing); **Snapper**; **DiffPlex** (used by Verify).

Mocking: **Moq** (cautious — "SponsorLink" controversy 4.20.0–4.20.2); **NSubstitute** (BSD, friendlier API, modern preference); **FakeItEasy** (MIT, fluent fake); **JustMock Lite / JustMock** (Telerik/Progress, free Lite + commercial); **TypeMock Isolator** (commercial unrestricted — mocks anything); **Microsoft.Reactive.Testing** (test schedulers for Rx).

Test data: **AutoFixture** (+`.AutoMoq/.AutoNSubstitute/.AutoFakeItEasy`, MIT); **Bogus** (Brian Chavez, MIT — Faker.js port, locale-aware); **Faker.Net** (maintenance); **NBuilder** (maintenance); **ObjectsComparer** (MIT — deep equality).

Property-based: **FsCheck** (BSD, F#-rooted), **CsCheck** (Apache-2.0, C#-first), **Hedgehog** (BSD, integrated shrinking).

Mutation: **Stryker.NET** (Apache-2.0).

Coverage: **Coverlet** (MIT, default for `dotnet test --collect`); **ReportGenerator** (Daniel Palme, Apache-2.0); **JetBrains.dotCover** (commercial); **OpenCover** (maintenance).

Integration / web: **Microsoft.AspNetCore.Mvc.Testing** (`WebApplicationFactory<T>`); **Microsoft.AspNetCore.TestHost**; **Microsoft.EntityFrameworkCore.{InMemory, Sqlite}** (Sqlite preferred for relational behavior); **Respawn** (Jimmy Bogard, Apache-2.0 — DB reset between tests); **Testcontainers for .NET** (MIT — Postgres, MySQL, Mongo, Redis, Rabbit, Kafka, Localstack, Azurite, Cosmos emulator); **Aspire.Hosting.Testing** (.NET Aspire integration test harness); **WireMock.Net** (Apache-2.0); **Stubbery**; **MockHttp** (`Richard.Szalay.MockHttp`); **HttpClientInterception** (Just Eat Takeaway, Apache-2.0); **PactNet** (MIT — consumer-driven contract testing).

Load/perf: **NBomber** (Apache-2.0, code-defined load in C#); **BenchmarkDotNet** (MIT, .NET Foundation, industry std); **Microsoft.Crank** (MIT — used by ASP.NET team); **Bombardier** (lightweight HTTP); **k6** (AGPL, mention); **Apache JMeter** (Apache-2.0); **Gatling** (Apache-2.0); **Artillery** (MPL-2.0).

### E.4 Multi-tenancy

| Lib | Niche | Active? | License | Notes |
|---|---|---|---|---|
| Finbuckle.MultiTenant | De-facto .NET multi-tenancy lib | Active | Apache-2.0 | Resolution (host/header/route/claim), per-tenant DbContext, options, identity |
| ABP.MultiTenancy (in `Volo.Abp`) | Multi-tenancy module of ABP | Active | LGPL / commercial | Tied to ABP framework |
| OrchardCore.Tenants | Per-tenant isolated apps | Active | BSD-3 | |
| SaasKit.Multitenancy | Older multi-tenancy kit | Inactive | MIT | Predecessor of Finbuckle |
| EF Core query filters | First-party row filtering | Active | MIT | Foundation for per-row tenancy |
| NeinLinq | LINQ rewriting (helpful for tenant filters) | Active | MIT | Composable predicate injection |
| GraphQL.Multitenant (HotChocolate-side) | Per-tenant schema variants | Mixed | MIT | Often hand-rolled |

Strategies: **Per-database (silo)** · **Per-schema (bridge)** · **Per-row (pool)** · **Hybrid**.

### E.5 Billing / payments

| Lib | Niche | Active? | License | Notes |
|---|---|---|---|---|
| Stripe.net | Official Stripe SDK | Very active | Apache-2.0 | Default for SaaS billing |
| Paddle.NET (community) | Paddle Billing | Mixed | MIT | No first-party .NET |
| Braintree | Official Braintree SDK | Active | MIT | PCI offload |
| Adyen.NET | Official Adyen SDK | Active | MIT | Strong for global card |
| ChargeBee.SDK | Subscription billing | Active | MIT | |
| Recurly (`recurly-client-net`) | Subscription billing | Active | MIT | |
| Square.Connect / Square.Net | Square SDK | Active | Apache-2.0 | POS + online |
| Klarna.Net (community) | Klarna API wrappers | Mixed | MIT | No first-party .NET |
| PayPalCheckoutSdk | PayPal Orders v2 | Active | Apache-2.0 | v1 SDKs deprecated |
| Coinbase.Commerce | Crypto payments | Mixed | MIT | Fringe |
| SubscriptionsKit (various) | Wrapper utilities | Mixed | MIT | Trial/prorate/dunning helpers |

### E.6 Communication — email / SMS / push / chat

Email: **MailKit** + **MimeKit** (Jeffrey Stedfast, MIT — replacement for legacy `SmtpClient`); **FluentEmail.\*** (Razor + multi-sender SMTP/SendGrid/Mailgun/SES); **SendGrid** (Twilio); **MailgunSharp** / **Mailgun.Net** (community); **Postmark.Client** (premium transactional); **AWSSDK.SimpleEmail/V2**; **Azure.Communication.Email**; **MailtrapClient** (dev/staging capture).

SMS / Voice: **Twilio** (default); **Vonage** (Nexmo successor); **Plivo.Net**; **MessageBird.Net** (EU-friendly); **Azure.Communication.Sms**; **AWSSDK.Pinpoint** (push+SMS+email); **Sinch.SDK**.

Push: **FirebaseAdmin** (FCM); **OneSignal** (`OneSignal.RestAPIv3.Client`); **dotAPNS** (Apple HTTP/2 with JWT); **PushSharp** (legacy, inactive); **Pushover.NET** (niche); **Azure.Messaging.Notifications** (Hubs).

Chat: **Microsoft.AspNetCore.SignalR** (+`.Client`); **Azure.Communication.Chat**; **Twilio.Conversations**; **Stream.Chat.NET**; **Sendbird.Net** (community); **WebSocketSharp-netstandard** (legacy).

### E.7 Geo / IP / maps

| Lib | Niche | Active? | License | Notes |
|---|---|---|---|---|
| NetTopologySuite | Geometry types + spatial ops | Very active | BSD-3 | The .NET JTS port; integrates with EF Core spatial |
| Geocoding.Net | Multi-provider geocoding facade | Active | MIT | Google, Bing, Yahoo, Mapquest |
| GoogleApi | Google Maps/Places/Directions | Active | MIT | Multi-API wrapper |
| Bing.Maps.REST.Client | Bing Maps REST | Mixed | MIT | Bing Maps deprecating in 2025 |
| MaxMind.GeoIP2 | GeoIP2 SDK | Active | Apache-2.0 | Default for IP→geo |
| IPInfo.io.Client (`IPinfo.api`) | IPInfo client | Mixed | MIT | Alternative to MaxMind |
| Nominatim.NET (community) | OSM geocoding | Mixed | MIT | Free OSM-backed |
| Mapbox.NET (community) | Mapbox API | Mixed | MIT | No first-party .NET |
| OpenLocationCode.Net | Plus Codes | Active | Apache-2.0 | Address-free location codes |
| H3.Net | Uber H3 hex grid | Active | Apache-2.0 | Spatial indexing |
| Geohash.Net | Geohash encoder | Active | MIT | Classic spatial hashing |
| ProjNet (`ProjNet4GeoAPI`) | Coordinate projections | Active | LGPL | SRID conversions |
| BAMCIS.GeoJSON / GeoJSON.Net | GeoJSON parsing | Active | MIT | (de)serialize GeoJSON |
| Microsoft.Spatial | OData spatial types | Active | MIT | OData-flavored geometry |

### E.8 Image / media / documents

Images: **SixLabors.ImageSharp** (+`.Web`, Apache-2.0 / commercial for orgs >1M$ — replacement for `System.Drawing.Common` on Linux); **SkiaSharp** (MIT, Skia bindings); **Magick.NET** (Apache-2.0, ImageMagick — format coverage > anyone); **NetVips** (MIT, libvips bindings — memory-efficient); **System.Drawing.Common** (legacy, Win-only post-.NET 6).

Video/audio: **FFMpegCore** (rosenbjerg, MIT); **Xabe.FFmpeg** (LGPL); **NReco.VideoConverter** (LGPL, maintenance); **NAudio** (Mark Heath, MIT, audio toolbox); **LibVLCSharp** (LGPL, VLC bindings).

PDF & Office: **QuestPDF** (modern declarative, MIT free under thresholds / commercial — current best PDF in .NET); **iText7** (AGPL/commercial — comprehensive but viral license); **iTextSharp** (legacy); **PdfSharp** + **MigraDoc** (MIT, OSS); **DinkToPdf** (MIT, wkhtmltopdf wrapper, maintenance); **SelectPdf** (commercial, free community); **Aspose.{PDF, Words, Cells, Slides}** + **Spire.{PDF, Office}** + **GemBox.{Document, Spreadsheet, Email}** (commercial Office suites); **DocumentFormat.OpenXml** (Microsoft, MIT — raw OOXML); **ClosedXML** (MIT, friendlier xlsx); **EPPlus** (Polyform commercial 5+, free LGPL ≤4.5); **MiniExcel** (Apache-2.0, streaming for large sheets); **DocX** (MS-PL, lightweight Word); **HtmlAgilityPack** (MIT); **AngleSharp** (MIT, modern HTML/CSS/SVG parser); **Markdig** (BSD-2, fastest .NET Markdown); **MarkdownLog** (niche).

Code/compression: **Microsoft.CodeAnalysis** (Roslyn, MIT); **Microsoft.CodeAnalysis.CSharp.Scripting**; **SharpZipLib** (MIT); built-in `System.IO.Compression`; **DotNetZip** (`Ionic.Zip`, MS-PL, maintenance).

### E.9 AI / ML / LLM / vector

Foundation frameworks: **Microsoft.Extensions.AI** (+`.Abstractions`, Microsoft, MIT — first-party LLM abstraction in .NET 9, the unifying interface); **Microsoft.SemanticKernel** (+`.Agents.*`, MIT, plugin-driven LLM orchestration); **Microsoft.KernelMemory** (RAG pipeline standalone); **Microsoft.AutoGen** (multi-agent framework port); **LangChain** (`tryAGI/LangChain`, MIT, less mature than Python); **LangChainSharp** (earlier port, niche).

Provider SDKs: **OpenAI** (official .NET SDK); **OpenAI-DotNet** (community, predates official); **Azure.AI.OpenAI** (Microsoft); **Anthropic.SDK** (tghamm, Claude); **AnthropicClient** (alt community); **AWSSDK.BedrockRuntime** (multi-model on AWS); **Mscc.GenerativeAI** (Gemini wrapper); **Google.Cloud.AIPlatform.V1** (Vertex); **Cohere.NET**; **Mistral.NET**; **OpenRouter.Client** (cross-provider gateway); **OllamaSharp** (Andreas Wäscher, local Ollama); **LLamaSharp** (SciSharp, llama.cpp bindings, on-device).

Vector stores: **Pinecone.NET** (Apache-2.0); **Weaviate.NET** (MIT); **Qdrant.Client** (first-party Apache-2.0); **Milvus.Client** (Apache-2.0); **ChromaDB.Client** (Apache-2.0, mixed activity); **Pgvector** (`Pgvector.EntityFrameworkCore`, Andrew Kane, MIT); **Redis.OM** (vector + search via NRedisStack); **Microsoft.SemanticKernel.Connectors.Memory.\*** (Pinecone, Qdrant, AzureSearch, Redis, Postgres); **Microsoft.Extensions.VectorData.Abstractions** (.NET 9 first-party).

Classical ML / inference: **Microsoft.ML** (ML.NET — trainers, AutoML, ONNX export); **Microsoft.ML.OnnxRuntime** (+`.DirectML/.Cuda/.Gpu`, MIT — runs any ONNX model); **TensorFlow.NET** (`SciSharp.TensorFlow.Redist`, Apache-2.0); **TorchSharp** (.NET Foundation, MIT — PyTorch tensors); **NumSharp**, **Pandas.NET** (SciSharp); **Microsoft.Data.Analysis** (official .NET DataFrame); **Accord.NET** (LGPL, abandoned — historical); **CNTK** (MIT, discontinued).

Speech / vision / generative media: **Whisper.net** (sandrohanea, MIT — on-device STT); **Microsoft.CognitiveServices.Speech** (Azure Speech SDK); **StableDiffusionDotnet** (community ONNX); **OnnxStack** (higher-level SD/LLM ONNX stack); **Edge-TTS** (community wrappers).

Agent / orchestration: **Microsoft.SemanticKernel.Agents.OpenAI** (OpenAI Assistants); **Microsoft.SemanticKernel.Process** (stateful processes, saga-style); **AgentChat**; **Microsoft.Extensions.AI.AzureAIInference**; **MAUI.AI** / **Microsoft.ML.Tokenizers** (BPE/SentencePiece tokenization).

### E.10 DevOps / CI / build / CLI tooling

Build: **Cake** (MIT — C#-scripted); **Nuke** (Matthias Koch / JetBrains, MIT — type-safe pipelines, modern winner); **FAKE** (F#, Apache-2.0, maintenance); **MSBuild** / `Microsoft.Build.*`; **dotnet CLI**.

CLI / Console UX: **Spectre.Console** + **Spectre.Console.Cli** (Patrik Svensson, MIT — default for beautiful CLIs); **System.CommandLine** (Microsoft, MIT — used by `dotnet`); **McMaster.Extensions.CommandLineUtils** (Apache-2.0, maintenance); **CommandLineParser** (MIT, predates `System.CommandLine`); **CommandDotNet** (Apache-2.0, convention-based); **CliWrap** (Tyrrrz, MIT — async piping, robust); **MedallionShell** (MIT, alt to CliWrap); **Sharprompt** (shibayan, MIT — Inquirer.js-style); **Crayon** (MIT, color); **ShellProgressBar** (Apache-2.0, drop-in progress).

GitHub / Git / DevOps APIs: **Octokit.NET** + **Octokit.GraphQL** (GitHub, MIT); **GitLabApiClient** (community); **BitBucketSharp**; `Microsoft.TeamFoundation.*` / `Microsoft.VisualStudio.Services.*` (Azure DevOps SDK); **LibGit2Sharp** (libgit2 bindings, MIT); **GitInfo** (MSBuild task); **GitVersion** (MIT, GitTools — SemVer from git history); **MinVer** (Apache-2.0 — minimal SemVer from tags); **Nerdbank.GitVersioning** (MIT — versioning via JSON config).

Octopus / Release / Secrets: **Octopus.Client** (Apache-2.0); **VaultSharp** (Apache-2.0); `Microsoft.Azure.KeyVault` / `Azure.Security.KeyVault.Secrets`; `AWSSDK.SecretsManager`; `dotnet-user-secrets`.

Containers / orchestration: **Microsoft.NET.Build.Containers** (first-party rootless image production); **Aspire** / **.NET.Aspire.Hosting** (distributed orchestration); **KubernetesClient** (`k8s.io/dotnet`); **Dapr.Client** / **Dapr.AspNetCore**; **Steeltoe.\***.

### E.11 Code quality / static analysis

| Lib | Niche | Active? | License | Notes |
|---|---|---|---|---|
| Microsoft.CodeAnalysis.NetAnalyzers | First-party FxCop successor | Very active | MIT | Default in modern SDKs |
| Microsoft.CodeAnalysis.CSharp / .Workspaces | Roslyn SDK | Very active | MIT | Build your own analyzer |
| Microsoft.VisualStudio.Threading.Analyzers | Threading correctness (VSTHRD) | Active | MIT | Avoid sync-over-async |
| StyleCop.Analyzers | Style conventions | Active | Apache-2.0 | Replacement for legacy StyleCop |
| SonarAnalyzer.CSharp | SonarQube-style rules | Very active | LGPL | Bug + smell catalog |
| Roslynator | Refactorings + analyzers | Active | Apache-2.0 | Huge refactoring catalog |
| ErrorProne.NET | Async/null/IDisposable correctness | Maintenance | MIT | Strong correctness rules |
| Meziantou.Analyzer | Pragmatic correctness rules | Very active | MIT | Most-recommended modern pack |
| IDisposableAnalyzers | IDisposable correctness | Active | MIT | Closes IDisposable gaps |
| ReflectionAnalyzers | Reflection-safety | Active | MIT | |
| AsyncFixer | Async/await mistakes | Active | MIT | Catches common await bugs |
| SecurityCodeScan / Puma.Security.Rules | Security analyzers | Active | LGPL / MIT | OWASP-style rules |
| NDepend | Architecture analysis | Active | Commercial | Code metrics, queries |
| JetBrains.ReSharper analyzers | ReSharper rules (IDE-integrated) | Active | Commercial | |
| JetBrains.Annotations | Nullability/contract attributes | Active | Apache-2.0 | Code-level documentation hints |
| xunit.analyzers / nunit.analyzers / MSTest.Analyzers | Test-framework correctness | Active | various OSS | Catch test mistakes |
| EditorConfig | Cross-tool style config | Active | MIT | Should ship in every repo |

### E.12 Documentation generation

| Tool | Niche | Active? | License | Notes |
|---|---|---|---|---|
| DocFX | First-party documentation site builder | Active | MIT | Generates API docs from XML + Markdown |
| Sandcastle Help File Builder | Older XML-doc HTML/CHM generator | Maintenance | MS-PL | Legacy MSDN-style |
| Statiq.Web / Statiq.Framework | C# static site generator | Active | MIT | Modern Wyam successor |
| Wyam | Earlier C# static site generator | Replaced by Statiq | MIT | Legacy |
| Microsoft.AspNetCore.OpenApi | First-party OpenAPI gen (.NET 9) | Active | MIT | New default |
| Swashbuckle.AspNetCore | Swagger/OpenAPI for ASP.NET | Active | MIT | Long-time default |
| NSwag | OpenAPI + client generator | Active | MIT | Strong codegen |
| Microsoft.OpenApi | OpenAPI doc model | Active | MIT | Underlying object model |
| Asp.Versioning.Mvc.ApiExplorer | Versioned APIs into Swagger | Active | MIT | Version-aware doc |
| MkDocs (Python) / VitePress (Vue) / Slate | Static docs sites | Active | varies | Common companions, non-.NET |
| Fluid (Liquid templates) | Templating used by DocFX | Active | MIT | Underlying template engine |

### E.13 Localization / i18n

`Microsoft.Extensions.Localization` (built-in IStringLocalizer) + `Microsoft.AspNetCore.Localization` (request-localization middleware); **OrchardCore.Localization** (DB-backed PO files, BSD-3); **Humanizer** (pluralization, dates, ordinals, casing — MIT); **Fluent.Net** (Mozilla Fluent FTL port, Apache-2.0); **I18Next.Net** (i18next-style, MIT); **NGettext** (GNU gettext .po, LGPL); **ResXResourceManager** (Tom Englert, MIT, VS extension); **Karambolo.PO** (gettext .po reader/writer, MIT); **LingoLib** (community, niche).

### E.14 Performance / profiling / diagnostics (community)

**BenchmarkDotNet** (.NET Foundation, MIT — industry std microbenchmarking); **MiniProfiler** (+`.AspNetCore`, Stack Exchange, MIT — per-request timing); **dotnet-counters / -trace / -dump / -gcdump / -monitor / -symbol / -stack / -sos** (Microsoft, MIT); **JetBrains.dotTrace / .dotMemory** (commercial); **PerfView** (Microsoft, MIT — ETW analyzer, deep-dive); **ClrMD** (`Microsoft.Diagnostics.Runtime`, MIT — live/dump CLR analysis); **EtwProfiler**; **Microsoft.Diagnostics.NETCore.Client** (programmatic dump/trace); **OpenTelemetry.\*** (OTel SDK); **Sentry** (+`.AspNetCore`, MIT); **Microsoft.ApplicationInsights** (+`.AspNetCore`, MIT); **Datadog.Trace** (Apache-2.0); **NewRelic.Agent.Api** (Apache-2.0).

### E.15 Niche utilities (de-facto standards, recap)

| Lib | Niche | License snapshot |
|---|---|---|
| Polly | Resilience pipelines | BSD-3 |
| Microsoft.Extensions.Http.Resilience | First-party HttpClient resilience | MIT |
| Refit | REST via interfaces | MIT |
| RestSharp | Older REST client | Apache-2.0 |
| Flurl.Http | Fluent HTTP/URL | MIT |
| MediatR | In-process mediator | **Commercial 12.0+** |
| MassTransit | Distributed messaging | **Commercial v9+ announced** |
| NServiceBus | Enterprise messaging | Commercial |
| CAP | Outbox + pub/sub | MIT |
| Brighter | Command processor + outbox | MIT |
| Wolverine | Mediator + messaging | MIT |
| FluentValidation | Validation rules | Apache-2.0 |
| AutoMapper | Object mapping | **Commercial 14.0+** |
| Mapster / Mapperly | Mapping (faster, source-gen) | MIT / Apache-2.0 |
| Hangfire / Quartz.NET / Coravel | Background jobs / scheduling | LGPL+commercial / Apache-2.0 / MIT |
| Newtonsoft.Json | Classic JSON | MIT |
| MessagePack-CSharp | Binary serializer | MIT |
| EFCore.BulkExtensions | Bulk EF | MIT |
| Linq2Db / Dapper / RepoDB / PetaPoco | ORMs | varies |
| Marten / EventStoreDB Client / Equinox | ES tooling | MIT / MIT / Apache-2.0 |
| FluentMigrator / DbUp / RoundhousE | DB migrations | varies |
| Akka.NET / Microsoft.Orleans / Proto.Actor | Actor models | Apache-2.0 / MIT / Apache-2.0 |
| Nito.AsyncEx | Async helpers | MIT |
| System.Reactive | Rx.NET | MIT |
| LanguageExt / OneOf / CSharpFunctionalExtensions / ErrorOr | Result/Either/Option | MIT |
| Ardalis.{GuardClauses, Specification, Result, SmartEnum} | Pre-conditions, specs, results | MIT |
| Vogen / StronglyTypedId | Source-gen value objects / IDs | Unlicense / MIT |
| Bogus / Spectre.Console | Test data + console | MIT |
| OpenIddict / Duende.IdentityServer | OIDC servers | Apache-2.0 / Commercial |
| Microsoft.AspNetCore.Identity.* | First-party identity | MIT |
| NodaTime | Better date/time | Apache-2.0 |
| Humanizer | Locale string formatting | MIT |
| ULID (`Cysharp/UlidCore`) / IdGen / Snowflake.Net | Sortable IDs | MIT |
| MimeTypes | MIME mappings | MIT |
| LiteDB | Embedded NoSQL | MIT |
| Lucene.Net | Search engine port | Apache-2.0 |
| Meilisearch.Net / Algolia.Search | Hosted search | MIT |

### E.16 Cross-cloud SDKs (recap)

Already enumerated in §A.21 (Azure), §C.6/C.14 (storage + AI). AWS: `AWSSDK.{Core, S3, SQS, SNS, DynamoDBv2, Lambda, SecretsManager, SimpleSystemsManagement, BedrockRuntime}` + `Amazon.Lambda.AspNetCoreServer`. GCP: `Google.Cloud.{Storage.V1, PubSub.V1, Firestore, Spanner.Data, Bigtable.V2, AIPlatform.V1}` + `Google.Apis.*`. Cloud-agnostic: **MinIO.NET**, **FluentStorage** (formerly Storage.Net), **CloudNative.CloudEvents** (CNCF spec types), **Dapr.Client** (sidecar abstractions), **Steeltoe.Connectors**, **Akka.Persistence** plugins.

### E.17 Verdict synthesis (agent-5 recommendations to consider)

The agent's bundled-package recommendations (consider for `targets.md`):

- **Modular Monolith first, microservices second** — make module boundaries cheap (DI conventions, Inbox/Outbox abstractions, in-process MediatR-equivalent for now, easy switch to MassTransit later).
- **Default messaging:** ship a thin `IRequestHandler<,>` / `INotificationHandler<>` MediatR-API-compatible mediator (because of MediatR's 2025 license shift); swap to Wolverine/MassTransit later.
- **Default mapping:** Riok.Mapperly (source-gen, AOT-safe). Avoid AutoMapper's commercial path.
- **Default validation:** FluentValidation (still permissive).
- **Default resilience:** `Microsoft.Extensions.Http.Resilience` + Polly v8 pipelines.
- **Default logging:** Serilog with OTel sinks; abstract behind `ILogger<T>` to stay swappable.
- **Default observability:** OpenTelemetry SDK preconfigured; `ActivitySource` around mediator + EF + outbox.
- **Default persistence:** EF Core 9 conventions; Marten optional for ES-leaning subsystems; Dapper for hot paths.
- **Default testing kit:** xUnit + Verify + NSubstitute + Bogus + Testcontainers + WireMock.Net + WebApplicationFactory + Respawn. Provide `WowTestHost<T>` wrapper.
- **Default multi-tenancy:** wrap Finbuckle.MultiTenant; offer per-row + per-DB strategies via `ITenantStore`.
- **Default AI:** wrap `Microsoft.Extensions.AI` + SK connectors so consumers swap providers (OpenAI / Azure OpenAI / Bedrock / Anthropic / Gemini / Ollama / LLamaSharp) by config only.
- **Default billing:** abstraction (`IBillingProvider`) with first-class Stripe.net adapter.
- **Default comms:** abstractions (`IEmailBroker`, `ISmsSender`, `IPushSender`) backed by FluentEmail + Twilio + FCM + dotAPNS.
- **Default geo/IP:** NetTopologySuite + MaxMind.GeoIP2 behind `IGeoIp`; geocoding facade over Geocoding.Net.
- **Default media:** ImageSharp (commercial threshold) + QuestPDF (same caveat); Magick.NET for format-heavy.
- **Default tooling:** Spectre.Console + Spectre.Console.Cli for internal CLIs; Nuke for build orchestration; Octokit for GitHub.
- **Code quality baseline:** Meziantou.Analyzer + StyleCop + SonarAnalyzer + IDisposableAnalyzers + JetBrains.Annotations + .editorconfig.
- **Reference architecture inspiration:** ardalis/CleanArchitecture skeleton + Kamil Grzybek modular boundaries + Jimmy Bogard vertical slices + eShop / Aspire for distributed.

---

## References

- Microsoft Learn: <https://learn.microsoft.com/en-us/dotnet/>
- ASP.NET Core docs: <https://learn.microsoft.com/en-us/aspnet/core/>
- EF Core docs: <https://learn.microsoft.com/en-us/ef/core/>
- .NET Aspire docs: <https://learn.microsoft.com/en-us/dotnet/aspire/>
- Microsoft.Extensions.AI: <https://learn.microsoft.com/en-us/dotnet/ai/>
- Companion UI catalog: `wow-two-sdk-beta.ui/engineering/architecture/analysis/ui-philosophy/ideas.md`
- Internal companion: [`targets.md`](./targets.md)
