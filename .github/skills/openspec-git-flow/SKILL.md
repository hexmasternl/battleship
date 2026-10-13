---
name: openspec-git-flow
description: Git branching, worktree, commit and pull-request rules for every OpenSpec change in this repository. Load before proposing, applying or archiving an OpenSpec change (opsx propose/apply/archive), before creating a worktree for a change, and whenever the user asks where a change should be committed, which branch to use, or how the PR flow works. Also use when the user says "git flow", "development branch", or "worktree" in the context of a change.
allowed-tools: Bash(git:*), Bash(gh:*), Bash(openspec:*), Bash(dotnet:*)
license: MIT
compatibility: Requires git, the GitHub CLI (gh) authenticated with repo scope, and the openspec CLI.
metadata:
  author: hexmaster
  version: "1.0"
---

Every OpenSpec change moves through git the same way: it is **proposed on `development`**, **implemented in its own worktree and branch**, **merged back into `development` through a pull request**, and **archived on `development`**. This skill is the only place those rules live. The generated `openspec-*` skills stay untouched; this skill wraps them.

## The two fixed branches and the main worktree

- The repository checkout at the repository root (the first line of `git worktree list`) is the **main worktree**. The user calls it `main`.
- `main` and `development` both live in the main worktree and nowhere else. Never create a worktree for either of them. Switch between them with `git checkout` in the main worktree.
- `development` is the integration branch. Every proposal and every archive is committed there. Pull requests from change branches target it.
- `main` is never committed to directly by these workflows.

**Ensure `development` exists** before any propose, apply or archive step. Run in the main worktree:

```bash
git fetch origin --prune
if git show-ref --verify --quiet refs/heads/development; then
  git checkout development
elif git show-ref --verify --quiet refs/remotes/origin/development; then
  git checkout -b development --track origin/development
else
  git checkout -b development main
fi
```

If the checkout fails because of conflicting local changes, stop and show the user the error. Do not stash or discard anything on their behalf.

## Commit scope and messages

- Stage paths, never everything, in the main worktree: proposals stage `openspec/changes/<name>`, archives stage `openspec/`. Other uncommitted work in the main worktree is left alone.
- Inside a change worktree the whole tree belongs to the change, so `git add -A` is fine there. Check `git status --short` before each commit for files that should not be there (build output, secrets, local settings).
- Message format: `<type>(<scope>): <imperative summary>` with `type` one of `feat`, `fix`, `refactor`, `test`, `docs`, `chore` and `scope` the service touched (`players`, `games`, `frontend`, `aspire`) or `openspec` for planning artifacts. Body lines: `Change: <change-name>`, `Task: <id>` for implementation commits, and `Refs #<n>` when the change tracks a GitHub issue (read `github-issue.yaml`).
- Commit autonomously where this skill says so. Do not ask for confirmation for those commits; the user has delegated them.

## Propose

Wraps `openspec-propose` (or a propose started from the `openspec-github-issues` skill).

1. In the main worktree, ensure `development` (above). Confirm `git branch --show-current` prints `development` before anything is written.
2. Run the propose workflow. Let it finish, including any linking the issue skill does.
3. Commit the change directory and nothing else:
   ```bash
   git add "openspec/changes/<name>"
   git commit -m "chore(openspec): propose <name>" -m "Change: <name>" [-m "Refs #<n>"]
   ```
4. Do not push. `development` is pushed by the apply flow before the first pull request, and by the user otherwise.
5. Report the commit hash and that `development` holds the proposal.

## Apply

Wraps `openspec-apply-change`. Implementation never happens in the main worktree.

1. **Preconditions** (main worktree): on `development`; the change directory is committed (`git status --porcelain openspec/changes/<name>` is empty, otherwise run the propose commit step first); all planning artifacts exist (`openspec status --change <name>`).
2. **Worktree and branch**. The worktree lives one level above the repository, in `../battleship-worktrees/<change-name>`. The branch is `change/<change-name>`.
   ```bash
   ROOT=$(git rev-parse --show-toplevel)
   WT="$ROOT/../battleship-worktrees/<change-name>"
   mkdir -p "$ROOT/../battleship-worktrees"
   if git worktree list --porcelain | grep -qx "branch refs/heads/change/<change-name>"; then
     : # resuming: the branch is already checked out in a worktree; use the path git lists for it
   elif git show-ref --verify --quiet "refs/heads/change/<change-name>"; then
     git worktree add "$WT" "change/<change-name>"
   else
     git worktree add "$WT" -b "change/<change-name>" development
   fi
   ```
   When resuming, use the `worktree <path>` line that precedes the matching `branch` line in `git worktree list --porcelain`; never create a second worktree for the same change.
