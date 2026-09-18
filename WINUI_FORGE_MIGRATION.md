# WinUI Forge migration

## Mission

WinUI Forge is a visual WinUI 3 design and interchange environment for humans and AI coding agents.

Target workflow:

~~~text
AI creates structured mock-up
        ->
human opens/edits it visually in WinUI Forge
        ->
Forge saves real XAML + semantic metadata
        ->
AI consumes exact corrections
        ->
real application implementation
        ->
semantic/visual validation
~~~

## Starting point

This repository is a fork of dotnet/XAMLStudio based on:

~~~text
92267797838d8e619dd26a0b3e6af9ba7944e71c
~~~

Initial development branch:

~~~text
winui-forge/winui3-port
~~~

## Phase 1 proof

Before broad rebranding or feature work:

1. .NET 10 / Windows App SDK application launches.
2. User edits one XAML document.
3. WinUI 3 dynamically renders the XAML.
4. Clicking a rendered named element maps to its source XML element.
5. The selected element can be highlighted without rebuilding unrelated UI.
6. At least one DependencyProperty is shown in the property panel.
7. Committing a property edit updates the source XAML.
8. Re-render preserves source/preview mapping.

## Architecture direction

Runtime hierarchy/properties are represented by real WinUI XAML. Forge-specific design semantics live in sidecar metadata.

Future Screen shape:

~~~text
screens/library/
    Library.xaml
    Library.forge.json
    sample-data.json
~~~

## Do not do yet

- do not port every XAML Studio feature before the proof;
- do not delete Workshop Prototype 1;
- do not bulk rename every namespace before renderer/coordinator works;
- do not preserve UWP compatibility unless it reduces migration risk;
- do not build the complete Toolbox before source synchronization works;
- do not treat runtime-only DependencyProperty mutation as a saved design edit.

## Reference

Authoritative plan:

~~~text
skonk/JLA3D-Workshop-Suite
docs/winui-forge/XAMLSTUDIO_PIVOT_PLAN.md
~~~

Prototype 1:

~~~text
skonk/JLA3D-Workshop-Suite/WinUIForge
~~~


## Milestone status — first WinUI 3 vertical slice

**Status: PASSED (2026-09-18)**

The first WinUI 3 proof has now passed both Windows CI and hands-on runtime testing.

Confirmed:

- .NET 10 / Windows App SDK / WinUI 3 application launches;
- real XAML is dynamically rendered with `Microsoft.UI.Xaml.Markup.XamlReader`;
- authored named elements are mapped to rendered `FrameworkElement` instances;
- preview selection is resolved from rendered authored bounds;
- authored elements in the current proof can be selected reliably and repeatedly through the live preview;
- nested clicks resolve to the correct deepest authored element rather than an unrelated sibling/ancestor;
- inspector identity tracks the selected authored control correctly;
- source location and runtime selection remain synchronized;
- Width can be represented as a source XAML edit in the proof document;
- build/test CI is green.

The migration may now move beyond proof-of-viability and begin porting/reusing the stronger XAML Studio subsystems.


## Milestone 2 — coordinator, render service, live properties and source editor

**Automated status: PASSED (2026-09-18)**  
**Hands-on runtime status: pending**

Milestone 2 replaces the one-off vertical-slice plumbing with reusable infrastructure adapted from the strongest XAML Studio concepts.

Implemented and Windows-CI validated:

- `ForgeRenderService` wraps WinUI 3 `XamlReader` and returns structured render success/failure diagnostics;
- invalid XAML no longer replaces the last valid preview;
- `ForgeVisualCoordinator` owns authored-XAML ↔ rendered-`FrameworkElement` mapping;
- the proven rendered-bounds selection algorithm is preserved in the coordinator;
- `ForgeXamlDocument` now models the authored hierarchy, source ranges, attributes, parents and children;
- property edits mutate only the relevant source attribute/value rather than reserializing the document;
- replacement, insertion, removal and XML escaping are covered by core tests;
- an authored Visual Tree is synchronized with preview/source/inspector selection;
- a curated live DependencyProperty inspector exposes common layout, Grid, appearance, typography, control, content, Border and StackPanel properties;
- Enter commits an inspector edit back into XAML and blank removes the local value;
- the selected element is preserved across valid re-renders;
- the temporary WinUI `TextBox` source editor has been replaced with WinUIEdit `0.0.5-prerelease` / `CodeEditorControl` with XML highlighting;
- Scintilla UTF-8 byte positions are converted to/from .NET UTF-16 source indices so non-ASCII XAML text does not corrupt source navigation;
- the original XAML Studio UWP solution remains intact for reference and upstream comparison.

