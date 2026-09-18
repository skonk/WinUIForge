# Workshop Storage & Settings v1 — benchmark notes

> Reference: `W2-14-storage-settings.png`  
> Viewport: **1672 x 941**  
> Stage: **C — dense production reference**  
> Implementation source deliberately excluded from the first pass.

## First-pass structural hypothesis

The initial reconstruction uses:

- 60 px global shell;
- 188 px left settings rail;
- flexible center workspace;
- 300 px storage inspector;
- three center rows:
  - Storage Overview;
  - Application Settings;
  - Storage Management;
- fixed console/tasks strip;
- fixed branded footer.

The screen is intentionally built from normal WinUI Grid/StackPanel/Border/control semantics rather than Canvas positioning.

## First runtime review checklist

### Major geometry

- [ ] left rail width and vertical item rhythm;
- [ ] inspector width;
- [ ] Storage Overview height;
- [ ] total-usage card width relative to the four category cards;
- [ ] Key Locations row height;
- [ ] Application Settings height;
- [ ] General / Behavior / Quick Actions column proportions;
- [ ] Storage Management / Storage Insights split;
- [ ] console/tasks strip height;
- [ ] footer height.

### Control semantics

- [ ] toggles align to a consistent right edge;
- [ ] ComboBoxes/TextBoxes do not create unintended row growth;
- [ ] no unnecessary ScrollViewer is introduced;
- [ ] storage table remains dense and readable;
- [ ] inspector controls fit without crowding.

### Appearance

- [ ] typography hierarchy;
- [ ] icon/glyph scale;
- [ ] orange accent use;
- [ ] muted/border contrast;
- [ ] selected left-nav treatment;
- [ ] table selected-row treatment.

## Forge limitations found

None yet — awaiting first runtime review.

## Corrections

None yet.
