---
name: hexmaster-modular-solution-structure
description: Applies when laying out or reorganising the solution, adding a module, data, API or test project, naming a project or folder, adding a project reference, or deciding whether a module needs its own API or should be split into a service. Gives the recommended filesystem structure, naming and dependency rules.
metadata:
  source: hexmaster-coding-standards
  document: modular-solution-structure
  status: accepted
---

# HexMaster design: Modular solution structure

Companion to ADR 0002. Use it to answer "where does this project go and what is it called".

## Layout

```
src/
  App/                                   frontend
  Aspire/
    Company.Product.Aspire.AppHost/
    Company.Product.Aspire.ServiceDefaults/
  Core/Company.Product.Core/             ICommandHandler, IQueryHandler, IClock
  {Module}/
    Company.Product.{Module}/
    Company.Product.{Module}.Abstractions/
    Company.Product.{Module}.Api/        one per web-exposed module
    Company.Product.{Module}.Data.{StorageType}/
    Company.Product.{Module}.Tests/
  Shared/Company.Product.IntegrationEvents/   optional
tests/Company.Product.IntegrationTests/       optional
```

## Rules

- Project names follow `{Company}.{Product}.{Module}[.{Layer}][.{Technology}]` with dots as
  separators. Folder names are the short module name only.
- Storage suffixes are spelled `SqlServer`, `PostgreSQL`, `MongoDb`, `CosmosDb`, `Redis`,
  `TableStorage`, `Blob`.
- Allowed dependency direction: Aspire orchestrates module APIs; an API references its module
  core; the core references its Abstractions; data projects reference Abstractions (and the
  module project for internal repository ports); everything may reference Core.
- Forbidden: module core to another module core, Abstractions to module core, a test project
  to another module's core, a data project owning several modules' databases, EF or storage
  types in Abstractions, one god Abstractions project for the whole solution.
- Every module has a `.Tests` project (xUnit, test class named `{ClassUnderTest}Tests`) with at
  least 80% line coverage.
- An `.Api` project contains `Endpoints/`, optional `Authorization/` and `BackgroundServices/`,
  and a `Program.cs` that calls `AddServiceDefaults`, `Add{Module}Module`, `AddOpenApi`,
  `MapDefaultEndpoints`, `Map{Module}Endpoints` and `MapOpenApi`.
- Create a new module for a distinct bounded context with its own data and rules; create an
  Abstractions project when another module needs the contract; give a module its own API when
  it must scale, deploy or release independently or talks to others via messaging.
- A `Directory.Build.props` at the solution root pins `net10.0`, nullable, implicit usings and
  warnings-as-errors for every project.
- Organise the solution file with folders mirroring `src/` and `tests/`.

## In this repository

The solution is `src/Battleship.slnx`; Company.Product is `HexMaster.Battleship`; the frontend
sits in `src/Frontend/`. Players and Games are the current modules. Add `src/Core/` and a
`Directory.Build.props` when the next backend change needs them.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `modular-solution-structure`.
