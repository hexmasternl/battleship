import {
  EnvironmentProviders,
  Service,
  computed,
  inject,
  provideAppInitializer,
  signal,
} from '@angular/core';

/** Where the runtime configuration lives; written by `scripts/aspire-start.mjs` or the deploy job. */
export const BACKEND_CONFIG_URL = '/config.json';

/** The URL settings read from the runtime configuration file. */
export interface RuntimeUrls {
  /** The backend gateway base URL (`backendBaseUrl`). */
  readonly baseUrl: string | null;
  /** The Events API base URL (`eventsBaseUrl`). */
  readonly eventsBaseUrl: string | null;
}

const NO_URLS: RuntimeUrls = { baseUrl: null, eventsBaseUrl: null };

/**
 * The backend URLs, read once at startup from the runtime `config.json`.
 *
 * `baseUrl()` is `null` when the file or its `backendBaseUrl` key is missing (for example under a
 * plain `ng serve`); requests under `/api/` then fail instead of going to an unintended host.
 * `eventsBaseUrl()` is `null` when `eventsBaseUrl` is missing; no event stream is opened then.
 */
@Service()
export class BackendConfig {
  readonly #urls = signal<RuntimeUrls>(NO_URLS);

  readonly baseUrl = computed(() => this.#urls().baseUrl);
  readonly eventsBaseUrl = computed(() => this.#urls().eventsBaseUrl);

  /**
   * Loads the configuration with `fetch` rather than `HttpClient`, so the API interceptor never
   * sees its own configuration request. Never rejects: missing settings are logged in one error,
   * not thrown.
   */
  async load(): Promise<void> {
    const urls = parseRuntimeUrls(await readConfig());
    const missing = missingSettings(urls);
    if (missing.length > 0) {
      console.error(
        `Backend configuration: ${missing.join(' and ')} not set in ${BACKEND_CONFIG_URL}. ` +
          'Start the app through the Aspire AppHost (npm run start:aspire) to generate it; ' +
          'backend requests and event streams that need these settings will fail until then.',
      );
    }
    this.#urls.set(urls);
  }
}

async function readConfig(): Promise<unknown> {
  try {
    const response = await fetch(BACKEND_CONFIG_URL, { cache: 'no-store' });
    if (!response.ok) return null;
    return await response.json();
  } catch {
    return null;
  }
}

/** The names of the settings that are missing, quoted as they appear in `config.json`. */
function missingSettings(urls: RuntimeUrls): string[] {
  return [
    ...(urls.baseUrl === null ? ['"backendBaseUrl"'] : []),
    ...(urls.eventsBaseUrl === null ? ['"eventsBaseUrl"'] : []),
  ];
}

/** Extracts both URL settings from the parsed file. */
export function parseRuntimeUrls(config: unknown): RuntimeUrls {
  return {
    baseUrl: parseUrlSetting(config, 'backendBaseUrl'),
    eventsBaseUrl: parseUrlSetting(config, 'eventsBaseUrl'),
  };
}

/** Extracts a usable `backendBaseUrl` from the parsed file, without a trailing slash. */
export function parseBaseUrl(config: unknown): string | null {
  return parseUrlSetting(config, 'backendBaseUrl');
}

/** Extracts a usable URL setting from the parsed file: trimmed, without a trailing slash. */
export function parseUrlSetting(config: unknown, key: string): string | null {
  if (typeof config !== 'object' || config === null || !(key in config)) return null;
  const value: unknown = (config as Record<string, unknown>)[key];
  if (typeof value !== 'string') return null;
  const trimmed = value.trim().replace(/\/+$/, '');
  return trimmed === '' ? null : trimmed;
}

/** Loads the backend configuration before the application starts. */
export function provideBackendConfig(): EnvironmentProviders {
  return provideAppInitializer(() => inject(BackendConfig).load());
}
