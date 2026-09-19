# WinUI Forge UI authoring conventions

These conventions apply to Workshop benchmark/workspace XAML and to Forge-authored application concepts.

## Single-column navigation and action stacks

When a container presents one vertical column of peer navigation items or actions, every row should explicitly fill the available container width.

Use:

```xml
<Button HorizontalAlignment="Stretch"
        HorizontalContentAlignment="Left"
        ... />
```

For selected/current rows implemented as Borders, also set:

```xml
<Border HorizontalAlignment="Stretch" ...>
```

This avoids ragged right edges and inconsistent hit targets as labels vary in length.

Use content-sized buttons only where actions are intentionally inline, grouped in a horizontal toolbar, or semantically compact.

## Panel-local toolbars

Panel headings, explanatory copy, and action buttons should not compete for the same narrow horizontal row. Prefer:

1. title row
2. descriptive/status row
3. dedicated contextual toolbar row
4. panel content
5. optional footer/status row

## Shell commands

Global application actions belong in the menu bar or top CommandBar. Panel-specific actions belong inside the panel they affect.
