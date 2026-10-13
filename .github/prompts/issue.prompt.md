---
description: "Triage a GitHub issue into an OpenSpec change, create an issue for a change, link the two, or sync issue status"
---

Run the `openspec-github-issues` skill with the arguments after `/issue`:

- `/issue triage` - triage every open issue that has no labels, each with its own linked change
- `/issue triage <number|url>` - read the issue, flesh it out, set labels and type, propose a linked OpenSpec change
- `/issue triage-only <number|url>` - the same, but stop before proposing a change
- `/issue create <change-name>` - create a GitHub issue from an existing change and link both ways
- `/issue link <change-name> <number|url>` - relate an existing change and an existing issue
- `/issue sync <change-name|number|url>` - refresh the issue's managed section; comment on progress; close when the change is archived

With no arguments, or `triage` without a reference, triage every open issue that has no labels; never ask which issue to pick. Follow the skill in `.github/skills/openspec-github-issues/SKILL.md` exactly, including its planning boundary: never start implementing in the same response.
