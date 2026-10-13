#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Reports drift between the Claude Code and GitHub Copilot configuration in this repository.

.DESCRIPTION
  Compares skills (.claude/skills <-> .github/skills), custom commands
  (.claude/commands <-> .github/prompts) and MCP servers (.mcp.json <-> .vscode/mcp.json)
  after applying the text rewrites documented in SKILL.md. Prints one line per problem and
  exits 1 when anything is out of sync, 0 when both sides agree.

.PARAMETER Root
  Repository root. Defaults to the nearest ancestor of this script that contains a .git entry.
#>
[CmdletBinding()]
param(
    [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path (Join-Path $dir '.git'))) {
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    if (-not $dir -or -not (Test-Path (Join-Path $dir '.git'))) {
        throw 'Could not find the repository root; pass -Root.'
    }
    $Root = $dir
}
$Root = (Resolve-Path $Root).Path

$issues = [System.Collections.Generic.List[string]]::new()
function Report([string]$area, [string]$message) { $script:issues.Add("[$area] $message") }

function Read-Text([string]$path) {
    $text = [System.IO.File]::ReadAllText($path)
    return ($text -replace "`r`n", "`n").TrimEnd("`n")
}

# Rewrites applied to a Claude Code file to obtain the expected Copilot file.
function ConvertTo-Copilot([string]$text) {
    $t = $text
    $t = [regex]::Replace($t, '\.claude/commands/([A-Za-z0-9_-]+)/([A-Za-z0-9_-]+)\.md', '.github/prompts/$1-$2.prompt.md')
    $t = [regex]::Replace($t, '\.claude/commands/([A-Za-z0-9_-]+)\.md', '.github/prompts/$1.prompt.md')
    $t = $t.Replace('.claude/skills/', '.github/skills/')
    $t = [regex]::Replace($t, '(?<![\w/])/([a-z0-9]+):([a-z0-9-]+)', '/$1-$2')
    $t = $t.Replace('$ARGUMENTS', 'the arguments given')
    return $t
}

function Split-FrontMatter([string]$text) {
    # Returns @{ Front = <hashtable of top-level scalar keys>; Body = <string> }
    $front = @{}
    $body = $text
    if ($text -match '(?s)^---\n(.*?)\n---\n?(.*)$') {
        $body = $Matches[2]
        foreach ($line in ($Matches[1] -split "`n")) {
            if ($line -match '^([A-Za-z0-9_-]+):\s*(.*)$') {
                $key = $Matches[1]
                $value = $Matches[2].Trim()
                if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") { $value = $Matches[1] }
                $front[$key] = $value
            }
        }
    }
    return @{ Front = $front; Body = $body.Trim() }
}

