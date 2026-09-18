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

## Second runtime comparison — observations

The second-pass 50% overlay shows a substantial improvement in structural fidelity:

- the five-card KPI row now reads as the same composition as the target;
- the three parallel lower content regions are in the correct structural relationship;
- the navigation and inspector rails are much closer to the target proportions;
- the console/footer split now matches the target hierarchy;
- the remaining visual mismatch is no longer dominated by an incorrect layout model.

The largest remaining differences are now:

- real thumbnail / preview imagery versus semantic placeholders;
- iconography (current reconstruction uses lightweight text/glyph substitutes);
- fine typography and text density;
- local spacing/padding and a few panel-height/proportion differences;
- finer inspector content/detail matching.

This is the desired benchmark transition: corrections are becoming **tuning**, not hierarchy reconstruction.

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

### Rebuild-free XAML iteration

The benchmark also confirmed that rebuilding Forge between every XAML revision would unnecessarily recreate the same slow loop the tool is intended to replace.

Benchmark-driven Forge change:

- **Open XAML…** loads a real file directly;
- **Reload XAML** rereads external/AI edits;
- **Save XAML** persists visual Forge corrections;
- the Workshop benchmark prefers the live repository `Screen.xaml` over its packaged fallback.

This allows:

~~~text
AI edits XAML
    -> git pull
    -> Reload XAML
    -> visual comparison/correction
    -> Save XAML
~~~

without rebuilding the Workshop application and, for normal XAML iterations, without rebuilding Forge.



## First exported review-package analysis

The first `.forge-review.zip` successfully provided:

- clean WinUI render;
- normalized 1672 x 941 reference;
- highlighted render;
- 50% comparison;
- exact named runtime bounds;
- authored hierarchy;
- environment/render diagnostics.

This immediately corrected one earlier overlay interpretation: the approved reference contains **four KPI cards**, not five. The semi-transparent overlay had made the extra authored Workers Online card appear plausible. The clean normalized reference removed that ambiguity.

### Measured major target geometry

The package allowed the following target structure to be inferred in the shared 1672 x 941 coordinate space:

- global top row: approximately **60 px**;
- command surface: approximately **84 px**;
- dashboard workspace begins at approximately **y = 144**;
- navigation rail: approximately **210 px**;
- inspector rail: approximately **280 px**;
- main dashboard content begins at approximately **x = 218 / y = 152** after padding;
- four-card KPI row: approximately **114 px** high;
- lower content columns are approximately **408 / 360 / 378 px** before local border variation;
- Quick Actions is approximately **322 px** high;
- Worker Status is the shorter remainder beneath it;
- console begins around **y = 763** and is approximately **110 px** high;
- branded footer occupies the final approximately **68 px**.

These measurements drove the third structural pass.

### Review-package issue found

The first package reported `case: Screen`, omitted `Screen.forge.json`, and therefore exported `semanticRegionCount: 0`. Highlight All still worked through the named-container fallback, but the semantic sidecar is preferable.

Forge now packages the benchmark sidecar with the application so future exports retain the benchmark case and semantic keys.


## Corrections

Second pass has completed its first runtime overlay review. Iterative visual tuning is active.


## Dashboard benchmark acceptance

The dashboard case is now accepted as a successful Stage B benchmark.

The final pass deliberately stops short of pixel-identical reproduction. The remaining visible differences are predominantly icon/glyph artwork, minor typography scale and small decorative spacing differences rather than incorrect WinUI structure.

The global top navigation received one final geometry correction: it is centred against the full application window rather than the residual space between the workspace title and the window controls.

Further dashboard work should only resume if a later Forge change causes a regression. The next benchmark should use a materially different and more difficult screen rather than over-fitting this case.
