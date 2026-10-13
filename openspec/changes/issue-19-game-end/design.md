# Design

## Context

See proposal.md (Why). State this change builds on (all applied and archived before it, in order 13 → 16 → 17 → 18 → 19):

- **`issue-13-create-new-game`**: the `Game` aggregate (`DomainModel<Guid>`, `GameState` `New`/`Started`/`Completed`/`Abandoned`, `PlayerOne`/`PlayerTwo`, `GiveFirstTurnTo`, `SwitchTurn`, `Touch(now, playerId?)`, `Reconcile`), `GameCode`, `IGameRepository`, the `games` table (PartitionKey = code, RowKey `"game"`, JSON `Document` + `DocumentVersion` + a `State` column), the feature-slice layout, `GamesEndpoints`, the frontend `GamesApi`.
- **`issue-16-join-game`**: the optimistic-concurrency update on `IGameRepository` (write with the stored ETag, reload and retry a bounded number of times on conflict). Reused here, not redesigned.
- **`issue-17-placement-phase`**: ship placements (`ShipKind` with lengths, origin, `Orientation`, derived cells), `Shot(Column, Row, Hit)`, `GameDocument` version 2, `GET /games/{code}` with the caller's view, the `GamePage` container with its component-scoped store and its subscription to `EventsService.events$`.
- **`issue-18-game-play-page`**: `POST /games/{code}/shots`, `Game.FireShot(playerId, coordinate, now)` (records the shot with its hit flag, then always `SwitchTurn`), its handler publishing `game.shot-fired.v1` then `game.turn-changed.v1` to `EventAudience.Game(p1, p2)`, and `GamePlayPhase` (tabs "You" / "Opponent", snackbar confirmation, turn-driven tab switching).
- **`issue-14-integration-events-catalog`** (archived): `GameEndedEvent(GameId, GameCode, GameEndReason Reason, Guid? WinnerId)` = `game.ended.v1`, `GameEndReason.Completed` serialized as `completed`. The frontend's `REALTIME_EVENT_TYPES` already lists `game.ended.v1`, so the events service needs no change.

#17 and #18 are being written in parallel with this change. Where this design names their types (`FireShotCommandHandler`, `FireShotResponse`, `GameView`, `GamePageStore`), the implementer uses whatever names those changes finally chose.

## Goals / Non-Goals

**Goals:**
- The end rule lives in the aggregate, so no handler can forget it and the turn rule stays consistent (nobody has the turn in a finished game).
- The winning shot produces exactly one end announcement and no turn change, in a fixed order after the shot event.
- The player sees the result even if one of the two signals (event or HTTP response) is lost, and sees it again after a refresh.

