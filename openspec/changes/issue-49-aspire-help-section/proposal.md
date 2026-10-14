# Proposal

> Tracks GitHub issue [hexmasternl/battleship#49](https://github.com/hexmasternl/battleship/issues/49).

## Why

This repository is a demo of .NET Aspire. It wires Dapr pub/sub and a cron binding in code, routes requests through a YARP gateway, ships a custom hosting integration (the dev bot) and adds Aspire dashboard resource commands that drive the bot. Someone who only opens the web app sees none of this; the explanation lives only in `README.md`.

A help section inside the app makes the demo explain itself, and tiles on the landing page invite visitors into it. Writing that content as markdown in `docs/` means the same pages read well on GitHub too.

## What Changes

- **Help topics as markdown.** Write each help topic as a markdown document in the repository's `docs/` folder. The topics are:
  - **prerequisites**: .NET 10 SDK, Docker Desktop, the Dapr CLI with `dapr init`, Node.js with npm, the Aspire CLI, and how to start the AppHost
  - **resources**: what an Aspire resource is, illustrated with this app's parameters, containers, projects, gateway, frontend and dev bot
  - **resource relations**: references, `WaitFor` ordering, and the request and event flow
  - **Dapr**: sidecars, the RabbitMQ pub/sub component, the cron binding that triggers game cleanup, and the prerequisite check
  - **custom integration**: `AddBattleshipBot`, `BattleshipBotResource` and the bot as a local opponent
  - **custom resource commands**: Set intelligence, Enable and Disable auto-join, Join game, and the bot properties mirrored to the Aspire dashboard
- **Diagrams.** Add diagrams where they make a topic easier to follow: the resource graph, the request and event flow, start-up ordering, Dapr sidecars, the bot integration, and the resource command round trip. They are SVG files in `docs/assets/`, drawn in the design system's colours and typefaces, each with alt text and a text equivalent.
- **Public `/help` section.** Add a public `/help` section to the Angular frontend:
  - an overview page at `/help` that links to every topic
  - one deep-linkable page per topic at `/help/<topic>`
  - each topic page is an Angular template that fetches its markdown document and renders it as HTML in the design system's styling
- **Rendering behaviour.**
  - Links between topic documents navigate inside the app.
  - Images resolve to the published `docs/assets/`.
  - A failed load shows an error with a retry.
- **New dependency.** Add one markdown parsing dependency, `marked`. It loads only with the help section.
- **Routing.** An unknown topic redirects to the help overview. The help routes need no user session.
- **Landing-page tiles.** Add a set of visually distinctive tiles to the landing page (`WelcomePage` at `/`). The issue calls this page the "dashboard"; the reporter confirmed it means the landing page, since the app has no other dashboard. Each tile deep-links to a key help topic.
- **Navigation.** Every help page offers a way back to the help overview and to the landing page.

## Capabilities

### New Capabilities
- `help-section`: the in-app help that explains how the Aspire environment is configured. It covers the routes, the topic set, the markdown sources and diagrams in `docs/`, how they are rendered, the content each topic must cover, navigation, and accessibility.

### Modified Capabilities
- `welcome-page`: the landing page gains a help tile section that deep-links into `help-section` topics.

## Impact

- **Repository docs:**
  - Six new markdown documents in `docs/`.
  - New SVG diagrams in `docs/assets/`.
  - The existing `docs/assets/architecture.svg` may be reused.
- **Frontend** (`src/Frontend`):
  - `app.routes.ts` gets a new lazy `help` route with child topic routes.
  - A new `src/app/help/` feature folder.
  - A new tiles section in `src/app/welcome/`.
  - An `angular.json` assets entry that publishes the help documents and their images from `docs/` under `/docs/`.
  - Unit tests.
- **Dependencies:** `marked` (exact version pinned). The help section's lazy chunk is the only thing that loads it.
- **No backend, AppHost or gateway changes.** The gateway's route table is untouched: `/help` and `/docs` are served by the frontend, not by a backend prefix.
- **Upkeep:** the help documents become the place to update when the AppHost changes. `README.md` can link to them instead of repeating them.
