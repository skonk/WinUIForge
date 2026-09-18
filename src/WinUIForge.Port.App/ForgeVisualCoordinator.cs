using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIForge.Port.Core;
using Windows.Foundation;

namespace WinUIForge.Port.App;

internal sealed class ForgeVisualCoordinator
{
    readonly Dictionary<string, FrameworkElement> visualByName = new(StringComparer.Ordinal);
    readonly Dictionary<FrameworkElement, ForgeXamlElement> sourceByVisual = new();

    public ForgeXamlDocument? Document { get; private set; }

    public UIElement? Root { get; private set; }

    public int MappedCount => visualByName.Count;

    public IReadOnlyDictionary<string, FrameworkElement> VisualsByName => visualByName;

    public void Initialize(ForgeXamlDocument document, UIElement root)
    {
        Document = document;
        Root = root;
        visualByName.Clear();
        sourceByVisual.Clear();

        Register(root);
    }

    public bool TryGetVisual(string? name, out FrameworkElement element)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            element = null!;
            return false;
        }

        return visualByName.TryGetValue(name, out element!);
    }

    public bool TryGetSource(FrameworkElement element, out ForgeXamlElement source) =>
        sourceByVisual.TryGetValue(element, out source!);

    public ForgeXamlElement? FindDeepestAuthoredAtPoint(UIElement coordinateRoot, Point point, out string hitPath)
    {
        hitPath = string.Empty;
        if (Document is null) return null;

        var hits = new List<AuthoredBoundsHit>();

        foreach (var pair in visualByName)
        {
            var source = Document.FindByName(pair.Key);
            if (source is null) continue;

            var visual = pair.Value;
            if (visual.ActualWidth <= 0 || visual.ActualHeight <= 0) continue;

            try
            {
                var transform = visual.TransformToVisual(coordinateRoot);
                var bounds = transform.TransformBounds(
                    new Rect(0, 0, visual.ActualWidth, visual.ActualHeight));

                if (!bounds.Contains(point)) continue;

                hits.Add(new(
                    source,
                    visual,
                    bounds,
                    Math.Max(1, bounds.Width) * Math.Max(1, bounds.Height)));
            }
            catch
            {
                // Visual can be transiently disconnected during a re-render.
            }
        }

        var best = hits
            .OrderByDescending(x => x.Source.Depth)
            .ThenBy(x => x.Area)
            .FirstOrDefault();

        hitPath = string.Join(
            " > ",
            hits
                .OrderBy(x => x.Source.Depth)
                .ThenByDescending(x => x.Area)
                .Select(x => x.Source.Name));

        return best?.Source;
    }

    void Register(DependencyObject node)
    {
        if (node is FrameworkElement element &&
            !string.IsNullOrWhiteSpace(element.Name) &&
            Document?.FindByName(element.Name) is { } source)
        {
            visualByName[element.Name] = element;
            sourceByVisual[element] = source;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            Register(VisualTreeHelper.GetChild(node, i));
    }

    sealed record AuthoredBoundsHit(
        ForgeXamlElement Source,
        FrameworkElement Visual,
        Rect Bounds,
        double Area);
}
