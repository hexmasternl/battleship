---
name: sync-agent-config
description: Keep the Claude Code and GitHub Copilot configuration in this repository in sync. Use after creating, updating or removing a skill, a custom command or prompt, or an MCP server, when asked "are the agent configs in sync", "sync copilot", "sync claude", or before committing changes under .claude/, .github/, .mcp.json or .vscode/mcp.json.
metadata:
  sync: verbatim
---

# Sync agent configuration (Claude Code <-> GitHub Copilot)

This repository is worked on by developers using Claude Code and by developers using
GitHub Copilot. Both tools read their configuration from the repository, from different
locations and in slightly different shapes. Every skill, custom command and MCP server
must exist on both sides, and the two copies must say the same thing.

**The rule:** a change to agent configuration is not finished until the mirror side has
the same change and the drift check passes. Never commit one side alone.

## What is mirrored

| Thing | Claude Code | GitHub Copilot |
|---|---|---|
| Skill | `.claude/skills/<name>/SKILL.md` (+ any supporting files) | `.github/skills/<name>/SKILL.md` (+ the same supporting files) |
| Custom command | `.claude/commands/<name>.md` | `.github/prompts/<name>.prompt.md` |
| Namespaced command | `.claude/commands/<ns>/<name>.md` (invoked as `/<ns>:<name>`) | `.github/prompts/<ns>-<name>.prompt.md` (invoked as `/<ns>-<name>`) |
| MCP server | `.mcp.json`, key `mcpServers.<name>` | `.vscode/mcp.json`, key `servers.<name>` |

Not mirrored, because they are tool-specific or per-developer:

- `.claude/settings.json`, `.claude/settings.local.json` (permissions, hooks, which project MCP servers a developer enabled)
- `.vscode/settings.json` and other editor settings
- `CLAUDE.md` and `.github/copilot-instructions.md` are both repository instructions, but they
  are free-form. If both exist, keep their guidance equivalent by hand; this skill does not diff them.

## Direction

The side that was just edited is the **source**. The other side is the **target** and is
overwritten from the source. Decide the source before touching anything. If both sides
were edited independently (the drift check reports differences in both directions), stop
and ask which side wins instead of merging.

## Transformation rules

Apply these when copying from Claude Code to Copilot. Apply the inverse when copying
from Copilot to Claude Code.

### Text rewrites (apply to every copied markdown file)

| In the Claude Code copy | In the Copilot copy |
|---|---|
| `/<ns>:<name>` (e.g. `/opsx:apply`) | `/<ns>-<name>` (e.g. `/opsx-apply`) |
| `.claude/skills/` | `.github/skills/` |
| `.claude/commands/<ns>/<name>.md` | `.github/prompts/<ns>-<name>.prompt.md` |
| `.claude/commands/<name>.md` | `.github/prompts/<name>.prompt.md` |
| `$ARGUMENTS` | "the arguments given" (Copilot appends whatever the user typed after the prompt name to the chat message; there is no placeholder) |

Everything else in the body stays byte-identical. Do not reword, reformat or "improve"
the target copy; the two files must diff clean after the rewrites above.

### Tool-specific behaviour

When a step genuinely differs per tool (for example Claude Code has an `EnterWorktree`
tool and Copilot does not), do **not** write two different sentences in the two copies.
Write the conditional once, in the source file, so it reads correctly in both tools:

> In Claude Code use `EnterWorktree` with the worktree path. In other agents `cd` into it.

Both copies then stay identical and the drift check stays meaningful. Hand-editing only
one copy is how sentences get mangled.

### Skills

- Copy the whole directory, including subdirectories such as `reference/` or `scripts/`.
- Frontmatter stays identical. Both tools read `name` and `description`; each ignores keys it
  does not know, so keeping Claude-specific keys on the Copilot side is harmless and keeps the diff clean.
- A skill whose frontmatter contains `metadata: { sync: verbatim }` is copied **without** the text
  rewrites, every file byte-for-byte. Use this only for a skill that must name both sides' paths
  literally (this skill is the one example). Everything else gets the rewrites.
- Removing a skill means deleting the directory on both sides.