**Non-Goals:**
- Abandoning a game, the server ending an inactive game, a rematch, statistics or a game history.
- Revealing the opponent's fleet after the end (#17's view rule stays).
- An outbox for the end event (the same trade-off as #13: publish failures are logged and swallowed).

## Decisions

### D1. The end check lives in `Game.FireShot`
After #18's step that records the shot on the shooter, `FireShot` asks whether the opponent's fleet is sunk:

```
opponent.Ships.SelectMany(s => s.Cells).All(cell => shooter.Shots.Any(shot => shot.Hit && shot.At == cell))
```

- Sunk → a private `Complete(winnerId, now)`: `State = Completed`, `WinnerId = shooter`, both `HasTurn` flags cleared through the same internal setter `SwitchTurn` uses, then `Touch(now, shooter)`.
- Not sunk → #18's `SwitchTurn(now)` as before.

`FireShot` returns a small `ShotOutcome(Coordinate At, bool Hit, bool GameCompleted)` (or #18's return type extended with `GameCompleted`), so the handler decides which event to publish without re-deriving the rule.

The existing guard "game must be `Started`" in `FireShot` refuses a shot on a `Completed` game with a `DomainException`, which #18's endpoint already maps to `409`. No new refusal path is needed, only a test that pins it.

- *Alternative: check for the end in the handler after `FireShot`.* Rejected. The turn would already have switched, and undoing it would break the "switch flips both" invariant from #13. It also puts a game rule outside the aggregate.
- *Alternative: compare a hit counter with 17.* Rejected. Counting cells derived from the placements is independent of the fleet composition and cannot drift if a duplicate hit were ever recorded.

**Interaction with #18.** #18 states "the turn switches after every shot". This change states, as an ADDED requirement in `game-ending`, that the winning shot does not switch the turn and that this takes precedence. When both are archived, the main specs read consistently: #18's rule covers every shot that does not end the game. A later clean-up may fold the exception into #18's requirement text.

### D2. `WinnerId` on the aggregate and in storage
- `IGame.WinnerId` (`Guid?`), set only by `Complete`, restored by `Reconcile`.
- `GameDocument` gains an optional `WinnerId` (`Guid?`). `DocumentVersion` stays at #17's version 2: the field is additive, a missing property deserializes to `null`, and a `Started` or `New` game has no winner anyway.
- The `State` column already written by #13's repository reflects `Completed`; no new column.

- *Alternative: bump `DocumentVersion` to 3.* Rejected. A version bump is for shapes a reader must convert; an absent optional field needs no conversion.
- *Alternative: a `IsWinner` flag per player.* Rejected. One value on the game cannot disagree with itself, two flags can.

### D3. Events from the fire-shot handler
#18's handler, after the optimistic-concurrency save succeeds:
1. Publishes `GameShotFiredEvent` (with `hit`) to `EventAudience.Game(p1, p2)`, unchanged.
2. If `outcome.GameCompleted`: publishes `new GameEndedEvent(game.Id, code, GameEndReason.Completed, winnerId)` to the same audience. Otherwise: #18's `GameTurnChangedEvent`.

Each publish has its own `try`/`catch` that logs at `Error` with the game code and swallows, so a failed shot-fired publish does not suppress the ended event.

On a concurrency conflict the handler reloads and retries (#16). If the reloaded game is already `Completed`, `FireShot` refuses and the request answers `409`; nothing is published twice because events are only sent after a successful save.

- **Tracing and metrics** (OpenTelemetry standard): the existing fire-shot activity gets the tag `game.completed` (bool); a counter `games.completed` is incremented once per completed game.

- *Alternative: a separate consumer that listens to `game.shot-fired.v1` and ends the game.* Rejected. The decision needs the opponent's ships, which only the aggregate has, and an asynchronous end would leave a window where the loser can still shoot.

### D4. API shapes (additive)
- `GET /games/{code}` view DTO gains `winnerId: Guid?`. Mapped straight from `IGame.WinnerId`.
- The shot response gains `state` (the `GameState` string after the shot, serialized the same way the view's `state` is).

Both are additive fields on DTOs #17 and #18 introduce; no client breaks. The shot response's `state` lets the winner's page end the game without depending on the event stream (D6).

- *Alternative: return `winnerId` on the shot response.* Rejected as redundant: only the shooter can win with their own shot, so `state == Completed` already says "you won".

### D5. Frontend: ended state in the `GamePage` store
The `GamePage` store (#17) gains:
- `winnerId: string | null` in its state, filled from the loaded view;
- an intent `endGame(winnerId: string | null)` that sets `state: 'Completed'`, `winnerId`, and clears both `hasTurn` flags. It is idempotent: when the state already is `Completed` it changes nothing;
- `computed` signals `isEnded` and `outcome: 'won' | 'lost' | 'neutral' | null`.

It is called from three places:
- the events subscription, for `game.ended.v1` whose `gameCode` equals the page's code (other codes are ignored);
- #18's fire-shot flow, when the shot response's `state` is `Completed` (winner = me);
- the initial load, when the view's `state` is `Completed`.

After applying `game.ended.v1`, the store re-reads the game once with `GamesApi.getGame(code)` to reconcile the final boards (the opponent's last shot normally arrived first through `game.shot-fired.v1`; the reload covers a dropped one). A failed reload keeps the current boards and does not affect the dialog.

### D6. Frontend: read-only final boards and the result dialog
- **Rendering.** `GamePage` renders `GamePlayPhase` for `Started` and for `Completed`. `GamePlayPhase` gets a `readonly` input: no clickable cells, no snackbar, no turn indicator, and the automatic tab switching is off. The selected tab stays where it was.
- **`games/game-result-dialog/`.** `GameResultDialog` is an Angular Material dialog taking `{ outcome }` as dialog data. Title and message: "You won" / "You lost" / "The game has ended". One `bs-btn bs-btn--primary` "Close" button with `cdkFocusInitial`. Styling from the central SASS variables and design-system tokens.
- **Opening once.** Opening a `MatDialog` is an imperative API, so the page opens it from an `effect()` that reads `outcome()`, guarded by a non-signal flag so it opens at most once per page instance. This is the agent's allowed "leave the reactive graph" use of `effect()`.
- **Closing.** The dialog keeps Material's default Escape handling and disables backdrop click. `afterClosed()` (whatever the cause) navigates to `/`. The page's teardown (#17) disconnects nothing extra.

- *Alternative: a dedicated `/games/:code/result` route.* Rejected. The issue asks for a dialog over the final boards, and the page already renders by phase.
- *Alternative: `disableClose: true` and only the Close button.* Rejected. Escape is the expected way out of a modal dialog; both lead to the same place.

### D7. Testing
- **Backend** (xUnit, Moq, Bogus, factories under `Factories/`):
  - `Game`: last cell completes the game (state, winner, no turn, activity); a non-final hit and a miss switch the turn; a shot on a `Completed` game is refused; `Reconcile` round trip with a winner.
  - Fire-shot handler: winning shot publishes shot-fired then ended (`Completed`, winner, game audience) and no turn-changed; an ended-event publish failure is logged and swallowed; a conflict retry that finds the game completed returns the conflict result.
  - Endpoints (`WebApplicationFactory`): `409` for a shot on a completed game; `GET` returns `winnerId`; the shot response carries `state`.
  - Storage (Azurite, skipped with a reason when unreachable): round trip of a completed game; a version-2 document without `WinnerId` reads with `null`.
  - Coverage on `HexMaster.Battleship.Games` stays at least 80%.
- **Frontend** (Vitest via `ng test`, by the `angular-frontend` agent): store `endGame` idempotence and outcomes; event filtering by game code; response-driven end; load of a `Completed` game; read-only `GamePlayPhase`; dialog texts, focus, Close and Escape navigating to `/`; one dialog when response and event both arrive.

## Risks / Trade-offs

- [#17 and #18 are written in parallel; names and exact DTO shapes may differ] → Tasks start with a gate that reads the archived #17/#18 specs and code and maps this design's names onto theirs before coding.
- [Two capabilities state turn rules (#18 "always switch", `game-ending` "not on the winning shot")] → The `game-ending` requirement says explicitly that it takes precedence; D1 records it. A later spec clean-up can merge them.
- [A swallowed `game.ended.v1` publish failure] → The shooter still sees the result from the shot response, and the loser sees it on the next load or refresh. Logged with the game code. An outbox is a later change.
- [The opponent's last `game.shot-fired.v1` might be lost while `game.ended.v1` arrives] → The store reloads the game once after the end event (D5).
- [The result dialog reappears on every visit to a finished game's URL] → Intended: the URL shows the outcome. Close always leads home.

## Migration Plan

Development only. Stored games without `WinnerId` read as "no winner", so no data migration is needed. Rollback is reverting the change; completed games written by it keep a `WinnerId` property that the previous reader ignores.
