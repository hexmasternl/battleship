---
name: openspec-github-issues
description: Relate GitHub issues and OpenSpec changes in both directions. Use when the user wants to triage a GitHub issue (read it, flesh it out, set service/kind/severity labels and the issue type, then create a linked OpenSpec change proposal), create a GitHub issue for an existing OpenSpec change, link a change and an issue, or sync an issue after apply or archive (progress comments, closing). Also use when the user mentions an issue number like "#12", pastes a GitHub issue URL, says "triage", or says "opsx issue" / "openspec issue".
allowed-tools: Bash(openspec:*), Bash(gh:*)
license: MIT
compatibility: Requires the openspec CLI and the GitHub CLI (gh) authenticated with repo scope.
metadata:
  author: hexmaster
  version: "1.0"
---

Relate GitHub issues and OpenSpec changes so each one points at the other and the issue keeps tracking the change through proposal, implementation and archive.

This skill builds on the OpenSpec skills already installed in this project. It never writes planning artifacts itself: creating a change always goes through the `openspec-propose` skill, implementation through `openspec-apply-change`, and archiving through `openspec-archive-change`. This skill only adds the GitHub side and the links between the two.

**Planning boundary**: Triaging an issue or proposing a change authorizes planning only. Do not edit project code in this workflow, even if the issue asks to fix or build something. Implementation starts only when the user explicitly asks to apply the change.

**Outward-facing writes**: Every `gh issue edit`, `gh issue comment`, `gh issue create`, `gh issue close` and `gh label create` is visible to everyone on the repository. Prepare the full content first, then write it in as few calls as possible. Never post partial drafts. Never close an issue unless its change is archived (see Sync).

---

## Modes

Pick the mode from the user's request. Arguments are an issue reference (`12`, `#12`, or an issue URL) and/or an OpenSpec change name.

| Mode | Trigger | What happens |
|---|---|---|
| **triage** `<issue>` | "triage #12", "pick up issue 12", "work out issue 12" | Read the issue, flesh it out, set labels and type, propose a linked OpenSpec change, link both ways. |
| **triage-only** `<issue>` | "just triage", "label issue 12", "triage without a proposal" | Same as triage but stop before proposing a change. |
| **issue** `<change>` | "create an issue for change X", "open a GitHub issue for X" | Create a GitHub issue from an existing change's proposal, then link both ways. |
| **link** `<change>` `<issue>` | "link change X to issue 12" | Both already exist. Write the link on both sides without rewriting the issue body. |
| **sync** `<change>` or `<issue>` | "sync issue 12", "update the issue for X", after apply or archive | Refresh the managed section on the issue from the change's current state. Comment on progress. Close when archived. |

If the request names an issue that already has a linked change (see **Finding links**), triage degrades to sync and says so. If a change already has a linked issue, `issue` mode degrades to sync.

## Conventions

Read `reference/labels.md` for the label catalog, the severity rubric and the issue type mapping. Read `reference/templates.md` for the issue body, the managed section, the sidecar file and the comment templates. Both files are authoritative for wording and formatting.

- **Repository**: resolve once with `gh repo view --json nameWithOwner -q .nameWithOwner`. Never hard-code it.
- **Services**: the service labels are the lowercase names of the top-level directories under `src/` (ignore `.vs`, solution files and anything that is not a directory), plus `frontend`. Discover them with `ls src` every time; a new directory means a new service label.
- **Change name**: `issue-<number>-<slug>` where `<slug>` is a short kebab-case description, for example `issue-12-player-login-timeout`. The number makes the relation visible in the changes directory.
- **Change → issue link**: three things, all inside the change directory so they move with it on archive:
  1. `github-issue.yaml` sidecar (format in `reference/templates.md`). This is the machine-readable link.
  2. `.openspec.yaml`: set `goal:` to the issue title and `affected_areas:` to the service labels. Only these two keys; OpenSpec warns about unknown keys, so the GitHub data stays in the sidecar.
  3. A blockquote directly under the `# Proposal` heading in `proposal.md`: `> Tracks GitHub issue [<owner>/<repo>#<n>](<url>).`
- **Issue → change link**: a managed section at the end of the issue body between `<!-- openspec:begin -->` and `<!-- openspec:end -->`, plus the `openspec` label. Only ever rewrite the text between the markers. Everything above them belongs to the author and the triage.
- **Artifact links**: point at `https://github.com/<owner>/<repo>/blob/<branch>/openspec/changes/<name>/<file>` using the current branch (`git branch --show-current`). State the branch in the managed section; the link resolves once the branch is pushed.

## Finding links

- Issue → change: `grep -ls "issue: <n>$" openspec/changes/*/github-issue.yaml openspec/changes/archive/*/github-issue.yaml`. A hit under `archive/` means the change is already done.
- Change → issue: read `openspec/changes/<name>/github-issue.yaml` (or the archive path).
- Always resolve the OpenSpec root first with `openspec context --json` and use `root.path`. If it reports `no_openspec_root`, stop and follow the project-check rules in the `openspec-propose` skill. Do not initialize OpenSpec from here.

## Triage

1. **Read the issue**
   ```bash
   gh issue view <n> --json number,title,body,state,url,author,labels,comments,assignees
   ```
   If the issue is closed, say so and stop unless the user insists. If it already carries a managed section or a sidecar points at it, switch to sync.

2. **Understand it against the code**. Read-only. Find the services involved (`ls src`, then the relevant project under `src/<Service>/`), the endpoints, handlers or components the report touches, and what the code does today. Distinguish observed behaviour from what the reporter assumes. Keep this proportional: enough to write acceptance criteria you can defend, not a design.

