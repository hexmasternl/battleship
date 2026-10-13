---
name: hexmaster-modular-monolith-project-structure
description: Applies when adding a project or module to the solution, naming a project, deciding where a type lives, adding a project reference between modules, or sharing data between modules. Defines the src layout, project naming and the module boundary rules.
metadata:
  source: hexmaster-coding-standards
  document: modular-monolith-project-structure
  status: accepted
---

# HexMaster ADR 0002: Modular monolith project structure

## Rules

- `src/` top level: `App/` (frontend), `Aspire/` (AppHost and ServiceDefaults), `Core/`
  (shared CQRS interfaces, `IClock`, cross-cutting abstractions), one short-named folder per
  bounded context (`Players/`, `Games/`), and optionally `Shared/` or `IntegrationMessages/`
  for cross-module event contracts.
- Each module folder holds `Company.Product.{Module}` (domain, feature slices, handlers),
  `Company.Product.{Module}.Abstractions` (public contracts), `Company.Product.{Module}.Api`
  (Minimal API host, standard for every web-exposed module), `Company.Product.{Module}.Tests`,
  and optionally `.Data.{StorageType}` (persistence adapter) and `.Integrations` (integration
  event consumers).
- The `.Api` project is per module, never shared. Each module is an independently deployable
  unit with its own API surface.
- Naming: module folder is the short name only; projects follow
  `Company.Product.{Module}[.Abstractions|.Api|.Data.{Store}|.Tests]`; Aspire projects are
  `Company.Product.Aspire.AppHost` and `Company.Product.Aspire.ServiceDefaults`.
- No cross-module domain entity sharing. Modules talk through Abstractions DTOs and
  interfaces or through integration events.
- Infrastructure types (EF `DbContext`, SDK clients) stay inside the data project. Domain
  projects carry no ORM attributes or SDK references.
- Repository interfaces live in the module implementation project, not in Abstractions.
  Abstractions holds only cross-module service contracts and DTOs.
- The `.Api` project wires the module through its registration extension method
  (`services.Add{Module}Module()`).
- Start simple and introduce Abstractions when another module needs a stable contract; add
  extra data projects only with an ADR explaining why. Resist a "shared dumping ground"
  project.

## In this repository

Company.Product is `HexMaster.Battleship`. `src/Players/` and `src/Games/` follow the module
layout; `src/Frontend/` plays the role of `App/`. `src/Core/HexMaster.Battleship.Core` does not
exist yet and should be created when the first shared handler interface is needed.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `modular-monolith-project-structure`.
