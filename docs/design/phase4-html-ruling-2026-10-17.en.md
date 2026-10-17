[English](phase4-html-ruling-2026-10-17.en.md) | [中文](phase4-html-ruling-2026-10-17.md)

# Phase 4 HTML Small Screen · Boundary Ruling and Architecture Decision (2026-10-17)

## 1. Ruling record

User ruling, 2026-10-17: "Phase 4 HTML small screen (6 rows; an architecture decision must precede work; a counter-record exists in the repo) ... proceed with this design".

This document is the pre-work architecture decision deliverable. It answers the open question in `redesign-2026-10-16-契约.md` §4 (the HTML small screen vs. the `master-plan.md:769`
"no WebView" boundary; mapping-and-migration §6.3). The open question is closed as of this file.

## 2. Reconciliation with the counter-record

In-repo counter-record (`docs/master-plan.md:769`): "floating windows should use native Windows capabilities as far as possible, without
introducing WebView or any other UI framework to re-implement the desktop shell".

The design doc itself (`redesign-plan-2026-10-16.md`, last line of §7): "Phase 1 may use controlled HTML/CSS templates;
evaluate a restricted runtime only when JavaScript becomes necessary. React is not a required dependency."

The two do not conflict: the design's phase 1 does not require JavaScript, and Beacon's rendering segment is already a declarative
component tree (Phase 3 `ComponentDescriptor` / `ComponentSurfaceRenderer`). Phase 1 is therefore ruled as —

> **Controlled HTML/CSS subset + native rendering. No JavaScript, no WebView2, no React.**
> The HTML template is parsed inside Core into a `ComponentDescriptor` subtree and handed to the existing declarative renderer,
> which draws it with native WinUI controls. HTML is just "another language for writing component trees", not another runtime.

Faces that genuinely need JavaScript (arbitrary interaction logic, external scripts) are outside this ruling; a fresh ruling is required then.

## 3. Carrier and expansion point

- Carrier: the root node of any face key (tile / row / detail; the open face-key stance is unchanged) is declared as
  `{ "type": "htmltemplate", "props": { "html": "<template text>" } }`. Zero new model fields, zero new JSON
  top-level keys, zero new face keys — HTML is a node type, not a new storage shape.
- Expansion point: inside the pure function `WidgetInstanceResolver.Resolve`, `HtmlTemplateParser` parses the template text into
  a component subtree (a wrapper column), which then passes the existing `ComponentTreeValidator` (8 depth / 32 children / 256 nodes —
  the blast-radius caps bind the synthesized tree too). After expansion all consumers of tile / row / detail gain HTML faces with zero changes.
- The parser is a static pure function (upholding decision ⑩: no IO, no runtime services, no caching); parse failure returns null,
  and the caller falls back to traditional rendering — same stance as the existing "invalid tree, whole tree dropped".

## 4. Template format (closed vocabulary)

Grammar skeleton: optional `<style>` blocks (must precede all other elements) plus an element tree. Double-quoted attributes;
`<!-- comments -->` may be skipped over; `&` entities and a bare `&` are parse errors (a status-text vocabulary has no use for them; stay closed).

Element whitelist (mapped onto existing renderer registry type keys):

| Element | Mapped type key | Own attributes | Binding slot |
|---|---|---|---|
| card | card | — | — |
| column / row | column / row | gap | — |
| text | text | size(body/caption) | text |
| section | sectionlabel | — | text |
| metric | metric | unit | value |
| progress | progress | width | progress |
| dot | statusdot | size | severity |
| badge | badge | — | text |
| kv | keyvalue | key | value |
| divider | divider (self-closing) | — | — |
| button | actionbutton | data-action (required) | text (button label) |

Attributes common to all elements: `class`, `when` (conditional display, passed through to ComponentVisibility). Unknown element, unknown attribute,
an own attribute on the wrong element → parse error. The vocabulary is the whitelist: `<script>`, `<iframe>`, `<img>`, `<a href>`, `on*`
event attributes and external protocols are **inexpressible in the grammar** — not "filtering dangerous input", they simply do not exist in the vocabulary.

## 5. Controlled data bridge

The `{{...}}` interpolation desugars into existing `ComponentBinding` (zero binding-engine changes):

