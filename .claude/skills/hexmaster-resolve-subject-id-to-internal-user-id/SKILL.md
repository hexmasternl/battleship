---
name: hexmaster-resolve-subject-id-to-internal-user-id
description: Applies when an endpoint reads the caller's identity from a JWT, when a command, query, DTO, entity, stored record or event needs a user reference, or when registering users from an external identity provider. Requires the sub claim to be exchanged for an internal Guid UserId at the API boundary.
metadata:
  source: hexmaster-coding-standards
  document: resolve-subject-id-to-internal-user-id
  status: accepted
---

# HexMaster ADR 0010: Resolve the external subject id at the API boundary

## Rules

- The identity provider's `sub` claim (the SubjectId) is resolved to an internal `UserId`
  inside the endpoint, before any command or query is dispatched. Read it with
  `principal.FindFirstValue("sub")`, pass it to `IUserResolver.ResolveAsync(subjectId, ct)`,
  and build the command with the returned `UserId`.
- If no user exists for the SubjectId the endpoint returns `401 Unauthorized`, or creates the
  user when the feature is a registration flow.
- `UserId` is a `Guid` for every new module. `int` or `long` are tolerated only where an
  existing schema forces them.
- The SubjectId must not appear in domain model properties, command or query records, DTOs in
  `Abstractions/DataTransferObjects`, stored data records, or domain and integration events.
  It exists only in endpoint code and in the `IUserResolver` implementation.
- `IUserResolver` is a shared port (or the Users module's public interface):
  `Task<Guid> ResolveAsync(string subjectId, CancellationToken ct = default)`, throwing
  `UnauthorizedAccessException` when unknown. It is implemented once against the Users
  module's store, never inline in another module.
- The Users module is the single authority for the SubjectId to UserId mapping. No other module
  stores it. Cross-module filters and joins use `UserId` only.
- Mitigate the extra lookup per request by caching the resolved `UserId` in the request
  context or a distributed cache keyed on SubjectId.

## In this repository

The Players module is the natural owner of the SubjectId to UserId mapping and of the
`IUserResolver` implementation. Game commands and events reference players by their internal
`Guid` id only.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `resolve-subject-id-to-internal-user-id`.
