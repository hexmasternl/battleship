---
name: hexmaster-pragmatic-domain-driven-design
description: Applies when designing or changing a domain entity, aggregate, value object or repository, when deciding whether a primitive deserves a wrapper type, when writing the body of a command handler, or when reviewing where business rules live. Keeps domain models rich and valid without value-object ceremony.
metadata:
  source: hexmaster-coding-standards
  document: pragmatic-domain-driven-design
  status: draft
---

# HexMaster design: Pragmatic domain-driven design

**This standard is a draft.** It is still being settled and may change; apply it, but flag
when a decision rests on it.

## Rules

- All business operations go through the domain model. Entities enforce invariants, protect
  their state and express business rules; they are never anemic property bags.
- Entities have private setters, a private parameterless constructor for the ORM, a static
  factory (`Order.Create(...)`) and behaviour methods for every state transition. Collections
  are private lists exposed as `IReadOnlyList<T>`; never expose `List<T>`.
- Invariant violations throw `DomainException`. The domain is the authoritative validation
  layer; input validation on commands or DTOs (format, required, range) is an additional first
  pass, not a replacement.
- Command handlers orchestrate only: load aggregates through repositories, call domain methods,
  translate `DomainException` into a result, manage the transaction via a unit of work, return a
  typed result. No business logic and no direct property manipulation in handlers.
- Aggregate roots own their children. Child entities are created through the root with
  `internal` or `private` constructors. External code references roots only.
- Repositories exist for aggregate roots only. Never create a repository for a child entity.
- Create a value object for multi-field concepts (`Address`), interdependent fields
  (`DateRange`), format-validated values with behaviour (`EmailAddress`, `Money`) or
  domain-specific operations. Do not wrap simple primitives, ids or single-check values
  (`FirstName`, `Age`, `OrderId`, `Quantity`); validate those inside the entity.
- Emit domain events from the aggregate for significant occurrences and expose them as a
  read-only list with a clear method.
- Use specifications for complex reusable queries.
- Keep persistence out of the domain: no EF Core attributes on entities; map with fluent
  `IEntityTypeConfiguration<T>`, owned types for value objects, and ignore the domain events
  collection.
- Test domain behaviour directly with concrete objects; expect `DomainException` on invalid
  transitions.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `pragmatic-domain-driven-design`.
