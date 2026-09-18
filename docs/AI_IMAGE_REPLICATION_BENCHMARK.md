# WinUI Forge — AI image-reference replication benchmark

> **Purpose:** Measure and improve how reliably ChatGPT/Codex can translate a visual UI reference into real, editable WinUI 3 structure.
>
> **Status:** Active validation track — first Workshop Stage B case running (2026-09-18).
>
> **Run when:** Basic designer authoring exists (add, move/resize, Grid/StackPanel/container editing) and before the AI/.wforge workflow is considered mature.

## Why this benchmark exists

The long-term WinUI Forge workflow depends on an AI being able to look at a visual mock-up and produce a structured WinUI design that is close enough to become the basis of the real application.

The important question is not only:

> Does the rendered screenshot look similar?

It is also:

> Did the AI choose good WinUI structure and layout semantics so the design remains editable, responsive and useful to the real application?

The benchmark therefore tests both **visual fidelity** and **structural correctness**.

## Core benchmark loop

~~~text
reference image
      ↓
ChatGPT / Codex
      ↓
real WinUI XAML + Forge metadata
      ↓
WinUI Forge
      ↓
real WinUI 3 render
      ↓
reference ↔ render comparison
      ↓
human inspection / correction
      ↓
failure classification
      ↓
improve Forge controls, metadata, prompts and AI guidance
      ↓
repeat
~~~

The AI must author the structured design. It should not be allowed to satisfy the benchmark by embedding the reference image as the UI.

## Benchmark inputs

Each case should provide:

- one reference PNG or screenshot;
- target viewport width/height;
- any known font information;
- available image/icon assets when relevant;
- optional text/content specification;
- optional responsive expectations.

No implementation XAML should be provided to the AI for the primary reconstruction attempt.

## Required output

The AI should produce a Forge-openable design containing at minimum:

~~~text
screens/<screen>/
    Screen.xaml
    Screen.forge.json
~~~

As Forge project support matures, benchmark output should also include:

~~~text
forge.project.json
tokens/
components/
sample-data.json
README-AI.md
~~~

The generated XAML must use real WinUI 3 controls and valid layout containers.

## Evaluation dimensions

### 1. Visual geometry

Measure or inspect:

- major region bounds;
- panel widths/heights;
- margins and padding;
- gaps between controls;
- element alignment;
- border radii;
- divider positions;
- repeated-item spacing;
- overall composition.

### 2. Typography

Check:

- font size;
- weight;
- line height where controllable;
- wrapping;
- alignment;
- truncation;
- hierarchy between headings/body/labels.

### 3. Colour and appearance

Check:

- backgrounds;
- foregrounds;
- border colours;
- opacity;
- accent usage;
- shadows where supported/appropriate;
- theme-resource consistency.

### 4. Structural WinUI quality

The closest screenshot is not automatically the best result.

Inspect whether the AI chose sensible WinUI semantics:

- Grid rather than fixed Canvas coordinates for responsive regions;
- StackPanel or appropriate layout for ordered flows;
- Grid rows/columns for multi-pane layouts;
- sensible Auto / pixel / star sizing;
- reusable resources rather than repeated magic values;
- components for repeated visual structures;
- no unnecessary wrapper explosion;
- no brittle negative margins when proper layout can express the design.

### 5. Editability in Forge

The generated design should remain easy to correct:

- visible controls are selectable;
- hierarchy is understandable;
- major elements have stable identity;
- common properties are editable;
- containers expose meaningful layout behaviour;
- editing one region does not unexpectedly distort unrelated regions.

### 6. Responsive behaviour

Where relevant, resize the preview and inspect:

- whether major regions preserve intent;
- whether controls clip unexpectedly;
- whether star/Auto sizing behaves appropriately;
- whether fixed dimensions were used only where justified.

### 7. AI round-trip quality

After the human fixes the design in Forge:

- return the project to ChatGPT/Codex;
- ask the AI to explain the human changes;
- ask it to update an implementation or generate a revised design;
- verify it uses the actual structured changes instead of reinterpreting only the screenshot.

## Failure taxonomy

Every mismatch should be classified. This is more useful than a single visual score.

Suggested categories:

### Geometry interpretation

Examples:

- wrong panel width;
- incorrect spacing;
- alignment shifted;
- incorrect element size.

### Layout-semantic error

Examples:

- Canvas used where Grid was required;
- fixed width used where star sizing was intended;
- element centred because explicit Width was combined with Stretch alignment;
- wrong parent container selected.

### Typography error

Examples:

- wrong font size/weight;
- unexpected wrapping;
- line height or text alignment mismatch.

### Resource/token error

Examples:

- repeated literal colours;
- wrong accent;
- inconsistent spacing constants.

### Control-choice error

Examples:

- Border used when Button is semantically required;
- TextBlock used for interactive content;
- wrong WinUI control family.

### Hierarchy/component error

Examples:

- repeated component copied instead of referenced;
- unnecessary containers;
- important semantic region missing an identity.

### Unsupported-Forge-operation error

The AI selected a reasonable WinUI solution, but Forge cannot yet manipulate it well.

This category should create a Forge feature backlog item rather than being blamed on the AI.

### AI-guidance error

Forge supports the operation, but the AI did not know how to express it correctly.

This should improve:

- README-AI;
- Design XAML rules;
- examples;
- schemas;
- agent instructions.

