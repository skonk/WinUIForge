# Forge Projects

Forge Projects replace benchmark-specific load buttons with normal folders on disk.

## Project Explorer

Use **Projects → Add folder…** to add any folder containing Forge UI work. Added folders persist in:

`%LOCALAPPDATA%\JLA3D\WinUIForge\settings.json`

The explorer distinguishes:

- `◇ UI` — XAML files. Selecting one opens it in Source + Designer.
- `▣ REF` — PNG/JPG/BMP/WebP visual references.
- `· META` — `*.forge.json` semantic sidecars, loaded automatically with matching XAML.
- `◆ PROJECT` — `*.forgeproject` pairing metadata.

Project folders remain ordinary filesystem folders; Forge does not copy them into a proprietary store.

## .forgeproject

A `.forgeproject` file in the project root can pair XAML screens with differently named reference images:

```json
{
  "schema": 1,
  "name": "Workshop UI",
  "screens": [
    {
      "name": "Dashboard",
      "xaml": "workshop-dashboard-v1/Screen.xaml",
      "sidecar": "workshop-dashboard-v1/Screen.forge.json",
      "reference": "workshop-dashboard-v1/W2-01-dashboard.png",
      "viewport": {
        "width": 1672,
        "height": 941
      }
    }
  ]
}
```

Selecting either the paired XAML or reference image loads both when both files are present. If the reference has not been added yet, Forge still loads the XAML and reports the missing reference without failing.

If there is no descriptor, Forge also recognizes a sibling image with the same stem as a XAML file.

## Workshop UI migration

`benchmarks/Workshop UI.forgeproject` now describes the existing Dashboard and Storage & Settings benchmark folders. On a live repository build, Forge automatically adds the `benchmarks` folder as the initial project the first time it starts with no saved project folders.

Add the approved concept/reference PNGs beside their matching benchmark XAML using the filenames in the descriptor and they will pair automatically.

## Workspace persistence

Forge also persists:

- Source / Projects pane visibility.
- Inspector / Tools pane visibility.
- Source / Projects pane width.
- Inspector / Tools pane width.
- selected Tools tab.
- project folder roots.

The source and tools panes are separated from the Designer by draggable splitters.
