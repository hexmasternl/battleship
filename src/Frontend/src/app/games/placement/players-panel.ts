import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { OpponentView, PlayerView } from '../game-view';

/** One row of the players panel. */
interface PlayerRow {
  readonly key: string;
  readonly name: string;
  readonly isYou: boolean;
  /** No second player yet: the row is a greyed-out placeholder. */
  readonly absent: boolean;
  readonly ready: boolean;
  readonly status: string;
  readonly icon: string;
}

function row(
  key: string,
  player: PlayerView | OpponentView | null,
  name: string,
  isYou: boolean,
): PlayerRow {
  if (player === null) {
    return {
      key,
      name,
      isYou,
      absent: true,
      ready: false,
      status: 'Waiting to join',
      icon: 'person_add',
    };
  }
  return {
    key,
    name: player.playerName,
    isYou,
    absent: false,
    ready: player.ready,
    status: player.ready ? 'Ready' : 'Not ready',
    icon: player.ready ? 'check_circle' : 'hourglass_empty',
  };
}

/**
 * Both players of the game: the caller first, marked "You", then the opponent, or a greyed-out
 * "Player 2" while nobody has joined. The ready state is shown by an icon and by text, never by
 * colour alone.
 */
@Component({
  selector: 'battle-players-panel',
  imports: [MatIconModule],
  template: `
    <h3 class="players__title" id="players-title">Players</h3>
    <ul class="players" aria-labelledby="players-title">
      @for (player of rows(); track player.key) {
        <li class="players__row" [class.is-absent]="player.absent" [class.is-ready]="player.ready">
          <span class="players__name">
            {{ player.name }}
            @if (player.isYou) {
              <span class="players__you">You</span>
            }
          </span>
          <span class="players__status">
            <mat-icon aria-hidden="true">{{ player.icon }}</mat-icon>
            {{ player.status }}
          </span>
        </li>
      }
    </ul>
  `,
  styleUrl: './players-panel.scss',
})
export class PlayersPanel {
  readonly me = input.required<PlayerView | null>();
  readonly opponent = input.required<OpponentView | null>();

  protected readonly rows = computed(() => [
    row('me', this.me(), 'You', true),
    row('opponent', this.opponent(), 'Player 2', false),
  ]);
}
