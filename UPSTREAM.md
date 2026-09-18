# Upstream provenance

WinUI Forge is derived from the open-source XAML Studio project.

## Upstream

- Repository: https://github.com/dotnet/XAMLStudio
- Project: XAML Studio v2
- Upstream branch at fork: \`dev\`
- Baseline commit: \`92267797838d8e619dd26a0b3e6af9ba7944e71c\`
- Baseline reviewed: 2026-09-18
- License: MIT
- Copyright: .NET Foundation and Contributors

The upstream LICENSE and applicable third-party notices are retained.

## Fork purpose

This fork is being developed into **WinUI Forge**, a visual WinUI 3 design and interchange environment for humans and AI coding agents.

Expected major changes include:

- migration from UWP/WinUI 2 to .NET 10 + Windows App SDK / WinUI 3;
- replacement of UWP-only editor/dependencies;
- reliable XAML source ↔ rendered-element editing;
- visual drag/drop authoring;
- Forge Screens/Components/tokens;
- semantic IDs and AI Contract metadata;
- AI-generated editable mock-up projects;
- deterministic round-trip handoff to ChatGPT/Codex;
- visual-contract/reference export and validation.

## Remote policy

Local clones should use:

~~~text
origin   = this WinUI Forge fork
upstream = https://github.com/dotnet/XAMLStudio.git
~~~

Do not force-push upstream history.

## Attribution

This fork does not imply sponsorship or endorsement by Microsoft, the .NET Foundation, or the upstream XAML Studio maintainers.

Product branding may be replaced while required license/copyright notices remain intact.
