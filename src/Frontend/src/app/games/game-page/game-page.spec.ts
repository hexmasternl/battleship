import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { FakeEventsService, provideFakeEvents } from '../../../testing/fake-events';
import { FakeGamesApi, flushPromises, provideFakeGamesApi } from '../../../testing/fake-games-api';
import { GAME_CODE, gameView } from '../../../testing/game-views';
import { GameView } from '../game-view';
import { GamePage } from './game-page';

describe('GamePage', () => {
  let api: FakeGamesApi;
  let events: FakeEventsService;

  async function render() {
    api = new FakeGamesApi();
    events = new FakeEventsService();
    TestBed.configureTestingModule({
      imports: [GamePage],
      providers: [provideRouter([]), provideFakeGamesApi(api), provideFakeEvents(events)],
    });
    const fixture = TestBed.createComponent(GamePage);
    fixture.componentRef.setInput('gameCode', GAME_CODE);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await flushPromises();
      await fixture.whenStable();
    };
    const answer = async (view: GameView) => {
      api.last('getGame').resolve(view);
      await settle();
    };
    const heading = () => host.querySelector('h2')?.textContent?.trim();
    return { fixture, host, settle, answer, heading };
  }

  it('requests the game for the code and shows a loading indicator', async () => {
    const { host } = await render();
    expect(api.last('getGame').args).toEqual([GAME_CODE]);
    expect(host.querySelector('h1')?.textContent?.trim()).toBe(GAME_CODE);
    expect(host.querySelector('[role="status"] mat-progress-spinner')).not.toBeNull();
  });

  it('shows the placement phase for a new game', async () => {
    const { answer, heading } = await render();
    await answer(gameView());
    expect(heading()).toBe('Place your fleet');
  });

  it('shows the game started panel and no placement for a started game', async () => {
    const { host, answer, heading } = await render();
    await answer(gameView({ state: 'started' }));
    expect(heading()).toBe('The game has started');
    expect(host.textContent).not.toContain('Place your fleet');
  });

  it.each(['completed', 'abandoned'] as const)(
    'shows game over with a link home for a %s game',
    async (state) => {
      const { host, answer, heading } = await render();
      await answer(gameView({ state }));
      expect(heading()).toBe('Game over');
      expect(host.querySelector('a')?.getAttribute('href')).toBe('/');
    },
  );

  it('says the game was not found and links home on a 404', async () => {
    const { host, settle, heading } = await render();
    api.last('getGame').reject({ kind: 'game-not-found', status: 404 });
    await settle();
    expect(heading()).toBe('Game not found');
    expect(host.querySelector('a')?.getAttribute('href')).toBe('/');
  });

  it('shows an error with a retry that requests the game again', async () => {
    const { host, settle, answer, heading } = await render();
    api.last('getGame').reject({ kind: 'network', status: 0 });
    await settle();
    expect(heading()).toBe('The game could not be loaded');

    (host.querySelector('button') as HTMLButtonElement).click();
    await settle();
    expect(api.of('getGame').length).toBe(2);
    await answer(gameView());
    expect(heading()).toBe('Place your fleet');
  });

  it('switches to the started phase after a game started event for this game', async () => {
    const { answer, heading } = await render();
    await answer(gameView());
    events.emit('game.started.v1', { gameCode: GAME_CODE });
    await answer(gameView({ state: 'started' }));
    expect(heading()).toBe('The game has started');
  });

  it('has no accessibility violations while loading and once loaded', async () => {
    const { host, answer } = await render();
    document.body.appendChild(host);
    try {
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
      await answer(gameView({ state: 'started' }));
      expect((await axe(host)).violations.map((v) => v.id)).toEqual([]);
    } finally {
      host.remove();
    }
  }, 20000);
});
