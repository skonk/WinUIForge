using WinUIForge.Port.Core;

namespace WinUIForge.Port.App;

internal sealed record ForgeLayoutContext(
    string ParentLabel,
    string Mode,
    IReadOnlyList<string> Notes);

internal static class ForgeLayoutAdvisor
{
    public static ForgeLayoutContext Describe(ForgeXamlElement element)
    {
        if (element.Parent is null)
        {
            return new(
                "(document root)",
                "Root",
                ["The root element is sized by the Forge preview host."]);
        }

        var parent = element.Parent;
        var notes = new List<string>();

        switch (parent.TypeName)
        {
            case "StackPanel":
            {
                var orientation = Attribute(parent, "Orientation") ?? "Vertical";
                notes.Add($"{orientation} StackPanel controls child ordering.");
                notes.Add(
                    orientation == "Vertical"
                        ? "Dragging vertically reorders siblings; horizontal placement comes from alignment, width and margin."
                        : "Dragging horizontally reorders siblings; vertical placement comes from alignment, height and margin.");
                break;
            }
            case "Grid":
                notes.Add(
                    $"Grid placement: row {Attribute(element, "Grid.Row") ?? "0"}, column {Attribute(element, "Grid.Column") ?? "0"}, " +
                    $"row span {Attribute(element, "Grid.RowSpan") ?? "1"}, column span {Attribute(element, "Grid.ColumnSpan") ?? "1"}.");
                notes.Add("Dragging can move between Grid cells; smaller moves use Margin inside the current cell.");
                break;
            case "Canvas":
                notes.Add(
                    $"Canvas position: Left {Attribute(element, "Canvas.Left") ?? "0"}, Top {Attribute(element, "Canvas.Top") ?? "0"}.");
                notes.Add("Dragging maps directly to Canvas.Left / Canvas.Top.");
                break;
            case "Border":
                notes.Add("Border is a single-child container; its child is positioned by alignment, margin and available size.");
                break;
            case "ScrollViewer":
                notes.Add("ScrollViewer is a single-content host. Child layout is determined by the scrollable viewport and alignment.");
                break;
            default:
                notes.Add($"Parent '{parent.TypeName}' is not yet a specialized Forge layout adapter; movement falls back to Margin.");
                break;
        }

        if (Attribute(element, "Width") is not null &&
            Attribute(element, "HorizontalAlignment") is null)
        {
            notes.Add("Explicit Width is set while HorizontalAlignment is inherited/default. In WinUI this can change placement as well as size.");
        }

        if (Attribute(element, "Height") is not null &&
            Attribute(element, "VerticalAlignment") is null)
        {
            notes.Add("Explicit Height is set while VerticalAlignment is inherited/default. Resize behaviour follows WinUI layout semantics.");
        }

        return new(parent.DisplayName, parent.TypeName, notes);
    }

    static string? Attribute(ForgeXamlElement element, string name) =>
        element.Attributes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))?.Value;
}
