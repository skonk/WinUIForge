# WinUI 3 vertical-slice proof

This side-by-side solution is the first technical proof for the XAML Studio → WinUI Forge migration.

It intentionally does **not** port the entire UWP application.

## What it proves

- .NET 10 + Windows App SDK / WinUI 3 application shell.
- Dynamic rendering with `Microsoft.UI.Xaml.Markup.XamlReader`.
- A text-owned XAML document is the structural source of truth.
- Named authored XAML elements map to real rendered `FrameworkElement` instances.
- Clicking a rendered element maps back to its XAML start tag.
- Placing the source caret inside a named start tag selects the runtime element.
- Selection is drawn as a design-time overlay rather than by mutating the control template.
- `FrameworkElement.Width` can be edited and committed back into the XAML source.
- Re-render preserves selection by stable `x:Name`.
- Core source parsing/writing is tested without WinUI.

## Build

From the repository root:

~~~powershell
.\build-port.ps1 -Configuration Release
~~~

Build and launch:

~~~powershell
.\build-port.ps1 -Configuration Release -Run
~~~

Expected executable:

~~~text
src\WinUIForge.Port.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\WinUIForge.Port.App.exe
~~~

## Deliberate limitations

This is a migration proof, not yet the WinUI Forge product shell.

- Milestone 1 used a plain WinUI `TextBox`; Milestone 2 replaces it with WinUIEdit.
- Runtime mapping is still deliberately limited to authored elements with `x:Name` or `Name`.
- Source navigation remains scoped to element start tags.
- Milestone 2 expands the inspector to a curated set of common DependencyProperties.
- There is still no Toolbox, drag/drop, Grid visual editor, Components, tokens UI or Forge sidecar metadata yet.
- The existing XAML Studio UWP solution remains untouched.

These limitations are intentional. The next migration slice should strengthen the coordinator/property system rather than broaden the UI prematurely.


## Validation status — 2026-09-18

Windows CI run **35339794165** validated commit `b9eede97877f00a381c1ec9143fcf6de82192c76`.

Validated automatically:

- `parse-named-elements` — PASS
- `source-index-mapping` — PASS
- `write-property` — PASS
- `remove-property` — PASS
- `unknown-element` — PASS
- Release build — PASS
- Compiler warnings — 0
- Compiler errors — 0
- expected self-contained x64 executable — present

Not yet automatically validated:

- application startup on an interactive desktop;
- `XamlReader.Load` runtime behaviour for the sample document;
- click hit-testing and source reveal;
- source-caret → preview selection;
- visual selection-overlay alignment;
- Width edit → source rewrite → re-render UX.

Those items require the first hands-on Windows smoke test (or later UI automation) and must not be described as verified until exercised.


## Hands-on runtime validation — 2026-09-18

The first interactive Windows smoke test has now passed.

Validated manually in the running WinUI 3 proof application:

- application launches successfully;
- dynamically loaded WinUI XAML renders correctly;
- authored elements in the current proof can be clicked and selected reliably across the preview;
- the inspector reports the correct authored control type and `x:Name` for the selected element;
- the selection outline follows the selected element's rendered bounds;
- nested selection resolves to the deepest authored element under the pointer (for example `RootGrid > ContentStack > ActionButton`);
- Button, TextBlock, Border/container and root/Grid selection no longer collapse onto an unrelated element;
- repeated switching between authored elements remains stable;
- source reveal no longer paints the whole XAML start tag as a large selection.

This closes the core interaction risk for the first vertical-slice proof: real WinUI XAML can be rendered, mapped to runtime elements, selected visually, and tied back to authored source in a usable interaction loop.
