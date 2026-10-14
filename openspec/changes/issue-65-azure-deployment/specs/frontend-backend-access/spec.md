# Spec Delta

## MODIFIED Requirements

### Requirement: The gateway URL is loaded as runtime configuration
The app SHALL get the gateway URL and the Events API URL from a runtime configuration file. When Aspire starts the frontend, the file SHALL be generated from environment variables; when the app is deployed, the deployment SHALL write it with the deployed URLs. The app SHALL load the file before the first backend request or event stream. Neither URL SHALL be compiled into the bundle.

#### Scenario: Configuration reflects the current run
- **WHEN** the gateway starts on a different port than on a previous run
- **THEN** the frontend sends backend requests to the new port without any file in the repository being edited

#### Scenario: Events stream uses the configured URL
- **WHEN** the runtime configuration names an Events API URL
- **THEN** the app opens the event stream at that URL

#### Scenario: Same bundle for every environment
- **WHEN** the same frontend build is served with a different runtime configuration file
- **THEN** it calls the backend and opens the event stream named in that file

#### Scenario: Missing configuration is reported
- **WHEN** the app starts and the runtime configuration file is missing or has no gateway URL
- **THEN** the app logs a clear error naming the missing setting, and requests that start with `/api/` fail instead of being sent to an unintended host

#### Scenario: Missing events URL is reported
- **WHEN** the runtime configuration has no Events API URL
- **THEN** the app logs a clear error naming the missing setting and does not open an event stream to an unintended host
