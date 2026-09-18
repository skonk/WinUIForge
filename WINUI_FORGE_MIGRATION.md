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
