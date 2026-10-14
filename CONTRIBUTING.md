# Contributing to Battleship

Thanks for wanting to help. Battleship is a playable game and a reference project for building
distributed .NET apps with [Aspire](https://aspire.dev). Contributions that make either side
better are welcome: bug reports, fixes, features, documentation and ideas for the Aspire tour.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report a bug.** Open a [bug report](https://github.com/hexmasternl/battleship/issues/new?template=bug_report.yml).
  Include the steps, what you expected and what happened. Dashboard logs or a trace help a lot.
- **Suggest a feature or an Aspire scenario.** Open a [feature request](https://github.com/hexmasternl/battleship/issues/new?template=feature_request.yml).
  [`docs/suggestions.md`](docs/suggestions.md) lists Aspire extension points that would fit well.
- **Ask a question.** See [`SUPPORT.md`](SUPPORT.md).
- **Report a vulnerability.** Don't open a public issue. Follow [`SECURITY.md`](SECURITY.md).

## Before you write code

Open an issue first for anything bigger than a small fix, so we can agree on the scope before
you spend time on it. Small fixes (typos, an obvious bug, a missing test) can go straight to a
pull request.

## Set up your machine

Follow the [Quick start](README.md#-quick-start) in the README. In short, you need Docker, the
.NET 10 SDK, the Aspire CLI, the Dapr CLI (with `dapr init` run once) and Node.js with npm.
Then:

```shell
cd src/Frontend && npm install && cd -
cd src/Aspire/HexMaster.Battleship.Aspire && aspire run
```

## How a change is made

The project is spec-driven with [OpenSpec](https://github.com/Fission-AI/OpenSpec). The specs in
`openspec/specs/` describe the system as it is. A feature or behaviour change is a change under
`openspec/changes/<name>/` with a proposal, a design, delta specs and a task list.

| Branch | Purpose |
|---|---|
| `main` | Releases. Never commit to it directly. |
| `development` | The integration branch. Pull requests target this branch. |
| `change/<name>` | One OpenSpec change, implemented in its own worktree. |
| `fix/<topic>`, `docs/<topic>` | Small changes that don't need an OpenSpec change. |

The flow for a change:

1. A GitHub issue is triaged and linked to a proposed change on `development`.
2. The change is implemented on `change/<name>`, branched from `development`, with a commit per task.
3. A pull request goes into `development`. After review and merge, the change is archived into
   `openspec/specs/`.
4. `development` is released to `main` through a pull request.

The complete rules are in the [`openspec-git-flow`](.claude/skills/openspec-git-flow/SKILL.md)
skill. If you use Claude Code or GitHub Copilot, the repository's skills and prompts
(`/issue`, `/opsx:propose`, `/opsx:apply`, `/opsx:archive`) follow them for you. You don't have to
use an AI agent to contribute, but please keep the specs in step with the code.

## Coding standards

The standards are written down as skills under [`.claude/skills/`](.claude/skills) and mirrored in
[`.github/skills/`](.github/skills). The most important ones:

- **.NET 10 and C# 14** for every project.
- **Modular monolith with vertical slices.** Each module has a module project with features under
  `Features/` (a command or query record and its handler) and endpoint groups under `Endpoints/`,
  an `Abstractions` project with the DTOs other modules may use, a thin `Api` host and a `Tests`
  project. Modules never reference each other's internals.
- **Minimal APIs** with built-in OpenAPI and Scalar. No controllers.
- **CQRS.** Endpoints dispatch to injected handlers. Handlers don't know about HTTP and return DTOs.
- **OpenTelemetry** for traces, metrics and logs. Register handlers with `AddCommandHandler` or
  `AddQueryHandler` and follow [Observability](README.md#observability) in the README.
- **Services communicate through integration events** over Dapr pub/sub, never through direct calls.
- **Gateway routes.** A new first path segment in an API needs a route in `GatewayRoutes.cs`.
  Propose it in the issue or pull request first.
- **Frontend.** Angular 22 with Angular Material, zoneless and signal-driven. Each component keeps
  its data in a local immutable state store. Every colour, size and effect comes from the
  [design system](docs/design-system.md) tokens. See [`src/Frontend/README.md`](src/Frontend/README.md).

## Tests

Every change comes with tests. The backend uses **xUnit, Moq and Bogus**, with test data from
factories, and keeps coverage at 80% or more. The frontend uses Vitest with axe accessibility
checks.

```shell
dotnet test src/Battleship.slnx --filter "Category!=Integration"   # fast backend tests
dotnet test src/Battleship.slnx                                     # also the end-to-end tests (Docker + Dapr)
cd src/Frontend && npm test -- --watch=false                        # frontend
```

Table storage tests use Azurite on its default ports, or the connection string in
`BATTLESHIP_TESTS_TABLES`. They are skipped when neither is available. CI runs them against an
Azurite container.

## Commits

Use [Conventional Commits](https://www.conventionalcommits.org): `type(scope): summary`, for
example `feat(games): fire a shot` or `fix(frontend): keep the board focused after a shot`.
Common types are `feat`, `fix`, `test`, `docs`, `refactor`, `chore` and `ci`. The scope is the
module or area, such as `users`, `games`, `events`, `devtools`, `aspire`, `frontend` or `openspec`.

## Pull requests

- Target `development`, not `main`.
- Link the issue (`Closes #123`) and the OpenSpec change, if there is one.
- Keep the pull request about one thing.
- Make sure CI passes: the build, the backend tests and the frontend tests.
- Update the README or the docs when behaviour, configuration or the Aspire tour changes.
- Don't force-push a branch someone else is reviewing. Add commits instead.

A maintainer reviews, and merges when the pull request is ready.

## License

By contributing, you agree that your contributions are licensed under the [MIT license](LICENSE).
