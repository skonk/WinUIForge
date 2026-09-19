# WinUIForge / Forge — Codex Handoff

Date: 2026-09-19
Owner: James
Repository: `skonk/WinUIForge`
Active branch: `winui-forge/m4-benchmark`
Expected remote HEAD at handoff: `f3c4d97508f4f63ae4a88a6eca23f0894a82f55f`
Primary local checkout: `D:\other projects\WinUIForge`

## 1. What this project is

WinUIForge is being evolved from a WinUI/XAML proof/benchmark harness into a practical visual authoring tool for WinUI 3. The current work has two parallel goals:

1. Make Forge itself into a usable development-tool-style application.
2. Use Forge to reconstruct and iterate on Workshop 2 UI screens from reference images, using those screens as demanding real-world tests of Forge's authoring/review workflow.

The user explicitly likes a conventional Windows development-tool layout similar in spirit to VS Code / Visual Studio, but using WinUI 3 and Forge's graphite + orange design language.

## 2. Validation policy — important

GitHub Actions credits are exhausted. Do **not** rely on CI compilation for this work.

All compile/runtime validation must be done locally on James's PC.

Standard local build:

```powershell
cd "D:\other projects\WinUIForge"
.\build-port.ps1 -Configuration Release -Run
```

If a clean build is needed:

```powershell
cd "D:\other projects\WinUIForge"
dotnet clean .\WinUIForge.Port.slnx -c Release
Remove-Item ".\src\WinUIForge.Port.App\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue
.\build-port.ps1 -Configuration Release -Run
```

When builds fail, ask for the actual compiler-error lines rather than acting on the final `dotnet build failed` exception only.

Useful local error command:

```powershell
dotnet build .\WinUIForge.Port.slnx `
  -c Release `
  --no-restore `
  2>&1 |
  Tee-Object "$env:USERPROFILE\Downloads\WinUIForge-build-errors.txt" |
  Select-String -Pattern ": error ","error CS","error MSB","error NETSDK"
