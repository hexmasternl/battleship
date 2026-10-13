# Battleship

.NET solution (`src/Battleship.slnx`) with one project group per service under `src/` (`Users`, `Games`, `Aspire` for hosting). Planning is spec-driven with OpenSpec (`openspec/`).

## OpenSpec changes follow the git flow skill

Every OpenSpec change is governed by the `openspec-git-flow` skill (`.claude/skills/openspec-git-flow/SKILL.md`). Load it before `/opsx:propose`, `/opsx:apply`, `/opsx:archive`, `/issue`, or any request that proposes, implements or archives a change. The rules in short:

- `main` and `development` both live in the main worktree (this repository checkout). Never create a worktree for either. If `development` is missing, create it from `main`.
- A proposal is created on `development` and committed there autonomously (`chore(openspec): propose <name>`), staging only the change directory.
- Applying a change always happens in its own worktree at `../battleship-worktrees/<change-name>` on branch `change/<change-name>`, created from `development`. Commit after every task with a logical message. When all tasks are done, push and open a pull request into `development`. Do not merge it.
- Archiving happens on `development` after the pull request is merged, and the result is committed there autonomously (`chore(openspec): archive <name>`). Then the change worktree and branch are removed.
- Never commit to `main` directly, never force-push, never merge pull requests from a workflow.

## GitHub issues and changes

The `openspec-github-issues` skill (`/issue`) triages issues, labels and types them, proposes a linked change, and keeps the issue in sync through implementation and archive. Issue to change links live in `openspec/changes/<name>/github-issue.yaml`.

## Frontend work uses the Angular agent

The Angular app under `src/Frontend/` (Angular 22, Angular Material 22.2.2, zoneless, signals) is built by the `angular-frontend` agent (`.claude/agents/angular-frontend.md`). Use it for any screen, component, store, form, Material or frontend test work. It keeps component data in a local immutable state store exposed as read-only signals.

## Gateway routes are owned by a skill

The YARP `gateway` in the Aspire AppHost is the single backend endpoint the frontend calls. Its route table (`GatewayRoutes.cs` in the AppHost) is owned by the `yarp-gateway-routes` skill (`.claude/skills/yarp-gateway-routes/SKILL.md`). Load it when a backend module or `*.Api` project is added, when an API maps an endpoint under a new first path segment, or when the route table changes. Every new, renamed or removed prefix is proposed and approved before it is added.
