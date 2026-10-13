---
name: hexmaster-cqrs-for-aspnet-apis
description: Applies when adding or changing an HTTP endpoint, a use case, a command, a query or a handler in an ASP.NET API, or when deciding what an endpoint returns. Maps requests onto command/query records handled by injected, HTTP-free handlers that return DTO records.
metadata:
  source: hexmaster-coding-standards
  document: cqrs-for-aspnet-apis
  status: accepted
---

# HexMaster ADR 0004: Lightweight CQRS for ASP.NET APIs

## Rules

- Every request becomes a Command (intent to change state) or a Query (intent to read),
  expressed as a `sealed record`. HTTP payloads are mapped into request records, then into the
  command or query.
- A dedicated handler per command/query implements one of the shared interfaces:
  `ICommandHandler<TCommand, TResult>`, `ICommandHandler<TCommand>` (no result),
  `IQueryHandler<TQuery, TResult>`. Handlers are registered in DI and resolved by the endpoint.
- Handlers know nothing of HTTP: no `HttpContext`, no `IResult`, no status codes. The endpoint
  translates the handler's result into an HTTP response.
- Handler output is a DTO record suitable for serialization. Never return a domain entity.
- A command that succeeds without a return value maps to `Results.Accepted()` (202). Creation
  returns 201 with a location; reads return 200 or 404.
- Validate at the edge (endpoint filter or validator) before invoking the handler; business
  invariants are enforced in the domain model, not in the endpoint.
- Keep request, command/query, result and handler together per feature (vertical slice).
- The handler interfaces and marker interfaces (`ICommand`, `IQuery`) live once in a shared
  `Company.Product.Core` project that every module references. Core has no web or
  infrastructure dependencies.

## In this repository

There is no `src/Core/HexMaster.Battleship.Core` project yet. The first change that introduces
handlers must create it and define the handler interfaces there, instead of declaring them
inside `HexMaster.Battleship.Users` or `HexMaster.Battleship.Games`.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `cqrs-for-aspnet-apis`.
