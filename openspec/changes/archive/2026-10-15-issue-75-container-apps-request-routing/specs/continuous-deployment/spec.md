# Spec Delta

## MODIFIED Requirements

### Requirement: Production is updated only after publishing succeeds
The deploy stage SHALL run in the GitHub `production` environment only after every image and the frontend artifact were produced. It SHALL sign in to Azure without a stored password, deploy the infrastructure templates with the run's version so every Container App runs the new images, and then upload the frontend artifact with a runtime configuration that points at the backend address the same deployment produced. No backend address SHALL be typed into the repository or the workflow by hand.

#### Scenario: Containers move to the new version
- **WHEN** the deploy stage of a run with version `1.4.0` finishes
- **THEN** every API Container App runs its image tagged `1.4.0`

#### Scenario: A failed image push keeps production as it was
- **WHEN** pushing one of the images fails
- **THEN** the deploy stage does not run and production keeps the previous release

#### Scenario: Deployed frontend points at deployed backend
- **WHEN** the deploy stage finishes
- **THEN** the hosted frontend's `config.json` contains the environment route configuration URL as the backend URL and the deployed Events API URL

#### Scenario: Run summary names the app address
- **WHEN** the deploy stage finishes
- **THEN** the run summary lists `https://battleship.hexmaster.nl` as the app, the backend route configuration URL and the Events API URL
