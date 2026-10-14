import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  BACKEND_CONFIG_URL,
  BackendConfig,
  parseBaseUrl,
  parseRuntimeUrls,
  provideBackendConfig,
} from './backend-config';

const BOTH = { backendBaseUrl: 'http://gw', eventsBaseUrl: 'http://events' };

describe('BackendConfig', () => {
  let fetchSpy: ReturnType<typeof vi.spyOn>;
  let errorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    fetchSpy = vi.spyOn(globalThis, 'fetch');
    errorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function bootWithConfig(): Promise<BackendConfig> {
    TestBed.configureTestingModule({ providers: [provideBackendConfig()] });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    return TestBed.inject(BackendConfig);
  }

  it('starts with no URLs', () => {
    const config = TestBed.inject(BackendConfig);
    expect(config.baseUrl()).toBeNull();
    expect(config.eventsBaseUrl()).toBeNull();
  });

  it('loads both URLs from config.json during app initialisation', async () => {
    fetchSpy.mockResolvedValue(
      Response.json({
        backendBaseUrl: 'http://localhost:58947/',
        eventsBaseUrl: 'http://localhost:5317//',
      }),
    );

    const config = await bootWithConfig();

    expect(fetchSpy).toHaveBeenCalledWith(BACKEND_CONFIG_URL, expect.anything());
    expect(config.baseUrl()).toBe('http://localhost:58947');
    expect(config.eventsBaseUrl()).toBe('http://localhost:5317');
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it('logs one error naming both settings and keeps null when config.json is missing', async () => {
    fetchSpy.mockResolvedValue(new Response('Not found', { status: 404 }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(config.eventsBaseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('backendBaseUrl');
    expect(errorSpy.mock.calls[0][0]).toContain('eventsBaseUrl');
    expect(errorSpy.mock.calls[0][0]).toContain('config.json');
  });

  it('logs one error and keeps null when the request fails', async () => {
    fetchSpy.mockRejectedValue(new TypeError('Failed to fetch'));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(config.eventsBaseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
  });

  it('logs one error naming backendBaseUrl when only that setting is missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ eventsBaseUrl: 'http://events' }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(config.eventsBaseUrl()).toBe('http://events');
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('backendBaseUrl');
    expect(errorSpy.mock.calls[0][0]).not.toContain('eventsBaseUrl');
  });

  it('logs one error naming eventsBaseUrl when only that setting is missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ backendBaseUrl: 'http://gw' }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBe('http://gw');
    expect(config.eventsBaseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('eventsBaseUrl');
    expect(errorSpy.mock.calls[0][0]).toContain('config.json');
    expect(errorSpy.mock.calls[0][0]).not.toContain('backendBaseUrl');
  });

  it('treats an empty backendBaseUrl as missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ ...BOTH, backendBaseUrl: '  ' }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
  });

  it('treats an empty eventsBaseUrl as missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ ...BOTH, eventsBaseUrl: ' / ' }));

    const config = await bootWithConfig();

    expect(config.eventsBaseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('eventsBaseUrl');
  });
});

describe('parseBaseUrl', () => {
  it.each([
    [{ backendBaseUrl: 'http://gw' }, 'http://gw'],
    [{ backendBaseUrl: 'http://gw//' }, 'http://gw'],
    [{ backendBaseUrl: 42 }, null],
    [{}, null],
    [null, null],
    ['http://gw', null],
  ])('parses %j as %j', (input, expected) => {
    expect(parseBaseUrl(input)).toBe(expected);
  });
});

describe('parseRuntimeUrls', () => {
  it('reads both settings, trimmed and without trailing slashes', () => {
    expect(
      parseRuntimeUrls({ backendBaseUrl: 'http://gw/', eventsBaseUrl: ' http://ev// ' }),
    ).toEqual({ baseUrl: 'http://gw', eventsBaseUrl: 'http://ev' });
  });

  it.each([[{ eventsBaseUrl: 42 }], [{}], [null], ['http://ev']])(
    'reports no events URL for %j',
    (input) => {
      expect(parseRuntimeUrls(input).eventsBaseUrl).toBeNull();
    },
  );
});
