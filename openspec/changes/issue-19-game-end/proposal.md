# Proposal

> Tracks GitHub issue [hexmasternl/battleship#19](https://github.com/hexmasternl/battleship/issues/19).

## Why

After #18, players can shoot until every cell is used up, but nothing ever ends the game: sinking the opponent's whole fleet only switches the turn again, the game stays `Started` forever, and nobody is told who won. The `game.ended.v1` event and its `completed` reason already exist in the catalog (#14), but no service publishes them. This change closes the match loop: the server detects that a fleet is sunk, marks the game finished, announces the winner, and the frontend shows each player a result and takes them home.

## What Changes

- **End detection on the server.** After every recorded shot, the Games service checks whether every ship cell of the opponent has now been hit. When it has:
  - the game state becomes `Completed`;
  - the shooter is stored as the game's winner;
  - nobody has the turn. The turn does **not** switch on the winning shot, which takes precedence over #18's "the turn switches after every shot".
- **Events.** On the winning shot the Games service still publishes `game.shot-fired.v1` first, then publishes `game.ended.v1` with reason `completed` and the winner's id to both players, **instead of** `game.turn-changed.v1`. Publish failures are logged and swallowed, as in #13.
- **Finished games are read only.** `POST /games/{code}/shots` on a `Completed` game answers `409 Conflict`; nothing is recorded and nothing is published.
- **Winner in the game views.**
  - `GET /games/{code}` gains `winnerId` (null until the game is completed).
  - The shot response gains the game `state` after the shot, so the shooter learns that their shot won the game even when the event stream is down.
- **Persistence.** The stored game document gains an optional `winnerId`. Older documents read as "no winner". The state column already reflects `Completed`.
- **Frontend.**
  - When the game page receives `game.ended.v1` for its game, when the player's own shot response reports `Completed`, or when the page loads a game that is already `Completed`, the page shows the final boards read only (no clickable cells, no turn) and opens a modal result dialog.
  - The dialog says "You won" to the winner and "You lost" to the other player. A game that ended without a winner gets a neutral "The game has ended" message.
  - "Close" (and Escape) returns the player to the landing page. The final boards stay visible behind the dialog while it is open.
  - Built later by the `angular-frontend` agent, following the design system and the central SASS variables.

## Capabilities

### New Capabilities
- `game-ending`: how a started game is finished on the server. Covers detecting a sunk fleet after a shot, the `Completed` state with its winner and no turn, the precedence over the turn switch, the `game.ended.v1` announcement and its order after the shot event, refusing shots on a finished game, and the winner in the game view and the shot response.
- `game-result-dialog`: how the game page presents a finished game. Covers reacting to the end event, the shot response and a loaded `Completed` game, the read-only final boards, the won / lost / neutral result dialog, and returning to the landing page.

### Modified Capabilities
None. The behaviour this change alters in capabilities that #17 and #18 introduce (the turn switch after every shot, the game view's fields, the shot response) is stated as ADDED requirements in `game-ending` that take precedence, because those texts are still being written. The `integration-events` catalog and the `game.ended.v1` payload (`gameId`, `gameCode`, `reason`, optional `winnerId`) already cover this change unchanged, and the frontend already listens for `game.ended.v1`.

## Impact

- **Games module** (`src/Games/`):
  - `HexMaster.Battleship.Games.Abstractions`: `IGame.WinnerId`, `winnerId` on the game view DTO, `state` on the shot response DTO.
  - `HexMaster.Battleship.Games`: `Game.FireShot` gains the fleet-sunk check and a private `Complete` step; the fire-shot slice publishes `GameEndedEvent` instead of the turn-changed event on the winning shot; the get-game slice maps `WinnerId`.
  - `HexMaster.Battleship.Games.Data`: `GameDocument` gains an optional `WinnerId`.
  - `HexMaster.Battleship.Games.Tests`: domain, handler, endpoint and storage tests.
- **Integration events**: `GameEndedEvent` and `GameEndReason.Completed` from #14 are used as they are. No catalog change.
- **Frontend** (`src/Frontend/src/app/games/`): the `GamePage` store handles the ended state; `GamePlayPhase` gains a read-only mode; a new `GameResultDialog`; `GamesApi` types gain `winnerId` and the shot response `state`. No change to the events service (`game.ended.v1` is already in `REALTIME_EVENT_TYPES`).
- **Gateway**: none. The `games` prefix is used unchanged.
- **Depends on**, which must be applied (and archived) first, in order: `issue-13-create-new-game`, `issue-16-join-game` (optimistic concurrency), `issue-17-placement-phase` (`GET /games/{code}`, ship placements, shots with hit flags, `GamePage`), `issue-18-game-play-page` (`POST /games/{code}/shots`, `Game.FireShot`, `game.turn-changed.v1`, `GamePlayPhase`).
- **Assumed**:
  - The final boards keep #17's rule that a player never sees the opponent's ships, also after the game ends.
  - Abandoning a game, the server ending an inactive game (`ended-by-server`) and a rematch are out of scope. The dialog's neutral message is ready for them.
