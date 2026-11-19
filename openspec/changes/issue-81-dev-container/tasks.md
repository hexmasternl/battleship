# Tasks

## 1. Repository groundwork

- [x] 1.1 Add `.gitattributes` with `* text=auto` and `*.sh text eol=lf` (design 7); verify with `git ls-files --eol` that the existing files are not reported as changed and that a new `.sh` file is stored with LF

## 2. Dev container definition

- [x] 2.1 Create `.devcontainer/devcontainer.json` with the base image and the features from design 1 (dotnet 10.0, Aspire, docker-in-docker, dapr-cli, node 24, github-cli, azure-cli with Bicep, powershell, claude-code), `remoteEnv.SSL_CERT_DIR` as in the Aspire template, and no secrets or host paths; verify the JSON validates with `npx @devcontainers/cli read-configuration --workspace-folder .`
- [x] 2.2 Add the named volume mounts from design 4 (usersecrets, dapr, nuget, npm, node_modules, worktrees; Docker data comes from the docker-in-docker feature's own volume) and `portsAttributes` for the dashboard port; verify the merged `read-configuration` lists all seven mounts
- [x] 2.3 Add `customizations.vscode.extensions` (C# Dev Kit, Aspire, Angular Language Service, Dapr, Bicep, Docker, GitHub Actions, GitHub Copilot Chat, Claude Code, EditorConfig); verify the extension ids exist in the marketplace

## 3. Lifecycle scripts

- [ ] 3.1 Write `.devcontainer/post-create.sh` with `set -euo pipefail`, the `step` helper and the failure trap (design 7), and the steps from design 8: volume ownership, `safe.directory`, npm version from `packageManager`, the OpenSpec CLI, `dotnet restore`, lockfile-guarded `npm ci`, Playwright Chromium, wait for Docker, guarded `dapr init`, stop Redis and Zipkin; verify with `shellcheck` and by running the script twice in a built container, where the second run finishes without reinstalling
- [ ] 3.2 Write `.devcontainer/post-start.sh`: wait for Docker, restart stopped `dapr_placement` and `dapr_scheduler`, `aspire certs trust --non-interactive`, print the next step; wire `postCreateCommand` and `postStartCommand` in `devcontainer.json`; verify with `shellcheck` and by stopping and starting the container, after which `docker ps` shows the Dapr containers running
- [ ] 3.3 Write `.devcontainer/smoke-check.sh` (design 10): print the versions of `dotnet`, `aspire`, `dapr`, `docker`, `node`, `npm`, `gh`, `openspec`, `az bicep`, `pwsh`; fail if `~/.dapr/bin/daprd` is missing; Release build of `src/Battleship.slnx`; backend tests without the `Integration` category; verify it passes inside a freshly built container with `npx @devcontainers/cli up` and `exec`

## 4. End-to-end verification in the container

- [ ] 4.1 In a fresh container, run `aspire run` from `src/Aspire/HexMaster.Battleship.Aspire` and verify with `aspire describe` that every resource (frontend, gateway, three APIs, three sidecars, messaging, storage/tables, devbot) is running and the AppHost Dapr check passed
- [ ] 4.2 From the host browser, open the dashboard through the printed login link and the frontend through its forwarded port, and play a game against the dev bot to the end; verify shots arrive in real time and no CORS errors appear in the browser console
- [ ] 4.3 Run the full `dotnet test --solution src/Battleship.slnx` (Integration category included, with the test Azurite from design 9 started) and `npm test -- --watch=false` in `src/Frontend` inside the container; verify both pass and the storage tests are not skipped
- [ ] 4.4 Rebuild the container and start the AppHost again; verify the generated `rabbitmq-password` and `users-signing-key` are unchanged (`dotnet user-secrets list` in the AppHost folder), an earlier game is still listed, and no Docker image is pulled again
- [ ] 4.5 Inside the container, create a throwaway worktree at `../battleship-worktrees/devcontainer-check` on a scratch branch, build from it, rebuild the container, and verify it still exists; then remove the worktree and the branch

## 5. CI and dependency updates

- [ ] 5.1 Add `.github/workflows/devcontainer.yml` (design 10), triggered on pull requests and pushes to `development` and `main` that touch `.devcontainer/**` or `.gitattributes`, and on `workflow_dispatch`; it builds with `devcontainers/ci` and runs `smoke-check.sh` without pushing an image; verify the workflow passes on the change's pull request
- [ ] 5.2 Add the `devcontainers` ecosystem to `.github/dependabot.yml` (weekly, `target-branch: development`, `chore(deps)` prefix); verify the file is valid YAML and matches the shape of the existing entries

## 6. Documentation

- [ ] 6.1 Rewrite the README Quick start into "Option A: dev container (recommended)" and "Option B: install the tools", keeping the existing prerequisites table, `dapr init` and the launch command (local-messaging delta); verify the README still lists Docker, the Dapr CLI, `dapr init` and `aspire run`, and that its links resolve
- [ ] 6.2 Add a "Use the dev container" section to `docs/prerequisites.md` covering host requirements, what the container provides with Dapr initialised, first-run time, the certificate warning, port forwarding with the same-port caveat, the worktree caveat, and the test Azurite command (help-section delta); extend `src/Frontend/src/app/help/help-docs-content.spec.ts` with an assertion for that section; verify with `ng test --include src/app/help/help-docs-content.spec.ts`
- [ ] 6.3 Add a paragraph to `CONTRIBUTING.md` that points at the dev container as the recommended setup; verify the link to `docs/prerequisites.md` resolves