## Active case

The Stage B dashboard case is now accepted, and the first Stage C dense-production case is active:

~~~text
Stage B accepted:
benchmarks/workshop-dashboard-v1/
reference: W2-01-dashboard.png
viewport: 1672 x 941

Stage C active:
benchmarks/workshop-storage-settings-v1/
reference: W2-14-storage-settings.png
viewport: 1672 x 941
~~~

The dashboard case validated the complete image-reference loop and produced several Forge improvements: reference overlays, fixed logical viewport modes, live designer manipulation, semantic Highlight All, dense Visual Tree filtering, direct XAML open/save/reload, review-package export and collapsible source/tools panes.

The Storage & Settings case deliberately stresses different problems: dense settings controls, mixed Auto/star sizing, scroll behaviour, table density, form alignment, inspector composition and uneven information density.

## Benchmark stages

### Stage A — simple reconstruction

Use a reference with:

- one main container;
- heading/body text;
- one or two buttons;
- simple spacing;
- no repeated complex components.

Goal: establish basic geometry and typography control.

### Stage B — structured application screen

Use a realistic three-pane or dashboard UI containing:

- navigation/sidebar;
- central content;
- inspector/details pane;
- toolbar;
- cards/list rows;
- mixed fixed/star/Auto Grid sizing.

Goal: validate correct WinUI layout semantics.

A Workshop 2.0 mock-up is an ideal candidate.

### Stage C — dense production reference

Use a more complex image with:

- nested containers;
- repeated components;
- scrolling regions;
- tabs or navigation;
- richer typography;
- responsive constraints.

Goal: expose weaknesses in Forge authoring and AI structural reasoning.

## Comparison artifacts

For each run retain:

~~~text
reference.png
ai-first-pass/
    Screen.xaml
    Screen.forge.json
first-pass.png
first-pass.svg
human-corrected/
    Screen.xaml
    Screen.forge.json
corrected.png
benchmark-report.json
benchmark-notes.md
~~~

This makes regressions and improvements measurable over time.

## Benchmark report

A machine-readable report should eventually record:

~~~json
{
  "case": "workshop-library-v1",
  "viewport": [1672, 941],
  "aiModel": "recorded-by-runner",
  "firstPass": {
    "structurallyValid": true,
    "rendered": true
  },
  "corrections": [
    {
      "category": "layout-semantic",
      "element": "library.inspector",
      "description": "Used fixed width where design required star/min-max behaviour"
    }
  ],
  "forgeLimitations": [],
  "guidanceChanges": []
}
~~~

Do not reduce the benchmark to one opaque score. Individual error classes are more actionable.

## Success criteria

The benchmark becomes useful when repeated runs show that:

1. AI-generated XAML opens in Forge without repair.
2. Major layout regions are structurally correct on the first attempt.
3. Human corrections are mostly tuning rather than rebuilding hierarchy.
4. Forge exposes every important correction visually.
5. Returning the corrected project to the AI results in exact understanding of those changes.
6. The final application can reuse or closely map the authored XAML/components.
7. Each failure leads to a concrete improvement in Forge, its schemas, or AI guidance.

## Relationship to screenshot diffing

Pixel/region comparison is useful, but it is a **measurement tool**, not the source of truth.

The benchmark should eventually combine:

~~~text
reference PNG
      +
rendered screenshot
      +
semantic element bounds
      +
authored XAML hierarchy
      +
Forge metadata
~~~

This lets the system distinguish:

- visually wrong;
- visually close but structurally poor;
- structurally correct but visually under-tuned.

## Relationship to Workshop

Workshop should be the primary dogfood source for benchmark references.

Existing Workshop mock-ups are particularly valuable because they provide:

- realistic desktop application density;
- known design intent;
- existing implementation history;
- a concrete target application for the final AI handoff.

A future benchmark should deliberately ask ChatGPT/Codex to reconstruct one approved Workshop image **without access to its implementation XAML**, then compare the result against both the image and the eventual Workshop implementation.

## Product implication

This benchmark is not just a test of the AI.

It is a test of whether **WinUI Forge exposes enough semantic control for an AI to reproduce a design well**.

If the AI repeatedly fails at the same category, the response may be:

- improve AI instructions;
- add a higher-level Forge authoring primitive;
- expose missing layout metadata;
- add a better inspector/editor;
- add measurement/reference overlays;
- add design-token inference;
- add a visual correction workflow.

The benchmark therefore becomes a feedback loop for product development.


## WinUI Gallery as benchmark guidance

The benchmark may use the Microsoft WinUI Gallery sample catalog as a generic knowledge source for idiomatic WinUI 3 control and layout patterns.

Reference:

~~~text
microsoft/WinUI-Gallery
catalog/windows-samples.json
~~~

This is especially useful when the AI must decide between alternative WinUI structures such as Grid vs StackPanel, fixed vs star sizing, NavigationView/CommandBar patterns, adaptive states, or standard Fluent resource usage.

The Gallery catalog is **guidance, not target leakage**.

If a benchmark reference image comes directly from WinUI Gallery, the corresponding Gallery implementation/sample must be withheld from the AI during the first-pass reconstruction. The point is to measure image → structured WinUI reasoning, not source-code retrieval.

See [WINUI_GALLERY_REFERENCE.md](WINUI_GALLERY_REFERENCE.md).
