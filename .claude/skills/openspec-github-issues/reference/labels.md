# Labels, severity and issue types

Every issue that has been triaged carries exactly: one or more **service** labels, one **kind** label, one **severity** label, and the `openspec` label once a change is linked. Nothing else is added by this skill. Labels already on the issue that are not in this catalog are left alone.

## Ensuring labels exist

`gh label create` with `--force` is idempotent: it creates the label or updates its colour and description. Run it for each label you are about to apply, before `gh issue edit`. Service labels that are not in the table below (a new directory under `src/`) use the service colour and the description `Touches the <Name> service`.

```bash
gh label create "players"  --color 1d76db --description "Touches the Players service" --force
gh label create "games"    --color 1d76db --description "Touches the Games service" --force
gh label create "frontend" --color 1d76db --description "Touches the frontend" --force
gh label create "aspire"   --color 1d76db --description "Touches the Aspire host or service defaults" --force

gh label create "bug"           --color d73a4a --description "Something isn't working" --force
gh label create "feature"       --color 0e8a16 --description "New functionality that does not exist yet" --force
gh label create "enhancement"   --color a2eeef --description "Improvement to an existing feature" --force
gh label create "tech-debt"     --color fbca04 --description "Refactoring, tooling or internal quality work" --force
gh label create "documentation" --color 0075ca --description "Improvements or additions to documentation" --force

gh label create "severity:critical" --color b60205 --description "Data loss, security issue, or the game cannot be played" --force
gh label create "severity:high"     --color d93f0b --description "A core flow is broken and there is no workaround" --force
gh label create "severity:medium"   --color fbca04 --description "Degraded behaviour with a workaround, or a normal feature request" --force
gh label create "severity:low"      --color c2e0c6 --description "Cosmetic, minor, or nice to have" --force

gh label create "openspec"   --color 5319e7 --description "Tracked by an OpenSpec change" --force
gh label create "needs-info" --color d876e3 --description "Triage is blocked on information from the reporter" --force
```

`bug`, `enhancement` and `documentation` are GitHub defaults that already exist in this repository; `--force` keeps them in step with this catalog without creating duplicates.

## Service labels

| Label | Applies when the issue touches |
|---|---|
| `players` | `src/Players` (player registration, profiles, identity) |
| `games` | `src/Games` (game setup, boards, turns, rules) |
| `aspire` | `src/Aspire` (AppHost orchestration, ServiceDefaults, local hosting) |
| `frontend` | the web client, even before it has a directory under `src/` |
| `<new>` | any new top-level directory under `src/`, lowercased |

Apply every service the change will modify, not only the one where the symptom shows. An issue that spans services is normal; pick the services from the proposal's Impact section once it exists and correct the labels during sync if the proposal widened or narrowed scope.

## Kind labels

| Label | Use for |
|---|---|
| `bug` | Existing behaviour does not match what the spec or a reasonable user expects |
| `feature` | Behaviour the system does not have yet |
| `enhancement` | An existing feature should do something better, faster, or more completely |
| `tech-debt` | Refactoring, tooling, dependency or structure work with no behaviour change (the change will usually set `skip_specs: true`) |
| `documentation` | Docs only |

Exactly one kind label per issue. When a report mixes a bug with a feature request, split it: triage the bug, and tell the user the feature part deserves its own issue (offer to create it in `issue` mode after the proposal exists).

## Severity rubric

| Label | Criteria |
|---|---|
| `severity:critical` | Data loss or corruption, security or privacy exposure, a service that does not start, or nobody can play a game |
| `severity:high` | A core flow (registering, starting a game, taking a turn, finishing a game) is broken for a meaningful share of users and there is no workaround |
| `severity:medium` | Degraded behaviour with a workaround, incorrect edge cases, or any ordinary feature request |
| `severity:low` | Cosmetic issues, wording, logging, small conveniences |

Severity is about impact, not effort. A one-line fix that takes the game down is critical. A large feature nobody is blocked on is medium.

## Issue types

The organisation defines issue types that are set with `gh issue edit <n> --type <Name>` (or `--type` on `gh issue create`). Discover them before use; do not assume the names:

```bash
gh api graphql -f query='query($org:String!){ organization(login:$org){ issueTypes(first:20){ nodes{ name isEnabled } } } }' -f org="$(gh repo view --json owner -q .owner.login)"
```

Mapping from kind to type:

| Kind label | Issue type |
|---|---|
| `bug` | `Bug` |
| `feature`, `enhancement` | `Feature` |
| `tech-debt`, `documentation` | `Task` |

If the query returns no enabled types (a personal repository, or the feature is off), skip `--type` and mention in the report that only labels were set.
