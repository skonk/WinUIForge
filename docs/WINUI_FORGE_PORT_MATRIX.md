# WinUI Forge — XAML Studio port matrix

> **Reviewed upstream revision:** \`92267797838d8e619dd26a0b3e6af9ba7944e71c\`  
> **Purpose:** Initial migration inventory. This is a planning matrix, not a claim that every row has already been ported.

## Legend

- **Port** — retain the subsystem and migrate it to WinUI 3.
- **Adapt** — retain the concept/code but reshape it for Forge.
- **Replace** — upstream implementation is tied to UWP or no longer the preferred dependency.
- **Defer** — useful later, not required for the first proof.
- **Drop** — not part of the Forge product.

## Application/platform

| Area | Upstream state | Forge action | Notes |
|---|---|---|---|
| Application project | UWP / AppContainerExe | Replace | Create .NET 10 + Windows App SDK WinUI 3 app shell. |
| XAML namespace | Windows.UI.Xaml.* | Port | Move runtime UI to Microsoft.UI.Xaml.*. |
| WinUI package | WinUI 2 / Microsoft.UI.Xaml 2.8.x | Replace | Use Windows App SDK / WinUI 3. |
| Packaging | UWP package manifest | Adapt | Prefer unpackaged/self-contained development initially. |
| Telemetry/App Center | Present but deprecated | Drop | Do not inherit telemetry for the port. |
| Store engagement | UWP-specific | Drop | Not part of migration proof. |

## Editor

| Area | Upstream state | Forge action | Notes |
|---|---|---|---|
| Monaco.Editor UWP | Existing source editor | Replace | Upstream roadmap proposes WinUIEdit for UWP/WinUI 3/AOT compatibility. |
| Caret/selection mapping | Integrated | Port/Adapt | Preserve source reveal and selection coordination. |
| IntelliSense | Existing | Defer/Port | Full parity is not required for first proof. |
| Breadcrumb navigation | Existing | Port | Useful for XAML element identity/navigation. |

## Parsing and rendering

| Area | Upstream file/subsystem | Forge action | Why |
|---|---|---|---|
| XAML render service | XamlStudio.Toolkit/Services/XamlRenderService/ | **Port first** | Core dynamic XAML rendering pipeline. |
| XAML reader | Windows.UI.Xaml.Markup.XamlReader | Replace API namespace | Use WinUI 3 Microsoft.UI.Xaml.Markup.XamlReader. |
| XML syntax parser | GuiLabs.Language.Xml / parser services | Port initially | Required for source-node mapping and diagnostics. |
| Pre-render transformations | Render-service helpers | Adapt | Reduce brittle string manipulation over time. |
| Resource loading | Folder/resource path | Adapt | Must work with Forge project token/resource layout. |

## XAML ↔ rendered element coordination

| Area | Upstream file/subsystem | Forge action | Notes |
|---|---|---|---|
| XML/runtime map | XamlStudio.Toolkit/Services/XamlXmlTreeCoordinator.cs | **Port first** | One of the most valuable upstream components. |
| DependencyProperty catalog | Coordinator/property helpers | Port | Avoid recreating a tiny hand-maintained property set. |
| Source → preview selection | Coordinator + messages | Port | Required for round-trip authoring. |
| Preview → source selection | Coordinator + document design | Port | Required for click-select workflow. |
| VisualTreeHelper traversal | UWP XAML | Port | Replace namespaces/API differences as needed. |

## Properties / visual tree / states

| Area | Upstream file/subsystem | Forge action | Notes |
|---|---|---|---|
| Live Properties | XamlStudio/Views/Properties.* | **Port/Adapt first** | Existing DependencyProperty inspection/editing is high-value. |
| Property → source persistence | Incomplete upstream | **Forge work** | Runtime-only edits are insufficient; committed edits must update XAML. |
| Visual Tree | Properties tooling | Port | Keep authored/runtime distinction clear. |
| Visual States | Properties tooling | Port later | Strong post-proof feature. |
| Binding debugger | Existing | Defer | Valuable but not required for initial proof. |

## Designer / adorners

| Area | Upstream state | Forge action | Notes |
|---|---|---|---|
| AdornerLayer | Microsoft.Toolkit.Future/Adorners/ | Port/Adapt | Valuable infrastructure; review ownership/API design. |
| Surrounding/highlight adorner | Existing | Port | Useful immediately. |
| ModifySelectorAdorner | Experimental | Adapt | Starting point, not finished designer behaviour. |
| TextBlockEditAdorner | Existing special-case | Adapt | Useful type-specific example. |
| Add mode | Present concept/TODO | Forge work | Build reliable toolbox insertion and source mutation. |
| Modify mode | Experimental | Forge work | Upstream issue #11 documents known gaps. |
| Delete/duplicate | Incomplete | Forge work | Must mutate XAML transactionally. |
| Grid editor | Not complete | Forge work | Direct row/column/star/Auto editing. |
| StackPanel reorder | Not complete | Forge work | Order-based layout semantics. |
| Canvas move | Not complete | Forge work | Positional semantics. |
| Reparenting | Not complete | Forge work | Container-aware source mutation. |

## Documents/workspace

| Area | Upstream state | Forge action | Notes |
|---|---|---|---|
| Multi-document tabs | Existing | Adapt | Map to Forge Screens/Components. |
| Folder support | Existing v2 feature | Port | Useful foundation for .wforge folder projects. |
| Autosave/restore | Existing | Port/Adapt | Align with deterministic source files. |
| Settings | Existing | Adapt | Remove unrelated XAML Studio settings. |

## Forge-specific features to transplant from Prototype 1

| Feature | New implementation direction |
|---|---|
| .wforge project | Redesign around hybrid XAML + sidecar metadata. |
| Semantic IDs | Preserve and map to XAML nodes. |
| AI Contract | Preserve; sidecar metadata is authoritative. |
| Design tokens | Preserve; render through XAML ResourceDictionaries. |
| Sample data | Integrate with XAML Studio data-context tooling. |
| Prototype interactions | Sidecar metadata + preview simulation. |
| Visual contract export | Keep, source structure from actual XAML. |
| SVG overlays | Keep; measure real WinUI rendered elements. |
| Hash manifest | Keep. |
| README-AI.md | Keep and strengthen for Design XAML profile. |
| Change journal | Add from authoring transaction stream. |
| Workshop compatibility | Keep as first dogfood profile. |

## Upstream known gaps relevant to Forge

XAML Studio's own open issues validate the fork choice but also define what remains Forge work.

### Issue #5 — Live Property Panel

Includes source/document synchronization and persistence gaps.

### Issue #11 — Live Modify

Includes click hit-testing, bounding boxes, adorner positioning, reselection, source/dirty synchronization, and more control-specific adorners.

### Issue #34 — v2 Roadmap

Explicitly calls out WinUIEdit, Modern UWP + WinUI 3 support, pre-render refactoring, resource/folder work, and richer properties/adorners.

## First port spike

Minimum dependency cone:

1. modern application bootstrap/project;
2. XamlRenderService;
3. XML parser/model dependencies;
4. XamlXmlTreeCoordinator;
5. enough document model for one XAML string;
6. one preview host;
7. highlight/select adorner;
8. minimal live property inspection;
9. a WinUI-compatible editor or temporary TextBox.

Success means one XAML document can be edited, rendered, selected and property-inspected under WinUI 3.

## Validation policy

Every migration slice should have a build test, behavioural smoke test, fixture XAML document, explicit upstream reference, and no silent fallback to different UI semantics.

The first critical fixture should contain a Grid with pixel/star columns, nested StackPanel, Button, TextBlock, resource reference, and one named element.


## Milestone 2 implementation status — 2026-09-18

| Area | Status | Current Forge implementation |
|---|---|---|
| WinUI 3 app shell | ✅ implemented | .NET 10 / Windows App SDK side-by-side port |
| Dynamic XAML rendering | ✅ implemented | `ForgeRenderService` around WinUI 3 `XamlReader` |
| Last-valid preview | ✅ implemented | failed renders retain previous preview |
| Authored XAML hierarchy | ✅ implemented | `ForgeXamlDocument` tree with parent/children/source ranges |
| XML/runtime coordinator | ✅ first Forge slice | `ForgeVisualCoordinator`, named authored element mapping |
| Preview selection | ✅ runtime validated in Milestone 1 | rendered authored bounds |
| Visual Tree | ✅ implemented | authored hierarchy with synchronized selection |
| Live Properties | ✅ first catalog implemented | curated WinUI DependencyProperty metadata |
| Property → source persistence | ✅ implemented | surgical source attribute edits |
| Source formatting preservation | ✅ core-tested | replace/add/remove without full reserialization |
| Monaco UWP replacement | ✅ implemented | WinUIEdit `0.0.5-prerelease` |
| XML syntax highlighting | ✅ wired | WinUIEdit `HighlightingLanguage = "xml"` |
| UTF-8 ↔ UTF-16 caret mapping | ✅ implemented/tested | `ForgeTextPosition` |
| Visual States | ⏳ later | not part of Milestone 2 |
| Binding debugger | ⏳ later | not part of Milestone 2 |
| Toolbox / Add mode | ⏳ next designer phase | not started |
| drag / resize / reparent | ⏳ next designer phase | not started |
