import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RequirePlayer } from '../shared/player-name/require-player';
import { BridgeConsole } from './bridge-console';

/**
 * The full-viewport hero of the landing page: a marine background built from design system
 * tokens, a decorative radar, and the two-panel jumbotron with the welcome copy, the calls to
 * action and the bridge console. Stateless: everything on it is static or lives in the console;
 * the calls to action ask an unknown visitor for a player name first (`RequirePlayer`).
 */
@Component({
  selector: 'battle-hero-section',
  imports: [RouterLink, RequirePlayer, BridgeConsole],
  host: { class: 'hero' },
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.scss',
})
export class HeroSection {}
