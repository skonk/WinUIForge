# WinUI Forge — Milestone 2

> **Date:** 2026-09-18  
> **Automated status:** PASS  
> **Runtime smoke test:** substantially passed; follow-ups recorded  
> **CI:** 35351622131

Milestone 2 is the first substantial XAML Studio-derived infrastructure slice after the WinUI 3 feasibility proof.

## Delivered

### Source model and non-destructive editing

`ForgeXamlDocument` now records the authored XAML hierarchy, attributes, parent/child relationships and exact source spans.

Inspector edits are applied surgically:

~~~text
selected authored element
        ↓
specific XAML attribute
        ↓
replace / insert / remove only that source span
        ↓
parse + render
        ↓
preserve selection
~~~

The surrounding XAML is not reserialized.

### Render service

`ForgeRenderService` owns dynamic WinUI 3 XAML loading and returns structured diagnostics.

Invalid source leaves the last valid preview intact.

### Authored/runtime coordinator

`ForgeVisualCoordinator` owns the named authored-element ↔ rendered-`FrameworkElement` mapping and the already runtime-validated rendered-bounds selection algorithm.

### Visual Tree

The right pane now includes an authored hierarchy. Preview, source, Visual Tree and property inspector share the same selected semantic element.

Unnamed authored nodes remain visible in the tree but are not yet directly runtime-mapped.

### Live Properties

The proof-only Width box has been replaced by a reusable curated property catalog.

Current categories include:

- layout;
- Grid attached properties;
- appearance;
- interaction;
- control/content;
- typography;
- Border;
- Grid;
- StackPanel.

Local XAML values appear as editable values. Unset properties display runtime values as placeholders. Enter commits; blank removes the local XAML attribute.

### WinUIEdit

The temporary source TextBox has been replaced with WinUIEdit `0.0.5-prerelease`.

The Forge adapter provides:

- XML highlighting;
- get/set source text;
- caret navigation;
- modified/update notifications;
- UTF-8 Scintilla position ↔ UTF-16 .NET source-index conversion.

## Automated validation

CI run **35351622131** passes:

- authored-tree parsing;
- source-index mapping;
- formatting-preserving replace/add/remove;
- XML attribute escaping;
- non-ASCII UTF-8/UTF-16 caret conversion;
- unknown-element handling;
- .NET 10 Release build;
- self-contained WinUI 3 x64 executable creation.

## Hands-on acceptance checklist

Before marking Milestone 2 runtime validated:

1. source editor loads with XML syntax highlighting;
2. valid source edits re-render;
3. invalid source reports an error while retaining the previous preview;
4. preview selection remains as reliable as Milestone 1;
5. Visual Tree click selects the same preview element;
6. preview click selects the matching Visual Tree item;
7. source navigation selects the same element;
8. property edits such as Width, Margin, Content/Text and Grid.Column update the live preview;
9. blanking a property removes its local XAML value;
10. property edits preserve surrounding source formatting;
11. non-ASCII text such as `café 😺` does not shift source navigation.

## Explicitly deferred

Milestone 2 does not add:

- Toolbox/Add mode;
- drag/move/resize;
- container-aware reparenting;
- Grid row/column visual editing;
- component authoring;
- token UI;
- Forge sidecar metadata UI;
- VisualState authoring;
- binding debugger.

Those build on this coordinator/source foundation rather than bypassing it.


## Hands-on runtime findings — 2026-09-18

The first Milestone 2 hands-on pass has now exercised the new editor, Visual Tree, preview selection, live inspector, invalid-XAML diagnostics and non-ASCII source handling.

Observed working:

- WinUIEdit loads with XML syntax highlighting;
- valid source/property edits update the real WinUI preview;
- preview selection remains reliable;
- selecting a preview element navigates to/highlights the corresponding source location;
- named authored items in the Visual Tree select the matching preview element;
- malformed XAML surfaces a useful parser error with line/position information;
- non-ASCII content such as `café 😺` renders correctly and does not break source navigation;
- repeated selection itself does not destabilize the preview.

Observed limitations / follow-ups:

### Unnamed Visual Tree nodes

Nodes such as `Grid.Resources`, `SolidColorBrush`, `Grid.ColumnDefinitions` and individual `ColumnDefinition` entries are currently shown greyed and cannot be selected.

This matches the current coordinator design: runtime mapping is based on authored elements with stable `x:Name` / `Name`.

This is acceptable for Milestone 2, but later Forge authoring should support stable identity for important unnamed authored nodes without requiring developers to litter production XAML with artificial names.

### Width and WinUI layout semantics

Setting an explicit Width on the heading TextBlock changed its layout position rather than simply making the visible text region "wider".

That behaviour is a useful reminder that Forge is editing **real WinUI layout semantics**, not rectangles in a drawing program. Width interacts with parent layout and alignment (for example Stretch/Center behaviour).

Future designer work should therefore make layout context visible and, where useful, explain or co-edit related properties such as:

- HorizontalAlignment;
- VerticalAlignment;
- Grid row/column sizing;
- parent container behaviour;
- margins;
- min/max sizing.

This is not something Forge should hide by silently changing unrelated properties.

## Future AI/image benchmark

A later milestone will deliberately ask ChatGPT/Codex to reconstruct a UI from a reference image using real WinUI elements inside Forge.

That benchmark is intended to expose exactly the kinds of layout-semantic issues seen during this smoke test: the AI may visually infer a width or position correctly but choose the wrong WinUI property/container semantics.

The benchmark is specified in:

[AI_IMAGE_REPLICATION_BENCHMARK.md](AI_IMAGE_REPLICATION_BENCHMARK.md)

It will compare both:

- visual fidelity to the reference image; and
- structural quality/editability of the produced WinUI XAML.

The goal is to improve not only the AI's first-pass visual accuracy, but also Forge's ability to give the AI precise, controllable primitives for reproducing real UI designs.
