# Tasks

## 1. Scaling

- [ ] 1.1 Set `minReplicas: 1` for `users-api` in `infra/modules/environment.bicep`, keep `maxReplicas: 3`, and update its comment; verify `az bicep build --file infra/main.bicep` succeeds and the built template shows min 1 / max 3 for `users-api` and `games-api` and min 1 / max 1 for `events-api`
- [ ] 1.2 Update the `users-api` row of the resource table in `infra/README.md` (no longer scales to zero); verify the README states the replica range of all three apps

## 2. Integration

- [ ] 2.1 Run `az deployment sub what-if` against `battleship-production`; verify the only scale change it reports is `users-api` `minReplicas` 0 to 1