Windows CI run **35351622131** passed at commit `660cebd02497c9e164dd0ef2cd911e10574244e3`.

Core tests now cover:

- authored hierarchy parsing;
- source-index mapping;
- formatting-preserving property replacement;
- formatting-preserving property insertion;
- local-property removal;
- XML attribute escaping;
- UTF-8 ↔ UTF-16 caret-position round-tripping with non-ASCII text;
- unknown-element failure handling.

The Release app build completed successfully with the expected self-contained x64 executable.

### Runtime validation still required

Hands-on validation must now confirm:

- WinUIEdit loads and displays the XAML source correctly;
- XML syntax highlighting is active;
- editing valid source triggers the debounced preview render;
- malformed source leaves the last valid preview visible and surfaces the error;
- preview, Visual Tree and source selection remain synchronized;
- live property edits update only the intended XAML attribute and preserve surrounding formatting;
- blanking a property removes the local XAML value;
- source navigation remains correct with non-ASCII text.


## Planned validation track — AI image-reference replication

A dedicated image-reference reconstruction benchmark is now part of the product plan.

The benchmark should begin after basic visual authoring exists (Toolbox/Add mode, move/resize and container-aware layout editing) and should run before the AI/.wforge round-trip is considered mature.

The test loop is:

~~~text
reference UI image
    ->
ChatGPT/Codex authors real WinUI XAML + Forge metadata
    ->
WinUI Forge renders the design
    ->
compare reference image, rendered screenshot, semantic bounds and XAML hierarchy
    ->
human corrects the design in Forge
    ->
return corrected project to the AI
    ->
classify failures and improve Forge / schemas / AI guidance
~~~

The benchmark must evaluate both visual similarity and WinUI structural quality. A visually close result that uses brittle Canvas coordinates or inappropriate fixed sizing is not considered equivalent to a structurally correct Grid/StackPanel design.

Workshop mock-ups should provide the primary real-world benchmark cases.

See [docs/AI_IMAGE_REPLICATION_BENCHMARK.md](docs/AI_IMAGE_REPLICATION_BENCHMARK.md).


## Secondary upstream/reference source — Microsoft WinUI Gallery

WinUI Gallery is now an explicit secondary reference source for Forge.

Pinned review:

~~~text
repository: microsoft/WinUI-Gallery
revision: abb8cb4cef04a5080f5c0396f67a7ec502b36179
~~~

Its role differs from XAML Studio:

~~~text
XAML Studio -> designer/editor architecture
WinUI Gallery -> canonical WinUI control/design/sample knowledge
~~~

Particularly valuable is `catalog/windows-samples.json`, a machine-readable sample index containing real WinUI sample XAML/C# and metadata.

Forge should prefer consuming that catalog over scraping sample pages.

Planned uses include:

- Toolbox/control guidance;
- AI Design XAML examples;
- property/catalog coverage;
- Fluent resource/layout references;
- accessibility guidance;
- UI automation patterns;
- image-replication benchmark grounding.

See [docs/WINUI_GALLERY_REFERENCE.md](docs/WINUI_GALLERY_REFERENCE.md).


## Milestone 3 — visual authoring

**Automated status: PASSED (2026-09-18)**  
**Hands-on runtime status: PASSED (2026-09-18)**

Milestone 3 adds the first real visual authoring layer on top of the Milestone 2 coordinator/source foundation.

Implemented:

- Forge transaction history with undo/redo;
- authored identity for named, keyed and unnamed XAML nodes;
- full source spans and structural insert/delete/reorder/reparent operations;
- selectable source-only Visual Tree nodes;
- parent-aware layout guidance;
- Gallery-backed Toolbox knowledge;
- click-to-add Toolbox authoring;
- structural Up/Down/Reparent/Delete commands;
- designer move/reorder handle;
- designer resize handle;
- StackPanel-aware reorder semantics;
- Canvas.Left/Top movement;
- Grid cell movement and within-cell Margin fallback;
- source-backed resize through Width/Height.

Automated implementation validation: CI **35356977677**, 0 warnings / 0 errors.

The hands-on authoring pass found one integration defect: delayed WinUIEdit `Modified` notifications could clear Forge structural history immediately after a programmatic source synchronization, making Undo/Redo appear non-functional. Commit `09bca57b4d32f2c343421536f54ae4d755d37860` filters those no-op delayed notifications while preserving the intended history reset for genuine manual source edits.

Final Windows CI **35359224233** passed with 14 core authoring tests, 0 warnings and 0 errors. A subsequent hands-on Windows retest confirmed Undo and Redo now work, closing the Milestone 3 runtime gate.

See [docs/MILESTONE3.md](docs/MILESTONE3.md).
