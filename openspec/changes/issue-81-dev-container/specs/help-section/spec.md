# Spec Delta

## MODIFIED Requirements

### Requirement: The prerequisites topic lists what a developer needs
The prerequisites topic SHALL name the tools needed to run the demo locally:
- the .NET 10 SDK (C#)
- Docker Desktop or another container runtime
- the Dapr CLI, plus the one-time `dapr init`
- Node.js with npm
- the Aspire CLI

It SHALL also explain how to start the AppHost, and that the AppHost checks for the Dapr CLI and fails fast when it is missing or not initialised.

It SHALL also explain the dev container as the alternative to installing these tools: that it needs only Docker and an editor with dev container support on the host, and that it provides every tool above with `dapr init` already done.

#### Scenario: Tools listed
- **WHEN** the prerequisites topic renders
- **THEN** it names the .NET 10 SDK, Docker, the Dapr CLI with `dapr init`, Node.js with npm and the Aspire CLI, and shows the command that starts the AppHost

#### Scenario: Dev container explained
- **WHEN** the prerequisites topic renders
- **THEN** it has a section on the dev container that says what the host needs and that the container provides the listed tools with Dapr initialised
