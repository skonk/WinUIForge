# WinUI Forge — Milestone 3: visual authoring

> **Date:** 2026-09-18  
> **Automated status:** PASS  
> **Runtime smoke test:** pending  
> **CI:** 35356977677  
> **Validated commit:** `b6d8babc3f819e524aa0271fb4ea7e0133fdebff`

Milestone 3 turns the Milestone 2 XAML editor/inspector into the first real visual-authoring environment.

The architectural rule remains unchanged:

> Visual operations are committed only when they become deterministic edits to the authoritative XAML source.

## Delivered

### Transaction history / undo and redo

`ForgeEditHistory` records complete before/after source snapshots plus before/after authored selection identity for Forge visual operations.

Current Forge transactions include:

- property edits;
- Toolbox insertion;
- delete;
- sibling reorder;
- reparent;
- resize;
- Canvas movement;
- Grid-cell movement;
- Margin movement.

Undo/Redo restores both the XAML source and the intended authored selection.

Free-form typing in WinUIEdit deliberately clears the Forge structural history. WinUIEdit keeps its own text-edit undo behaviour; this prevents stale Forge snapshots from overwriting newer manual source changes.

### Authored identity for every XAML element

`ForgeXamlDocument` now assigns an identity to named, keyed and unnamed authored nodes.

Examples:

~~~text
name:ActionButton
name:RootGrid/key:AccentBrush
name:RootGrid/Grid.ColumnDefinitions[0]/ColumnDefinition[1]
~~~

This means source-only nodes such as:

- `Grid.Resources`;
- resource entries;
- `Grid.ColumnDefinitions`;
- `ColumnDefinition`;
- other unnamed property elements;

can participate in Forge selection and inspection without adding artificial `x:Name` values to production XAML.

Named elements continue to use their stable `x:Name` identity.

**Current limitation:** unnamed identity is a deterministic structural path, not yet a permanent immutable Forge UID. It survives property edits but can change when surrounding hierarchy/order changes. Persistent immutable UIDs belong in the future Forge sidecar metadata.

### Full source spans and structural mutation

The source model now records:

- start-tag position;
- start-tag close;
- closing-tag position;
- full-element end;
- self-closing state;
- parent/children;
- design-content children.

Forge can now mutate structure without serializing the complete XAML document:

- insert one child;
- delete one element;
- move one sibling earlier/later;
- move one element into another container.

Windows-specific XML line-position behaviour was normalized back to the actual `<` tag boundaries so whole-element edits use correct source spans.

### Selectable unnamed Visual Tree nodes

The Visual Tree no longer disables unnamed authored nodes.

Rows with direct runtime mappings behave as before.

Source-only rows are labelled with `· source`. Selecting one:

- navigates to its XAML;
- preserves its Forge authored identity;
- opens a source-attribute inspector;
- does not pretend a corresponding `FrameworkElement` exists.

This is the first answer to the Milestone 2 finding where `Grid.ColumnDefinitions` and `ColumnDefinition` were visible but greyed out.

### Parent-aware Layout Context

The Inspector now explains the selected element's parent layout semantics.

Examples:

#### StackPanel

- reports orientation;
- explains that dragging along the primary axis reorders children;
- explains that the cross-axis is controlled by alignment, size and margin.

#### Grid

- reports row/column and spans;
- explains Grid-cell movement vs within-cell Margin movement.

#### Canvas

- reports `Canvas.Left` / `Canvas.Top`;
- explains that movement maps directly to those attached properties.

#### Border / ScrollViewer

- explains single-content/container behaviour.

It also warns when explicit Width/Height is present while alignment is implicit, addressing the Milestone 2 observation that changing TextBlock Width also changed its apparent placement.

### Microsoft WinUI Gallery-backed Toolbox knowledge

The first Toolbox knowledge catalog is now wired into the app.

Reference:

~~~text
microsoft/WinUI-Gallery
abb8cb4cef04a5080f5c0396f67a7ec502b36179
~~~

A curated Forge asset records initial control metadata and insertion templates grounded in Gallery usage.

Initial authoring controls:

- Grid;
- StackPanel;
- Border;
- TextBlock;
- Button;
- TextBox;
- Image;
- Canvas.

Toolbox entries expose a short description and related controls.

This is intentionally a curated first slice of the Gallery knowledge source, not a vendored copy of the entire Gallery catalog.

### Toolbox / Add mode

Selecting a Toolbox entry:

1. resolves the selected element or nearest ancestor that can accept design children;
2. creates a unique `x:Name`;
3. inserts a real WinUI XAML fragment;
4. records one Forge transaction;
5. re-renders through the real WinUI 3 renderer;
6. selects the inserted element.

Supported design containers in this first slice:

- Grid;
- StackPanel;
- Canvas;
- RelativePanel;
- Border;
- ScrollViewer.

Border enforces one design child.

Self-closing containers are currently not automatically expanded.

### Structural authoring controls

The Inspector exposes:

- **Up**
- **Down**
- **Reparent**
- **Delete**

Up/Down reorder design children in source order.

