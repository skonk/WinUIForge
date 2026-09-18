# Workshop dashboard benchmark — AI handoff

This case measures image-reference -> structured WinUI authoring for the approved Workshop 2 dashboard target.

## Source policy

Primary visual target:

```text
W2-01-dashboard.png
1672 x 941
```

The first reconstruction must be authored from the approved visual target and documented design intent. Do **not** use Workshop implementation XAML/C# as reconstruction input.

## Structural intent

The authored screen must remain real, editable WinUI XAML.

Major regions:

- shared top Workshop shell / command bar;
- 205 px local dashboard pane;
- flexible center workspace;
- 259 px Project Inspector;
- four KPI/metric cards;
- Recent Activity;
- Recent Projects;
- Quick Actions, top-packed in two columns;
- Worker Status;
- bottom activity/resource console.

The center workspace is the flexible region. Do not replace the layout with a fixed Canvas reconstruction.

## First-pass workflow

1. Open `Screen.xaml` in WinUI Forge.
2. Set the benchmark viewport to 1672 x 941.
3. Load `W2-01-dashboard.png` as the reference overlay.
4. Compare geometry, typography, colour, hierarchy and responsive semantics.
5. Make corrections using Forge authoring controls.
6. Record every meaningful mismatch in `benchmark-report.json` / `benchmark-notes.md`.
7. Classify the mismatch rather than reducing the result to one visual score.

The image remains a validation artifact and must never become part of the authored UI.