```

## 3. Current Forge application state

### Core authoring foundation already implemented

Milestone 3 functionality is established and should be preserved:

- Source XAML is authoritative.
- Live WinUI 3 preview through `XamlReader`/render service.
- Source ↔ runtime ↔ Visual Tree mapping.
- Stable identities for named, keyed and unnamed XAML nodes.
- Property editing.
- Structural authoring: add, delete, reorder, reparent.
- Undo/redo transactions.
- Designer move/resize handles with live feedback.
- StackPanel movement maps to sibling reorder.
- Canvas movement maps to `Canvas.Left`/`Canvas.Top`.
- Grid movement maps to row/column or Margin where appropriate.
- Resize writes Width/Height while preserving relevant Auto behavior.
- Logical viewport controls (default benchmark size 1672 × 941).
- Fit / Fill / 1:1 designer view modes.
- Visual reference overlay.
- Highlight-all semantic overlay.
- `.forge-review.zip` export containing source, sidecar, references, renders, comparison image, geometry, hierarchy, bounds SVG and diagnostics.
- Direct `Open XAML…`, `Reload XAML`, `Save XAML` workflow.
- Visual Tree filtering and `Named only` mode.

### Current shell/UI direction

The self-hosted Forge UI concept was created in:

- `benchmarks/forge-self-host-ui-v1/Screen.xaml`
- `benchmarks/forge-self-host-ui-v1/Screen.forge.json`
- `benchmarks/forge-self-host-ui-v1/README-AI.md`
- `benchmarks/Forge Self UI.forgeproject`

That concept was reviewed inside Forge itself and then implemented into the real shell in `src/WinUIForge.Port.App/MainWindow.cs`.

The intended shell now has:

- native-style `MenuBar` at the top
- left-aligned `CommandBar` with icon + text actions
- slim activity rail
- full-height Projects pane
- full-height Source pane
- live Designer beside Source
- Toolbox / Visual Tree / Inspector pane on the right
- draggable splitters between Projects ↔ Source, Source ↔ Designer, Designer ↔ Tools
- persistent pane visibility and widths
- global status bar

The user is very happy with this direction.

### Activity rail search

The latest branch HEAD (`f3c4d975...`) makes the activity-rail Search button functional instead of a placeholder.

Search behavior:

- ensures Projects pane is visible
- toggles a project search field
- searches visible project files recursively across added project roots
- shows matched XAML, references, Forge metadata/project descriptors, etc.
- clicking Search again closes and clears the search

This change still needs local runtime validation after the handoff if James has not already tested it.

## 4. Projects system

Forge now supports persistent project folders rather than hard-coded Workshop load buttons.

User settings live under:

`%LOCALAPPDATA%\JLA3D\WinUIForge\settings.json`

Persisted state includes:

- Projects pane visible
- Source pane visible
- Inspector/Tools pane visible
- Projects pane width
- Source pane width
- Tools pane width
- selected tools tab
- project folder roots

Project Explorer conventions:

- `.xaml` = UI source, opens in Source + Designer
- image files = reference assets
- `*.forge.json` = semantic sidecars, loaded automatically with matching XAML
- `*.forgeproject` = project descriptor/pairing metadata

Docs:

- `docs/PROJECTS.md`

Workshop project descriptor:

- `benchmarks/Workshop UI.forgeproject`

Forge self-design descriptor:

- `benchmarks/Forge Self UI.forgeproject`

The hard-coded `Load W2 dashboard` and `Load W2 settings` buttons were removed.

## 5. UI authoring conventions

Read and preserve:

- `docs/UI-AUTHORING-CONVENTIONS.md`

Most important rule from the user:

> If a panel contains a single vertical column of peer navigation buttons / action rows / list-like buttons, every row should fill the full container width, with left-aligned content.

Use explicit:

```xml
HorizontalAlignment="Stretch"
HorizontalContentAlignment="Left"
```

Selected/current rows implemented as `Border` should also use `HorizontalAlignment="Stretch"`.

Content-sized buttons are appropriate only for inline/horizontal toolbars or deliberately compact actions.

Panel layout rule:

1. title row
2. explanatory/status row
3. dedicated contextual toolbar row
4. content
5. optional footer/status row

Do not make explanatory copy compete with panel toolbar buttons for the same narrow row.

## 6. Workshop benchmark/UI state

### Dashboard

Directory:

- `benchmarks/workshop-dashboard-v1/Screen.xaml`
- `benchmarks/workshop-dashboard-v1/Screen.forge.json`

Dashboard is considered visually close to accepted.

Recent changes:

- top workspace menu now uses a real WinUI `MenuBar`
- menu headings: Create / Process / Review / Library / Tools / Activity
- this gives standard Windows menu behavior: hover highlights; click opens; after a menu is open, moving across sibling headings switches flyouts automatically
- `LocalDashboardPane` rows (`Recent Overview`, `Recent Jobs`, `Projects`, `Assets`, `Reports`, `Analytics`, `Settings`) now explicitly fill the panel width
- Quick Actions already contain six cards and are explicitly Stretch
- the erroneous branded `CONDUIT` footer was removed

### Storage & Settings

Directory:

- `benchmarks/workshop-storage-settings-v1/Screen.xaml`
- `benchmarks/workshop-storage-settings-v1/Screen.forge.json`

Recent change:

- settings navigation rows now explicitly fill the full navigation panel width, matching the Dashboard convention

This full-width rule should be carried into all future Workshop workspace sidebars/navigation lists.

### Shared Workshop workspace status footer

Both Dashboard and Storage & Settings now use a functional XAML status footer rather than a decorative footer image/branding panel.

Semantic elements include:

- `WorkspaceStatusFooter`
- `HostStatusChip`
- `QueueStatusChip`
- `ActiveTasksChip`
- `StorageStatusChip`
- `SyncStatusText`
- `VersionText`

Current design displays representative values such as:

- Host ready
- Queue healthy
- 3 active tasks
- Storage 31%
- Synced
- Autosave on
- v2.0

Future Workshop implementation should bind these to real shared application state so the same footer remains present across every workspace.

## 7. Forge self-host UI review history

The user has been exporting `.forge-review.zip` packages for the self-host UI and using those to refine Forge from inside Forge.

Recent review conclusions:

- overall development-tool composition is good
- CommandBar should be left aligned — done
- contextual panel icons were too large / clipped — reduced
- activity rail icons were still slightly large — reduced again to 14px in both concept and real shell direction
- Projects panel header text originally collided with its action buttons — reworked into separate title, description/status, toolbar, content and footer rows
- Designer was the most constrained region, so concept side panes were narrowed slightly

Continue using review packages for objective layout checks rather than eyeballing alone.

## 8. Application icon history — do not regress this casually

The app icon took substantial troubleshooting.

Working configuration is intentionally based on the proven Workshop 2 icon mechanism:

`WinUIForge.Port.App.csproj` uses:

```xml
<ApplicationIcon>..\..\assets\workshop-ui\app-icon\Workshop.ico</ApplicationIcon>
```

The app also applies runtime/window/taskbar icons from `MainWindow.cs` using Windows App SDK and Win32 fallback calls.

Important: the icon currently committed on the remote branch is the known-good Workshop 2-format icon blob (`assets/workshop-ui/app-icon/Workshop.ico`, ~68 KB). James later replaced it locally with the unique orange-cube Forge artwork and confirmed it worked.

**Before touching the icon, run `git status --short`.** The unique Forge icon may still exist only as a local modification if James has not committed it yet.

Do not reintroduce the abandoned custom post-build PE resource stamper.

## 9. Local repo / Git workflow cautions

There was a period where James's local branch was far behind remote even though the branch name was correct. Always verify both branch and commit before assuming a test reflects current source:

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
```

