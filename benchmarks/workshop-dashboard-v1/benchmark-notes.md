# Workshop dashboard v1 — benchmark notes

> Reference: `W2-01-dashboard.png`  
> Viewport: **1672 x 941**  
> First pass: authored from the approved visual target/design contract only.  
> Workshop implementation source was intentionally excluded.

## First-pass structural hypothesis

The first reconstruction uses:

- a fixed-height shared command bar;
- a three-column dashboard work area:
  - 205 px local pane;
  - flexible center;
  - 259 px inspector;
- a fixed-height bottom activity/resource console;
- four equal KPI cards;
- a 3:2 split for the lower center content;
- Recent Activity above Recent Projects;
- Quick Actions above Worker Status;
- Quick Actions packed from the top in a 2 x 2 grid.

This is intentionally a semantic WinUI reconstruction rather than pixel-positioned Canvas artwork.

## Runtime comparison checklist

Record corrections under the benchmark taxonomy.

### Geometry interpretation

- [ ] top command-bar height / alignment;
- [ ] 205 px local pane;
- [ ] 259 px inspector;
- [ ] KPI card height and inter-card gaps;
- [ ] Recent Activity / Recent Projects proportions;
- [ ] Quick Actions width, tile size and top alignment;
- [ ] Worker Status height;
- [ ] bottom console height.

### Typography

- [ ] dashboard heading size/weight;
- [ ] metric hierarchy;
- [ ] panel heading sizes;
- [ ] dense body text sizing;
- [ ] inspector hierarchy.

### Colour / appearance

- [ ] graphite surface hierarchy;
- [ ] orange accent intensity;
- [ ] border contrast;
- [ ] selected/local-pane treatment;
- [ ] status colours.

### Structural quality

- [ ] center remains responsive/star-sized;
- [ ] no unnecessary Canvas positioning;
- [ ] repeated card structures remain understandable;
- [ ] important elements remain selectable/editable.

### Forge limitations found

Add an entry here whenever the desired correction is reasonable WinUI but awkward/impossible in Forge.

## Corrections

None yet — awaiting first runtime overlay comparison.
