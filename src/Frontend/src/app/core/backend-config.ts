import {
  EnvironmentProviders,
  Service,
  inject,
  provideAppInitializer,
  signal,
} from '@angular/core';

/** Where the runtime configuration lives; written by `scripts/aspire-start.mjs`. */
export const BACKEND_CONFIG_URL = '/config.json';

/**
 * The backend gateway base URL, read once at startup from the runtime `config.json`.
 *
 * `baseUrl()` is `null` when the file or its `backendBaseUrl` key is missing (for example under a
 * plain `ng serve`); requests under `/api/` then fail instead of going to an unintended host.
 */
@Service()
export class BackendConfig {
  readonly #baseUrl = signal<string | null>(null);

  readonly baseUrl = this.#baseUrl.asReadonly();

  /**
   * Loads the configuration with `fetch` rather than `HttpClient`, so the API interceptor never
   * sees its own configuration request. Never rejects: a missing setting is logged, not thrown.
   */
  async load(): Promise<void> {
    const baseUrl = await readBaseUrl();
    if (baseUrl === null) {
      console.error(
        `Backend configuration: "backendBaseUrl" is missing from ${BACKEND_CONFIG_URL}. ` +
          'Start the app through the Aspire AppHost (npm run start:aspire) to generate it; ' +
          'requests to /api/ will fail until then.',
      );
    }
    this.#baseUrl.set(baseUrl);
  }
}

async function readBaseUrl(): Promise<string | null> {
  try {
    const response = await fetch(BACKEND_CONFIG_URL, { cache: 'no-store' });
    if (!response.ok) return null;
    return parseBaseUrl(await response.json());
  } catch {
    return null;
  }
}

/** Extracts a usable `backendBaseUrl` from the parsed file, without a trailing slash. */
export function parseBaseUrl(config: unknown): string | null {
  if (typeof config !== 'object' || config === null || !('backendBaseUrl' in config)) return null;
  const { backendBaseUrl } = config;
  if (typeof backendBaseUrl !== 'string') return null;
  const trimmed = backendBaseUrl.trim().replace(/\/+$/, '');
  return trimmed === '' ? null : trimmed;
}

/** Loads the backend configuration before the application starts. */
export function provideBackendConfig(): EnvironmentProviders {
  return provideAppInitializer(() => inject(BackendConfig).load());
}
