using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIForge.Port.Core;

namespace WinUIForge.Port.App;

/// <summary>
/// WinUI 3 adaptation of XAML Studio's XamlXmlTreeCoordinator.
///
/// It maintains a bidirectional mapping between authored XAML elements and runtime
/// DependencyObjects. Named elements are matched first. Remaining visual elements are
/// matched by authored hierarchy, type and document order, which allows unnamed
/// controls to participate in the Visual Tree and selection workflow.
/// </summary>
internal sealed class ForgeXamlTreeCoordinator
{
    readonly Dictionary<string, DependencyObject> runtimeBySourceId =
        new(StringComparer.Ordinal);
    readonly Dictionary<DependencyObject, ForgeXamlElement> sourceByRuntime =
        new(ReferenceEqualityComparer.Instance);

    ForgeXamlDocument? document;
    DependencyObject? runtimeRoot;

    public int Count => runtimeBySourceId.Count;

    public void Initialize(ForgeXamlDocument sourceDocument, DependencyObject root)
    {
        document = sourceDocument;
        runtimeRoot = root;
        runtimeBySourceId.Clear();
        sourceByRuntime.Clear();

        var visuals = EnumerateVisualTree(root).ToArray();

        // XAML Studio gives x:Name/Name the strongest confidence. Do the same first.
        var namedVisuals = visuals
            .OfType<FrameworkElement>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var source in document.Elements.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (namedVisuals.TryGetValue(source.Name!, out var runtime) &&
                TypeNamesMatch(runtime, source))
            {
                AddMapping(source, runtime);
            }
        }

        // Map the root even when it is unnamed.
        if (!runtimeBySourceId.ContainsKey(document.RootElement.Id) &&
            IsVisualSource(document.RootElement) &&
            TypeNamesMatch(root, document.RootElement))
        {
            AddMapping(document.RootElement, root);
        }

        // Then follow authored hierarchy and type order for unnamed elements.
        foreach (var source in document.Elements.OrderBy(x => x.Depth).ThenBy(x => x.StartIndex))
        {
            if (runtimeBySourceId.ContainsKey(source.Id) || !IsVisualSource(source))
                continue;

            var searchRoot = FindNearestMappedAncestor(source) ?? root;
            var candidate = EnumerateVisualTree(searchRoot)
                .Where(x => !sourceByRuntime.ContainsKey(x))
                .FirstOrDefault(x => TypeNamesMatch(x, source));

            if (candidate is not null)
                AddMapping(source, candidate);
        }
    }

    public bool TryGetRuntimeElement(string sourceId, out DependencyObject element) =>
        runtimeBySourceId.TryGetValue(sourceId, out element!);

    public bool TryGetRuntimeElement(
        ForgeXamlElement source,
        out DependencyObject element) =>
        TryGetRuntimeElement(source.Id, out element);

    public bool TryGetSourceElement(
        DependencyObject runtime,
        out ForgeXamlElement source) =>
        sourceByRuntime.TryGetValue(runtime, out source!);

    public IReadOnlyList<(ForgeXamlElement Source, DependencyObject Runtime)> GetMappings()
    {
        if (document is null) return [];

        return document.Elements
            .Where(x => runtimeBySourceId.ContainsKey(x.Id))
            .Select(x => (x, runtimeBySourceId[x.Id]))
            .ToArray();
    }

    public IEnumerable<(ForgeXamlElement Source, FrameworkElement Runtime)> GetFrameworkMappings() =>
        GetMappings()
            .Where(x => x.Runtime is FrameworkElement)
            .Select(x => (x.Source, (FrameworkElement)x.Runtime));

    DependencyObject? FindNearestMappedAncestor(ForgeXamlElement source)
    {
        if (document is null) return null;

        var parentId = source.ParentId;
        while (parentId is not null)
        {
            if (runtimeBySourceId.TryGetValue(parentId, out var runtime))
                return runtime;

            var parent = document.FindById(parentId);
            parentId = parent?.ParentId;
        }

        return null;
    }

    void AddMapping(ForgeXamlElement source, DependencyObject runtime)
    {
        if (runtimeBySourceId.ContainsKey(source.Id) || sourceByRuntime.ContainsKey(runtime))
            return;

        runtimeBySourceId[source.Id] = runtime;
        sourceByRuntime[runtime] = source;
    }

    static bool TypeNamesMatch(DependencyObject runtime, ForgeXamlElement source) =>
        string.Equals(
            runtime.GetType().Name,
            source.TypeName,
            StringComparison.Ordinal);

    static bool IsVisualSource(ForgeXamlElement source)
    {
        if (source.IsPropertyElement) return false;

        return source.TypeName switch
        {
            "ResourceDictionary" => false,
            "SolidColorBrush" => false,
            "LinearGradientBrush" => false,
            "RadialGradientBrush" => false,
            "GradientStop" => false,
            "Style" => false,
            "Setter" => false,
            "DataTemplate" => false,
            "ControlTemplate" => false,
            "ColumnDefinition" => false,
            "RowDefinition" => false,
            _ => true
        };
    }

    static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);

        queue.Enqueue(root);
        seen.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            int count;
            try
            {
                count = VisualTreeHelper.GetChildrenCount(current);
            }
            catch
            {
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                DependencyObject child;
                try
                {
                    child = VisualTreeHelper.GetChild(current, i);
                }
                catch
                {
                    continue;
                }

                if (seen.Add(child))
                    queue.Enqueue(child);
            }
        }
    }
}