### Commands <-> prompts

Frontmatter is the only structural difference.

| Claude Code command frontmatter | Copilot prompt frontmatter |
|---|---|
| `description` | `description` (required, same text) |
| `name`, `category`, `tags` | dropped (Copilot derives the name from the file name) |
| `allowed-tools: Bash(x:*), ...` | dropped. Copilot has `tools: [...]` with its own tool names; add it only when the command genuinely needs to restrict tools, otherwise leave it out |
| `argument-hint` | dropped; describe the arguments in the body instead |
| `model` | dropped (Copilot picks the model in the chat UI) |

The body is copied with the text rewrites above. See `.claude/commands/issue.md` and
`.github/prompts/issue.prompt.md` for a worked pair.

Removing a command means deleting both files. If that empties a namespace directory
under `.claude/commands/`, remove the directory too.

### MCP servers

`.mcp.json` (Claude Code) and `.vscode/mcp.json` (Copilot in VS Code) both describe the
same servers. The top-level key differs and a few fields are spelled differently.

| Claude Code `.mcp.json` | Copilot `.vscode/mcp.json` |
|---|---|
| `{ "mcpServers": { ... } }` | `{ "servers": { ... } }` |
| `"type": "http", "url": "..."` | identical |
| `"type": "sse", "url": "..."` | identical |
| `"command": "...", "args": [...]` (type omitted or `"stdio"`) | `"type": "stdio", "command": "...", "args": [...]` |
| `"env": { "KEY": "${MY_VAR}" }` | `"env": { "KEY": "${env:MY_VAR}" }` |
| `"headers": { "Authorization": "Bearer ${TOKEN}" }` | `"headers": { "Authorization": "Bearer ${input:token}" }` plus an entry in the top-level `inputs` array with `"password": true` |

Never put a real secret in either file. Claude Code reads secrets from environment
variables; Copilot prompts for them through `inputs`. Keep the server **name** identical on
both sides, because `.claude/settings.local.json` and developers' notes refer to it.

`.gitignore` ignores `.vscode/*` with explicit exceptions. `!.vscode/mcp.json` is one of
them; do not remove that line, or the Copilot MCP configuration stops being committed.

## Procedure

1. **Identify the change.** Look at what you just created, edited or deleted, or run
   `git status --short -- .claude .github .mcp.json .vscode/mcp.json` to see what differs
   from the last commit. Name the source side.
2. **Mirror it** using the mapping and transformation rules above. One change at a time:
   skill, then command, then MCP server.
3. **Run the drift check** from the repository root:

   ```powershell
   pwsh -File .claude/skills/sync-agent-config/scripts/check-sync.ps1
   # identical copy on the Copilot side:
   pwsh -File .github/skills/sync-agent-config/scripts/check-sync.ps1
   ```

   It lists skills, commands and MCP servers that exist on only one side, and files whose
   contents still differ after the text rewrites. Exit code 0 means in sync.
   If `pwsh` is not available, do the comparison by hand: `diff -r .claude/skills .github/skills`
   must show only the rewrites listed above, every command must have a prompt and vice versa,
   and the server name sets in both MCP files must be equal.
4. **Fix every reported item** and rerun until clean.
5. **Report** in one short list: what was mirrored, in which direction, and that the check passed.
   Stage both sides together.

## Special cases

- **OpenSpec-managed files.** The `openspec-*` skills and the `opsx` commands/prompts are
  generated by `openspec init` / `openspec update`, which writes both sides itself. Do not
  hand-edit them; after running an OpenSpec update, run the drift check to confirm it left
  both sides consistent.
- **This skill itself** lives on both sides, as `.claude/skills/sync-agent-config/` and
  `.github/skills/sync-agent-config/`, and has a command (`/sync-agent-config` in both tools)
  that runs it. It is marked `sync: verbatim`, so after editing it copy the directory as-is
  (`cp -r`, no rewrites) and rerun the check.
- **Agents.** If `.claude/agents/<name>.md` custom agents are ever added, mirror them to
  `.github/agents/<name>.agent.md` with the same frontmatter mapping as commands. The drift
  check does not cover agents today.
