# Spec Delta

## MODIFIED Requirements

### Requirement: The APIs and gateway run as Container Apps
The Games, Users and Events APIs SHALL each run as a Container App in one Container Apps environment. The APIs SHALL pull their images from the existing registry with its credentials. Only the Events API SHALL accept traffic on its own address from the internet; the Games and Users APIs SHALL be reachable only inside the environment and through the environment's route configuration. No gateway Container App SHALL run in the environment.

#### Scenario: Module APIs are not public on their own address
- **WHEN** someone on the internet calls the Games or Users API on the app's own address
- **THEN** the request does not reach the API

#### Scenario: Environment routes to the modules
- **WHEN** the frontend calls `<backend>/users/...` or `<backend>/games/...` on the environment's route configuration address
- **THEN** the environment forwards it to the Users or Games API with the path unchanged

#### Scenario: No gateway app
- **WHEN** the templates are deployed
- **THEN** the environment holds Container Apps for the three APIs only, and no `gateway` Container App exists afterwards

### Requirement: The deployed gateway uses the same route table as local runs
The environment's route configuration SHALL route exactly the prefixes declared in the shared gateway route table, to the Container Apps of the same modules. A prefix that is not declared, including platform and maintenance routes, SHALL NOT be routed. Each prefix SHALL match on a path-segment boundary.

#### Scenario: Route tables cannot drift
- **WHEN** a prefix is added to the gateway route table but not to the deployed route configuration source
- **THEN** a test fails in CI

#### Scenario: Live routes can be checked against the table
- **WHEN** the route sync skill compares the deployed route configuration with the shared route table
- **THEN** it reports every module, prefix or target app that differs, or that they are in sync

#### Scenario: Maintenance route is not exposed
- **WHEN** someone calls `<backend>/maintenance/game-cleanup`
- **THEN** the environment does not forward the request

#### Scenario: Prefix matches whole segments
- **WHEN** someone calls `<backend>/usersx/anything`
- **THEN** the request is not forwarded to the Users API

### Requirement: Browsers are allowed only from the hosted frontend
The APIs SHALL allow cross-origin requests from the frontend's custom hostname origin, `https://battleship.hexmaster.nl`, and SHALL reject cross-origin requests from other origins. The Users and Games APIs SHALL answer preflight and actual requests themselves from a configured list of origins in every environment; the Events API SHALL do the same for its stream. No API SHALL allow any origin.

#### Scenario: Hosted frontend can call the backend
- **WHEN** the frontend served on `https://battleship.hexmaster.nl` calls the Users or Games API through the route configuration or opens the event stream
- **THEN** the browser accepts the responses, including the preflight

#### Scenario: Other origins are blocked
- **WHEN** a page on another origin calls the backend from the browser
- **THEN** the browser blocks the response

#### Scenario: No origins configured outside Development
- **WHEN** a Users or Games API runs outside Development with no allowed origins configured
- **THEN** it accepts no cross-origin request

### Requirement: The frontend is hosted on Azure Static Web Apps
The frontend SHALL be served by an Azure Static Web App on the custom hostname `battleship.hexmaster.nl` over HTTPS with a valid certificate. The custom domain SHALL be declared in the templates and validated through a CNAME record that the repository documents. Deep links to app routes SHALL serve the application instead of a 404.

#### Scenario: App loads on the custom hostname
- **WHEN** a player opens `https://battleship.hexmaster.nl`
- **THEN** the application loads over HTTPS without a certificate warning

#### Scenario: Deep link loads the app
- **WHEN** a player opens `https://battleship.hexmaster.nl/games/<id>` directly
- **THEN** the application loads and shows that route

#### Scenario: DNS record is documented
- **WHEN** someone prepares a fresh environment
- **THEN** the infrastructure documentation names the CNAME record that must exist before the deployment can validate the custom domain
