---
name: hexmaster-unit-testing-with-xunit-moq-bogus
description: Applies when writing or changing a unit test, creating or editing a test project, generating test data, mocking a collaborator, or when tempted to add a test or assertion library. Fixes the stack at xUnit, Moq and Bogus with factory-built data and an 80 percent coverage floor.
metadata:
  source: hexmaster-coding-standards
  document: unit-testing-with-xunit-moq-bogus
  status: accepted
---

# HexMaster recommendation: Unit testing with xUnit, Moq and Bogus

## Rules

- Test framework is xUnit for every unit test project. Test projects target `net10.0` and
  reference `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` (PrivateAssets all)
  and `coverlet.collector` (PrivateAssets all).
- Mock interfaces and collaborators with Moq. Prefer pure domain tests with concrete objects
  where behaviour can be validated without mocks; do not over-mock.
- Generate test data with Bogus. When a test asserts on generated values, seed it
  (`Randomizer.Seed = new Random(1234)`) so it is deterministic.
- Encapsulate object construction in factory methods or builder classes under `Factories/`
  (or similar) that use Bogus internally for plausible defaults and accept overrides. Do not
  construct objects ad hoc across tests.
- Maintain at least 80% unit test coverage for Core and Server code, enforced in CI with
  coverlet.
- Do not add another test framework, assertion library, snapshot tool or generator without an
  ADR or an issue/PR discussion that justifies it. Keep the stack identical across modules.
- Test classes are named `{ClassUnderTest}Tests` and mirror the feature folders of the module
  under test.

## In this repository

`HexMaster.Battleship.Users.Tests` and `HexMaster.Battleship.Games.Tests` already have the
xUnit and coverlet references. Add `Moq` and `Bogus` to them when the first handler or domain
test needs them, and keep factories in a `Factories/` folder per test project.

## Source

The complete standard can be retrieved from the HexMaster coding standards MCP server with
the `get_document` tool, document id `unit-testing-with-xunit-moq-bogus`.
