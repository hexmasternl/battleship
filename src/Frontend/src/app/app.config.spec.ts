import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { EVENTS_API_URL } from './events/events.tokens';

describe('appConfig', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function eventsApiUrlFor(config: unknown): Promise<string | null> {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(Response.json(config));
    TestBed.configureTestingModule({ providers: appConfig.providers });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    return TestBed.inject(EVENTS_API_URL);
  }

  it('provides the Events API URL from eventsBaseUrl in config.json', async () => {
    const url = await eventsApiUrlFor({
      backendBaseUrl: 'http://gw',
      eventsBaseUrl: 'https://events.example/',
    });

    expect(url).toBe('https://events.example');
  });

  it('provides null when config.json has no eventsBaseUrl', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    const url = await eventsApiUrlFor({ backendBaseUrl: 'http://gw' });

    expect(url).toBeNull();
  });
});
