# Spec Delta

## MODIFIED Requirements

### Requirement: Local prerequisites are documented and checked
The repository SHALL document the tools a developer needs to run the AppHost with messaging (Docker, the Dapr CLI and an initialised Dapr runtime), and the AppHost SHALL fail with a message naming the missing prerequisite when the Dapr runtime is not initialised. The documentation SHALL also name the dev container as the recommended way to get these tools without installing them on the host.

#### Scenario: Prerequisites are documented
- **WHEN** a developer reads the repository README
- **THEN** it lists Docker, the Dapr CLI and `dapr init` as prerequisites for a local run and names the command to start the application

#### Scenario: Dev container route is documented
- **WHEN** a developer reads the quick start in the repository README
- **THEN** it explains how to open the repository in the dev container, and that the dev container already provides Docker, the Dapr CLI and an initialised Dapr runtime

#### Scenario: Missing Dapr runtime is reported
- **WHEN** the AppHost is started on a machine where `dapr init` has not been run
- **THEN** the run stops with an error that names Dapr as the missing prerequisite instead of leaving the sidecars in a restart loop