- `{{state.summary}}` → state source; the field name must be in the engine's closed field set (summary / severity /
  progress / detailUrl / lifecycle / connectionId / widgetId / widgetType);
- `{{payload.run_id}}` → state source in `payload:run_id` form (non-empty key, no second colon);
- `{{selector.value-hint}}` → selector source; the name must be in the selector registry (value-hint / quota-windows);
- mixed interpolation in text content → split into a literal / bound child-node sequence; an attribute value that is exactly one interpolation → that attribute slot gets a binding,
  an attribute value mixing literal text with interpolation → parse error (error out, do not guess); `when` / `gap` / `class` / `size` / `unit` /
  `key` / `width` / `data-action` accept no interpolation (attributes are the configuration face; the data face is binding slots only).

Unresolvable values become empty strings (the binding engine's existing fail-closed stance) — the process never crashes, no placeholder garbage is left.

## 6. Action ID bridge

`<button data-action="rerun-failed">Retry failed</button>` → an `actionbutton` node; the action ID accepts a
**static literal** only (no interpolation, no whitespace) — design §7.3 "never trust data passed in by the page": remote state data must not
pick actions; the action face is declarable only by local configuration.

The execution side lives in the renderer (App; per decision ⑤ the type vocabulary belongs to the renderer): the ID is looked up in the actions.json + connection DSL
action universe, filtered by `ActionVisibility` (widgetType / connection_id constraints apply as usual); nonexistent or invisible →
a diagnostic placeholder is rendered (no clickable button); present and visible → executed through the existing `ActionRunner.ExecuteAsync` path,
`RequireConfirmation` honored as usual. A template declaring a nonexistent action ID does not crash the process — the face renders as usual, the button slot shows
diagnostic text.

## 7. Theme tokens and basic style adaptation

A `<style>` block accepts only the `.class { prop: value; }` form (one class per rule, no combinators, no pseudo-classes):

- prop whitelist: `tone` (info / ok / warn / bad), `pad` (sm / md / lg), `gap` (sm / md / lg);
- values accept token words only; `#hex`, `rgb()`, named CSS colors — literal colors are parse errors — **a template cannot
  hardcode colors in the grammar**, and light / dark / high-contrast adapt automatically (rendering goes through ThemeColors / UiPalette severity colors);
- the DIP conversion of sm / md / lg happens renderer-side (sm=4 / md=8 / lg=12, aligned with DesignTokens Space4/8/12);
  Core stores only token words — type conversion stays at render time (the established all-string Props stance);
- duplicate class definition, unknown prop, unknown token word → parse error.

## 8. Incremental update semantics

- Template parses once per configuration revision: the instance is immutable and resolved with the configuration (the established "re-layout only re-resolves" invariant),
  and the template text re-parses only when the configuration changes;
- data arrival = existing binding re-evaluation + face re-render (the Phase 3 whole-tree re-render stance, trees ≤256 nodes);
  **"regenerating the whole HTML on every data change" is not the mechanism** (design §7.2) — the HTML text never participates in the
  data update path at all; updates go through structured bindings (the engine evaluates the same synthesized tree against the new state snapshot);
- Core-testable fact: the same instance with two state snapshots → different binding values, unchanged synthesized tree object.

## 9. Instance lifecycle and error fallback

- Instance lifecycle = the existing WidgetInstance lifecycle: resolved on configuration load / change, shared read-only, no separate
  runtime state, no timers, no background tasks (the HTML face introduces no new lifecycle shape);
- error fallback: parse failure / out-of-vocabulary → the face is dropped, traditional rendering resumes, with diagnostics (load side into LoadErrors,
  resolve side fail-closed null) — a broken template must not crash the resident process, nor be silently trimmed into half a tree;
- the storage layer's `SanitizeWidgetComponents` expands and validates at load: a bad template is reported and dropped at load time,
  not discovered at first render.

## 10. Explicitly out of scope

- JavaScript / restricted script runtimes / WebView2 / React — a fresh user ruling is required when needed;
- arbitrary CSS styling (layout models, fonts, motion, images) — extending the vocabulary whitelist is later evolution, reviewed item by item;
- styles shared across templates, external template files, template hot-reload — templates travel only with widgets.json;
- cross-widget data reads (additional-source declarations follow the existing Phase 3 stance; whitelist validation is unchanged).
