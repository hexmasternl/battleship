#!/usr/bin/env bash
# Runs every time the dev container starts. The Docker engine starts empty after a container
# restart, so the Dapr containers are started again, and the Aspire development certificate is
# trusted for tools inside the container.
set -euo pipefail

current_step="start"
step() {
  current_step="$1"
  echo
  echo "==> $current_step"
}
trap 'echo >&2; echo "dev container start failed at step: $current_step" >&2' ERR

step "Wait for the Docker engine"
for attempt in $(seq 1 60); do
  if docker info >/dev/null 2>&1; then
    break
  fi
  if [ "$attempt" -eq 60 ]; then
    echo "The Docker engine inside the dev container did not start within 60 seconds." >&2
    false
  fi
  sleep 1
done

step "Start the Dapr placement and scheduler containers"
# Missing containers are left to post-create.sh, which runs dapr init.
for name in dapr_placement dapr_scheduler; do
  if docker container inspect "$name" >/dev/null 2>&1; then
    if [ "$(docker container inspect --format '{{.State.Running}}' "$name")" != "true" ]; then
      docker start "$name" >/dev/null
    fi
    echo "$name running"
  fi
done

step "Trust the Aspire development certificate"
aspire certs trust --non-interactive

echo
echo "Start the app with:"
echo "  cd src/Aspire/HexMaster.Battleship.Aspire && aspire run"
