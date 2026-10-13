---
name: hexmaster-vertical-slice-architecture
description: Applies when implementing or reviewing a feature, use case or handler, when about to add a method to an existing service class, when deciding where validation goes, or when choosing what to share between features. Requires feature-centric vertical slices instead of technical layers.
metadata:
  source: hexmaster-coding-standards
  document: vertical-slice-architecture
  status: accepted
---

# HexMaster ADR 0007: Vertical slice architecture

Required for application code. For the physical folder layout inside a module, ADR 0009
(feature slices within modules) is authoritative and supersedes the layout sketched here.

## Rules

- Group code by feature, not by technical layer. A slice holds its command or query, handler,
  slice-specific validation and result type under `Features/{FeatureName}/`.
- New features are new slices. Never add a feature as another method on an existing service
  class.
- Slices are largely independent. Shared concerns go through abstractions: ports and
  cross-cutting helpers in the module root or the Core project, domain entities in
  `DomainModels/`, DTOs in the Abstractions project under `DataTransferObjects/`.
- Do not share handler logic between slices and do not create "helper" handlers. Keep
  validation rules inside the slice they belong to. Promote a slice-local result type to
  Abstractions only once another module or the API project consumes it.
- Adapters (HTTP endpoints, message consumers, CLI commands) stay thin and delegate to the
  slice handler.
- Validation has two levels. Level 1 is a shallow endpoint filter that checks presence, format
  and range of the incoming DTO and returns `Results.ValidationProblem(...)`. Level 2 is the
  domain model, which enforces invariants through factories (`Order.Create(...)`) and behaviour
  methods and throws `DomainException`. Level 2 is authoritative.
- Handlers take their collaborators (repositories, `IClock`, `ILogger<T>`) through the
  constructor, guard their input, load aggregates, call domain methods, persist and return a
  result record.
- Prefer direct DI registration of each handler over a mediator library. Adopt a mediator only
  when uniform pipeline behaviours are genuinely needed.
- Tests live in the module's separate `.Tests` project, in a folder per feature, with xUnit,
  Moq and Bogus.
- Extract to Core when three or more slices repeat the same pattern; review slice boundaries
  in code review.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `vertical-slice-architecture`.
