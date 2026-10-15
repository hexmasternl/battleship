# Proposal

## Why

The Users API in production scales to zero when idle, so the first player after a quiet period waits for a cold start (container pull, .NET start-up, Dapr sidecar) before the welcome screen can register or refresh a token. Every Container App should keep at least one warm replica.

## What Changes

- `users-api` runs with at least 1 and at most 3 replicas (was 0 to 3).
- `games-api` keeps 1 to 3 replicas (unchanged, already the target).
- `events-api` keeps exactly 1 replica. Raising its maximum to 3 was requested but is left out: the Events API keeps the routing state of open event streams in memory, and with several replicas the Dapr subscription delivers each integration event to only one of them, so browsers connected to another replica would miss live game events. The `azure-hosting` spec requires the single instance ("The Events API runs as a single instance"). Scaling it out needs shared stream state first and is a separate change.
- `infra/README.md` describes the new scaling of each app.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. Replica counts of the Users and Games APIs are a hosting detail, not specified behaviour; the Events API requirement is unchanged. The change sets `skip_specs: true`.

## Impact

- `infra/modules/environment.bicep`: `minReplicas` of `users-api`.
- `infra/README.md`: resource table.
- Cost: one always-on replica for the Users API (0.5 vCPU, 1 GiB) instead of scale to zero.
