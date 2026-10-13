// Starts the dev server the way the Aspire AppHost expects (npm script `start:aspire`).
// Aspire passes the YARP gateway URL in BACKEND_GATEWAY_URL and the port to serve on in PORT.
// The gateway URL is written to public/config.json, which the app loads at runtime, so the
// changing port never ends up in the bundle or in a committed file.
import { spawn } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

/** Writes `public/config.json` for the given gateway URL and returns the path written. */
export function writeRuntimeConfig(gatewayUrl, configPath = join(root, 'public', 'config.json')) {
  const backendBaseUrl = gatewayUrl.trim().replace(/\/+$/, '');
  writeFileSync(configPath, `${JSON.stringify({ backendBaseUrl }, null, 2)}\n`);
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

  const configPath = writeRuntimeConfig(gatewayUrl);
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
