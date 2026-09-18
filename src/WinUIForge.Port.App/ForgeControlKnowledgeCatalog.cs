using System.Text.Json;

namespace WinUIForge.Port.App;

internal sealed class ForgeControlKnowledgeCatalog
{
    public const string GalleryRepository = "microsoft/WinUI-Gallery";
    public const string GalleryRevision = "abb8cb4cef04a5080f5c0396f67a7ec502b36179";

    public IReadOnlyList<ForgeControlKnowledge> Controls { get; }

    ForgeControlKnowledgeCatalog(IReadOnlyList<ForgeControlKnowledge> controls)
    {
        Controls = controls;
    }

    public static ForgeControlKnowledgeCatalog Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "gallery-toolbox.json");
        if (!File.Exists(path))
            return new ForgeControlKnowledgeCatalog(Fallback());

        try
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<ForgeControlCatalogFile>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (model?.Controls is { Count: > 0 })
                return new ForgeControlKnowledgeCatalog(model.Controls);
        }
        catch
        {
        }

        return new ForgeControlKnowledgeCatalog(Fallback());
    }

    static IReadOnlyList<ForgeControlKnowledge> Fallback() =>
    [
        new("Grid", "Grid", "Two-dimensional row/column layout container.", "grid", ["StackPanel", "Canvas"],
            "<Grid x:Name=\"{name}\" MinWidth=\"160\" MinHeight=\"100\">\n</Grid>"),
        new("StackPanel", "StackPanel", "Ordered vertical or horizontal layout container.", "stackpanel", ["Grid"],
            "<StackPanel x:Name=\"{name}\" Spacing=\"8\">\n</StackPanel>"),
        new("Border", "Border", "Single-child surface with padding, stroke and corner radius.", "border", ["Grid"],
            "<Border x:Name=\"{name}\" Padding=\"12\" BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" BorderThickness=\"1\" CornerRadius=\"8\">\n</Border>"),
        new("TextBlock", "TextBlock", "Read-only text content.", "textblock", ["RichTextBlock"],
            "<TextBlock x:Name=\"{name}\" Text=\"Text\" />"),
        new("Button", "Button", "Standard action control.", "button", ["ToggleButton", "AppBarButton"],
            "<Button x:Name=\"{name}\" Content=\"Button\" />"),
        new("TextBox", "TextBox", "Editable text input.", "textbox", ["RichEditBox"],
            "<TextBox x:Name=\"{name}\" PlaceholderText=\"Text\" />"),
        new("Image", "Image", "Displays bitmap or vector image content.", "image", [],
            "<Image x:Name=\"{name}\" Width=\"160\" Height=\"100\" Stretch=\"Uniform\" />"),
        new("Canvas", "Canvas", "Absolute-positioning layout surface.", "canvas", ["Grid"],
            "<Canvas x:Name=\"{name}\" Width=\"320\" Height=\"200\">\n</Canvas>")
    ];
}

internal sealed record ForgeControlCatalogFile(
    string SourceRepository,
    string SourceRevision,
    List<ForgeControlKnowledge> Controls);

internal sealed record ForgeControlKnowledge(
    string TypeName,
    string Title,
    string Description,
    string GalleryId,
    IReadOnlyList<string> RelatedControls,
    string XamlTemplate)
{
    public string CreateXaml(string name) =>
        XamlTemplate.Replace("{name}", name, StringComparison.Ordinal);

    public override string ToString() => Title;
}
