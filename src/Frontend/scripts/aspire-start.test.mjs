// Tests for scripts/aspire-start.mjs. Run with `npm run test:scripts` (node:test, no Angular).
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { serveArgs, writeRuntimeConfig } from './aspire-start.mjs';

const dir = mkdtempSync(join(tmpdir(), 'aspire-start-'));
after(() => rmSync(dir, { recursive: true, force: true }));

function written(gatewayUrl, eventsUrl) {
  const path = writeRuntimeConfig(gatewayUrl, eventsUrl, join(dir, 'config.json'));
  return JSON.parse(readFileSync(path, 'utf8'));
}

describe('writeRuntimeConfig', () => {
  it('writes both URLs without trailing slashes', () => {
    assert.deepEqual(written(' http://localhost:58947/ ', 'http://localhost:5317//'), {
      backendBaseUrl: 'http://localhost:58947',
      eventsBaseUrl: 'http://localhost:5317',
    });
  });

  it('omits eventsBaseUrl when no events URL is given', () => {
    assert.deepEqual(written('http://gw', undefined), { backendBaseUrl: 'http://gw' });
    assert.deepEqual(written('http://gw', '   '), { backendBaseUrl: 'http://gw' });
  });
});

describe('serveArgs', () => {
  it('adds the port when Aspire assigned one', () => {
    assert.deepEqual(serveArgs(' 4300 '), ['serve', '--host', 'localhost', '--port', '4300']);
  });

  it('leaves the port out otherwise', () => {
    assert.deepEqual(serveArgs(undefined), ['serve', '--host', 'localhost']);
  });
});
