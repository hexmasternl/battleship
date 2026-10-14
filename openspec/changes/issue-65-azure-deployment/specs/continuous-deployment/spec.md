# Spec Delta

## Purpose

Defines how a change on `main` becomes a running release in Azure: what triggers a deployment, what must pass first, how a release is versioned, which artifacts it produces and how they reach production.

## ADDED Requirements

### Requirement: A push to main starts a deployment
The repository SHALL have a deployment workflow that runs on every push to `main` and can also be started manually. Pushes to other branches and pull requests SHALL NOT start it. The existing CI workflow SHALL keep its own triggers.

#### Scenario: Merge to main deploys
- **WHEN** a commit is pushed to `main`
- **THEN** the deployment workflow starts for that commit

#### Scenario: Development does not deploy
- **WHEN** a commit is pushed to `development` or a pull request is opened
- **THEN** the deployment workflow does not run, and CI runs as before

### Requirement: Build and tests gate the release
Before anything is published, the workflow SHALL compile and test every backend module and the frontend in jobs that run in parallel. If any of them fails, no image SHALL be pushed, no infrastructure SHALL be deployed and no frontend SHALL be uploaded.

#### Scenario: A failing test stops the release
- **WHEN** a unit test in any backend module or in the frontend fails
- **THEN** the run fails before the publish stage and production is unchanged

#### Scenario: Modules are verified in parallel
- **WHEN** the workflow starts
- **THEN** the backend modules and the frontend are built and tested by separate jobs that run at the same time

### Requirement: Every release has one semantic version
The workflow SHALL compute one semantic version (`major.minor.patch`) per run from the repository history and use it for every container image and for the deployment. Two successful runs on different commits SHALL NOT produce the same version.

#### Scenario: Version is shared by the release
- **WHEN** a run computes version `1.4.0`
- **THEN** every image of that run is tagged `1.4.0` and every Container App is deployed with `1.4.0`

#### Scenario: Next commit gets a higher version
- **WHEN** a later commit on `main` is deployed
- **THEN** its version is higher than the previous release's version

### Requirement: Each API is published as a container image
After the gate passes, the workflow SHALL build a container image for each deployable API with the .NET SDK's container publishing, named `battleship/<service>-api:<version>`, and push it to the registry named by the `ACR_LOGIN_SERVER` secret using the `ACR_LOGIN_USERNAME` and `ACR_LOGIN_PASSWORD` secrets. The images SHALL be built in parallel.

#### Scenario: Images land in the registry
- **WHEN** a run with version `1.4.0` passes the gate
- **THEN** the registry holds `battleship/games-api:1.4.0`, `battleship/users-api:1.4.0` and `battleship/events-api:1.4.0`

#### Scenario: Development-only tools are not published
- **WHEN** the images are published
- **THEN** no image is published for the development bot

### Requirement: The frontend build is kept as a workflow artifact
In parallel with the images, the workflow SHALL produce a production build of the frontend and store it as an artifact of the run, so the deploy stage uploads exactly what was built and tested.

#### Scenario: Artifact is available to the deploy stage
- **WHEN** the publish stage succeeds
- **THEN** the run lists a frontend artifact containing the production build

### Requirement: Production is updated only after publishing succeeds
The deploy stage SHALL run in the GitHub `production` environment only after every image and the frontend artifact were produced. It SHALL sign in to Azure without a stored password, deploy the infrastructure templates with the run's version so every Container App runs the new images, and then upload the frontend artifact with a runtime configuration that points at the deployed backend.

#### Scenario: Containers move to the new version
- **WHEN** the deploy stage of a run with version `1.4.0` finishes
- **THEN** every API Container App runs its image tagged `1.4.0`

#### Scenario: A failed image push keeps production as it was
- **WHEN** pushing one of the images fails
- **THEN** the deploy stage does not run and production keeps the previous release

#### Scenario: Deployed frontend points at deployed backend
- **WHEN** the deploy stage finishes
- **THEN** the hosted frontend's `config.json` contains the deployed gateway URL and the deployed Events API URL
