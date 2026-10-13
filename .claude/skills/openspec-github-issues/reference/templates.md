# Templates

All bodies and comments are GitHub-flavoured Markdown. Write them to a temporary file and pass it with `--body-file`; never inline multi-line text on the command line. Replace every `<placeholder>`. Remove a section only where the template says it is conditional.

## Sidecar: `openspec/changes/<name>/github-issue.yaml`

Machine-readable link from the change to the issue. Lives in the change directory so it is archived with the change. Keep exactly these keys.

```yaml
repo: <owner>/<repo>
issue: <number>
url: https://github.com/<owner>/<repo>/issues/<number>
title: <issue title at time of linking>
linked: <YYYY-MM-DD>
```

## `.openspec.yaml` additions

Append to the scaffolded file (which already has `schema:` and `created:`). Do not add other keys.

```yaml
goal: <issue title>
affected_areas:
  - <service label>
  - <service label>
```

## Proposal tracking line

Insert as the first line after the `# Proposal` heading, separated by blank lines:

```markdown
# Proposal

> Tracks GitHub issue [<owner>/<repo>#<number>](https://github.com/<owner>/<repo>/issues/<number>).

## Why
```

## Managed section on the issue

Always the last thing in the issue body. Everything between the markers is owned by this skill and rewritten on every sync; nothing outside them is touched by sync. Keep the markers exactly as written.

```markdown
<!-- openspec:begin -->
## OpenSpec

| | |
|---|---|
| Change | `<change-name>` |
| Status | <proposed \| in progress (<done>/<total> tasks) \| implemented (awaiting archive) \| archived> |
| Location | `openspec/changes/<name>/` on branch `<branch>` |
| Pull request | <#<number> (<open \| merged \| closed>) linking to the PR, or "not yet opened"> |
| Proposal | [proposal.md](https://github.com/<owner>/<repo>/blob/<branch>/openspec/changes/<name>/proposal.md) |
| Design | [design.md](...) |
| Specs | [specs/](https://github.com/<owner>/<repo>/tree/<branch>/openspec/changes/<name>/specs) |
| Tasks | [tasks.md](...) |

_Links resolve once the branch is pushed. Managed by the openspec-github-issues skill; edit above this section, not inside it._
<!-- openspec:end -->
```

Omit the Design row when the change skipped `design.md`, and the Specs row when the change has `skip_specs: true`. `<branch>` is `development` while the change is proposed or archived, and `change/<name>` while it is being implemented (see the `openspec-git-flow` skill). When the change is archived, the Location row becomes `openspec/changes/archive/<archived-name>/` and the links follow.

Replacing the section without touching the rest of the body:

```bash
gh issue view <n> --json body -q .body > body.md
# drop an existing managed block, then append the fresh one
awk '/<!-- openspec:begin -->/{skip=1} !skip{print} /<!-- openspec:end -->/{skip=0}' body.md > body.new.md
printf '\n' >> body.new.md && cat section.md >> body.new.md
gh issue edit <n> --body-file body.new.md
```

## Triage body (rewrite of an existing issue)

```markdown
## Summary

<Two to four sentences: what is wrong or wanted, who is affected, why it matters. Written from the code, not just the report.>

## Context

- **Services**: <users, games, ...>
- **Where**: <endpoints, handlers, components or files involved, with paths>
- **Current behaviour**: <what the code does today, as observed>

## Steps to reproduce

<Bugs only. Numbered steps, then "Actual" and "Expected". Remove this section for features.>

## Acceptance criteria

- [ ] <observable, testable outcome>
- [ ] <observable, testable outcome>

## Scope

**In**: <what the change will cover>
**Out**: <what it deliberately will not cover>

## Open questions

<Questions the reporter or owner must answer, or assumptions made in their absence, each marked as "Assumed: ...". Remove the section when there are none.>

<details>
<summary>Original report</summary>

<the original body, verbatim>

</details>

<managed section>
```

Add the managed section only once a change is linked (triage step 7). In triage-only mode or when blocked on `needs-info`, end the body after the details block.

## Issue body created from a change

```markdown
## Summary

<Why section of the proposal, condensed to a paragraph>

## What changes

<What Changes bullets from the proposal>

## Context

- **Services**: <affected_areas or services from Impact>
- **Capabilities**: <new and modified capabilities from the proposal>

## Acceptance criteria

- [ ] <one per requirement or scenario in the delta specs>

<managed section>
```

## Comments

**Proposal linked** (after triage step 7 or `issue` mode):

```markdown
Triaged and tracked by OpenSpec change `<change-name>`.

**Why**: <Why paragraph>

**What changes**
<What Changes bullets>

**Capabilities**: <list, or "none (skip_specs)">

Artifacts: proposal, specs, design, tasks under `openspec/changes/<name>/` on `<branch>`. Implementation has not started.
```

**Needs info** (triage blocked):

```markdown
Triage is blocked until these are answered:

1. <question>
2. <question>

Labelled `needs-info`. Once answered, triage can continue and a change proposal will be created.
```

**Progress** (sync, only when the count moved):

```markdown
Implementation progress on `<change-name>`: <done>/<total> tasks complete.

<optional one line on what was finished since the last update>
```

**Pull request opened** (sync, first time a pull request exists):

```markdown
Implementation of `<change-name>` is complete and up for review in #<pr-number> (into `development`). This issue closes once the pull request is merged and the change is archived.
```

**Implemented** (sync, all tasks checked, no pull request yet):

```markdown
All <total> tasks of `<change-name>` are complete. The change is awaiting archive; this issue closes when the specs are synced and the change is archived.
```

**Archived** (used as the closing comment):

```markdown
Implemented and archived as `openspec/changes/archive/<archived-name>/`.

Specs updated: <capability paths, or "none (skip_specs)">.
```

**Linked** (`link` mode):

```markdown
Now tracked by OpenSpec change `<change-name>` (`openspec/changes/<name>/` on `<branch>`).
```
