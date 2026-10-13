---
name: hexmaster-feature-slices-module-structure
description: Applies when scaffolding a new module or a new feature inside one and you need the exact folders, file names, namespaces, class names and registration wiring. A copy-paste scaffold for the feature-slice conventions of ADR 0009, including the matching test layout.
metadata:
  source: hexmaster-coding-standards
  document: feature-slices-module-structure
  status: accepted
---

# HexMaster structure: Feature slices module scaffold

## Layout

```
src/{Module}/
  Company.Product.{Module}/
    DomainModels/{Entity}.cs
    I{Entity}Repository.cs                 repository port, module root
    Features/{FeatureName}/
      {FeatureName}Command.cs  or  {FeatureName}Query.cs
      {FeatureName}CommandHandler.cs  or  {FeatureName}QueryHandler.cs
    Observability/{Module}Metrics.cs       ActivitySource and meters
    Services/                              application or domain services
    Extensions/                            DI helpers
    {Module}ModuleRegistration.cs          Add{Module}Module(this IServiceCollection)
  Company.Product.{Module}.Abstractions/
    DataTransferObjects/{FeatureName}Request.cs, {Entity}Dto.cs
    Services/I{Module}Service.cs           cross-module port, only if consumed externally
  Company.Product.{Module}.Api/
    Endpoints/{Resource}Endpoints.cs       Map{Resource}Endpoints(this IEndpointRouteBuilder)
    Authorization/  BackgroundServices/    optional
    Program.cs
  Company.Product.{Module}.Data.{Store}/   optional: repository implementation, DbContext
  Company.Product.{Module}.Tests/
    {FeatureName}/{FeatureName}CommandHandlerTests.cs
    DomainModels/{Entity}Tests.cs
    Factories/{Entity}Faker.cs             Bogus factories
    Helpers/
```

## Rules

1. All feature namespaces are `Company.Product.{Module}.Features.{FeatureName}`.
2. A feature has at least two files: the command/query record and the handler.
3. Every type that crosses the module boundary is in
   `{Module}.Abstractions.DataTransferObjects`.
4. Commands, queries, DTOs and results are `sealed record` types, never classes.
5. Endpoint methods only map DTO to command/query and result to response; no domain logic.
6. DTOs never reference domain entities or EF Core types.
7. One `Add{Module}Module` registration method wires every handler and infrastructure
   adapter; `Program.cs` calls `AddServiceDefaults`, `Add{Module}Module`, `MapDefaultEndpoints`
   and `Map{Resource}Endpoints`.

## Naming reference

| Artifact | Pattern |
|---|---|
| Command / query record | `{FeatureName}Command` / `{FeatureName}Query` |
| Handler class | `{FeatureName}CommandHandler` / `{FeatureName}QueryHandler` |
| API request DTO | `{FeatureName}Request` |
| API response DTO | `{Entity}Dto` or `{FeatureName}Result` |
| Repository interface | `I{Entity}Repository` at module project root |
| Registration method | `Add{Module}Module` |
| Endpoint mapping method | `Map{Resource}Endpoints` |

## Anti-patterns

DTOs in the implementation project; commands or queries in Abstractions; business logic in
endpoint lambdas; one handler shared by several features; another module referencing the
implementation project instead of Abstractions; `class` instead of `sealed record` for
commands, queries and DTOs.

## Handler and test shape

A handler guards its input (`ArgumentNullException.ThrowIfNull`), applies the domain rule or
calls the domain factory, persists through the repository port, logs with a message template
and returns the result record. Its test class builds the handler with `Mock<I{Entity}Repository>`
and `Mock<ILogger<...>>`, uses a `Faker`, and covers the happy path, the domain failure
(`DomainException`) and the null command (`ArgumentNullException`).

## In this repository

Company.Product is `HexMaster.Battleship`; current modules are `Users` and `Games`.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `feature-slices-module-structure`.