3. **Classify**. Using `reference/labels.md`, decide:
   - the service labels (one or more)
   - the kind: `bug`, `feature`, `enhancement`, `tech-debt` or `documentation`
   - the severity label
   - the issue type: `Bug`, `Feature` or `Task`
   - whether the issue needs more information before a proposal can be written

   Material ambiguity is anything that would change scope, externally observable behaviour, compatibility or acceptance criteria. If you find some, ask the user (open-ended, no preset options) before writing anything to GitHub. If the user cannot answer, add the `needs-info` label, post the open questions as a comment using the template, update the body, and stop before proposing.

4. **Flesh out and write the issue**. Draft the full body from the triage template in `reference/templates.md`. Keep the reporter's original text verbatim inside the `Original report` details block. Then write everything in one pass:
   ```bash
   # ensure every label you will apply exists (idempotent, see reference/labels.md)
   gh label create "<name>" --color <hex> --description "<text>" --force
   gh issue edit <n> --body-file <drafted-body.md> --add-label "<svc>,<kind>,<severity>" --type <Type>
   ```
   Draft the body in the scratchpad or a temporary file, never inside the repository. Set the type only when the organisation exposes it (`gh api graphql` query in `reference/labels.md`); otherwise rely on labels and say so.
   If the title is vague, improve it with `--title`, keeping the reporter's intent.

   In **triage-only** mode, report what you set and stop here.

5. **Propose the change**. Load the `openspec-git-flow` skill and follow its Propose flow: it puts the main worktree on `development` before anything is written. Then invoke the `openspec-propose` skill with the change name from the conventions and a description built from the fleshed-out issue: summary, acceptance criteria, affected services and the open assumptions. The propose skill owns every planning artifact and its own clarification questions. Wait until it reports that all artifacts exist.

6. **Link the change to the issue, then commit**. In the change directory: write `github-issue.yaml`, add `goal:` and `affected_areas:` to `.openspec.yaml`, and insert the tracking blockquote into `proposal.md`. Run `openspec validate <name>` and fix anything you introduced. Then commit the change directory to `development` exactly as the `openspec-git-flow` Propose flow says (`chore(openspec): propose <name>` with `Refs #<n>`), so the link ships with the proposal.

7. **Link the issue to the change**. Replace or append the managed section in the issue body (template in `reference/templates.md`, status `proposed`), add the `openspec` label, and post one comment with the proposal summary: the Why paragraph, the What Changes bullets, the capabilities touched and the artifact list. One `gh issue edit`, one `gh issue comment`.

8. **Report**. Issue number and title, labels and type set, change name and path, and the reminder that implementation starts with `/opsx:apply`. Do not start implementing.

## Issue (change → new issue)

1. Read the change: `openspec show <name> --json`, `proposal.md`, `tasks.md`, `.openspec.yaml`. If a sidecar already exists, switch to sync.
2. Classify as in Triage step 3 from the proposal content. Services come from `affected_areas` when present, otherwise from the impact section and the code.
3. Build the issue body from the proposal template in `reference/templates.md` including the managed section with status `proposed`. Create it in one call:
   ```bash
   gh issue create --title "<title>" --body-file <body.md> --label "<svc>,<kind>,<severity>,openspec" --type <Type>
   ```
   Take the issue number from the URL the command prints.
4. Link the change to the issue (Triage step 6). The change keeps its existing name; do not rename it.
5. Report the issue URL and the change name.

## Link (both exist)

1. Confirm both exist. Refuse if the issue already has a different linked change or the change a different issue; the user must say which link wins.
2. Write the change side (Triage step 6) and the issue side (Triage step 7, without rewriting anything above the markers). Post a short comment that the issue is now tracked by the change.

## Sync

Run this after `/opsx:apply` sessions and after `/opsx:archive`. The OpenSpec config in this project reminds those workflows to call it.

1. Resolve the pair from either side (see **Finding links**). Stop and say so if there is no link.
2. Determine the state:
   - change directory under `openspec/changes/archive/`: **archived**
   - otherwise `openspec status --change <name> --json`, and count `- [x]` versus `- [ ]` in `tasks.md`: **proposed** when no task is checked, **in progress** with `<done>/<total>` otherwise, **implemented** when all are checked but not archived
   - the pull request, if any: `gh pr list --head "change/<name>" --state all --json number,url,state`. The implementation branch is always `change/<name>` (see the `openspec-git-flow` skill). When the change is being applied in its worktree, read `tasks.md` from the worktree copy at `../battleship-worktrees/<name>/openspec/changes/<name>/tasks.md`, since the main worktree still holds the proposal-time version.
3. Rewrite only the managed section with the current status, change path, task count, pull request and artifact links. One `gh issue edit --body-file`.
4. Comment only when something changed since the last sync comment (read the existing comments first): progress moved, implementation complete, or archived. Use the matching template.
5. When **archived**: close the issue with the archive comment, reason completed:
   ```bash
   gh issue close <n> --reason completed --comment "<archive comment>"
   ```
   Never close for any other state. If the issue is already closed but the change is still active, report the mismatch instead of reopening.

## Guardrails

- Read every file from disk when you need it; do not rely on what was in the conversation earlier.
- Never modify the generated `openspec-*` skills or commands; integrate through this skill and `openspec/config.yaml` only.
- Never put labels, types or severity on an issue you have not read in full, including its comments.
- Never discard the reporter's words. Rewrites keep them in the `Original report` block.
- Never create a change for a closed issue, a duplicate, or an issue labelled `wontfix` without the user confirming.
- Keep drafts out of the repository working tree. Use the scratchpad or a temporary file for bodies and comments.
- If `gh` is not authenticated or the repository has issues disabled, stop and tell the user exactly which command failed.
