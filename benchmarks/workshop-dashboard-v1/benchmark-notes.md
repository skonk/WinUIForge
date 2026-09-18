# Workshop dashboard v1 — benchmark notes

> Reference: `W2-01-dashboard.png`  
> Viewport: **1672 x 941**  
> Reconstruction policy: approved visual target/design contract only.  
> Workshop implementation source was intentionally excluded.

## First runtime comparison — findings

The first pass rendered successfully as real WinUI and preserved the intended responsive centre workspace, but the 50% overlay exposed several structural misses.

### Geometry interpretation

- **Top shell:** the target uses a taller two-level visual hierarchy: global navigation plus a contextual dashboard command area. First pass compressed too much into one 54 px row.
- **Left pane:** overlay indicates a pane closer to **224 px**, with application/navigation commands rather than the simplified Overview/Focus treatment used initially.
- **Right inspector:** **259 px** was already close and is retained.
- **Metrics:** target has **five** KPI cards, not four:
  - Active Projects;
  - Queued Jobs;
  - Workers Online;
  - Assets Processed;
  - Worker Health.
- **Main content:** target is three parallel columns:
  - Recent Activity;
  - Recent Projects;
  - Quick Actions / Worker Status.
  First pass incorrectly stacked Recent Projects below Recent Activity.
- **Bottom region:** target combines a console/resource strip with a branded footer; first pass collapsed those into one strip.
- **Inspector:** target contains a substantial visual preview and richer project metadata.

### Structural quality

The overlay confirmed that the target can still be represented with normal WinUI layout semantics:

- fixed navigation and inspector rails;
- star-sized centre workspace;
- Grid-based KPI row;
- three responsive content columns;
- Grid/StackPanel composition rather than Canvas positioning.

No screenshot-as-UI shortcut is required.

## Second-pass changes

- [x] left pane changed from 205 px to 224 px;
- [x] global navigation shell added;
- [x] contextual dashboard command row added;
- [x] five KPI cards implemented;
- [x] Assets Processed and Worker Health added;
- [x] Recent Activity / Recent Projects / utility region changed to three parallel columns;
- [x] Quick Actions kept as 2 x 2 top-packed actions;
- [x] Worker Status expanded beneath Quick Actions;
- [x] Project Inspector expanded with visual-preview placeholder and richer details;
- [x] bottom console separated from branded JLA3D / CONDUIT footer.

## Second runtime comparison checklist

### Geometry

- [ ] global top-nav height;
- [ ] contextual dashboard toolbar height;
- [ ] 224 px navigation rail;
- [ ] 259 px inspector rail;
- [ ] five KPI card widths/heights;
- [ ] three lower content-column proportions;
- [ ] Quick Actions / Worker Status split;
- [ ] console/footer split.

### Typography / appearance

- [ ] production-dashboard heading size;
- [ ] dense activity/project text;
- [ ] accent saturation;
- [ ] border contrast;
- [ ] navigation selected state;
- [ ] footer branding scale.

### Remaining expected visual differences

The reference contains real project thumbnails/preview imagery. The benchmark second pass uses semantic placeholder panels because implementation assets were intentionally excluded from reconstruction input. That difference should be classified as **content asset unavailable**, not as a WinUI layout failure.

## Forge limitations / product findings

### Dense Visual Tree navigation

The second-pass dashboard contains hundreds of authored XAML nodes. The original flat Visual Tree remained technically correct but became inefficient for production-density correction because unnamed Grid/property nodes overwhelmed the meaningful semantic regions.

Benchmark-driven Forge change:

- add a Visual Tree text filter;
- add **Named only** mode;
- default the Workshop benchmark to Named only;
- preserve the full tree for low-level source work.

Implemented in Forge commit `4f03754160569e5ede999b34e67d274f4ac6cd1a`.

This is classified as an **editability / Forge authoring limitation**, not an AI reconstruction error.

The earlier move/resize feedback issues were fixed before this comparison pass.

## Corrections

Second pass is ready for runtime overlay review.
