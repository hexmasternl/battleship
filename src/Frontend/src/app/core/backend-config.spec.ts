import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  BACKEND_CONFIG_URL,
  BackendConfig,
  parseBaseUrl,
  provideBackendConfig,
} from './backend-config';

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

  it('starts with no base URL', () => {
    expect(TestBed.inject(BackendConfig).baseUrl()).toBeNull();
  });

  it('loads the base URL from config.json during app initialisation', async () => {
    fetchSpy.mockResolvedValue(Response.json({ backendBaseUrl: 'http://localhost:58947/' }));

    const config = await bootWithConfig();

    expect(fetchSpy).toHaveBeenCalledWith(BACKEND_CONFIG_URL, expect.anything());
    expect(config.baseUrl()).toBe('http://localhost:58947');
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it('logs one error and keeps null when config.json is missing', async () => {
    fetchSpy.mockResolvedValue(new Response('Not found', { status: 404 }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('backendBaseUrl');
    expect(errorSpy.mock.calls[0][0]).toContain('config.json');
  });

  it('logs one error and keeps null when the request fails', async () => {
    fetchSpy.mockRejectedValue(new TypeError('Failed to fetch'));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
  });

  it('logs one error and keeps null when backendBaseUrl is missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ somethingElse: 'x' }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain('backendBaseUrl');
  });

  it('treats an empty backendBaseUrl as missing', async () => {
    fetchSpy.mockResolvedValue(Response.json({ backendBaseUrl: '  ' }));

    const config = await bootWithConfig();

    expect(config.baseUrl()).toBeNull();
    expect(errorSpy).toHaveBeenCalledTimes(1);
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
