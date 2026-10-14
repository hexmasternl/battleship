// Starts the dev server the way the Aspire AppHost expects (npm script `start:aspire`).
// Aspire passes the YARP gateway URL in BACKEND_GATEWAY_URL, the Events API URL in EVENTS_API_URL
// and the port to serve on in PORT. Both URLs are written to public/config.json, which the app
// loads at runtime, so the changing ports never end up in the bundle or in a committed file.
import { spawn } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

/** Trims a URL and drops trailing slashes; `undefined` when nothing is left. */
function normalizeUrl(url) {
  const trimmed = url?.trim().replace(/\/+$/, '');
  return trimmed ? trimmed : undefined;
}

/**
 * Writes `public/config.json` for the given gateway and Events API URLs and returns the path
 * written. `eventsBaseUrl` is left out of the file when no events URL is given.
 */
export function writeRuntimeConfig(
  gatewayUrl,
  eventsUrl,
  configPath = join(root, 'public', 'config.json'),
) {
  const config = { backendBaseUrl: normalizeUrl(gatewayUrl) };
  const eventsBaseUrl = normalizeUrl(eventsUrl);
  if (eventsBaseUrl) config.eventsBaseUrl = eventsBaseUrl;
  writeFileSync(configPath, `${JSON.stringify(config, null, 2)}\n`);
  return configPath;
}

/** Arguments for `ng serve`; `--port` is left out when Aspire did not assign one. */
export function serveArgs(port) {
  const args = ['serve', '--host', 'localhost'];
  if (port?.trim()) args.push('--port', port.trim());
  return args;
}

function main() {
  const gatewayUrl = process.env.BACKEND_GATEWAY_URL;
  if (!gatewayUrl?.trim()) {
    console.error(
      'aspire-start: the environment variable BACKEND_GATEWAY_URL is not set. ' +
        'Start the frontend through the Aspire AppHost, which sets it to the gateway endpoint, ' +
        'or run `npm start` for UI-only work.',
    );
    process.exit(1);
  }

  const eventsUrl = process.env.EVENTS_API_URL;
  if (!eventsUrl?.trim()) {
    console.warn(
      'aspire-start: the environment variable EVENTS_API_URL is not set, so config.json gets no ' +
        'eventsBaseUrl and the app opens no event stream. The Aspire AppHost sets it to the ' +
        'Events API endpoint.',
    );
  }

  const configPath = writeRuntimeConfig(gatewayUrl, eventsUrl);
  console.log(`aspire-start: wrote ${configPath}`);

  const ng = join(root, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
  const child = spawn(process.execPath, [ng, ...serveArgs(process.env.PORT)], {
    cwd: root,
    stdio: 'inherit',
  });

  for (const signal of ['SIGINT', 'SIGTERM']) {
    process.on(signal, () => child.kill(signal));
  }
  child.on('exit', (code, signal) => process.exit(code ?? (signal ? 1 : 0)));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
