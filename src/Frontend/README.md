# Battleship

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.2.2.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Calling the backend

Backend calls go through `HttpClient` to a relative URL under `/api/`, followed by the module
prefix and the rest of the endpoint path, for example `http.get('/api/players/me')`. Never write
a host or port into the app: the backend runs behind the local YARP gateway, whose port changes
on every run.

- **Interceptor.** `apiGatewayInterceptor` (`src/app/core/api-gateway.interceptor.ts`) rewrites
  every request starting with `/api/` to `<gateway>/<rest>`, stripping `/api` and keeping the
  query string, so `/api/players/me?x=1` goes to `<gateway>/players/me?x=1`. Other URLs, such as
  assets or absolute URLs, pass through unchanged.
- **Runtime configuration.** The gateway URL comes from `public/config.json`
  (`{ "backendBaseUrl": "http://localhost:<port>" }`), which `BackendConfig`
  (`src/app/core/backend-config.ts`) loads with `fetch` before the app starts. The file is
  generated and git-ignored; never commit it.
- **Starting through Aspire.** Run the AppHost from the repository root:

  ```bash
  dotnet run --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.AppHost
  ```

  It starts the `frontend` resource with `npm run start:aspire` once the `gateway` is up, passing
  `BACKEND_GATEWAY_URL` and `PORT`. `scripts/aspire-start.mjs` writes `public/config.json` from
  `BACKEND_GATEWAY_URL` and runs `ng serve --host localhost --port $PORT`. Running
  `npm run start:aspire` without `BACKEND_GATEWAY_URL` stops with an error naming the variable.

- **UI-only work.** Plain `ng serve` (`npm start`) still works for screens that do not call the
  backend. Without `config.json` the app logs one error naming `backendBaseUrl` at start-up, and
  every `/api/` request fails with an `HttpErrorResponse` instead of being sent anywhere.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Theming

The application is themed from the Battleship design system (`docs/design-system.md` at the repository root). Three files under `src/` carry it:

- `styles/_tokens.scss` holds the `--bs-*` custom properties, copied verbatim from the design system's token reference.
- `styles/_palettes.scss` builds the Material 3 tonal palettes from the design system colour scales; every tone is commented with the token or mix it comes from.
- `styles.scss` calls `mat.theme()` with those palettes (dark only), pins the surface, text and outline roles to the design system semantic roles with `mat.theme-overrides()`, and sets the `body` defaults.

Run the commands below from the repository root in a Bash shell (Git Bash on Windows).

### Checking that the frontend tokens agree with the design system

Compares every `--bs-*` name and value in `_tokens.scss` with the token reference in `docs/design-system.md`, and every `$bs-*` scale step that `_palettes.scss` repeats as a Sass variable with the resolved document value. Prints each difference; the trailing line reports `0 differences` and the exit code is 0 when the files agree. When the design system changes, copy its `:root` block over the one in `_tokens.scss`, update the Sass variables in `_palettes.scss`, and re-run.

```bash
node -e '
const fs=require("fs");
const block=f=>{const s=fs.readFileSync(f,"utf8"),i=s.indexOf(":root {");return s.slice(i,s.indexOf("\n}",i))};
const pairs=f=>Object.fromEntries([...block(f).matchAll(/(--bs-[a-z0-9-]+)\s*:\s*([^;]+);/g)].map(m=>[m[1],m[2].trim()]));
const doc=pairs("docs/design-system.md"),app=pairs("src/Frontend/src/styles/_tokens.scss");
const resolve=v=>{let g=0;while(v&&/var\(/.test(v)&&g++<20)v=v.replace(/var\((--[a-z0-9-]+)\)/g,(_,n)=>doc[n]);return v};
let bad=0;
for(const k of new Set([...Object.keys(doc),...Object.keys(app)]))if(doc[k]!==app[k]){bad++;console.log(`${k}: document=${doc[k]??"(missing)"} frontend=${app[k]??"(missing)"}`)}
for(const m of fs.readFileSync("src/Frontend/src/styles/_palettes.scss","utf8").matchAll(/^\$(bs-[a-z0-9-]+):\s*(#[0-9a-f]{6});/gm))if(resolve(doc["--"+m[1]])!==m[2]){bad++;console.log(`$${m[1]}: document=${resolve(doc["--"+m[1]])??"(missing)"} palettes=${m[2]}`)}
console.log(`${Object.keys(doc).length} tokens in the document, ${Object.keys(app).length} in the frontend, ${bad} differences`);
process.exit(bad?1:0)'
```

### Measuring contrast of the Material text roles

