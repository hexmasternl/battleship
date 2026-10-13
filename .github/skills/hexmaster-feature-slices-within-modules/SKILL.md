---
name: hexmaster-feature-slices-within-modules
description: Applies when adding a feature to a module (Players, Games or a new one), creating a command, query, handler, request or response type, deciding whether a type belongs in the module project or its Abstractions project, or writing the endpoint that dispatches to a handler. Settles the Features namespace layout and the DTO boundary.
metadata:
  source: hexmaster-coding-standards
  document: feature-slices-within-modules
  status: accepted
---

# HexMaster ADR 0009: Feature slices within module projects

Required structure when adding a feature to a module.

## Rules

- A module has two core projects: `Company.Product.{Module}` (domain models, handlers, feature
  slices) and `Company.Product.{Module}.Abstractions` (public contracts). Other modules and the
  API host depend on Abstractions only, never on the implementation project.
- Each feature lives in its own namespace `Company.Product.{Module}.Features.{FeatureName}` and
  holds at least two files: the command or query record and its handler.
- Handler naming: `{FeatureName}CommandHandler` or `{FeatureName}QueryHandler`, implementing
  `ICommandHandler<,>`, `ICommandHandler<>` or `IQueryHandler<,>` from the shared Core project.
- Commands, queries and feature-local result records are internal to the module and are never
  placed in Abstractions.
- Everything that crosses the module boundary (API request payloads, response envelopes,
  event contracts) goes in `Company.Product.{Module}.Abstractions.DataTransferObjects`. DTOs are
  `sealed record` types, carry data only, no ORM or serializer attributes, and are named with
  the suffix `Request`, `Response`, `Dto` or `Event`.
- Repository interfaces (`I{Entity}Repository`) sit at the root of the module implementation
  project. They are internal persistence ports; a data adapter project references the module
  project to implement them. Only cross-module service ports (`I{Module}Service`) belong in
  Abstractions.
- Module project layout: `DomainModels/`, `Features/{FeatureName}/`, `Observability/`,
  `Services/`, `I{Entity}Repository.cs`, `{Module}ModuleRegistration.cs`.
- Flow: the Minimal API endpoint receives the DTO, maps it to the command or query, calls the
  handler, and translates the result DTO to an HTTP response. Handlers never see HTTP.
- Each module exposes `Add{Module}Module(this IServiceCollection)` in the implementation
  project that registers every handler and its infrastructure. The API host calls it in
  `Program.cs`.
- Abstractions must never reference the module implementation project.

## In this repository

Modules are `src/Players/HexMaster.Battleship.Players{,.Abstractions,.Api,.Tests}` and the
equivalent under `src/Games/`. New features go in `Features/{FeatureName}/` inside
`HexMaster.Battleship.Players` or `HexMaster.Battleship.Games`; their payloads go in the matching
`.Abstractions/DataTransferObjects/`.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `feature-slices-within-modules`.
