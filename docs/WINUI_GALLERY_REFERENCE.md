# WinUI Forge — Microsoft WinUI Gallery reference plan

> **Reference repository:** `microsoft/WinUI-Gallery`  
> **Revision reviewed:** `abb8cb4cef04a5080f5c0396f67a7ec502b36179`  
> **License:** MIT  
> **Role in Forge:** canonical WinUI 3 control/design knowledge source and UI-quality reference.

## Relationship to XAML Studio

WinUI Gallery and XAML Studio solve different parts of the Forge problem:

~~~text
XAML Studio
= editing / parsing / rendering / source ↔ runtime coordination

WinUI Gallery
= canonical control usage / Fluent design patterns / adaptive layouts /
  accessibility / sample XAML / UI automation examples
~~~

Forge should use both.

XAML Studio remains the main architectural foundation for the editor/designer.

WinUI Gallery should become a reference and knowledge source for how real WinUI 3 controls are normally composed.

## Machine-readable sample catalog

The most valuable asset is:

~~~text
catalog/windows-samples.json
~~~

WinUI Gallery documents this as a generated, machine-readable index of its embedded samples.

The catalog contains, where available:

- control identity and display name;
- descriptions;
- API namespaces;
- tags and curated keywords;
- related controls;
- documentation links;
- required xmlns imports;
- inline XAML samples;
- inline C# samples;
- provenance back to the Gallery source files.

The catalog follows the shared WinUI sample-index contract rather than a Gallery-only format.

### Forge use

Forge should eventually ingest or query this catalog as a **Control Knowledge Catalog**.

Conceptually:

~~~text
Button
├─ API namespace
├─ description
├─ related controls
├─ sample XAML
├─ common composition patterns
├─ accessibility examples
└─ Forge property/layout metadata
~~~

This could power:

- Toolbox help;
- property-inspector guidance;
- AI generation hints;
- README-AI examples;
- control-search suggestions;
- "insert example" workflows;
- diagnostics when a control is being used in an unusual way.

The catalog is preferable to scraping Gallery pages or relying entirely on model recall.

## Fluent design reference

The Gallery itself is a useful example of polished WinUI 3 composition.

Useful patterns observed in the reviewed revision include:

- `ThemeResource` usage instead of unnecessary hard-coded colours;
- standard Fluent surface resources such as card/background/stroke brushes;
- restrained card borders and corner radii;
- native `TitleBar`;
- `MicaBackdrop`;
- `NavigationView`;
- `AutoSuggestBox` search;
- `Expander` for secondary content;
- `SelectorBar` for mode switching;
- `InfoBar` for contextual warnings;
- `VisualStateManager` + `AdaptiveTrigger` for responsive reflow;
- dedicated high-contrast resources;
- systematic `AutomationProperties`.

Forge should use these as idiomatic examples, not blindly copy Gallery application-specific UI.

## Why this matters to the image-replication benchmark

The image benchmark is not only about matching pixels.

An AI may correctly infer that a pane appears 320 px wide in one screenshot while choosing poor WinUI semantics such as:

- fixed Canvas coordinates;
- unnecessary absolute Width values;
- incorrect HorizontalAlignment;
- brittle negative margins;
- wrong Grid star/Auto relationships.

WinUI Gallery gives Forge/AI a corpus of real WinUI solutions for common layout and control problems.

During benchmark generation, Gallery examples may be used as **generic control/layout guidance**.

They must not be used as hidden access to the target implementation. If a benchmark reference image itself comes from WinUI Gallery, the matching Gallery XAML must be excluded from the AI input for that benchmark run.

## Layout semantics reference

Gallery samples are especially useful for teaching or validating:

- Grid rows and columns;
- Auto / pixel / star sizing;
- ColumnSpacing / RowSpacing;
- StackPanel flows;
- adaptive visual states;
- responsive option panes;
- navigation layouts;
- card-like grouping;
- control-specific sizing behaviour.

This directly addresses the Milestone 2 finding where setting Width on a TextBlock changed layout position rather than behaving like a drawing-program rectangle.

Forge should surface enough parent/container context for both humans and AI to understand these semantics.

## Accessibility reference

Gallery includes dedicated accessibility content and consistently demonstrates automation metadata.

Forge should eventually derive guidance/diagnostics for:

- AutomationProperties.Name;
- heading levels;
- keyboard navigation;
- focus behaviour;
- high contrast;
- accessible grouping;
- useful automation IDs.

This should feed both Forge diagnostics and AI Contract metadata.

## UI automation reference

The repository includes:

~~~text
tests/WinUIGallery.UITests/
~~~

and deliberate UI Automation helper elements in its application shell.

When Forge adds UI automation, this is worth studying for:

- reliable launch/wait-for-idle flows;
- selection regression tests;
- screenshot capture;
- interaction smoke tests;
- accessibility-tree assertions.

This would reduce dependence on manual testing for designer-selection regressions.

## Potential Forge feature: Gallery-backed Toolbox intelligence

A later Toolbox could optionally expose a small guidance surface:

~~~text
Button
  Standard action control
  Related: ToggleButton, HyperlinkButton, AppBarButton

  Examples:
  • Standard button
  • Accent button
  • Command surface button
~~~

The underlying examples should be sourced from the Gallery catalog and filtered to the Forge Design XAML profile.

This is particularly valuable for ChatGPT/Codex because it provides grounded examples of control usage without requiring the agent to search the web or guess API syntax.

## Potential Forge feature: sample-to-design import

A Gallery XAML fragment could eventually be imported into a scratch Screen or Component.

The import path should:

1. read the catalog sample;
2. apply required xmlns imports;
3. reject/flag app-specific bindings or code-behind;
4. normalize only what Forge must normalize;
5. render it using the same WinUI 3 preview pipeline;
6. retain provenance metadata.

This should be a convenience/reference feature, not the main project format.

## Resource/style policy

Forge should prefer native WinUI resources and ThemeResources when they express the intended design.

However, Forge must not force every design to look like WinUI Gallery.

The target hierarchy is:

~~~text
native WinUI semantics
        +
Fluent defaults/resources where appropriate
        +
project-specific tokens/theme
        =
WinUI Forge design
~~~

For JLA3D/Workshop, graphite/orange branding remains project-specific even when the underlying control composition follows Fluent/WinUI patterns.

## Licensing/provenance

WinUI Gallery is MIT licensed.

If Forge copies or substantially adapts Gallery code or XAML rather than merely referencing the public catalog/patterns, preserve the applicable Microsoft copyright and MIT license notice.

Any imported sample provenance should record at minimum:

- source repository;
- source revision;
- catalog sample/control id;
- original source path when available.

## Near-term actions

Do not vendor the whole Gallery repository into Forge.

Instead:

1. keep this repository/revision documented as a reference source;
2. add the Gallery sample index as an optional development/AI knowledge source;
3. use Gallery XAML to expand the Forge property/control coverage tests;
4. use Gallery patterns when designing Toolbox defaults and container-aware authoring;
5. use its accessibility and UI-test patterns when those Forge systems are implemented;
6. include Gallery-grounded guidance in the image-replication benchmark analysis.

## Strategic value

XAML Studio helps Forge understand **how to edit XAML**.

WinUI Gallery helps Forge understand **what good WinUI XAML looks like**.

Together they give the project a much stronger basis for the intended human ↔ AI design workflow.