Reads the built global stylesheet (run `ng build` or `ng build --configuration development` in `src/Frontend` first), resolves the `--mat-sys-*` roles including the `var(--bs-*)` overrides, and measures the WCAG 2.x contrast of the text roles the material-theme spec names. Exit code 1 means a pair fails its threshold. Paste the output over the table below whenever a colour changes.

```bash
node -e '
const fs=require("fs"),dir="src/Frontend/dist/battleship/browser",css=fs.readFileSync(dir+"/"+fs.readdirSync(dir).find(f=>/^styles.*\.css$/.test(f)),"utf8"),t={};
for(const m of css.matchAll(/(--(?:mat-sys|bs)-[a-z0-9-]+)\s*:\s*([^;]+);/g))t[m[1]]=m[2].trim();
const r=v=>{let g=0;while(/var\(/.test(v)&&g++<20)v=v.replace(/var\((--[a-z0-9-]+)\)/g,(_,n)=>t[n]);return v};
const rgb=v=>v.startsWith("#")?[1,3,5].map(i=>parseInt(v.substr(i,2),16)):v.match(/\d+/g).map(Number);
const L=v=>{const[a,b,c]=rgb(v).map(x=>x/255).map(c=>c<=.04045?c/12.92:((c+.055)/1.055)**2.4);return .2126*a+.7152*b+.0722*c};
const C=(f,b)=>{const[x,y]=[L(f),L(b)].sort((p,q)=>q-p);return(x+.05)/(y+.05)};
const P=[["on-surface","surface",4.5,"body text on the page and base surface"],["on-surface-variant","surface",4.5,"secondary text on the page"],["on-surface","surface-container-highest",4.5,"body text on the highest container"],["on-primary","primary",4.5,"labels on filled primary buttons"]];
let bad=0;console.log("| Foreground | Background | Ratio | Needs | Use | Result |\n|---|---|---|---|---|---|");
for(const[f,b,n,u]of P){const F=r(t["--mat-sys-"+f]),B=r(t["--mat-sys-"+b]),c=C(F,B),ok=c>=n;bad+=!ok;console.log(`| \`${f}\` (${F}) | \`${b}\` (${B}) | ${c.toFixed(2)}:1 | ${n}:1 | ${u} | ${ok?"pass":"FAIL"} |`)}
process.exit(bad?1:0)'
```

Last measured 2026-10-13:

| Foreground | Background | Ratio | Needs | Use | Result |
|---|---|---|---|---|---|
| `on-surface` (#b2c2d4) | `surface` (#011627) | 10.09:1 | 4.5:1 | body text on the page and base surface | pass |
| `on-surface-variant` (#7a8694) | `surface` (#011627) | 4.95:1 | 4.5:1 | secondary text on the page | pass |
| `on-surface` (#b2c2d4) | `surface-container-highest` (#163953) | 6.63:1 | 4.5:1 | body text on the highest container | pass |
| `on-primary` (rgb(1, 37, 56)) | `primary` (#00c0ff) | 7.55:1 | 4.5:1 | labels on filled primary buttons | pass |

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.

## Real-time events

`src/app/events/` holds `EventsService`, the application-wide client for the Events API stream
(`GET /events?playerId=<id>`, server-sent events). It wraps the browser's `EventSource`, which
reconnects on its own and sends `Last-Event-ID` so the server replays what was missed. The service
exposes `connect(playerId)`, `disconnect()`, `events$` (type, id and parsed JSON payload; heartbeats
are dropped) and a `state` signal (`disconnected`, `connecting`, `open`, `reconnecting`). Nothing
connects at start-up yet; the first screen that needs live updates calls `connect`.

Every event type the server pushes must be listed in `src/app/events/realtime-event-types.ts`:
`EventSource` only delivers named events that have a listener, so an unlisted type is lost. Unnamed
messages are logged to the console as a warning.

### Pointing the app at the Events API

The base URL comes from the `EVENTS_API_URL` injection token, provided in `src/app/app.config.ts`:

```ts
{ provide: EVENTS_API_URL, useValue: 'http://localhost:5317' },
```

`http://localhost:5317` is the `http` endpoint the Aspire AppHost gives the Events API
(`hexmaster-battleship-events-api` in the Aspire dashboard). If the dashboard shows a different
address, change the value there; it is the base URL only, the service appends `/events`.

The Events API accepts cross-origin requests only from the origins in `Events:AllowedOrigins`
(default `http://localhost:4200`). Serve the app with plain `ng serve` on port 4200, or add your
dev-server origin to that setting, otherwise the browser blocks the stream and the service ends in
`disconnected` with a CORS error in the console.

