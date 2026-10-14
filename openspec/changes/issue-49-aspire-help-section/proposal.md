# Proposal

> Tracks GitHub issue [hexmasternl/battleship#49](https://github.com/hexmasternl/battleship/issues/49).

## Why

This repository is a demo of .NET Aspire. It wires Dapr pub/sub and a cron binding in code, routes requests through a YARP gateway, ships a custom hosting integration (the dev bot) and adds dashboard resource commands that drive the bot. Someone who only opens the web app sees none of this; the explanation lives only in `README.md`. A help section inside the app makes the demo explain itself, and tiles on the landing page invite visitors into it.

## What Changes

- Add a public `/help` section to the Angular frontend:
  - an overview page at `/help` that links to every topic
  - one deep-linkable page per topic at `/help/<topic>`
- Ship these topics:
  - **prerequisites**: .NET 10 SDK, Docker Desktop, the Dapr CLI with `dapr init`, Node.js with npm, the Aspire CLI, and how to start the AppHost
  - **resources**: what an Aspire resource is, illustrated with this app's parameters, containers, projects, gateway, frontend and dev bot
  - **resource relations**: references, `WaitFor` ordering, and the request and event flow
  - **Dapr**: sidecars, the RabbitMQ pub/sub component, the cron binding that triggers game cleanup, and the prerequisite check
  - **custom integration**: `AddBattleshipBot`, `BattleshipBotResource` and the bot as a local opponent
  - **custom resource commands**: Set intelligence, Enable and Disable auto-join, Join game, and the bot properties mirrored to the dashboard
- Content is authored as Angular templates. No markdown library or other new dependency is added.
- An unknown topic redirects to the help overview. The help routes need no user session.
- Add a set of visually distinctive tiles to the landing page. Each tile deep-links to a key help topic.
- Every help page offers a way back to the help overview and to the landing page.

## Capabilities

### New Capabilities
- `help-section`: the in-app help that explains how the Aspire environment is configured. It covers the routes, the topic set, the content each topic must cover, navigation, and accessibility.

### Modified Capabilities
- `welcome-page`: the landing page gains a help tile section that deep-links into `help-section` topics.

## Impact

- **Frontend only** (`src/Frontend`):
  - `app.routes.ts` gets a new lazy `help` route with child topic routes.
  - A new `src/app/help/` feature folder.
  - A new tiles section in `src/app/welcome/`.
  - New styles that use the existing design tokens.
  - Unit tests.
- **No backend, AppHost or gateway changes.** The gateway's route table is untouched: `/help` is a frontend route, not a backend prefix.
- **No new npm dependencies.**
- Help content must be kept consistent with `README.md` and the AppHost by hand when either changes.
