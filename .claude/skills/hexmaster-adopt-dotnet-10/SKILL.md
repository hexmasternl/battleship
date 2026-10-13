---
name: hexmaster-adopt-dotnet-10
description: Applies when creating a new .NET project, editing a .csproj or Directory.Build.props, choosing a TargetFramework or LangVersion, or deciding whether a C# language feature may be used. Fixes net10.0 and C# 14 as the baseline for every project in this solution.
metadata:
  source: hexmaster-coding-standards
  document: adopt-dotnet-10
  status: accepted
---

# HexMaster ADR 0001: .NET 10 is the target framework

## Rules

- Every project targets `net10.0`. New `.csproj` files must contain
  `<TargetFramework>net10.0</TargetFramework>`; never introduce an older target or multi-target
  to an older framework.
- .NET 10 is the current LTS release with three years of support. That is the reason for the
  choice; do not reassess it per project. Moving to .NET 12 LTS requires a superseding ADR.
- Use modern C# 14 language features where they improve clarity and safety (file-scoped
  namespaces, records, primary constructors, collection expressions, pattern matching). Do not
  write down-level C# to stay compatible with older runtimes.
- Package references must support .NET 10. Prefer the package version line that matches the
  runtime major (for example `Microsoft.AspNetCore.OpenApi` 10.x).
- Accepted consequence: consumers and build agents need a .NET 10 SDK and runtime.

## In this repository

All projects under `src/` (Players, Games, Aspire AppHost and ServiceDefaults, test projects)
already target `net10.0`. Keep new module, data, integration and test projects on the same
target.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `adopt-dotnet-10`.
