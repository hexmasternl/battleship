---
description: "Bring the Claude Code and GitHub Copilot configuration in this repository back in sync (skills, commands/prompts, MCP servers)"
---

Run the `sync-agent-config` skill (`.github/skills/sync-agent-config/SKILL.md`) with the arguments given:

- no arguments - run the drift check, mirror every reported difference from the side that was most recently edited (use `git status` and `git diff` to tell), rerun the check, report
- `check` - only run the drift check and report; change nothing
- `from claude` - treat `.claude/` and `.mcp.json` as the source and overwrite the Copilot side
- `from copilot` - treat `.github/skills`, `.github/prompts` and `.vscode/mcp.json` as the source and overwrite the Claude Code side

If both sides were edited independently and no direction was given, stop and ask which side wins. Follow the skill's transformation rules exactly and do not reword either copy.
