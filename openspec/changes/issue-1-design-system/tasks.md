# Tasks

## 1. Tokens

- [ ] 1.1 Generate the colour scales (primary, secondary, danger, text at steps 50 to 900; surface-0 to surface-4) from the six base colours in OKLCH with a throwaway script in the scratchpad, and verify each scale is monotonic in lightness and contains its base colour exactly
- [ ] 1.2 Write the full token block (colour bases, scales, semantic roles, fonts, type scale, leading, spacing, radius, shadows, glows, durations, easings, breakpoints) as `--bs-*` custom properties on `:root`, and verify every semantic role uses `var()` and no literal hex
- [ ] 1.3 Compute WCAG contrast ratios for text and text-dim on bg, surface-1 to surface-4, and for text-on-accent and text-on-danger with a throwaway Node script, and verify every pair meets 4.5:1 (body) or 3:1 (large) before adjusting any step

## 2. Reference document

- [ ] 2.1 Create `docs/design-system.md` with the Introduction, Colour (bases, scales with hex values and derivation formula, semantic roles, contrast table from 1.3), Typography, Spacing and radius, Breakpoints, Effects and Motion sections, each listing token name and value, and verify every token from 1.2 appears in it
- [ ] 2.2 Add the Components section (buttons, inputs, cards and panels, status messages, each with all states and the effect token each state uses), the Game elements section (grid, ships, placing states, cell states with their non-colour cues) and the Instruments section (radar, sonar, compass, RPM gauge), and verify each element lists the tokens it consumes
- [ ] 2.3 Add the "Maintaining this document" section with the scale-generation formula, the contrast command and the token-diff command from design.md, and verify both commands run as written from the repository root

## 3. Showcase page

- [ ] 3.1 Create `docs/design-system.html` with the embedded token block from 1.2, the Google Fonts link for Quantico, Courier Prime and Bitcount Single (weight 200 only), base styles, the sticky anchor bar, and the Colour and Typography sections, and verify it opens from disk with no console errors, renders all scales as swatches with their names, and shows headings in Quantico, body in Courier Prime and the intro panel in Bitcount Single 200
- [ ] 3.2 Add the Spacing and radius, Effects and Motion sections and the Components section with every element in every documented state including a visible 2px offset focus ring, and verify each state renders by tabbing through the page and hovering each control
- [ ] 3.3 Add the Game elements section: the 12x12 grid with A to L and 1 to 12 coordinates, the five ships horizontal and vertical at their lengths, placing states and the four cell states, and verify the grid shows 144 playable cells and each ship spans its documented cell count
- [ ] 3.4 Add the Instruments section with the radar sweep, sonar ping, compass and RPM gauge as inline SVG and CSS animations using only role tokens, and verify they animate with JavaScript disabled and freeze under `prefers-reduced-motion: reduce` (emulate it in browser devtools)
- [ ] 3.5 Make the page mobile first: base layout for 360px and enhancements at the sm, md, lg and xl breakpoints, and verify in devtools at 360, 480, 768, 1024, 1280 and 1920px that `document.documentElement.scrollWidth` equals the viewport width

## 4. Agreement and final checks

- [ ] 4.1 Run the token-diff command from 2.3 and verify it reports no difference between the markdown and the HTML
- [ ] 4.2 Re-run the contrast script against the final token values and verify the table in `docs/design-system.md` matches the output
- [ ] 4.3 Open the page with web fonts blocked and verify every section stays readable on the fallback stacks, then record the fallback check in the Maintaining section
