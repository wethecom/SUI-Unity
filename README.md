# SUI (Sandbox-Style UI Runtime for Unity)

SUI is a Unity-first, Razor-driven UI runtime designed to **replace traditional Unity UI workflows** for game menus, HUDs, overlays, and in-world screens.

It provides a familiar markup + stylesheet workflow while keeping gameplay logic in normal Unity C# scripts.

## Why SUI

- Razor-style markup authoring in Unity
- CSS-like styling with pseudo states
- Runtime controls and input handling
- Modal windows, drag/drop, scroll containers
- MonoBehaviour action bridge (UI events -> script methods)
- Resolution-aware panel anchoring
- Built for game UI iteration speed

---

## Current Features

### Markup + Controls
- Tags: `div`, `p`, `span`, `label`, `h1-h6`, `button`, `ul`, `ol`, `li`, `textarea`, `select`, `option`, `listbox`, `progress`, `meter`, `img`, `window`, `window-header`, `window-body`, `modal`
- Controls:
  - `input` (text)
  - `input type="checkbox"`
  - `input type="radio"`
  - `input type="range"`
  - `textarea`
  - `select/listbox` + `option`
  - `progress`, `meter`
  - `img`

### Styling
- Selectors: `tag`, `.class`, `#id`
- Merge order: `tag -> class -> id -> inline`
- Pseudo states:
  - `:hover`
  - `:active`
  - `:focus`
  - `:disabled`
  - `:invalid`
- Visual effects:
  - border + radius
  - box-shadow
  - text-shadow

### Layout
- Flex direction
- Gap
- Justify-content / align-items
- Min/max sizing
- Flex grow/shrink + wrap
- Relative/absolute positioning
- Z-index
- Scroll containers (`overflow-y`)

### Runtime UX
- Keyboard focus loop (`Tab`, `Shift+Tab`, `Enter`, `Esc`)
- Mouse interaction events
- Modal support (backdrop, focus/pointer trap)
- Drag/drop events + drag tokens

### Binding + State
- Token replacement (`@token`)
- Typed value bindings:
  - `String`
  - `Bool`
  - `Int`
  - `Float`
- Validation rules:
  - `required`
  - `minlength` / `maxlength`
  - `pattern`
  - `min` / `max`

### Unity Integration
- MonoBehaviour bridge:
  - call public `void` methods from markup actions
- Optional action binding via UnityEvents
- Custom inspector for easier binding editing

---

## Quick Start

1. Add `SuiRuntimeHost` to a GameObject.
2. Assign:
   - `Razor File` (e.g. `Controls.razor`)
   - `Style Sheet File` (e.g. `Controls.styles.css`)
3. Add initial bindings in `Values`.
4. Add actions in `Actions` or expose public methods through `Script Bridge`.
5. Press Play.

---

## Action Bridge Example

```razor
<button onclick="Submit">Submit</button>
<button onclick="PlayerHud.OpenSettings">Settings</button>
