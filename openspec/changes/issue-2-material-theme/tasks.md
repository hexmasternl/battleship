# Tasks

## 1. Design system tokens in the frontend

- [x] 1.1 Create `src/Frontend/src/styles/_tokens.scss` with the `--bs-*` token block copied verbatim from `docs/design-system.md` onto `:root`, `@use` it from `src/styles.scss`, and verify a throwaway Node script that extracts name/value pairs from both files reports no difference
- [x] 1.2 Record the token-diff command in a "Theming" section of `src/Frontend/README.md` and verify it runs as written from the repository root

## 2. Material palettes and theme

- [x] 2.1 Create `src/Frontend/src/styles/_palettes.scss` with the primary (plus nested secondary, neutral, neutral-variant, error) and tertiary palette maps built per the tone-to-token table in design.md, each tone commented with its design system token or mix, and verify `ng build --configuration development` compiles the Sass without error
- [x] 2.2 Replace the `mat.theme()` call in `src/styles.scss` with `theme-type: dark`, the custom primary and tertiary palettes, `brand-family` Quantico and `plain-family` Courier Prime with the design system fallback stacks, `regular-weight 400`, `medium-weight 700`, `bold-weight 700`, `density 0`; set `color-scheme: dark` on `body`; and verify in the built CSS that `--mat-sys-primary` is `#00c0ff`, `--mat-sys-tertiary` is `#2f87ff`, `--mat-sys-error` is `#ff2146`, `--mat-sys-surface` is `#011627`, `--mat-sys-on-surface` is `#b2c2d4`, `--mat-sys-on-surface-variant` is `#7a8694`, `--mat-sys-headline-large-font` starts with Quantico and `--mat-sys-body-medium-font` starts with Courier Prime
- [ ] 2.3 Add `mat.theme-overrides()` after `mat.theme()` pinning background, surface, surface-dim, the five surface-container roles, surface-bright, on-surface, on-surface-variant, outline and outline-variant to the matching `--bs-color-*` semantic role tokens, and verify the computed values in a browser equal the hex the palettes produce (no visual change) and that the surface-container roles are ordered darkest to lightest
- [ ] 2.4 Compute WCAG contrast with a throwaway Node script for on-surface on surface, on-surface-variant on surface, on-surface on surface-container-highest and on-primary on primary from the built CSS, verify each meets 4.5:1 (or 3:1 where only used for large text), and record the command and results in the README theming section

## 3. Web fonts

- [ ] 3.1 Replace the Roboto link in `src/Frontend/src/index.html` with the Google Fonts request for Quantico 400/700, Courier Prime 400/700 and 400 italic, and Bitcount Single weight 200 with `display=swap`, keep the preconnect and Material Symbols links, and verify in the browser network panel that Quantico, Courier Prime and Bitcount Single are requested and Roboto is not
- [ ] 3.2 Block web fonts in devtools and verify headings fall back to the Quantico stack and body to the Courier Prime stack with all text readable

## 4. Visual verification and integration

- [ ] 4.1 Add a temporary theme preview to the root component with a filled button, an outlined form field in valid and invalid states, a card and a dialog trigger, run `ng serve`, and verify the button is `#00c0ff`, the focused field outline is `#00c0ff`, the invalid field is `#ff2146`, the card and dialog surfaces are surface scale steps, hover and focus states are visible, and headings use Quantico while body text uses Courier Prime
- [ ] 4.2 Emulate a light colour scheme in devtools and verify the application stays dark with the `#011627` background and dark native controls
- [ ] 4.3 Remove the temporary preview (unless the user asks to keep it as a dev route), run `ng build` in the production configuration and `ng test`, and verify both succeed with no budget error