### Confirming that events arrive

1. Start the AppHost (`aspire run` from `src/Aspire/HexMaster.Battleship.Aspire`, see the
   repository README) and wait until the Events API is healthy in the dashboard.
2. Start the app with `ng serve` and open `http://localhost:4200/` with the developer tools open on
   the **Network** tab.
3. Open the stream. Until a screen calls `connect`, either inject `EventsService` in a component
   and call `connect('A')` for the duration of the check, or run
   `new EventSource('http://localhost:5317/events?playerId=A')` in the browser console, which uses
   the same request the service makes.
4. Filter the network list on `events`. The `/events?playerId=A` request stays pending with status
   `200` and type `eventsource`; select it and open the **EventStream** tab (Chrome and Edge). A
   `heartbeat` row with data `null` appears every 15 seconds while the stream is idle.
5. Publish an event for player A from a shell:

   ```bash
   dapr publish --publish-app-id events-api --pubsub pubsub --topic integration-events \
     --data '{"id":"e-9","type":"game.shot-fired.v1","target":"player","targetIds":["A"],"occurredAt":"2026-10-13T10:00:00Z","payload":{"gameCode":"ABCD1234","column":3,"row":5}}'
   ```

   A `game.shot-fired.v1` row appears in the EventStream tab with the server's sequence number as
   its id and the payload as its data, and subscribers of `events$` receive
   `{ type: 'game.shot-fired.v1', id: '<sequence>', payload: { gameCode: 'ABCD1234', column: 3, row: 5 } }`.
   The browser only listens for the names in `REALTIME_EVENT_TYPES`, so a type outside the
   integration event catalog does not show up.

Verified on 2026-10-13 against the running AppHost: the service opened the stream (`state` read
`connecting`, then `open`), no value was emitted during a 17-second idle period that carried a
heartbeat, the published envelope was surfaced as
`{"type":"shot-fired","id":"3","payload":{"x":3,"y":5}}`, and `disconnect()` closed the request and
set `state` to `disconnected`. Port 4200 was taken by another dev server at the time, so the app ran
on another port in Chromium with web security disabled; the CORS answer for
`Origin: http://localhost:4200` (`Access-Control-Allow-Origin: http://localhost:4200`) was checked
separately with `curl`, and with security enabled the other origin was blocked as described above.

## Welcome page

The landing page at `/` (`src/app/welcome/`) is built from the design system tokens in
`src/styles/_tokens.scss` and the button, card and panel recipes in `src/styles/_components.scss`.
Its automated accessibility check runs with axe in `welcome-page.spec.ts` (`ng test`). jsdom cannot
resolve colours from CSS custom properties, so the colour contrast of every text and background
pair the page uses is measured separately with the script below, which reads the token partial,
resolves the `var()` chains and applies the WCAG 2.x relative-luminance formula. Body text needs
4.5:1; large text (bold at 1.25rem or larger) needs 3:1. Exit code 1 means a pair fails.

Save the script as `contrast.mjs` outside the repository and run it from the repository root:

```bash
node contrast.mjs src/Frontend/src/styles/_tokens.scss
```

<details>
<summary>contrast.mjs</summary>

