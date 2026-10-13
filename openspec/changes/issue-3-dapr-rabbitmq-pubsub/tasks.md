# Tasks

## 1. Hosting packages and the broker

- [x] 1.1 Add `Aspire.Hosting.RabbitMQ` and the latest `CommunityToolkit.Aspire.Hosting.Dapr` release compatible with Aspire 13.6 to the AppHost project, record the chosen versions and the verified extension method names in design.md, and verify `dotnet build src/Battleship.slnx` succeeds
- [x] 1.2 Add `AddRabbitMQ("messaging")` with a `rabbitmq-username` parameter (default `guest`), a generated `rabbitmq-password` parameter and `.WithManagementPlugin()` to `AppHost.cs`, run the AppHost and verify the `messaging` resource reaches healthy on the dashboard and the management UI opens on the port the dashboard shows
- [ ] 1.3 Add a "Running locally" section to `README.md` listing Docker Desktop, the .NET 10 SDK, the Aspire CLI, the Dapr CLI and `dapr init` as prerequisites with the `aspire run` start command, and verify every command in the section runs as written on a machine with the prerequisites

## 2. Dapr component and sidecars

- [ ] 2.1 Register Dapr in the AppHost, create the `pubsub` pub/sub component backed by the `messaging` resource (through a resource reference, or through reference expressions on the component metadata if the toolkit cannot reference RabbitMQ directly; note which in design.md), and verify the generated component manifest in the sidecar logs names the RabbitMQ pub/sub type and contains no literal host, port or password from a repository file
- [ ] 2.2 Attach a Dapr sidecar that references `pubsub` to the Games API and the Players API project resources, run the AppHost and verify both sidecars appear on the dashboard with the `pubsub` component loaded and both API processes receive `DAPR_HTTP_ENDPOINT` and `DAPR_GRPC_ENDPOINT`
- [ ] 2.3 Add a startup check in the AppHost that verifies the Dapr CLI and the initialised runtime exist and throws with a message naming the missing step, and verify by temporarily renaming the Dapr runtime directory that the AppHost stops with that message instead of restarting sidecars

## 3. Dapr client and subscriptions in the services

- [ ] 3.1 Add `Dapr.AspNetCore` to ServiceDefaults, add an `AddDaprMessaging()` extension that registers the Dapr client and call it from `AddServiceDefaults()`, and verify with a unit test in the Games test project that a host built with `AddServiceDefaults()` resolves `DaprClient`
- [ ] 3.2 Add `UseCloudEvents()` and `MapSubscribeHandler()` to both API pipelines, add the development-only smoke endpoints (`POST /smoke/publish` on the Players API publishing to topic `smoke` on `pubsub`; `POST /smoke/received` with `[Topic("pubsub", "smoke")]` and `GET /smoke/received/{id}` on the Games API), and verify with the AppHost running that `GET /dapr/subscribe` on the Games API lists the `smoke` topic on `pubsub` with its route
- [ ] 3.3 Verify manually with the AppHost running that posting to the Players smoke endpoint makes the id retrievable from the Games received endpoint, and that a handler that throws is redelivered by the sidecar (temporarily throw on first delivery and observe the second delivery in the sidecar log)

## 4. End-to-end verification

- [ ] 4.1 Add `Aspire.Hosting.Testing` to the Games test project and write an integration test (trait `Integration`) that starts the AppHost, waits for `messaging`, both APIs and both sidecars to be healthy, posts to the Players smoke endpoint and polls the Games received endpoint for up to thirty seconds, and verify `dotnet test src/Battleship.slnx --filter Category=Integration` passes and `dotnet test src/Battleship.slnx --filter Category!=Integration` still passes without Docker
- [ ] 4.2 Run the full `dotnet build` and `dotnet test` for `src/Battleship.slnx`, run the AppHost once more from a clean `aspire run`, and verify the dashboard shows `messaging`, the two APIs and their sidecars healthy with no errors in the sidecar logs
