#!/usr/bin/env bash
# Prepares the dev container once it is created: restores packages, installs the workflow tools
# and initialises Dapr. Every step is a no-op when its result is already there, so the script can
# run again at any time (bash .devcontainer/post-create.sh).
set -euo pipefail

# Pinned tool versions. The OpenSpec CLI matches the version the planning artifacts were made with.
readonly OPENSPEC_VERSION="1.14.0"
readonly PLAYWRIGHT_VERSION="1.65.1"

readonly WORKSPACE="/workspaces/battleship"
readonly FRONTEND="$WORKSPACE/src/Frontend"
readonly WORKTREES="/workspaces/battleship-worktrees"

current_step="start"
step() {
  current_step="$1"
  echo
  echo "==> $current_step"
}
trap 'echo >&2; echo "dev container setup failed at step: $current_step" >&2' ERR

wait_for_docker() {
  local _
  for _ in $(seq 1 60); do
    if docker info >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "The Docker engine inside the dev container did not start within 60 seconds." >&2
  return 1
}

container_exists() {
  docker container inspect "$1" >/dev/null 2>&1
}

step "Take ownership of the persisted volumes"
# Docker creates volume mount points owned by root.
for path in "$HOME/.microsoft" "$HOME/.dapr" "$HOME/.nuget" "$HOME/.npm" "$FRONTEND/node_modules" "$WORKTREES"; do
  sudo mkdir -p "$path"
  if [ "$(stat -c %U "$path")" != "$(id -un)" ]; then
    sudo chown -R "$(id -u):$(id -g)" "$path"
  fi
done

step "Mark the workspace as a safe git directory"
# The bind-mounted repository can be owned by another UID than the container user.
if ! git config --global --get-all safe.directory | grep -qx "$WORKSPACE"; then
  git config --global --add safe.directory "$WORKSPACE"
fi

step "Install the npm version the frontend declares"
npm_wanted="$(node -p "require('$FRONTEND/package.json').packageManager.split('@')[1]")"
if [ "$(npm --version)" != "$npm_wanted" ]; then
  npm install --global "npm@$npm_wanted"
fi

step "Install the OpenSpec CLI $OPENSPEC_VERSION"
if [ "$(openspec --version 2>/dev/null || true)" != "$OPENSPEC_VERSION" ]; then
  npm install --global "@fission-ai/openspec@$OPENSPEC_VERSION"
fi

step "Restore the .NET solution"
dotnet restore "$WORKSPACE/src/Battleship.slnx"

step "Install the frontend packages"
# node_modules is a volume; reinstall only when the lockfile changed since the last install.
lock_hash="$(sha256sum "$FRONTEND/package-lock.json" | cut -d' ' -f1)"
lock_marker="$FRONTEND/node_modules/.devcontainer-lock"
if [ ! -f "$lock_marker" ] || [ "$(cat "$lock_marker")" != "$lock_hash" ]; then
  (cd "$FRONTEND" && npm ci)
  echo "$lock_hash" > "$lock_marker"
else
  echo "package-lock.json unchanged, keeping the installed packages"
fi

step "Install Playwright $PLAYWRIGHT_VERSION with headless Chromium"
if [ "$(playwright --version 2>/dev/null || true)" != "Version $PLAYWRIGHT_VERSION" ]; then
  npm install --global "playwright@$PLAYWRIGHT_VERSION"
fi
playwright install --with-deps chromium

step "Wait for the Docker engine"
wait_for_docker

step "Initialise Dapr"
# ~/.dapr and the Docker data are separate volumes; initialise again when either is missing.
if [ -x "$HOME/.dapr/bin/daprd" ] && container_exists dapr_placement && container_exists dapr_scheduler; then
  echo "Dapr runtime and containers are present"
else
  if [ -e "$HOME/.dapr/bin/daprd" ] || container_exists dapr_placement; then
    dapr uninstall
  fi
  dapr init
fi

step "Stop the Dapr containers this app does not use"
# dapr init also starts Redis and Zipkin. Nothing uses them here (the sidecars get their tracing
# configuration from the AppHost), so they are stopped and kept from restarting to save memory.
for name in dapr_redis dapr_zipkin; do
  if container_exists "$name"; then
    docker update --restart=no "$name" >/dev/null
    docker stop "$name" >/dev/null
  fi
done

echo
echo "Dev container ready. Start the app with:"
echo "  cd src/Aspire/HexMaster.Battleship.Aspire && aspire run"