```js
import { readFileSync } from 'node:fs';

const src = readFileSync(process.argv[2] ?? 'src/Frontend/src/styles/_tokens.scss', 'utf8');
const t = {};
for (const m of src.matchAll(/(--bs-[a-z0-9-]+)\s*:\s*([^;\n]+);/g)) t[m[1]] = t[m[1]] || m[2].trim();
const r = (v) => {
  let g = 0;
  while (/var\(/.test(v) && g++ < 20) v = v.replace(/var\((--[a-z0-9-]+)\)/g, (_, n) => t[n]);
  return v;
};
const L = (h) => {
  const [a, b, c] = [1, 3, 5]
    .map((i) => parseInt(h.substr(i, 2), 16) / 255)
    .map((c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * a + 0.7152 * b + 0.0722 * c;
};
const C = (f, b) => {
  const [x, y] = [L(f), L(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
};
const P = [
  ['--bs-color-text', '--bs-color-bg', 4.5, 'body: intro copy, page text'],
  ['--bs-color-text-strong', '--bs-color-surface', 4.5, 'body: welcome heading on the panel'],
  ['--bs-color-text', '--bs-color-surface', 4.5, 'body: intro paragraph on the panel'],
  ['--bs-color-text', '--bs-color-surface-raised', 4.5, 'body: card copy'],
  ['--bs-color-text-strong', '--bs-color-surface-raised', 4.5, 'body: card headings, ship names'],
  ['--bs-color-text-dim-raised', '--bs-color-surface-raised', 4.5, 'body: ship lengths on cards'],
  ['--bs-color-text-dim', '--bs-color-surface', 4.5, 'body: console header'],
  ['--bs-color-text-dim', '--bs-color-bg', 4.5, 'body: console timestamps on the screen'],
  ['--bs-color-text-on-accent', '--bs-color-accent', 4.5, 'body: "Create a new game" label'],
  ['--bs-color-text-on-accent', '--bs-color-accent-hover', 4.5, 'body: primary button hover'],
  ['--bs-color-text-on-accent', '--bs-color-secondary', 4.5, 'body: "Join an existing game" label'],
  ['--bs-color-text-on-accent', '--bs-color-secondary-hover', 4.5, 'body: secondary button hover'],
  ['--bs-color-accent', '--bs-color-surface', 4.5, 'body: console text, eyebrow, panel-info text'],
  ['--bs-color-accent', '--bs-color-bg', 4.5, 'body: console text on the screen, section heading'],
  ['--bs-color-text', '--bs-color-bg', 4.5, 'body: ghost link label on the coming-soon page'],
  ['--bs-color-text-strong', '--bs-color-hit', 3, 'large: bold 1.25rem cross glyph on the hit marker (decorative, text alongside)'],
  ['--bs-color-text-strong', '--bs-color-sunk', 4.5, 'body: cross glyph on the sunk marker'],
];
let bad = 0;
console.log('| Foreground | Background | Ratio | Needs | Use | Result |\n|---|---|---|---|---|---|');
for (const [f, b, n, u] of P) {
  const F = r(t[f]);
  const B = r(t[b]);
  const c = C(F, B);
  const ok = c >= n;
  bad += !ok;
  console.log(`| \`${f}\` (${F}) | \`${b}\` (${B}) | ${c.toFixed(2)}:1 | ${n}:1 | ${u} | ${ok ? 'pass' : 'FAIL'} |`);
}
process.exit(bad ? 1 : 0);
```

</details>

**Last run:** 2026-10-13, 17 pairs, all pass.

| Foreground | Background | Ratio | Needs | Use | Result |
|---|---|---|---|---|---|
| `--bs-color-text` (#b2c2d4) | `--bs-color-bg` (#011627) | 10.09:1 | 4.5:1 | body: intro copy, page text | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-surface` (#041e32) | 14.54:1 | 4.5:1 | body: welcome heading on the panel | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface` (#041e32) | 9.35:1 | 4.5:1 | body: intro paragraph on the panel | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface-raised` (#09273c) | 8.46:1 | 4.5:1 | body: card copy | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-surface-raised` (#09273c) | 13.16:1 | 4.5:1 | body: card headings, ship names | pass |
| `--bs-color-text-dim-raised` (#94a5b8) | `--bs-color-surface-raised` (#09273c) | 6.10:1 | 4.5:1 | body: ship lengths on cards | pass |
| `--bs-color-text-dim` (#7a8694) | `--bs-color-surface` (#041e32) | 4.58:1 | 4.5:1 | body: console header | pass |
| `--bs-color-text-dim` (#7a8694) | `--bs-color-bg` (#011627) | 4.95:1 | 4.5:1 | body: console timestamps on the screen | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-accent` (#00c0ff) | 8.72:1 | 4.5:1 | body: "Create a new game" label | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-accent-hover` (#63cdff) | 10.22:1 | 4.5:1 | body: primary button hover | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-secondary` (#2f87ff) | 5.27:1 | 4.5:1 | body: "Join an existing game" label | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-secondary-hover` (#60a1ff) | 7.02:1 | 4.5:1 | body: secondary button hover | pass |
| `--bs-color-accent` (#00c0ff) | `--bs-color-surface` (#041e32) | 8.07:1 | 4.5:1 | body: console text, eyebrow, panel-info text | pass |
| `--bs-color-accent` (#00c0ff) | `--bs-color-bg` (#011627) | 8.72:1 | 4.5:1 | body: console text on the screen, section heading | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-bg` (#011627) | 10.09:1 | 4.5:1 | body: ghost link label on the coming-soon page | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-hit` (#ff2146) | 3.24:1 | 3:1 | large: bold 1.25rem cross glyph on the hit marker (decorative, text alongside) | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-sunk` (#9c1d2c) | 6.85:1 | 4.5:1 | body: cross glyph on the sunk marker | pass |

The hit marker's cross is a shape cue, not text: the legend's meaning is carried by the words next
to it, so the glyph only needs the large-text ratio that the design system also applies to the
danger button label on the same colour.
