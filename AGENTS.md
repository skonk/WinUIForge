# WinUI Forge agent instructions

This repository is a fork of \`dotnet/XAMLStudio\` and is being migrated into **WinUI Forge**.

## Repository relationship

- \`origin\` = the WinUI Forge fork.
- \`upstream\` = \`https://github.com/dotnet/XAMLStudio.git\`.
- Upstream baseline for the migration: \`92267797838d8e619dd26a0b3e6af9ba7944e71c\`.
- Active migration branch: \`winui-forge/winui3-port\`.

Do not make WinUI Forge product changes directly on the upstream-tracking \`dev\` branch.

## Product mission

WinUI Forge is a visual WinUI 3 design and interchange environment for humans and AI coding agents.

The key workflow is:

\`\`\`text
AI-generated structured mock-up
    -> human visual review/editing in WinUI Forge
    -> saved real WinUI XAML + Forge metadata
    -> AI consumes exact corrections
    -> implementation in the real application
    -> visual/semantic validation
\`\`\`

## Architectural rules

- Real WinUI XAML is authoritative for normal runtime UI hierarchy and properties.
- Forge sidecar metadata is authoritative for semantic/design information that XAML does not naturally carry well.
- Preserve stable semantic IDs for AI-significant elements.
- Visual edits are not committed until they are represented deterministically in XAML and/or Forge metadata.
- PNG/SVG outputs are derived validation artifacts, not structural source-of-truth.
- Do not silently translate unsupported XAML into materially different UI semantics.

## Migration rules

The first proof is deliberately narrow:

1. .NET 10 / Windows App SDK / WinUI 3 app launches.
2. One XAML document can be edited.
3. WinUI 3 dynamically renders it.
4. A rendered named element maps back to the correct XML/XAML syntax element.
5. Selection/highlight works without rebuilding unrelated UI.
6. At least one DependencyProperty is inspectable.
7. Committing a property edit updates source XAML.
8. Re-render preserves source/preview mapping.

Do not bulk-convert the entire UWP solution before this vertical slice works.

Prefer vertical migration slices with tests over broad namespace replacement.

## Upstream code

Preserve upstream history, attribution, LICENSE and applicable NOTICE information.

Before replacing or deleting an upstream subsystem, identify what product capability it currently provides.

High-value upstream areas include:

- \`XamlStudio.Toolkit/Services/XamlRenderService/\`
- \`XamlStudio.Toolkit/Services/XamlXmlTreeCoordinator.cs\`
- \`XamlStudio/Views/Properties.*\`
- \`XamlStudio/Views/Document.Design.xaml.cs\`
- adorner/highlight infrastructure
- folder/document persistence
- source/preview selection coordination

## Dependencies

- Replace UWP-only dependencies deliberately.
- Prefer maintained WinUI 3 packages.
- The upstream roadmap already identifies WinUIEdit as a likely replacement for Monaco UWP.
- Isolate temporary compatibility shims so they can be removed later.

## Commits and validation

Create a Git commit after each major implementation milestone.

Before publishing a migration milestone:

- restore/build on Windows;
- run available tests;
- run a focused behavioural smoke test;
- verify the migration fixture still renders and maps correctly.

Do not claim GUI behaviour is verified unless it was actually exercised in a Windows UI run or a suitable automated UI test.

## Prototype 1

The existing custom Forge implementation remains in:

\`\`\`text
skonk/JLA3D-Workshop-Suite/WinUIForge
\`\`\`

Treat it as Prototype 1 and a source of Forge-specific concepts/code:

- \`.wforge\` project ideas
- semantic IDs
- AI Contract
- design tokens
- validation
- visual-contract export
- SVG overlays
- manifest hashing
- README-AI
- Workshop compatibility

Do not delete Prototype 1 until the XAML Studio-based implementation reaches equivalent core acceptance gates.

## Reference documents

In this fork:

- \`UPSTREAM.md\`
- \`WINUI_FORGE_MIGRATION.md\`
- \`docs/WINUI_FORGE_PORT_MATRIX.md\`

Canonical product plan currently also exists in:

\`\`\`text
skonk/JLA3D-Workshop-Suite
docs/winui-forge/XAMLSTUDIO_PIVOT_PLAN.md
\`\`\`
