#!/usr/bin/env bash
# Checks that a freshly built dev container has every tool, an initialised Dapr runtime, and can
# build and test the solution. CI runs it inside the container (.github/workflows/devcontainer.yml);
# run it yourself with: bash .devcontainer/smoke-check.sh
set -euo pipefail

readonly WORKSPACE="/workspaces/battleship"

current_step="start"
step() {
  current_step="$1"
  echo
  echo "==> $current_step"
}
trap 'echo >&2; echo "dev container smoke check failed at step: $current_step" >&2' ERR

step "Tool versions"
echo "dotnet    $(dotnet --version)"
echo "aspire    $(aspire --version)"
echo "dapr      $(dapr --version | tr '\n' ' ')"
echo "docker    $(docker version --format '{{.Server.Version}}')"
echo "node      $(node --version)"
echo "npm       $(npm --version)"
echo "gh        $(gh --version | head -n 1)"
echo "openspec  $(openspec --version)"
echo "bicep     $(az bicep version 2>&1 | head -n 1)"
echo "pwsh      $(pwsh --version)"
echo "playwright $(playwright --version)"

step "Dapr runtime is initialised"
test -x "$HOME/.dapr/bin/daprd"
echo "daprd present at $HOME/.dapr/bin/daprd"

step "Build the solution"
dotnet build "$WORKSPACE/src/Battleship.slnx" --configuration Release

step "Run the backend tests"
# The Integration category starts the whole AppHost; it runs locally, not in this smoke check.
dotnet test --solution "$WORKSPACE/src/Battleship.slnx" --no-build --configuration Release \
  --filter-not-trait "Category=Integration"

echo
echo "Smoke check passed."
