# Battleship

.NET solution (`src/Battleship.slnx`) with one project group per service under `src/` (`Players`, `Games`, `Aspire` for hosting). Planning is spec-driven with OpenSpec (`openspec/`).

## OpenSpec changes follow the git flow skill

Every OpenSpec change is governed by the `openspec-git-flow` skill (`.claude/skills/openspec-git-flow/SKILL.md`). Load it before `/opsx:propose`, `/opsx:apply`, `/opsx:archive`, `/issue`, or any request that proposes, implements or archives a change. The rules in short:

- `main` and `development` both live in the main worktree (this repository checkout). Never create a worktree for either. If `development` is missing, create it from `main`.
- A proposal is created on `development` and committed there autonomously (`chore(openspec): propose <name>`), staging only the change directory.
- Applying a change always happens in its own worktree at `../battleship-worktrees/<change-name>` on branch `change/<change-name>`, created from `development`. Commit after every task with a logical message. When all tasks are done, push and open a pull request into `development`. Do not merge it.
- Archiving happens on `development` after the pull request is merged, and the result is committed there autonomously (`chore(openspec): archive <name>`). Then the change worktree and branch are removed.
- Never commit to `main` directly, never force-push, never merge pull requests from a workflow.

## GitHub issues and changes

The `openspec-github-issues` skill (`/issue`) triages issues, labels and types them, proposes a linked change, and keeps the issue in sync through implementation and archive. Issue to change links live in `openspec/changes/<name>/github-issue.yaml`.