Reparent opens a target-container picker and structurally moves the selected XAML element.

Delete removes the complete authored element.

All are Forge transactions.

### Designer chrome

Named runtime-mapped elements now receive two orange designer handles:

- top-left: move/reorder;
- bottom-right: resize.

The chrome sits over the real rendered WinUI element and commits only when a manipulation completes.

### Container-aware move semantics

Movement does not use one universal X/Y model.

#### StackPanel

Primary-axis drag becomes sibling reorder.

#### Canvas

Drag becomes:

~~~text
Canvas.Left
Canvas.Top
~~~

#### Grid

When the named parent Grid is runtime-mapped, dragging across a cell boundary changes:

~~~text
Grid.Row
Grid.Column
~~~

A drag that remains in the current cell falls back to Margin adjustment.

#### Border

Forge does not invent fake coordinates. It reports that child positioning is controlled by alignment/margin.

#### Other containers

The current fallback is Margin.

These are deliberately real WinUI layout semantics rather than drawing-program coordinates.

### Resize semantics

The bottom-right handle commits:

~~~text
Width
Height
~~~

to the selected authored element and records one transaction.

This is intentionally transparent: in real WinUI, explicit size interacts with alignment and parent layout. The Layout Context explains that interaction rather than silently changing unrelated alignment properties.

## Automated validation

Windows CI run **35356977677** passed with:

~~~text
0 warnings
0 errors
~~~

Core tests passing:

- parse-authored-tree;
- source-index-mapping;
- formatting-preserving property replacement;
- formatting-preserving property insertion;
- local-property removal;
- XML attribute escaping;
- UTF-8 ↔ UTF-16 source-position round-trip;
- unknown-element failure;
- unnamed-node identity;
- unnamed property edit preserves identity;
- insert/delete structural edit;
- sibling reorder structural edit;
- reparent structural edit;
- transaction undo/redo.

The self-contained x64 WinUI 3 executable was also produced successfully.

## Hands-on acceptance checklist

Before marking Milestone 3 runtime validated, exercise:

1. **Existing selection**
   - preview selection remains reliable;
   - source/tree selection stays synchronized.

2. **Unnamed Visual Tree**
   - click `Grid.ColumnDefinitions`;
   - click each `ColumnDefinition`;
   - confirm they are no longer disabled;
   - source navigation should follow;
   - direct runtime outline is not expected for source-only nodes.

3. **Source-only property editing**
   - select the second `ColumnDefinition`;
   - change its `Width`;
   - confirm the real Grid layout updates.

4. **Toolbox**
   - select `ContentStack` or one of its children;
   - add TextBlock/Button;
   - confirm XAML insertion, preview render and new selection;
   - Undo and Redo the insertion.

5. **Delete / Undo**
   - add a temporary control;
   - delete it;
   - Undo should restore it and its selection.

6. **StackPanel reorder**
   - select `ActionButton`;
   - use Up/Down;
   - test the top-left move handle vertically;
   - source order and preview order should change together.

7. **Reparent**
   - add a new Grid or StackPanel;
   - reparent a temporary control into it;
   - confirm hierarchy/source/preview all agree.
   - An occupied Border should correctly reject a second child.

8. **Resize**
   - drag the lower-right handle;
   - confirm Width/Height appear in source;
   - Undo should restore the old layout.

9. **Grid move**
   - drag a child across a Grid cell boundary;
   - verify `Grid.Row` / `Grid.Column` changes when a target cell is crossed;
   - a smaller within-cell movement may change Margin instead.

10. **Canvas move**
    - add/select a Canvas and a child;
    - move the child;
    - confirm `Canvas.Left` / `Canvas.Top` are written.

11. **Layout context**
    - select the heading TextBlock;
    - confirm the Inspector explains the StackPanel parent and the Width/alignment interaction.

12. **Regression checks**
    - malformed XAML still leaves the last valid preview visible;
    - `café 😺` still preserves correct source navigation.

## Known first-slice limitations

- unnamed identity is structural-path based until Forge sidecar UIDs arrive;
- Toolbox is click-to-add; drag-from-Toolbox placement is a later refinement;
- self-closing containers are not automatically expanded to accept children;
- StackPanel drag reorders one sibling per completed drag, not arbitrary insertion-index preview yet;
- Grid cell movement is strongest when the parent Grid has a runtime mapping/name; otherwise Margin fallback is used;
- resize currently writes explicit Width + Height;
- no multi-selection;
- no snapping/alignment guides yet;
- no Grid row/column definition visual editor yet;
- no Component authoring yet;
- no VisualState authoring yet.

## Gate to the image-replication benchmark

Milestone 3 is the minimum authoring layer needed before beginning the Stage A image-reference benchmark.

After the hands-on smoke test stabilizes this authoring shell, the next valuable exercise is:

> Give ChatGPT/Codex a simple reference UI image, generate real WinUI XAML, open it in Forge, compare against the image, and use the observed corrections to drive Milestone 4.

See [AI_IMAGE_REPLICATION_BENCHMARK.md](AI_IMAGE_REPLICATION_BENCHMARK.md).