3. **Move the session into the worktree.** In Claude Code use ``cd` into the worktree directory. Every build, test, edit and git command for the implementation runs from the worktree. The worktree has its own copy of `openspec/`, so `openspec` commands there resolve the right root.
4. **Implement with frequent, logical commits.** Run the apply workflow task by task. After each task is done and verified (`dotnet build` and the relevant `dotnet test` from the worktree), tick its checkbox in `tasks.md` and commit the code and `tasks.md` together with the commit format above. One task per commit at minimum; a large task may take several commits, each a coherent step that builds. Never leave a session with completed work uncommitted.
5. **Finish.** When every task is checked:
   - Run the full build and test suite for `src/Battleship.slnx` from the worktree. Fix failures before continuing; do not push red.
   - Make sure `development` exists on the remote, then push the change branch:
     ```bash
     git push -u origin development:development   # no-op when already up to date
     git push -u origin "change/<change-name>"
     ```
   - Create the pull request into `development`:
     ```bash
     gh pr create --base development --head "change/<change-name>" --title "<goal from .openspec.yaml, or the proposal title>" --body-file <pr-body.md>
     ```
     Body (write it to a temporary file outside the repo):
     ```markdown
     ## Summary
     <Why paragraph from proposal.md>

     ## What changed
     <What Changes bullets from proposal.md>

     ## Tasks
     <the checked task list from tasks.md>

     ## Specs
     <capabilities added or modified, or "none (skip_specs)">

     ## Tracking
     OpenSpec change `openspec/changes/<change-name>/`.
     Relates to #<n>   <- only when github-issue.yaml exists

     ## Verification
     <build and test commands run and their result>
     ```
     Do not write `Closes #<n>`: the pull request targets `development`, not the default branch, and the issue is closed by the archive flow.
   - If the change tracks a GitHub issue, run the `openspec-github-issues` skill in sync mode so the issue shows the pull request.
6. **Stop.** Do not merge the pull request and do not remove the worktree. Report the pull request URL, the branch, the worktree path and the commit count. Stay in the worktree until the user asks to leave; the propose and archive flows need the main worktree, so return there (`ExitWorktree` with `keep`, or `cd`) before starting either.

## Archive

Wraps `openspec-archive-change`. Runs only after the change's pull request is merged.

1. **Precondition**: the pull request is merged into `development`.
   ```bash
   gh pr list --head "change/<change-name>" --state all --json number,state,baseRefName,mergedAt
   ```
   Require one entry with `state` `MERGED` and `baseRefName` `development`. Otherwise stop and tell the user what the pull request state is. A change without a pull request is archived only when the user explicitly confirms it needs no implementation.
2. In the main worktree, ensure `development` and bring it up to date:
   ```bash
   git pull --ff-only origin development
   ```
   A non-fast-forward pull means local `development` diverged; stop and show the user.
3. Run the archive workflow (it moves the change and syncs the specs).
4. Commit the result and nothing else:
   ```bash
   git add openspec
   git commit -m "chore(openspec): archive <name>" -m "Change: <name>" [-m "Refs #<n>"]
   ```
   Do not push; the user pushes `development`, or the next apply flow does.
5. Clean up the implementation worktree and branch now that they are merged:
   ```bash
   git worktree remove "../battleship-worktrees/<change-name>"
   git branch -d "change/<change-name>"
   git push origin --delete "change/<change-name>"
   ```
   If the worktree has uncommitted changes, `git worktree remove` refuses; show the user instead of forcing.
6. If the change tracked a GitHub issue, run the `openspec-github-issues` skill in sync mode; it closes the issue.
7. Report the archive commit, the archived path and what was cleaned up.

## Guardrails

- Never run implementation commands, builds for a change, or `openspec-apply-change` in the main worktree.
- Never create a worktree inside the repository or under `.claude/worktrees/`; the location is always `../battleship-worktrees/<change-name>` next to the repository.
- Never commit to `main`. Never force-push. Never rewrite history on `development` or on a pushed change branch.
- Never merge a pull request from these workflows; the user merges.
- One change per worktree and per branch. Two changes never share a branch.
- If any git command fails, stop at that point, show the exact error, and leave the tree as it is.
