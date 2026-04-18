# Razor in Unity (Sandbox.Razor bridge)

> Note: for the replacement runtime track (not UI Toolkit), use `Assets/Scripts/SuiRuntime/README.md`.

## Why this layout
- `Sandbox.Razor` source stays outside `Assets`.
- Unity consumes compiled DLLs from `Assets/Plugins/SandboxRazor`.

## Sync DLLs into Unity
Run from repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Sync-SandboxRazor.ps1
```

## Runtime renderer
Use `RazorUiDocumentView` on a GameObject that has `UIDocument`.

1. Assign a `.razor` `TextAsset` (for example `Assets/Scripts/RazorUi/Samples/Counter.razor`).
2. Add value bindings (example: `title=Sandbox UI`, `count=0`).
3. Add action binding with `actionName=Increment` and hook its UnityEvent.
4. Call `Render Razor UI` from the component context menu (or let `OnEnable` render automatically).

## Supported Razor/UI subset
- Tags: `div`, headings (`h1`..`h6`), `p`, `span`, `label`, `button`, `input`, `img`.
- Text tokens like `@count` via inspector value bindings.
- Button actions via `onclick` or `@onclick` mapped to UnityEvents.
- Basic inline styles: `flex-direction`, `padding`, `margin`, `width`, `height`, `background-color`, `color`.

## Notes
- This is a practical bridge for Unity UI Toolkit, not full Blazor runtime behavior.
- `Sandbox.Razor` compile output is still captured for inspection/debugging.