To update safely when clean:

```powershell
git fetch origin
git pull --ff-only origin winui-forge/m4-benchmark
```

Do not destroy local changes without checking first, especially the unique Forge ICO.

## 10. Most relevant recent commits

Current handoff HEAD:

- `f3c4d97508f4f63ae4a88a6eca23f0894a82f55f` — functional project search from activity rail
- `eed8b1246a3c30960790afb0f241e9908e4ff9c0` — full-width single-column UI convention
- `54fb53937b63e2dd980f876c32131e5fbc1118c5` — full-width Storage & Settings nav
- `cb7c4c09b8890cd01fa89d3266d35321efb0248a` — shell compile fix for WinUI Thickness constructors
- `2cbcc206696cc923017d64fe3788c2fde5aec294` — shell helpers
- `5a75e379b70b653bc177cac87711a4af5e01d933` — implement Forge-designed development-tool workspace
- `ef5c68dec60ac7971cc6d8e752d1571c288ebe3d` — reduce activity rail icon size in self-host concept
- `4aba64ce538df0460da03ce33e34f6e9eed78442` — stretch Dashboard local navigation rows
- `beaf312d4ff489ee3083a746b95ecccab200240b` — separate Projects labels and toolbar rows in self-host concept
- `bbac2fb045314e3b6638a42bd0ef0807bb106e28` — Workshop Dashboard real MenuBar behavior

## 11. Good next tasks for Codex

Recommended order:

1. **Locally validate current HEAD**, especially project search and the latest real Forge shell.
2. If there are compile/runtime issues, fix them without changing the accepted layout direction.
3. Make the real Forge shell match the accepted self-host concept where any small discrepancies remain.
4. Continue using Forge itself to design/refine future Forge UI changes before hard-coding them.
5. Expand Workshop benchmark coverage to additional workspaces and apply the full-width single-column rule consistently.
6. Move the shared Workshop workspace footer from representative design data toward actual live host/queue/task/storage/sync/autosave/version state when working in the Workshop repo.
7. Improve `build-port.ps1` so local compiler failures automatically print compact compiler-error lines, similar to Workshop 2's build scripts.
8. Eventually implement a real workspace search UX beyond the initial project-file filter if useful (search within XAML/content is not yet the same as project filename/path search).

## 12. User preferences / design intent

- Graphite/dark charcoal base with bright orange accent.
- Proper Windows/WinUI interaction conventions are preferred over custom imitations when native controls provide the expected behavior.
- Standard Windows MenuBar behavior is specifically liked.
- Development-tool density/layout is welcome; avoid giant consumer-app controls.
- Source and Designer should stay side-by-side so editing either gives immediate feedback in the other.
- Projects should be a full-height independent pane.
- Panel-specific controls should live in the relevant panel.
- Global app controls should live in MenuBar/CommandBar.
- Use Forge and review packages as part of the development process rather than treating Forge only as the final product.

## 13. Immediate startup checklist for Codex

```powershell
cd "D:\other projects\WinUIForge"
git branch --show-current
git rev-parse HEAD
git status --short
```

Expected branch:

`winui-forge/m4-benchmark`

Expected remote HEAD at handoff:

`f3c4d97508f4f63ae4a88a6eca23f0894a82f55f`

Then inspect:

- `src/WinUIForge.Port.App/MainWindow.cs`
- `src/WinUIForge.Port.App/ForgeUserSettings.cs`
- `docs/PROJECTS.md`
- `docs/UI-AUTHORING-CONVENTIONS.md`
- `benchmarks/forge-self-host-ui-v1/Screen.xaml`
- `benchmarks/workshop-dashboard-v1/Screen.xaml`
- `benchmarks/workshop-storage-settings-v1/Screen.xaml`

Build locally and continue from the current state rather than reconstructing earlier milestones.
