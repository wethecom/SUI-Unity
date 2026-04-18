# SUI Long-Term Goals

This document defines the long-term product goals and the development path for SUI as a Unity-first, Razor-driven UI runtime.

## Vision

Build a production-ready UI runtime in Unity that uses Razor-style markup and CSS-like styling while replacing traditional Unity UI workflows for game HUDs, menus, and in-world screens.

## Architecture Decision (Locked)

Game-focused hybrid model:
- Rendering/layout/input stays in SUI runtime (compiled Unity C#).
- Gameplay logic stays in regular Unity `MonoBehaviour`/systems (compiled C#).
- UI event bridge calls C# methods on Unity scripts directly.
- Roslyn is optional and used only for UI glue/code-behind authoring, not core gameplay loops.

Why this is the default:
- Best runtime predictability for games.
- Native access to Unity systems.
- Fast authoring without forcing full dynamic scripting.

## Product Outcomes (Why This Wins)

1. Clarity: players understand UI state/actions in under 2 seconds.
2. Responsiveness: visible UI reaction in 1 frame (or <= 50ms perceived).
3. Consistency: one design language across all screens and states.
4. Reliability: no major breakage across target resolutions.
5. Performance: UI stays inside frame budget in gameplay scenes.
6. Authoring speed: new screen in < 1 hour, common edits in minutes.

## Current Baseline (Already Implemented)

- Razor source ingestion and parsing pipeline.
- Runtime host with bindings/actions.
- Layout with flex-direction, gap, justify-content, align-items, min/max sizing.
- Style selectors: tag, class, id, inline precedence.
- Pseudo selectors: hover, active, focus, disabled, invalid.
- Controls: input text, checkbox, radio, range, textarea, select/listbox+option, button, image, progress, meter.
- Lists: ul/ol/li.
- Shadow effects: box-shadow and text-shadow (multi-layer).
- Custom inspector for value bindings (table + bulk mode).
- Typed value bindings (`string/bool/int/float`) with validation state support.
- Authoring automation:
  - auto-sync actions from Razor event attributes,
  - auto-assign stylesheet from Razor filename,
  - auto-create value bindings from `@tokens` and control bindings.
- Unity menu item to create SUI Runtime Host quickly.
- Modal foundations (open/close API + backdrop + trap).
- Scroll containers + clipping.
- Positioning and z-order (`relative/absolute`, `z-index`).
- Drag/drop event system.
- MonoBehaviour event bridge (markup actions -> public `void` script methods).
- Demo pack covering current capabilities.

## Build Goals (Make / Done When)

## 1) Core Control Kit v1

Make:
- `button`, `input`, `textarea`, `checkbox`, `radio`, `select/listbox`, `slider`, `progress`, `meter`, `image`, `list`.

Done when:
- Every control has `default/hover/active/focus/disabled` styles and sample usage.

## 2) Layout Engine v2

Make:
- `flex-grow`, `flex-shrink`, `wrap`, absolute/relative positioning, scroll container.

Done when:
- One complex menu screen works at 16:9, 21:9, and 4:3 without breaking.

## 3) Visual Styling v2

Make:
- border, border-radius, gradients, shadows, theme tokens.

Done when:
- We can ship a polished main menu with no Unity UI components.

## 4) State + Binding System

Make:
- two-way value binding, radio group model, select model, validation/error state.

Done when:
- A full settings form saves/loads and updates live.

## 5) Performance Pass

Make:
- subtree diff updates + partial layout invalidation.

Done when:
- UI updates do not full-redraw and stay within frame budget in gameplay scene.

## 6) Tooling + Workflow

Make:
- visual debug inspector, screen templates, hot-reload loop, lint checks.

Done when:
- New screen creation is under 1 hour and iteration is under 5 minutes.

## 8) Authoring Automation v1

Make:
- auto action sync from Razor events,
- auto stylesheet pairing from Razor source,
- auto value generation from `@tokens` and controls,
- one-click demo discovery/coverage guide.

Done when:
- Initial scene setup requires minimal manual list editing in inspector.

## 7) Modal Windows + Drag/Drop + Scripted Animation

Make:
- modal windows with backdrop and focus/input trap,
- draggable windows (header handle, z-order bring-to-front),
- drop zones with enter/leave/over/drop events,
- scriptable animation hooks (open/close/move) and style transitions.

Done when:
- We can build an inventory/settings modal flow with dragable windows and animated open/close entirely in SUI.

## Execution Path (Now / Next / Later)

## Now (Current Execution)

- [x] Finish Control Kit v1 state completeness (`disabled` styling/behavior path).
- [x] State + Binding v1: typed values (`string/bool/int/float`) and form validation states.
- [x] Authoring Automation v1 (auto action/style/value sync + demo pack).
- [x] Tooling v1: visual debug overlay (layout bounds, z-order, focus, scroll, drag targets).
- [x] State + Binding v1.1: validation messages + touched/dirty state + submit gating.

## Next

- [x] Performance pass phase 1: subtree diff + partial layout invalidation.
- [x] Modal/Window v1.1: bring-to-front on focus, bounds clamp, close-button pattern.
- [x] Roslyn integration adapter (optional mode) for UI code-behind only.
- [x] Animation hooks phase 1 (`onopen/onclose/onmove`) and transition primitives.

## Later

- [x] Live hot-reload and lint checks.
- [x] s&box compatibility aliases and migration cookbook.
- [x] Screen templates from Unity asset menu + host/inspector quick-create flow (basic/modal/hud/settings/inventory starters).

## Working Cadence

- Use 2-week iterations.
- Each iteration includes:
  - 1 feature deliverable
  - 1 stabilization task
  - 1 tooling/test improvement
- End each iteration with a demo scene proving "Done when" criteria progress.

## Definition of Done (Per Feature)

- Feature documented in README support table.
- Sample updated to demonstrate feature.
- Edge-case behavior tested.
- No regression in existing sample controls.
- Build passes without new warnings/errors introduced by the feature.