# ---------------------------------------------------------------- skills
$claudeSkills = Join-Path $Root '.claude/skills'
$copilotSkills = Join-Path $Root '.github/skills'
$a = @(); $b = @()
if (Test-Path $claudeSkills) { $a = @(Get-ChildItem $claudeSkills -Directory | Select-Object -ExpandProperty Name) }
if (Test-Path $copilotSkills) { $b = @(Get-ChildItem $copilotSkills -Directory | Select-Object -ExpandProperty Name) }
foreach ($name in ($a | Where-Object { $_ -notin $b })) { Report 'skill' "$name exists only in .claude/skills" }
foreach ($name in ($b | Where-Object { $_ -notin $a })) { Report 'skill' "$name exists only in .github/skills" }
foreach ($name in ($a | Where-Object { $_ -in $b })) {
    $left = Join-Path $claudeSkills $name
    $right = Join-Path $copilotSkills $name
    # A skill whose SKILL.md frontmatter carries "metadata: { sync: verbatim }" is mirrored
    # byte-for-byte, without the rewrites (it documents both sides' paths literally).
    $verbatim = $false
    $skillMd = Join-Path $left 'SKILL.md'
    if (Test-Path $skillMd) {
        $raw = Read-Text $skillMd
        if ($raw -match '(?s)^---\n(.*?)\n---' -and $Matches[1] -match '(?m)^\s+sync:\s*verbatim\s*$') { $verbatim = $true }
    }
    $leftFiles = @(Get-ChildItem $left -File -Recurse -Name | ForEach-Object { $_.Replace('\', '/') })
    $rightFiles = @(Get-ChildItem $right -File -Recurse -Name | ForEach-Object { $_.Replace('\', '/') })
    foreach ($f in ($leftFiles | Where-Object { $_ -notin $rightFiles })) { Report 'skill' "$name/$f missing in .github/skills" }
    foreach ($f in ($rightFiles | Where-Object { $_ -notin $leftFiles })) { Report 'skill' "$name/$f missing in .claude/skills" }
    foreach ($f in ($leftFiles | Where-Object { $_ -in $rightFiles })) {
        $expected = Read-Text (Join-Path $left $f)
        if (-not $verbatim) { $expected = ConvertTo-Copilot $expected }
        $actual = Read-Text (Join-Path $right $f)
        if ($expected -ne $actual) {
            $how = if ($verbatim) { 'verbatim' } else { 'after rewrites' }
            Report 'skill' "$name/$f differs between .claude/skills and .github/skills ($how)"
        }
    }
}

# ---------------------------------------------------------------- commands <-> prompts
$claudeCommands = Join-Path $Root '.claude/commands'
$copilotPrompts = Join-Path $Root '.github/prompts'
$matchedPrompts = [System.Collections.Generic.HashSet[string]]::new()
if (Test-Path $claudeCommands) {
    foreach ($rel in @(Get-ChildItem $claudeCommands -File -Recurse -Name -Filter '*.md' | ForEach-Object { $_.Replace('\', '/') })) {
        $cmdPath = Join-Path $claudeCommands $rel
        $promptName = ($rel -replace '\.md$', '') -replace '/', '-'
        $promptFile = "$promptName.prompt.md"
        $promptPath = Join-Path $copilotPrompts $promptFile
        if (-not (Test-Path $promptPath)) { Report 'command' ".claude/commands/$rel has no .github/prompts/$promptFile"; continue }
        [void]$matchedPrompts.Add($promptFile)
        $c = Split-FrontMatter (Read-Text $cmdPath)
        $p = Split-FrontMatter (Read-Text $promptPath)
        if (-not $p.Front.ContainsKey('description')) { Report 'command' ".github/prompts/$promptFile has no description" }
        elseif ($c.Front['description'] -ne $p.Front['description']) { Report 'command' "description differs: .claude/commands/$rel vs .github/prompts/$promptFile" }
        if ((ConvertTo-Copilot $c.Body) -ne $p.Body) { Report 'command' "body differs (after rewrites): .claude/commands/$rel vs .github/prompts/$promptFile" }
    }
}
if (Test-Path $copilotPrompts) {
    foreach ($p in Get-ChildItem $copilotPrompts -File -Filter '*.prompt.md') {
        if (-not $matchedPrompts.Contains($p.Name)) { Report 'command' ".github/prompts/$($p.Name) has no matching file under .claude/commands" }
    }
}

# ---------------------------------------------------------------- MCP servers
function Get-Servers([string]$path, [string]$key, [string]$label) {
    if (-not (Test-Path $path)) { return $null }
    $json = Get-Content $path -Raw | ConvertFrom-Json -AsHashtable
    if (-not $json.ContainsKey($key)) { Report 'mcp' "$label has no top-level '$key' object"; return @{} }
    return $json[$key]
}
function Get-ServerShape([hashtable]$s, [bool]$isClaude) {
    # Collapses a server definition to a comparable string. Secret references
    # (${VAR} on the Claude side, ${env:VAR} / ${input:id} on the Copilot side) compare equal.
    $n = [ordered]@{}
    if ($s.ContainsKey('type')) { $n['type'] = [string]$s['type'] }
    elseif ($s.ContainsKey('command')) { $n['type'] = 'stdio' }
    else { $n['type'] = '' }
    foreach ($k in 'url', 'command') { if ($s.ContainsKey($k)) { $n[$k] = [string]$s[$k] } }
    if ($s.ContainsKey('args')) { $n['args'] = (@($s['args']) -join '|') }
    foreach ($k in 'env', 'headers') {
        if ($s.ContainsKey($k)) {
            $pairs = foreach ($e in ($s[$k].GetEnumerator() | Sort-Object Key)) {
                $v = [string]$e.Value
                $v = [regex]::Replace($v, '\$\{(env:|input:)?[A-Za-z_][A-Za-z0-9_-]*\}', '<secret>')
                "$($e.Key)=$v"
            }
            $n[$k] = $pairs -join '|'
        }
    }
    return (@($n.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n")
}
$claudeMcp = Get-Servers (Join-Path $Root '.mcp.json') 'mcpServers' '.mcp.json'
$copilotMcp = Get-Servers (Join-Path $Root '.vscode/mcp.json') 'servers' '.vscode/mcp.json'
if ($null -eq $claudeMcp -and $null -ne $copilotMcp -and $copilotMcp.Count -gt 0) { Report 'mcp' '.vscode/mcp.json defines servers but .mcp.json is missing' }
if ($null -eq $copilotMcp -and $null -ne $claudeMcp -and $claudeMcp.Count -gt 0) { Report 'mcp' '.mcp.json defines servers but .vscode/mcp.json is missing' }
if ($null -ne $claudeMcp -and $null -ne $copilotMcp) {
    foreach ($name in @($claudeMcp.Keys | Where-Object { -not $copilotMcp.ContainsKey($_) })) { Report 'mcp' "server '$name' exists only in .mcp.json" }
    foreach ($name in @($copilotMcp.Keys | Where-Object { -not $claudeMcp.ContainsKey($_) })) { Report 'mcp' "server '$name' exists only in .vscode/mcp.json" }
    foreach ($name in @($claudeMcp.Keys | Where-Object { $copilotMcp.ContainsKey($_) })) {
        $l = Get-ServerShape $claudeMcp[$name] $true
        $r = Get-ServerShape $copilotMcp[$name] $false
        if ($l -ne $r) { Report 'mcp' "server '$name' differs between .mcp.json and .vscode/mcp.json (type/url/command/args/env/headers)" }
    }
}
$ignore = Join-Path $Root '.gitignore'
if ((Test-Path $ignore) -and (Test-Path (Join-Path $Root '.vscode/mcp.json'))) {
    $lines = @(Get-Content $ignore)
    if (($lines -contains '.vscode/*' -or $lines -contains '.vscode/' -or $lines -contains '.vscode') -and -not ($lines -contains '!.vscode/mcp.json')) {
        Report 'mcp' '.gitignore ignores .vscode without a !.vscode/mcp.json exception, so .vscode/mcp.json will not be committed'
    }
}

# ---------------------------------------------------------------- result
if ($issues.Count -eq 0) {
    Write-Host 'Claude Code and GitHub Copilot configuration are in sync.'
    exit 0
}
Write-Host "Found $($issues.Count) sync issue(s):"
$issues | ForEach-Object { Write-Host "  $_" }
exit 1
