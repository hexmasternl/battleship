# Proposal

> Tracks GitHub issue [hexmasternl/battleship#17](https://github.com/hexmasternl/battleship/issues/17).

## Why

After creating (#13) or joining (#16) a game, a player lands on `/games/{game-code}` and sees only a placeholder. The game cannot move past `New`: players have no way to load the game, place their fleet, or say they are ready, so no game is ever started. The placement phase is the first real screen of a match, and it is the step that turns a `New` game into a `Started` one that the game play page (#18) builds on.

## What Changes

- **Fleet and board.** The game uses a 12x12 board (0-based `column` and `row`, 0..11) and the classic fleet of five ships: Carrier 5, Battleship 4, Cruiser 3, Submarine 3, Destroyer 2. Ships lie horizontally or vertically, fully inside the board. They may touch, they may not overlap.
- **Domain model.** #13's flat ship coordinates become ship placements (kind, origin coordinate, orientation; the cells are derived). Shots become `Shot(column, row, hit)` so #18 can persist the hit flag. Each player gains a ready flag. The stored game document moves to version 2, and version 1 documents are upgraded on read.
- **Get a game.** A new authenticated endpoint, `GET /games/{code}` (`/api/games/{code}` through the gateway), returns the caller's view of the game:
  - the game id, code and state;
  - `me`: the caller's id, name, ready and turn flags, placed ships and fired shots;
  - `opponent` (null until a second player joined): id, name, ready and turn flags and fired shots, never the opponent's ships.

  An unknown code, or a caller who is not a player of the game, gets `404`.
- **Place a ship.** `PUT /games/{code}/ships/{kind}` with `{ column, row, orientation }` places or moves one ship of the caller. Out of the board or overlapping gives `400`. A caller who is already ready, or a game that is no longer `New`, gives `409`. The frontend saves every drop and rotation.
- **Ready.** `POST /games/{code}/ready` marks the caller ready when all five ships are placed. It publishes a new catalog event, `game.player-ready.v1`, to the players of the game. Player one may be ready before player two joins. When the second player becomes ready, the game becomes `Started`, a randomly chosen player gets the first turn, and `game.started.v1` is published to both players. Simultaneous readies start the game exactly once, through #16's optimistic concurrency.
- **Integration events.** The catalog gains `game.player-ready.v1` with payload `gameId`, `gameCode`, `playerId`. The frontend's list of stream event types gains it too.
- **Frontend game page.** #13's `GamePlaceholder` is replaced by a `GamePage` on `/games/:gameCode`:
  - it sends a visitor without a player back to the landing page;
  - it loads the game, shows a not-found message for `404`, and renders the phase from the state;
  - it connects the realtime event stream for the player and reloads the game on `game.joined.v1`, `game.player-ready.v1` and `game.started.v1` for this game;
  - it can be refreshed at any moment and shows the same state.
- **Frontend placement screen.**
  - A 12x12 grid and a ship list that shows every ship's size.
  - Ships are placed and moved with drag and drop, and with tap or keyboard as an alternative.
  - Selecting a ship makes it active (border), and the active ship shows a rotate control (90° clockwise). An invalid rotation or drop is refused with feedback.
  - A players panel shows both players, with "Player 2" greyed out until someone joins, each with name and ready state.
  - "Ready" is enabled only when all five ships are validly placed. After ready the board is read only.
  - Mobile first: players, grid, ships stacked. On wide screens: grid left, players top right, ships bottom right.
  - Once the game is `Started` the page shows a "game started" placeholder until #18.

## Capabilities

### New Capabilities
- `game-view`: how a player reads a game from the server. Covers authentication, the not-a-player rule, and what the caller's view contains and hides.
- `fleet-placement`: the server-side placement phase. Covers the board and fleet rules, placing and moving a ship, becoming ready, the player ready event, and starting the game when both players are ready.
- `game-page`: the frontend page for one game. Covers the player gate, loading and not-found handling, refresh safety, live updates from the event stream and choosing what to render per game state.
- `placement-screen`: the frontend placement phase. Covers the grid, the ship list, drag and drop with tap and keyboard alternatives, active ship and rotation, saving, the players panel, the ready action and the responsive layout.

### Modified Capabilities
- `game-state` (introduced by `issue-13-create-new-game`): "A game has two player slots" changes. Each player holds ship placements, shots with a hit flag and a ready flag instead of flat coordinate lists. A new requirement covers upgrading stored documents of an earlier version.
- `integration-events`: "The system publishes a fixed catalog of integration events" and "Each event carries a minimal payload" gain `game.player-ready.v1`.

## Impact

- **Games module** (`src/Games/`):
  - `Games.Abstractions`: `ShipKind`, `Orientation`, `ShipPlacement`, `Shot`, the board and fleet rules, `IPlayer` changes, the `GameView` DTOs.
  - `Games`: `Game.PlaceShip`, `Game.MarkReady`, `Game.Start`; the `GetGame`, `PlaceShip` and `MarkReady` feature slices; three endpoints in `GamesEndpoints`; a first-turn picker abstraction.
  - `Games.Data`: `GameDocument` version 2 with the version 1 upgrade.
  - `Games.Tests`: domain, handler, endpoint and repository tests.
- **Integration events** (`src/Core/HexMaster.Battleship.IntegrationEvents`): `GamePlayerReadyEvent`, the catalog entry, the README table.
- **Frontend** (`src/Frontend/src/app/`): `games/game-page/` and `games/placement/` (page, stores, grid, ship list, players panel), `GamesApi` additions, the route, and `events/realtime-event-types.ts`. Built by the `angular-frontend` agent.
- **Gateway**: none. The `games` prefix already routes `/api/games/**`.
- **Depends on**: `issue-16-join-game` (join endpoint, `IGameRepository` optimistic-concurrency update, refusal-to-problem-details mapping), and through it `issue-13-create-new-game` and `issue-7-user-identification`. Both `game-state` (from #13) and the #16 specs must be archived before this change is archived.
- **Followed by**: #18 (game play page, shots), then #19 (game end).
- **Assumed**:
  - The ship names follow the product owner's fleet (Cruiser 3, Submarine 3, Destroyer 2), not the design-system showcase names (Destroyer 3, Submarine 3, Patrol Boat 2). The lengths are the same.
  - Rotation keeps the ship's origin (top or left cell) fixed and toggles the orientation.
  - A ready player cannot become unready, and a placed ship cannot be taken back off the board.
- **Out of scope**: shooting and the game play screen (#18), game end (#19), un-ready, abandoning a game, spectators.
