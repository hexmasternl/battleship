# Tasks

## 1. Shared API reference in ServiceDefaults

- [x] 1.1 Add `Scalar.AspNetCore` (exact current version) to ServiceDefaults. Add a reference path constant to `AspireConstants`. Add the `AddApiReference()` and `MapApiReference()` extensions, which map the OpenAPI document and the Scalar UI only in Development (design D1). Verify `WebApplicationFactory` tests on the Players API: the reference path and `/openapi/v1.json` answer 200 in Development and 404 in Production.
- [x] 1.2 Add `BearerSecuritySchemeTransformer` and register it in `AddApiReference()`. Configure Scalar to prefer the `Bearer` scheme (design D2). Verify a test on the generated Players document: the `bearer` security scheme exists, `GET /users/me` carries the requirement, and `POST /users/tokens` and `POST /users/tokens/refresh` do not.

## 2. Wire every API

- [x] 2.1 Replace `AddOpenApi()` / `MapOpenApi()` with `AddApiReference()` / `MapApiReference()` in the Players, Games and Events `Program.cs`. Verify the solution builds and the existing API tests pass.
- [x] 2.2 Exclude `/integration-events` and the Dapr subscribe route from the Events and Players documents (design D3). Verify a test on the Events document: it contains `GET /events` and no `/integration-events` or `/dapr/subscribe` paths.

## 3. AppHost and gateway

- [x] 3.1 Add an "API reference" dashboard URL to each API resource through a shared helper, using the path constant (design D4). Verify `aspire run` shows the link on all three API resources and each link opens its reference.
- [x] 3.2 Add `scalar` to `GatewayRoutes.ReservedPrefixes` (design D5). Verify a new `GatewayRoutesTests` case: a module declaring `scalar` is rejected with the reserved-prefix message.

## 4. Documentation

- [ ] 4.1 Mention the API reference link in the README's "Your first tour of the dashboard" section and in the Players section (try the endpoints from the reference with a pasted access token). Verify the steps work as written during a local run.

## 5. Integration check

- [ ] 5.1 Run `dotnet test src/Battleship.slnx`, then `aspire run`. From the Players reference, create a token pair, authorize with the access token and call `GET /users/me` successfully. Confirm the Events reference hides the delivery endpoint and that `<gateway>/scalar` returns 404. Verify all pass and note the Scalar version used in the pull request.
