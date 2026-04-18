# SUI Runtime (Unity UI replacement path)

This folder is the **replacement track**, not a wrapper around Unity UI Toolkit.

## What this runtime owns
- Razor markup ingestion (`.razor`/text source assets)
- SUI document model (`SuiNode`)
- Layout pass (`SuiLayoutEngine`)
- Input/action dispatch (`onclick` -> action map)
- Rendering pass (`SuiImmediateRenderer`)
- Anchored root panel (`SuiPanel`) for resolution-independent placement
- Retained tree with dirty updates (parse/layout only when state or panel/screen changes)
- Keyboard focus loop (`Tab` / `Shift+Tab`, `Esc` to clear focus, `Enter` to activate focused control)
- Mouse events for non-control nodes (`onclick`, `onmousedown`, `onmouseup`, `onmouseenter`, `onmouseleave`)

## Quick start
1. Add `SuiRuntimeHost` to a GameObject.
2. Assign a Razor source asset in `razorFile` (you can drag `.razor`, `.txt`, or a `TextAsset`; example: `Assets/Scripts/SuiRuntime/Samples/Controls.razor`).
3. (Optional) Assign a stylesheet asset in `styleSheetFile` (example: `Assets/Scripts/SuiRuntime/Samples/Controls.styles.css`).
4. Add action binding: `Increment` and connect it to `SuiCounterState.Increment`.
5. Add `SuiCounterState` on the same GameObject.
6. Choose a `Panel > anchor` preset on `SuiRuntimeHost` (for example `TopLeft` or `Stretch`).
7. Press Play.

## Current supported subset
- Tags: `div`, `p`, `span`, `label`, `h1`-`h6`, `button`, `ul`, `ol`, `li`, `textarea`, `select`, `option`, `listbox`, `progress`, `meter`, `window`, `window-header`, `window-body`.
- Controls: `input` (text), `input type="checkbox"`, `input type="radio"`, `input type="range"`, `textarea`, `select/listbox` (+ `option`), `progress`, `meter`, `img`.
- Data tokens: `@name` from host token map.
- Actions: `onclick="ActionName"`.
- Actions can invoke either:
  - Inspector `ActionBinding` UnityEvents, or
  - Public parameterless methods on `MonoBehaviour` script targets (for example `onclick="Submit"` or `onclick="PlayerHud.Submit"`).
- Control events: `onchange="ActionName"` and `oninput="ActionName"` (text input).
- Non-control mouse events: `onmousedown`, `onmouseup`, `onmouseenter`, `onmouseleave`.
- Control binding key: use `bind`, or fallback to `name`, then `id`.
- Inline style basics: `flex-direction`, `padding`, `margin`, `width`, `height`, `background-color`, `color`, `font-size`, `border`, `border-width`, `border-color`, `border-radius`.
- Effects: `box-shadow` and `text-shadow` (single or comma-separated multiple layers).
- Layout: `gap`, `justify-content`, `align-items`, `min-width`, `max-width`, `min-height`, `max-height`, `flex-grow`, `flex-shrink`, `flex-wrap`, `overflow-y`, `position`, `left/right/top/bottom`, `z-index`.
- Percent sizing: `width: 50%; height: 30%;` on nodes.
- Styling v1: external selectors `tag`, `.class`, `#id` merged in order `tag -> class -> id -> inline` (inline wins).
- Styling v1.2: pseudo selectors `:hover`, `:active`, `:focus`, `:disabled`, `:invalid` on `tag`, `.class`, and `#id`.
- Window foundation v1: `<window draggable=\"true\">` can be dragged by header region (`window-header` or top band).
- Modal option: use `<modal>` (modal by default) or `<window modal="true">`. While open, focus and pointer are trapped to the top-most modal window. Toggle globally with `SuiRuntimeHost.enableModalBehavior`.
- Modal backdrop: configure with `backdrop-color="#00000099"`, `backdrop-alpha="0.55"`, and `show-backdrop="false"` on the modal/window node.
- Modal open state: use `open="true|false"` in markup, or call runtime API (`OpenModal`, `CloseModal`, `SetModalOpen`, `CloseTopModal`).
- Scroll containers: set `overflow-y: auto` (or `scroll`/`hidden`) with fixed `height`; mouse wheel scroll supported (`scroll-speed` optional).
- Positioning: set parent `position: relative`, then child `position: absolute` with `top/left/right/bottom`; layering uses `z-index` for render and hit-test order.
- Drag/drop: mark source nodes `draggable="true"` and optional `drag-data="..."`; mark targets `droppable="true"` (or just `ondrop="Action"`). Events supported: `ondragstart`, `ondrag`, `ondragend`, `ondragenter`, `ondragover`, `ondragleave`, `ondrop`.
- Drag tokens available in Razor: `@suiDragSourceId`, `@suiDragSource`, `@suiDragData`, `@suiDropTargetId`, `@suiDropTarget`, `@suiDragging`.
- Disabled behavior: set `disabled="true"` (or `aria-disabled="true"`) on controls to block interaction/focus and enable `:disabled` styling.
- Typed value bindings: each `Values` row can be `String`, `Bool`, `Int`, or `Float`; runtime keeps parsed state and synchronized token text.
- Validation state: `required`, `minlength`, `maxlength`, `pattern`, `min`, `max` are evaluated for controls and can be styled with `:invalid`.

## MonoBehaviour bridge
- In `SuiRuntimeHost`, use `Script Bridge`:
  - `includeSiblingMonoBehaviours`: auto-discover scripts on the same GameObject.
  - `scriptTargets`: explicit scripts to expose.
- Any public `void MethodName()` can be called from markup events.
- Example:
```razor
<button onclick="Ping">Ping</button>
<button onclick="SuiMonoBridgeExample.Submit">Submit</button>
```

## Samples
- `Assets/Scripts/SuiRuntime/Samples/Controls.razor` demonstrates text input, checkbox, image, and button.
- `Assets/Scripts/SuiRuntime/Samples/Controls.styles.css` demonstrates external class styling.
- `img src` supports `Resources` keys (for builds) and in-Editor convenience via asset path or filename lookup (for example `src="ui/logo"` or `src="Assets/Textures/image_6.png"` or `src="image_6"`).

## Modal scripting
- Call from C# on your host component:
```csharp
host.OpenModal("settings");      // matches id="settings" (or name)
host.CloseModal("settings");
host.SetModalOpen("settings", true);
host.CloseTopModal();
var isOpen = host.IsModalOpen("settings");
```

## Important
- This is the first vertical slice of a full SUI runtime.
- `UIDocument` / UI Toolkit is not required for this runtime path.
