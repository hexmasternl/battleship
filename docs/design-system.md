# Battleship design system

The visual contract for every Battleship screen. It is dark only, built for phones first, and styled after a warship's bridge: deep ocean surfaces, cyan instrument light, glow instead of gloss. The companion page [`design-system.html`](design-system.html) renders every token and element described here and is the reference implementation.

**How to adopt it.** Copy the `:root` block from the [Token reference](#10-token-reference) into the frontend's global stylesheet. Nothing else is needed: every colour, size, effect and timing is a CSS custom property, so any framework can consume them unchanged. Build components from the semantic roles (`--bs-color-accent`, `--bs-color-surface`), not from scale steps, so a theme swap stays a one-block change.

**Naming.** Every token starts with two dashes and `bs`, then a category, a name and an optional step, for example `--bs-color-primary-300` or `--bs-space-4`. The prefix `bs` keeps the system clear of any component library's own variables. Categories: `color`, `font`, `text` (sizes), `leading`, `tracking`, `space`, `radius`, `shadow`, `glow`, `duration`, `ease`, `bp`, plus a few layout values (`gutter`, `container`, `focus-width`, `focus-offset`).

**Scope.** Tokens, the core interface elements, the game elements and the four instrument motifs. No framework components, no light theme, no build step. See the OpenSpec change `issue-1-design-system` for the requirements this document satisfies.

Contents: [1 Colour](#1-colour) · [2 Typography](#2-typography) · [3 Spacing and radius](#3-spacing-and-radius) · [4 Breakpoints](#4-breakpoints) · [5 Effects](#5-effects) · [6 Motion](#6-motion) · [7 Components](#7-components) · [8 Game elements](#8-game-elements) · [9 Instruments](#9-instruments) · [10 Token reference](#10-token-reference) · [11 Maintaining this document](#11-maintaining-this-document)

---

## 1. Colour

### 1.1 Base colours

Six colours came with the brief and are used byte for byte. They are the only literal colours that are not generated; every other colour below is derived from one of them.

| Token | Value | Role in the brief |
|---|---|---|
| `--bs-color-bg-base` | `#011627` | General background |
| `--bs-color-primary-base` | `#00c0ff` | Primary |
| `--bs-color-secondary-base` | `#2f87ff` | Secondary |
| `--bs-color-text-base` | `#b2c2d4` | Text |
| `--bs-color-text-dim-base` | `#7a8694` | Text dim |
| `--bs-color-danger-base` | `#ff2146` | Danger |

### 1.2 How the tints and shades are derived

All derivation happens in OKLCH (lightness `L` 0 to 1, chroma `C`, hue `H` in degrees) so that a tint of cyan is still cyan and a shade of red is still red. The hue of a base colour never changes across its scale.

- **Tints** (steps lighter than the base): `L' = L + (1 - L) · t`, `C' = C · (1 - t)`, `H' = H`. Moving toward white.
- **Shades** (steps darker than the base): `L' = L + (L_bg - L) · t`, `C' = C + (C_bg - C) · t`, `H' = H`, where `L_bg = 0.1928` and `C_bg = 0.0452` are the OKLCH lightness and chroma of the background. Moving toward the background, so dark steps sit naturally on it.
- **Mix amounts `t`** for primary, secondary and danger (base at step 500): 50 → 0.90, 100 → 0.78, 200 → 0.60, 300 → 0.40, 400 → 0.20, 600 → 0.22, 700 → 0.42, 800 → 0.62, 900 → 0.80.
- **Mix amounts `t`** for text (base at step 300, because the base is already light): 50 → 0.72, 100 → 0.48, 200 → 0.24, 400 → 0.15, 500 → 0.30, 600 → 0.45, 700 → 0.60, 800 → 0.75, 900 → 0.88.
- **Surfaces** (elevation steps of the background): `L_n = L_bg + 0.035 · n`, `C_n = C_bg + 0.004 · n`, `H` unchanged, for `n` = 1 to 4. Surface 0 is the background itself.
- **Success and warning** have no base colour of their own. Success is the primary colour with its hue rotated to 150° (L and C kept). Warning is the danger colour with its hue rotated to 80° and `L` raised by 0.15. Their 800 step is the shade formula at `t` = 0.62.
- Any result outside the sRGB gamut has its chroma reduced until it fits; lightness and hue are kept.

The generator is reproduced in [section 11](#11-maintaining-this-document).

### 1.3 Scales

Lightness decreases monotonically down each column. The bold step is the base colour.

| Step | Primary | Secondary | Danger | Text |
|---|---|---|---|---|
| 50 | `#eef9ff` | `#ecf4ff` | `#ffefee` | `#e9eef3` |
| 100 | `#d9f2ff` | `#d5e6ff` | `#ffdbd9` | `#d6dfe9` |
| 200 | `#b9e7ff` | `#b2d1ff` | `#ffbcba` | `#c4d0de` |
| 300 | `#92daff` | `#8abaff` | `#ff9796` | **`#b2c2d4`** |
| 400 | `#63cdff` | `#60a1ff` | `#ff6b70` | `#94a5b8` |
| 500 | **`#00c0ff`** | **`#2f87ff`** | **`#ff2146`** | `#78899c` |
| 600 | `#0097c9` | `#266bcb` | `#ca2038` | `#5c6e81` |
| 700 | `#00739a` | `#1e539d` | `#9c1d2c` | `#425468` |
| 800 | `#00516e` | `#163c72` | `#701820` | `#293b4f` |
| 900 | `#003449` | `#0e294e` | `#4b1316` | `#15273a` |

Token names are the colour category, the column name and the step, for example `--bs-color-primary-300`, `--bs-color-secondary-700`, `--bs-color-danger-900` and `--bs-color-text-50`.

### 1.4 Surfaces

| Token | Value | Use |
|---|---|---|
| `--bs-color-surface-0` | `#011627` (the background) | Page |
| `--bs-color-surface-1` | `#041e32` | Panels, the ocean of the grid |
| `--bs-color-surface-2` | `#09273c` | Cards, inputs |
| `--bs-color-surface-3` | `#103048` | Overlays, hover on cards |
| `--bs-color-surface-4` | `#163953` | Borders, dividers |

### 1.5 Derived status hues

| Token | Value | Derivation |
|---|---|---|
| `--bs-color-success-500` | `#5ecb7b` | primary, hue → 150° |
| `--bs-color-success-800` | `#215630` | success-500, shade `t` = 0.62 |
| `--bs-color-warning-500` | `#f1ad00` | danger, hue → 80°, L + 0.15 |
| `--bs-color-warning-800` | `#664700` | warning-500, shade `t` = 0.62 |

### 1.6 Semantic roles

Components use these and only these. Every role is a `var()` reference to a base or scale token, never a literal, so re-theming is a matter of reassigning roles.

| Role | Points at | Use |
|---|---|---|
| `--bs-color-bg` | surface-0 | Page background |
| `--bs-color-surface` | surface-1 | Panels |
| `--bs-color-surface-raised` | surface-2 | Cards, inputs |
| `--bs-color-surface-overlay` | surface-3 | Overlays, card hover |
| `--bs-color-border` | surface-4 | Default borders and dividers |
| `--bs-color-border-strong` | text-700 | Borders that must read on raised surfaces |
| `--bs-color-text` | text-300 | Body text |
| `--bs-color-text-strong` | text-50 | Headings, emphasised text |
| `--bs-color-text-dim` | text-dim-base | Secondary text on the page background and on surface-1 only |
| `--bs-color-text-dim-raised` | text-400 | Secondary text on surface-2 and above (the dim base does not reach AA there) |
| `--bs-color-text-on-accent` | bg-base | Labels on primary and secondary fills |
| `--bs-color-text-on-danger` | text-50 | Labels on danger fills |
| `--bs-color-accent` | primary-500 | Primary actions, links, active indicators |
| `--bs-color-accent-hover` | primary-400 | Hover on primary actions |
| `--bs-color-accent-active` | primary-300 | Pressed primary actions (pressing brightens, see 7.1) |
| `--bs-color-accent-subtle` | primary-900 | Tinted backgrounds behind accent content |
| `--bs-color-secondary` | secondary-500 | Secondary actions |
| `--bs-color-secondary-hover` | secondary-400 | Hover on secondary actions |
| `--bs-color-secondary-active` | secondary-300 | Pressed secondary actions |
| `--bs-color-secondary-subtle` | secondary-900 | Tinted backgrounds behind secondary content |
| `--bs-color-focus-ring` | primary-300 | Keyboard focus ring |
| `--bs-color-danger` | danger-500 | Destructive actions, errors |
| `--bs-color-danger-hover` | danger-400 | Hover on destructive actions |
| `--bs-color-danger-active` | danger-700 | Pressed destructive actions |
| `--bs-color-danger-surface` | danger-900 | Error message background |
| `--bs-color-success` | success-500 | Success text and icons |
| `--bs-color-success-surface` | success-800 | Success message background |
| `--bs-color-warning` | warning-500 | Warning text and icons |
| `--bs-color-warning-surface` | warning-800 | Warning message background |

### 1.7 Game roles

| Role | Points at | Use |
|---|---|---|
| `--bs-color-ocean` | surface-1 | Grid cell background |
| `--bs-color-grid-line` | primary-900 | Lines between cells |
| `--bs-color-coordinate` | text-dim-base | Row and column labels |
| `--bs-color-ship` | secondary-700 | Ship hull fill |
| `--bs-color-ship-edge` | secondary-500 | Ship outline |
| `--bs-color-placement-valid` | primary-500 | Ship outline and glow while a placement is legal |
| `--bs-color-placement-invalid` | danger-500 | Ship outline and glow while a placement overlaps or leaves the board |
| `--bs-color-miss` | text-dim-base | Miss marker |
| `--bs-color-hit` | danger-500 | Hit marker |
| `--bs-color-sunk` | danger-700 | Sunk ship fill |

### 1.8 Contrast

Measured with the WCAG 2.x relative-luminance formula on the resolved hex values. Body text needs 4.5:1, large text (bold at 1.25rem or larger) needs 3:1. Regenerate the table with the command in section 11 whenever a value changes.

| Foreground | Background | Ratio | Needs | Use | Result |
|---|---|---|---|---|---|
| `--bs-color-text` (#b2c2d4) | `--bs-color-bg` (#011627) | 10.09:1 | 4.5:1 | body | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface-1` (#041e32) | 9.35:1 | 4.5:1 | body | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface-2` (#09273c) | 8.46:1 | 4.5:1 | body | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface-3` (#103048) | 7.51:1 | 4.5:1 | body | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-surface-4` (#163953) | 6.63:1 | 4.5:1 | body | pass |
| `--bs-color-text-dim` (#7a8694) | `--bs-color-bg` (#011627) | 4.95:1 | 4.5:1 | body | pass |
| `--bs-color-text-dim` (#7a8694) | `--bs-color-surface-1` (#041e32) | 4.58:1 | 4.5:1 | body | pass |
| `--bs-color-text-dim-raised` (#94a5b8) | `--bs-color-surface-2` (#09273c) | 6.10:1 | 4.5:1 | body | pass |
| `--bs-color-text-dim-raised` (#94a5b8) | `--bs-color-surface-3` (#103048) | 5.42:1 | 4.5:1 | body | pass |
| `--bs-color-text-dim-raised` (#94a5b8) | `--bs-color-surface-4` (#163953) | 4.78:1 | 4.5:1 | body | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-bg` (#011627) | 15.71:1 | 4.5:1 | body | pass |
| `--bs-color-accent` (#00c0ff) | `--bs-color-bg` (#011627) | 8.72:1 | 4.5:1 | body (links, readouts) | pass |
| `--bs-color-accent` (#00c0ff) | `--bs-color-surface-2` (#09273c) | 7.30:1 | 4.5:1 | body (links, readouts) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-accent` (#00c0ff) | 8.72:1 | 4.5:1 | body (primary button) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-accent-hover` (#63cdff) | 10.22:1 | 4.5:1 | body (primary button hover) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-accent-active` (#92daff) | 11.96:1 | 4.5:1 | body (primary button active) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-secondary` (#2f87ff) | 5.27:1 | 4.5:1 | body (secondary button) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-secondary-hover` (#60a1ff) | 7.02:1 | 4.5:1 | body (secondary button hover) | pass |
| `--bs-color-text-on-accent` (#011627) | `--bs-color-secondary-active` (#8abaff) | 9.21:1 | 4.5:1 | body (secondary button active) | pass |
| `--bs-color-text-on-danger` (#e9eef3) | `--bs-color-danger` (#ff2146) | 3.24:1 | 3:1 | large (danger button label, bold 1.25rem) | pass |
| `--bs-color-text-on-danger` (#e9eef3) | `--bs-color-danger-active` (#9c1d2c) | 6.85:1 | 4.5:1 | body (danger button active) | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-danger-surface` (#4b1316) | 8.23:1 | 4.5:1 | body (danger status) | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-success-surface` (#215630) | 4.74:1 | 4.5:1 | body (success status) | pass |
| `--bs-color-text` (#b2c2d4) | `--bs-color-warning-surface` (#664700) | 4.69:1 | 4.5:1 | body (warning status) | pass |
| `--bs-color-danger` (#ff2146) | `--bs-color-bg` (#011627) | 4.85:1 | 3:1 | large (danger heading) | pass |
| `--bs-color-success` (#5ecb7b) | `--bs-color-bg` (#011627) | 9.00:1 | 4.5:1 | body (success text) | pass |
| `--bs-color-warning` (#f1ad00) | `--bs-color-bg` (#011627) | 9.36:1 | 4.5:1 | body (warning text) | pass |
| `--bs-color-coordinate` (#7a8694) | `--bs-color-bg` (#011627) | 4.95:1 | 4.5:1 | body (grid coordinates) | pass |
| `--bs-color-text-strong` (#e9eef3) | `--bs-color-ship` (#1e539d) | 6.47:1 | 4.5:1 | body (ship label) | pass |

Two rules follow from the table. Dim text uses `--bs-color-text-dim` only on the page background and on surface-1, and `--bs-color-text-dim-raised` on anything lighter. Danger buttons set their label in the heading face at `--bs-text-lg` bold, which is large text, so the 3.24:1 ratio of light text on the danger base is sufficient; their pressed state (danger-700) passes at body size anyway.

---

## 2. Typography

Three typefaces, all from Google Fonts, each with a system fallback that the page must stay readable on.

| Token | Value | Use |
|---|---|---|
| `--bs-font-heading` | `"Quantico", "Segoe UI", system-ui, sans-serif` | All headings, the top bar, button labels, compass letters. [Quantico](https://fonts.google.com/specimen/Quantico) |
| `--bs-font-body` | `"Courier Prime", "Courier New", Courier, monospace` | Default for body copy, inputs, grid coordinates and instrument readouts. Monospaced, so no separate instrument face is needed. [Courier Prime](https://fonts.google.com/specimen/Courier+Prime) |
| `--bs-font-panel` | `"Bitcount Single", "Courier New", Courier, monospace` | Informational panels that set the stage only: the intro banner, status strips, mission text. Never body text or controls. Always weight 200. [Bitcount Single](https://fonts.google.com/specimen/Bitcount+Single) |

Weights: `--bs-font-weight-regular` 400, `--bs-font-weight-bold` 700, `--bs-font-weight-panel` 200. Only weight 200 of Bitcount Single is loaded.

Load line (also embedded in the showcase):

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Quantico:wght@400;700&family=Courier+Prime:wght@400;700&family=Bitcount+Single:wght@200&display=swap" rel="stylesheet">
```

### 2.1 Type scale

A 1.25 ratio from a 16px base. The two largest sizes use `clamp()` so they fit a 360px screen without wrapping headings awkwardly.

| Token | Value | Typical use |
|---|---|---|
| `--bs-text-xs` | `0.64rem` | Coordinates, badges, captions |
| `--bs-text-sm` | `0.8rem` | Secondary text, table cells |
| `--bs-text-md` | `1rem` | Body |
| `--bs-text-lg` | `1.25rem` | Lead paragraphs, button labels in danger buttons, h4 |
| `--bs-text-xl` | `1.5625rem` | h3 |
| `--bs-text-2xl` | `clamp(1.6rem, 1.2rem + 2vw, 1.953rem)` | h2 |
| `--bs-text-3xl` | `clamp(1.9rem, 1.3rem + 3vw, 2.441rem)` | h1 |

### 2.2 Line height and tracking

| Token | Value | Use |
|---|---|---|
| `--bs-leading-tight` | `1.15` | Headings (Quantico) |
| `--bs-leading-panel` | `1.3` | Informational panels (Bitcount Single) |
| `--bs-leading-body` | `1.6` | Body (Courier Prime) |
| `--bs-tracking-heading` | `0.04em` | Headings |
| `--bs-tracking-caps` | `0.12em` | Uppercase labels and badges |

Headings are uppercase with `--bs-tracking-heading`. Body text is never uppercase. Readouts (RPM, bearing, coordinates) use the body face so digits align.

---

## 3. Spacing and radius

Spacing is a 4px base, linear to step 8 and then stepping by 8px so large gaps stay on the grid.

| Token | Value | | Token | Value |
|---|---|---|---|---|
| `--bs-space-1` | `4px` | | `--bs-space-7` | `28px` |
| `--bs-space-2` | `8px` | | `--bs-space-8` | `32px` |
| `--bs-space-3` | `12px` | | `--bs-space-9` | `40px` |
| `--bs-space-4` | `16px` | | `--bs-space-10` | `48px` |
| `--bs-space-5` | `20px` | | `--bs-space-11` | `56px` |
| `--bs-space-6` | `24px` | | `--bs-space-12` | `64px` |

| Token | Value | Use |
|---|---|---|
| `--bs-radius-sm` | `4px` | Inputs, badges, grid cells |
| `--bs-radius-md` | `8px` | Buttons, cards |
| `--bs-radius-lg` | `16px` | Panels, dialogs |
| `--bs-radius-full` | `999px` | Pills, ship ends, markers |

Layout: `--bs-gutter` (`var(--bs-space-4)`, the side gutter on every width), `--bs-container` (`1200px`, maximum content width), `--bs-focus-width` (`2px`) and `--bs-focus-offset` (`2px`) for the focus ring.

---

## 4. Breakpoints

Mobile first: the base styles target 360px and each breakpoint adds, never removes. Custom properties cannot be used inside `@media`, so the tokens below are reference values and the stylesheet repeats them literally.

| Token | Value | What changes at and above it |
|---|---|---|
| `--bs-bp-sm` | `480px` | Button rows stop stacking; swatch grids go from 2 to 3 columns |
| `--bs-bp-md` | `768px` | Two-column layouts (board beside fleet, instruments 2 × 2); the top bar shows all anchors |
| `--bs-bp-lg` | `1024px` | Three and four column swatch grids; instruments in one row |
| `--bs-bp-xl` | `1280px` | Content is centred at `--bs-container` with the page background visible either side |

Nothing may force horizontal page scrolling between 360px and 1920px. Wide content (the grid, tables) shrinks or scrolls inside its own container instead.

---

## 5. Effects

### 5.1 Shadows

Elevation on a navy background needs darker, softer shadows than on white.

| Token | Value | Use |
|---|---|---|
| `--bs-shadow-1` | `0 1px 2px rgb(0 0 0 / 0.5)` | Inputs, flat cards |
| `--bs-shadow-2` | `0 4px 12px rgb(0 0 0 / 0.45), 0 1px 3px rgb(0 0 0 / 0.4)` | Raised cards, panels |
| `--bs-shadow-3` | `0 12px 32px rgb(0 0 0 / 0.55), 0 2px 6px rgb(0 0 0 / 0.4)` | Overlays, dialogs, the top bar |

### 5.2 Glows

Glows are two-layer `box-shadow` values: a tight layer in the 500 step and a soft layer in the 400 step, both made translucent with `color-mix()` so they follow the scale if it changes.

| Token | Value | Use |
|---|---|---|
| `--bs-glow-primary` | `0 0 4px color-mix(in srgb, var(--bs-color-primary-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-primary-400) 35%, transparent)` | Primary buttons on hover and focus, valid placement, the radar sweep |
| `--bs-glow-secondary` | `0 0 4px color-mix(in srgb, var(--bs-color-secondary-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-secondary-400) 35%, transparent)` | Secondary buttons on hover, placed ships |
| `--bs-glow-danger` | `0 0 4px color-mix(in srgb, var(--bs-color-danger-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-danger-400) 35%, transparent)` | Danger buttons on hover, invalid placement, hit markers |
| `--bs-glow-text-primary` | `0 0 6px color-mix(in srgb, var(--bs-color-primary-400) 55%, transparent)` | `text-shadow` on accent headings and readouts |
| `--bs-glow-text-danger` | `0 0 6px color-mix(in srgb, var(--bs-color-danger-400) 55%, transparent)` | `text-shadow` on danger headings |

Glow is a state signal, not decoration: an element glows because it is interactive and hot (hover, focus, pressed), because it is live (radar, sonar), or because it needs attention (hit, invalid). Resting elements do not glow.

---

## 6. Motion

| Token | Value | Use |
|---|---|---|
| `--bs-duration-fast` | `120ms` | Colour and glow changes on hover |
| `--bs-duration-base` | `200ms` | Most transitions |
| `--bs-duration-slow` | `400ms` | Panels appearing, ship placement settling |
| `--bs-duration-sweep` | `4s` | One radar rotation, one gauge oscillation |
| `--bs-duration-ping` | `2.4s` | One sonar ring expanding |
| `--bs-ease-standard` | `cubic-bezier(0.2, 0, 0, 1)` | Entering and changing |
| `--bs-ease-emphasised` | `cubic-bezier(0.2, 0.8, 0.2, 1)` | Attention: a hit landing, a ship snapping into place |
| `--bs-ease-linear` | `linear` | Continuous loops (radar, sonar) |

Rules:

- Only `transform` and `opacity` are animated in loops. Colour and shadow transitions are limited to the short durations.
- Everything animates with CSS alone; the showcase has no script.
- Under `@media (prefers-reduced-motion: reduce)` every loop stops (`animation: none`) and the instruments show a static frame; transitions drop to `--bs-duration-fast`. Nothing informative is conveyed by motion alone.

---

## 7. Components

Class names below are the ones the showcase uses. They are a reference vocabulary, not a requirement: a framework may name things differently as long as it consumes the same tokens.

### 7.1 Buttons

`.bs-btn` with one variant class. Font `--bs-font-heading`, uppercase, `--bs-tracking-heading`, size `--bs-text-md`, padding `--bs-space-3` by `--bs-space-5`, radius `--bs-radius-md`, minimum height 44px for touch. Transitions use `--bs-duration-fast` and `--bs-ease-standard`. Pressing brightens: the active fill is the 300 step, one lighter than hover, so a press reads as the control lighting up rather than dimming.

| Variant | Default | Hover | Active (pressed) | Focus | Disabled |
|---|---|---|---|---|---|
| `.bs-btn--primary` | fill `--bs-color-accent`, label `--bs-color-text-on-accent` | fill `--bs-color-accent-hover`, `--bs-glow-primary` | fill `--bs-color-accent-active`, `--bs-glow-primary` | ring `--bs-color-focus-ring` | fill `--bs-color-surface-overlay`, label `--bs-color-text-dim-raised`, no glow |
| `.bs-btn--secondary` | fill `--bs-color-secondary`, label `--bs-color-text-on-accent` | fill `--bs-color-secondary-hover`, `--bs-glow-secondary` | fill `--bs-color-secondary-active`, `--bs-glow-secondary` | ring `--bs-color-focus-ring` | as primary disabled |
| `.bs-btn--ghost` | transparent, 1px border `--bs-color-border-strong`, label `--bs-color-text` | border and label `--bs-color-accent` | fill `--bs-color-accent-subtle`, label `--bs-color-accent` | ring `--bs-color-focus-ring` | border `--bs-color-border`, label `--bs-color-text-dim-raised` |
| `.bs-btn--danger` | fill `--bs-color-danger`, label `--bs-color-text-on-danger` at `--bs-text-lg` bold (large text) | fill `--bs-color-danger-hover`, `--bs-glow-danger` | fill `--bs-color-danger-active`, `--bs-glow-danger` | ring `--bs-color-focus-ring` | as primary disabled |

Focus is always `outline: var(--bs-focus-width) solid var(--bs-color-focus-ring); outline-offset: var(--bs-focus-offset)` on `:focus-visible`, so it is never conveyed by colour change alone. Disabled buttons use `opacity: 1` with the muted tokens above, not a faded copy of the enabled state, so they stay legible.

### 7.2 Inputs

`.bs-field` wraps a `<label>`, a `.bs-input` and an optional `.bs-field__hint`. Input font `--bs-font-body`, size `--bs-text-md`, padding `--bs-space-3`, radius `--bs-radius-sm`, minimum height 44px.

| State | Tokens |
|---|---|
| Default | fill `--bs-color-surface-raised`, 1px border `--bs-color-border-strong`, text `--bs-color-text`, placeholder `--bs-color-text-dim-raised`, `--bs-shadow-1` |
| Focus | border `--bs-color-accent`, `--bs-glow-primary`, plus the focus ring |
| Error (`aria-invalid="true"` or `.is-error`) | border `--bs-color-danger`, `--bs-glow-danger`; the hint turns `--bs-color-danger` and starts with a warning glyph |
| Disabled | fill `--bs-color-surface`, border `--bs-color-border`, text `--bs-color-text-dim`, no shadow |

### 7.3 Cards and panels

| Element | Tokens |
|---|---|
| `.bs-card` | fill `--bs-color-surface-raised`, radius `--bs-radius-md`, `--bs-shadow-2`, padding `--bs-space-5`; on hover `--bs-color-surface-overlay`; heading in `--bs-font-heading` `--bs-text-lg` |
| `.bs-panel` | fill `--bs-color-surface`, 1px border `--bs-color-border`, radius `--bs-radius-lg`, padding `--bs-space-6`; a 2px top rule in `--bs-color-accent` marks the panel as live |
| `.bs-panel--info` | as `.bs-panel` with the text in `--bs-font-panel` at `--bs-font-weight-panel`, `--bs-leading-panel`, `--bs-text-lg`, colour `--bs-color-accent` with `--bs-glow-text-primary`. This is the only place Bitcount Single appears. |

### 7.4 Status messages

`.bs-status` with one variant. Font `--bs-font-body`, radius `--bs-radius-sm`, padding `--bs-space-3` by `--bs-space-4`, a 3px left rule and a leading glyph so the kind is readable without colour.

| Variant | Fill | Rule, glyph | Text | Glyph |
|---|---|---|---|---|
| `.bs-status--info` | `--bs-color-accent-subtle` | `--bs-color-accent` | `--bs-color-text` | `i` in a circle |
| `.bs-status--success` | `--bs-color-success-surface` | `--bs-color-success` | `--bs-color-text` | check mark |
| `.bs-status--warning` | `--bs-color-warning-surface` | `--bs-color-warning` | `--bs-color-text` | exclamation in a triangle |
| `.bs-status--danger` | `--bs-color-danger-surface` | `--bs-color-danger` | `--bs-color-text` | cross in a circle |

---

## 8. Game elements

### 8.1 The board

`.bs-board` is a CSS grid of 13 columns and 13 rows: one header row of column letters A to L, one header column of row numbers 1 to 12, and 144 playable cells. Each cell is square (`aspect-ratio: 1`). The board is `width: 100%` with `max-width: 520px`, so at 360px the cells are about 25px and at the `md` breakpoint and above about 38px.

| Part | Tokens |
|---|---|
| Board background | `--bs-color-ocean`, radius `--bs-radius-md`, 1px border `--bs-color-border` |
| Cell lines | 1px gap filled by `--bs-color-grid-line` |
| Coordinates `.bs-board__coord` | `--bs-font-body` `--bs-text-xs`, colour `--bs-color-coordinate`, uppercase |

Touch targets on the real game screens must reach 44px; the game screen does this by letting the board scroll or zoom, not by changing these tokens. The showcase only demonstrates visual states.

### 8.2 Ships

Five ships, each one cell wide. `.bs-ship` spans its length with `grid-column: span N` (horizontal) or `grid-row: span N` with `.bs-ship--v` (vertical). Hull: gradient from `--bs-color-secondary-600` to `--bs-color-secondary-800`, 1px outline `--bs-color-ship-edge`, radius `--bs-radius-full` on the bow and stern ends so direction is readable, and a bridge mark (a lighter cell) so the fleet reads as ships rather than bars.

| Ship | Length |
|---|---|
| Carrier | 5 |
| Battleship | 4 |
| Destroyer | 3 |
| Submarine | 3 |
| Patrol Boat | 2 |

Placing-stage states:

| State | Tokens |
|---|---|
| `.is-valid` (legal placement being dragged) | outline `--bs-color-placement-valid`, `--bs-glow-primary` |
| `.is-invalid` (overlap or off board) | outline `--bs-color-placement-invalid`, `--bs-glow-danger`, hatch pattern over the hull |
| `.is-placed` (committed) | fill `--bs-color-ship`, outline `--bs-color-ship-edge`, `--bs-glow-secondary` for `--bs-duration-slow` then no glow |

### 8.3 Cell states (battle stage)

Each state has a colour and a shape, so colour alone never carries meaning.

| State | Colour | Non-colour cue |
|---|---|---|
| `.is-unknown` (default) | `--bs-color-ocean` | plain cell |
| `.is-miss` | ring in `--bs-color-miss` | a small hollow circle centred in the cell |
| `.is-hit` | fill `--bs-color-hit`, `--bs-glow-danger` | a cross glyph in `--bs-color-text-strong` |
| `.is-sunk` | fill `--bs-color-sunk` | the cross glyph plus a diagonal hatch pattern |

A hit lands with `--bs-duration-base` and `--bs-ease-emphasised` (scale from 1.3 to 1); under reduced motion it appears instantly.

---

## 9. Instruments

Four motifs give the interface its bridge feel. Each is an inline SVG inside a `.bs-instrument` panel (fill `--bs-color-surface`, radius `--bs-radius-lg`, border `--bs-color-border`) with a readout line in `--bs-font-body`. They use role tokens only, animate with CSS only, and stop under reduced motion.

| Motif | Class | Drawing | Motion |
|---|---|---|---|
| Radar | `.bs-radar` | Range rings and crosshair in `--bs-color-grid-line`, a 60° sweep wedge in `--bs-color-accent` fading to transparent with `--bs-glow-primary`, two blips in `--bs-color-accent` | The sweep rotates once per `--bs-duration-sweep` with `--bs-ease-linear`; blips pulse in opacity |
| Sonar | `.bs-sonar` | Three concentric rings in `--bs-color-accent`, a centre dot | Rings scale from 0 to full and fade over `--bs-duration-ping`, staggered by a third each |
| Compass | `.bs-compass` | Rose with 16 ticks in `--bs-color-grid-line`, cardinal letters in `--bs-font-heading` and `--bs-color-text-strong`, a needle with the north half in `--bs-color-danger` and the south half in `--bs-color-text-dim` | Static (a compass only moves when the ship turns) |
| Engine RPM | `.bs-gauge` | A 240° arc in `--bs-color-grid-line`, ticks every 10°, the top 20% of the arc in `--bs-color-danger`, a needle in `--bs-color-accent`, a readout below | The needle oscillates between 40% and 70% over `--bs-duration-sweep` with `--bs-ease-standard` |

Instruments are decoration with a purpose: they frame loading, waiting for the opponent, and the transition from placing to battle. They are never the only indicator of state.

---

## 10. Token reference

The authoritative block. The showcase embeds exactly this; the command in section 11 checks that the two agree.

```css
:root {
  /* ---- Base colours (verbatim from the brief, never edited) ---- */
  --bs-color-bg-base: #011627;
  --bs-color-primary-base: #00c0ff;
  --bs-color-secondary-base: #2f87ff;
  --bs-color-text-base: #b2c2d4;
  --bs-color-text-dim-base: #7a8694;
  --bs-color-danger-base: #ff2146;

  /* ---- Primary scale (base at 500) ---- */
  --bs-color-primary-50: #eef9ff;
  --bs-color-primary-100: #d9f2ff;
  --bs-color-primary-200: #b9e7ff;
  --bs-color-primary-300: #92daff;
  --bs-color-primary-400: #63cdff;
  --bs-color-primary-500: var(--bs-color-primary-base);
  --bs-color-primary-600: #0097c9;
  --bs-color-primary-700: #00739a;
  --bs-color-primary-800: #00516e;
  --bs-color-primary-900: #003449;

  /* ---- Secondary scale (base at 500) ---- */
  --bs-color-secondary-50: #ecf4ff;
  --bs-color-secondary-100: #d5e6ff;
  --bs-color-secondary-200: #b2d1ff;
  --bs-color-secondary-300: #8abaff;
  --bs-color-secondary-400: #60a1ff;
  --bs-color-secondary-500: var(--bs-color-secondary-base);
  --bs-color-secondary-600: #266bcb;
  --bs-color-secondary-700: #1e539d;
  --bs-color-secondary-800: #163c72;
  --bs-color-secondary-900: #0e294e;

  /* ---- Danger scale (base at 500) ---- */
  --bs-color-danger-50: #ffefee;
  --bs-color-danger-100: #ffdbd9;
  --bs-color-danger-200: #ffbcba;
  --bs-color-danger-300: #ff9796;
  --bs-color-danger-400: #ff6b70;
  --bs-color-danger-500: var(--bs-color-danger-base);
  --bs-color-danger-600: #ca2038;
  --bs-color-danger-700: #9c1d2c;
  --bs-color-danger-800: #701820;
  --bs-color-danger-900: #4b1316;

  /* ---- Text scale (base at 300) ---- */
  --bs-color-text-50: #e9eef3;
  --bs-color-text-100: #d6dfe9;
  --bs-color-text-200: #c4d0de;
  --bs-color-text-300: var(--bs-color-text-base);
  --bs-color-text-400: #94a5b8;
  --bs-color-text-500: #78899c;
  --bs-color-text-600: #5c6e81;
  --bs-color-text-700: #425468;
  --bs-color-text-800: #293b4f;
  --bs-color-text-900: #15273a;

  /* ---- Surface scale (background at 0, each step lighter) ---- */
  --bs-color-surface-0: var(--bs-color-bg-base);
  --bs-color-surface-1: #041e32;
  --bs-color-surface-2: #09273c;
  --bs-color-surface-3: #103048;
  --bs-color-surface-4: #163953;

  /* ---- Derived status hues (rotated from primary and danger) ---- */
  --bs-color-success-500: #5ecb7b;
  --bs-color-success-800: #215630;
  --bs-color-warning-500: #f1ad00;
  --bs-color-warning-800: #664700;

  /* ---- Semantic roles: always a var(), never a literal ---- */
  --bs-color-bg: var(--bs-color-surface-0);
  --bs-color-surface: var(--bs-color-surface-1);
  --bs-color-surface-raised: var(--bs-color-surface-2);
  --bs-color-surface-overlay: var(--bs-color-surface-3);
  --bs-color-border: var(--bs-color-surface-4);
  --bs-color-border-strong: var(--bs-color-text-700);
  --bs-color-text: var(--bs-color-text-300);
  --bs-color-text-strong: var(--bs-color-text-50);
  --bs-color-text-dim: var(--bs-color-text-dim-base);
  --bs-color-text-dim-raised: var(--bs-color-text-400);
  --bs-color-text-on-accent: var(--bs-color-bg-base);
  --bs-color-text-on-danger: var(--bs-color-text-50);
  --bs-color-accent: var(--bs-color-primary-500);
  --bs-color-accent-hover: var(--bs-color-primary-400);
  --bs-color-accent-active: var(--bs-color-primary-300);
  --bs-color-accent-subtle: var(--bs-color-primary-900);
  --bs-color-secondary: var(--bs-color-secondary-500);
  --bs-color-secondary-hover: var(--bs-color-secondary-400);
  --bs-color-secondary-active: var(--bs-color-secondary-300);
  --bs-color-secondary-subtle: var(--bs-color-secondary-900);
  --bs-color-focus-ring: var(--bs-color-primary-300);
  --bs-color-danger: var(--bs-color-danger-500);
  --bs-color-danger-hover: var(--bs-color-danger-400);
  --bs-color-danger-active: var(--bs-color-danger-700);
  --bs-color-danger-surface: var(--bs-color-danger-900);
  --bs-color-success: var(--bs-color-success-500);
  --bs-color-success-surface: var(--bs-color-success-800);
  --bs-color-warning: var(--bs-color-warning-500);
  --bs-color-warning-surface: var(--bs-color-warning-800);

  /* ---- Game roles ---- */
  --bs-color-ocean: var(--bs-color-surface-1);
  --bs-color-grid-line: var(--bs-color-primary-900);
  --bs-color-coordinate: var(--bs-color-text-dim-base);
  --bs-color-ship: var(--bs-color-secondary-700);
  --bs-color-ship-edge: var(--bs-color-secondary-500);
  --bs-color-placement-valid: var(--bs-color-primary-500);
  --bs-color-placement-invalid: var(--bs-color-danger-500);
  --bs-color-miss: var(--bs-color-text-dim-base);
  --bs-color-hit: var(--bs-color-danger-500);
  --bs-color-sunk: var(--bs-color-danger-700);

  /* ---- Typography ---- */
  --bs-font-heading: "Quantico", "Segoe UI", system-ui, sans-serif;
  --bs-font-body: "Courier Prime", "Courier New", Courier, monospace;
  --bs-font-panel: "Bitcount Single", "Courier New", Courier, monospace;
  --bs-font-weight-regular: 400;
  --bs-font-weight-bold: 700;
  --bs-font-weight-panel: 200;
  --bs-text-xs: 0.64rem;
  --bs-text-sm: 0.8rem;
  --bs-text-md: 1rem;
  --bs-text-lg: 1.25rem;
  --bs-text-xl: 1.5625rem;
  --bs-text-2xl: clamp(1.6rem, 1.2rem + 2vw, 1.953rem);
  --bs-text-3xl: clamp(1.9rem, 1.3rem + 3vw, 2.441rem);
  --bs-leading-tight: 1.15;
  --bs-leading-panel: 1.3;
  --bs-leading-body: 1.6;
  --bs-tracking-heading: 0.04em;
  --bs-tracking-caps: 0.12em;

  /* ---- Spacing (4px base) ---- */
  --bs-space-1: 4px;
  --bs-space-2: 8px;
  --bs-space-3: 12px;
  --bs-space-4: 16px;
  --bs-space-5: 20px;
  --bs-space-6: 24px;
  --bs-space-7: 28px;
  --bs-space-8: 32px;
  --bs-space-9: 40px;
  --bs-space-10: 48px;
  --bs-space-11: 56px;
  --bs-space-12: 64px;

  /* ---- Radius ---- */
  --bs-radius-sm: 4px;
  --bs-radius-md: 8px;
  --bs-radius-lg: 16px;
  --bs-radius-full: 999px;

  /* ---- Elevation shadows ---- */
  --bs-shadow-1: 0 1px 2px rgb(0 0 0 / 0.5);
  --bs-shadow-2: 0 4px 12px rgb(0 0 0 / 0.45), 0 1px 3px rgb(0 0 0 / 0.4);
  --bs-shadow-3: 0 12px 32px rgb(0 0 0 / 0.55), 0 2px 6px rgb(0 0 0 / 0.4);

  /* ---- Glows (tight inner layer at 500, soft outer layer at 400) ---- */
  --bs-glow-primary: 0 0 4px color-mix(in srgb, var(--bs-color-primary-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-primary-400) 35%, transparent);
  --bs-glow-secondary: 0 0 4px color-mix(in srgb, var(--bs-color-secondary-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-secondary-400) 35%, transparent);
  --bs-glow-danger: 0 0 4px color-mix(in srgb, var(--bs-color-danger-500) 60%, transparent), 0 0 18px color-mix(in srgb, var(--bs-color-danger-400) 35%, transparent);
  --bs-glow-text-primary: 0 0 6px color-mix(in srgb, var(--bs-color-primary-400) 55%, transparent);
  --bs-glow-text-danger: 0 0 6px color-mix(in srgb, var(--bs-color-danger-400) 55%, transparent);

  /* ---- Motion ---- */
  --bs-duration-fast: 120ms;
  --bs-duration-base: 200ms;
  --bs-duration-slow: 400ms;
  --bs-duration-sweep: 4s;
  --bs-duration-ping: 2.4s;
  --bs-ease-standard: cubic-bezier(0.2, 0, 0, 1);
  --bs-ease-emphasised: cubic-bezier(0.2, 0.8, 0.2, 1);
  --bs-ease-linear: linear;

  /* ---- Breakpoints (reference values: media queries repeat them literally) ---- */
  --bs-bp-sm: 480px;
  --bs-bp-md: 768px;
  --bs-bp-lg: 1024px;
  --bs-bp-xl: 1280px;

  /* ---- Layout ---- */
  --bs-gutter: var(--bs-space-4);
  --bs-container: 1200px;
  --bs-focus-width: 2px;
  --bs-focus-offset: 2px;
}
```
