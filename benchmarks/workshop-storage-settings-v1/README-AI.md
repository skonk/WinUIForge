# Workshop Storage & Settings benchmark — AI handoff

This is the first Stage C dense-production benchmark for WinUI Forge.

## Visual target

```text
W2-14-storage-settings.png
1672 x 941
```

The first reconstruction is authored from the approved mockup only. Do not use Workshop implementation XAML/C# as reconstruction input.

## Structural intent

Major semantic regions:

- full-width Workshop shell with window-centred navigation;
- 188 px settings navigation rail;
- flexible center settings workspace;
- 300 px Storage Location inspector;
- Storage Overview with total-usage + four category cards;
- Key Locations row;
- Application Settings three-column form surface;
- Storage Management drive table;
- Storage Insights;
- console / active-task strip;
- branded footer.

## What this benchmark is meant to stress

- nested Auto/star Grid sizing;
- form labels + toggles + ComboBoxes + TextBoxes;
- mixed information density;
- avoiding unnecessary ScrollViewer use;
- table/list density;
- inspector sizing;
- maintaining editability in a very dense screen.

## Review workflow

1. Load `Screen.xaml` in Forge.
2. Set viewport to 1672 x 941.
3. Load `W2-14-storage-settings.png` as the reference.
4. Use Highlight All and 50% overlay for first inspection.
5. Export a `.forge-review.zip`.
6. Classify mismatches before changing structure.
7. Prefer semantic layout corrections over pixel-positioned Canvas fixes.
