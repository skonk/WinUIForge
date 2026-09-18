using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using WinUIForge.Port.Core;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace WinUIForge.Port.App;

public sealed class MainWindow : Window
{
    readonly TextBox sourceEditor = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 13,
        Padding = new Thickness(12),
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    readonly TreeView visualTree = new()
    {
        SelectionMode = TreeViewSelectionMode.Single
    };

    readonly Grid previewStage = new();
    readonly ContentControl previewContent = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    readonly Canvas selectionLayer = new() { IsHitTestVisible = false };

    readonly TextBlock selectedType = new() { FontSize = 18 };
    readonly TextBlock selectedName = new() { TextWrapping = TextWrapping.Wrap };
    readonly StackPanel propertyForm = new() { Spacing = 8 };
    readonly TextBlock diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.NoWrap };

    readonly ForgeXamlRenderService renderService = new();
    readonly ForgeXamlTreeCoordinator coordinator = new();
    readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };
    readonly Dictionary<string, TreeViewNode> treeNodesById = new(StringComparer.Ordinal);

    ForgeXamlDocument? document;
    string? selectedSourceId;
    FrameworkElement? selectedFrameworkElement;
    bool suppressSourceTextChanged;
    bool rebuildingVisualTree;

    static readonly SolidColorBrush WindowBrush = Brush(23, 27, 29);
    static readonly SolidColorBrush PanelBrush = Brush(34, 37, 42);
    static readonly SolidColorBrush ElevatedBrush = Brush(47, 50, 54);
    static readonly SolidColorBrush BorderBrush = Brush(65, 68, 72);
    static readonly SolidColorBrush AccentBrush = Brush(249, 116, 25);
    static readonly SolidColorBrush TextBrush = Brush(242, 243, 245);
    static readonly SolidColorBrush MutedBrush = Brush(179, 185, 193);

    public MainWindow()
    {
        Title = "WinUI Forge · XAML Studio migration";
        AppWindow.Resize(new SizeInt32(1680, 980));
        AppWindow.TitleBar.BackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ForegroundColor = Color.FromArgb(255, 242, 243, 245);
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 242, 243, 245);

        Content = BuildShell();
        WireEvents();

        sourceEditor.Text = SampleXaml;
        RenderSource();
    }

    UIElement BuildShell()
    {
        var root = new Grid
        {
            Background = WindowBrush,
            RequestedTheme = ElementTheme.Dark
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid
        {
            Background = PanelBrush,
            Padding = new Thickness(14, 10, 14, 10),
            ColumnSpacing = 12
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "WinUI Forge · XAML Studio migration",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = TextBrush
                },
                new TextBlock
                {
                    Text = "Real XAML · render service · authored/runtime coordinator · Visual Tree · live property source edits",
                    Foreground = MutedBrush
                }
            }
        });

        var renderButton = new Button
        {
            Content = "Render now",
            Background = AccentBrush,
            Foreground = WindowBrush,
            Padding = new Thickness(14, 7, 14, 7)
        };
        renderButton.Click += (_, _) => RenderSource();
        Grid.SetColumn(renderButton, 1);
        header.Children.Add(renderButton);
        root.Children.Add(header);

        var workspace = new Grid { ColumnSpacing = 1, Background = BorderBrush };
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star), MinWidth = 380 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 460 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350), MinWidth = 300 });
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);

        workspace.Children.Add(BuildSourceAndTreePane());

        var previewHost = new Grid { Background = WindowBrush };
        previewHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewHost.Children.Add(SectionHeader(
            "WinUI 3 preview",
            "Click any mapped authored element. Selection is based on its real rendered bounds."));

        var previewFrame = new Border
        {
            Margin = new Thickness(18),
            Background = ElevatedBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1)
        };
        previewStage.Background = ElevatedBrush;
        previewStage.Children.Add(previewContent);
        previewStage.Children.Add(selectionLayer);
        previewFrame.Child = previewStage;
        Grid.SetRow(previewFrame, 1);
        previewHost.Children.Add(previewFrame);
        Grid.SetColumn(previewHost, 1);
        workspace.Children.Add(previewHost);

        var inspector = BuildInspectorPane();
        Grid.SetColumn(inspector, 2);
        workspace.Children.Add(inspector);

        var statusBorder = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        status.Foreground = MutedBrush;
        statusBorder.Child = status;
        Grid.SetRow(statusBorder, 2);
        root.Children.Add(statusBorder);

        return root;
    }

    UIElement BuildSourceAndTreePane()
    {
        var host = new Grid { Background = PanelBrush };
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.72, GridUnitType.Star), MinHeight = 260 });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.28, GridUnitType.Star), MinHeight = 160 });

        var sourceHost = new Grid();
        sourceHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourceHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sourceHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourceHost.Children.Add(SectionHeader(
            "XAML source",
            "The text is authoritative. Property edits patch only the relevant attribute."));
        Grid.SetRow(sourceEditor, 1);
        sourceHost.Children.Add(sourceEditor);

        diagnostics.Margin = new Thickness(12, 8, 12, 10);
        diagnostics.Foreground = MutedBrush;
        diagnostics.MaxHeight = 72;
        Grid.SetRow(diagnostics, 2);
        sourceHost.Children.Add(diagnostics);
        host.Children.Add(sourceHost);

        var divider = new Border { Background = BorderBrush };
        Grid.SetRow(divider, 1);
        host.Children.Add(divider);

        var treeHost = new Grid();
        treeHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        treeHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        treeHost.Children.Add(SectionHeader(
            "Visual Tree",
            "Authored XAML elements mapped to their actual WinUI runtime objects."));
        visualTree.Margin = new Thickness(6);
        Grid.SetRow(visualTree, 1);
        treeHost.Children.Add(visualTree);
        Grid.SetRow(treeHost, 2);
        host.Children.Add(treeHost);

        return host;
    }

    UIElement BuildInspectorPane()
    {
        var inspector = new Grid
        {
            Background = PanelBrush,
            Padding = new Thickness(16)
        };
        inspector.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inspector.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inspector.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        inspector.Children.Add(SectionHeader(
            "Live Properties",
            "Curated WinUI dependency properties. Apply commits a minimal XAML source edit."));

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(0, 16, 0, 12) };
        selectedType.Text = "Nothing selected";
        selectedType.Foreground = TextBrush;
        identity.Children.Add(selectedType);
        selectedName.Foreground = MutedBrush;
        identity.Children.Add(selectedName);
        Grid.SetRow(identity, 1);
        inspector.Children.Add(identity);

        var scroll = new ScrollViewer
        {
            Content = propertyForm,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 2);
        inspector.Children.Add(scroll);

        return inspector;
    }

    void WireEvents()
    {
        renderTimer.Tick += (_, _) =>
        {
            renderTimer.Stop();
            RenderSource();
        };

        sourceEditor.TextChanged += (_, _) =>
        {
            if (suppressSourceTextChanged) return;
            renderTimer.Stop();
            renderTimer.Start();
        };

        sourceEditor.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnSourcePointerReleased),
            true);

        visualTree.ItemInvoked += OnVisualTreeItemInvoked;

        previewStage.SizeChanged += (_, _) => DrawSelection();
        previewStage.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPreviewPointerPressed),
            true);
    }

    void RenderSource()
    {
        renderTimer.Stop();
        var previousSelection = selectedSourceId;
        var result = renderService.Render(sourceEditor.Text);

        if (!result.Succeeded || result.Document is null || result.Element is null)
        {
            diagnostics.Text = string.Join(Environment.NewLine, result.Diagnostics);
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Render failed. The last valid preview remains visible.";
            return;
        }

        ApplyValidRender(result, previousSelection);
    }

    void ApplyValidRender(ForgeXamlRenderResult result, string? preferredSelection)
    {
        document = result.Document!;
        previewContent.Content = result.Element;
        coordinator.Initialize(document, result.Element!);

        RebuildVisualTree();

        diagnostics.Text =
            $"Rendered successfully · {document.Elements.Count} authored XML element(s) · " +
            $"{coordinator.Count} mapped runtime element(s)";
        diagnostics.Foreground = MutedBrush;
        status.Text = "Source, Visual Tree and preview are synchronized.";

        if (!string.IsNullOrWhiteSpace(preferredSelection) &&
            coordinator.TryGetRuntimeElement(preferredSelection, out _))
        {
            SelectAuthoredElement(preferredSelection, revealSource: false);
        }
        else
        {
            var root = document.RootElement;
            if (coordinator.TryGetRuntimeElement(root.Id, out _))
                SelectAuthoredElement(root.Id, revealSource: false);
            else
                ClearSelection();
        }
    }

    void OnSourcePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        sourceEditor.DispatcherQueue.TryEnqueue(() =>
        {
            if (document is null) return;
            var mapped = document.FindAtSourceIndex(sourceEditor.SelectionStart);
            if (mapped is not null && coordinator.TryGetRuntimeElement(mapped.Id, out _))
                SelectAuthoredElement(mapped.Id, revealSource: false);
        });
    }

    void OnVisualTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (rebuildingVisualTree) return;

        if (args.InvokedItem is VisualTreeEntry entry)
            SelectAuthoredElement(entry.SourceId, revealSource: true);
    }

    void OnPreviewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (document is null || previewContent.Content is not UIElement) return;

        var point = e.GetCurrentPoint(previewStage).Position;
        var candidates = new List<AuthoredHit>();

        foreach (var pair in coordinator.GetFrameworkMappings())
        {
            var runtimeElement = pair.Runtime;
            if (runtimeElement.ActualWidth <= 0 || runtimeElement.ActualHeight <= 0) continue;

            try
            {
                var transform = runtimeElement.TransformToVisual(previewStage);
                var bounds = transform.TransformBounds(
                    new Rect(0, 0, runtimeElement.ActualWidth, runtimeElement.ActualHeight));

                if (!bounds.Contains(point)) continue;

                candidates.Add(new AuthoredHit(
                    runtimeElement,
                    pair.Source,
                    bounds,
                    Math.Max(1, bounds.Width) * Math.Max(1, bounds.Height)));
            }
            catch
            {
            }
        }

        var best = candidates
            .OrderByDescending(x => x.Source.Depth)
            .ThenBy(x => x.Area)
            .FirstOrDefault();

        if (best is null)
        {
            status.Text = $"Preview hit ({point.X:0},{point.Y:0}) did not intersect a mapped authored element.";
            return;
        }

        SelectAuthoredElement(best.Source.Id, revealSource: true);

        var hitPath = string.Join(
            " > ",
            candidates
                .OrderBy(x => x.Source.Depth)
                .ThenByDescending(x => x.Area)
                .Select(x => x.Source.DisplayName));

        status.Text =
            $"Selected {best.Source.DisplayName} · bounds hits: {hitPath}";
    }

    void SelectAuthoredElement(string sourceId, bool revealSource)
    {
        if (document?.FindById(sourceId) is not { } sourceElement) return;
        if (!coordinator.TryGetRuntimeElement(sourceId, out var runtime)) return;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedSourceId = sourceId;
        selectedFrameworkElement = runtime as FrameworkElement;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged += SelectedElement_SizeChanged;

        selectedType.Text = sourceElement.TypeName;
        selectedName.Text =
            (sourceElement.Name is null ? $"source id = {sourceElement.Id}" : $"x:Name = {sourceElement.Name}") +
            $"\nSource: line {sourceElement.Line}, column {sourceElement.Column}";

        RebuildPropertyInspector(sourceElement, runtime);
        SyncVisualTreeSelection(sourceElement.Id);

        if (revealSource)
            sourceEditor.Select(sourceElement.StartIndex, 0);

        DrawSelection();
        status.Text = $"Selected {sourceElement.DisplayName} · source ↔ runtime mapping active.";
    }

    void RebuildPropertyInspector(ForgeXamlElement source, DependencyObject runtime)
    {
        propertyForm.Children.Clear();

        var values = ForgeDependencyPropertyCatalog.Inspect(runtime, source);
        foreach (var group in values.GroupBy(x => x.Definition.Group))
        {
            propertyForm.Children.Add(new TextBlock
            {
                Text = group.Key,
                Margin = new Thickness(0, 10, 0, 2),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TextBrush
            });

            foreach (var value in group)
                propertyForm.Children.Add(BuildPropertyRow(source.Id, value));
        }
    }

    UIElement BuildPropertyRow(string sourceId, ForgePropertyValue value)
    {
        var host = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(0, 3, 0, 3)
        };
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var editor = new TextBox
        {
            Header = value.SourceValue is null
                ? value.Definition.AttributeName
                : $"{value.Definition.AttributeName} · set in XAML",
            Text = value.EditorValue,
            IsEnabled = value.IsEditable,
            MinWidth = 190
        };

        var apply = new Button
        {
            Content = "Apply",
            Padding = new Thickness(9, 5, 9, 5),
            VerticalAlignment = VerticalAlignment.Bottom,
            IsEnabled = value.IsEditable,
            Tag = new PropertyEditorContext(sourceId, value.Definition, editor)
        };
        apply.Click += OnPropertyApply;

        host.Children.Add(editor);
        Grid.SetColumn(apply, 1);
        host.Children.Add(apply);
        return host;
    }

    void OnPropertyApply(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PropertyEditorContext context })
            return;

        CommitProperty(context);
    }

    void CommitProperty(PropertyEditorContext context)
    {
        if (document is null) return;

        var rawValue = context.Editor.Text.Trim();
        if (!ForgeDependencyPropertyCatalog.TryValidateValue(
                context.Definition,
                rawValue,
                out var validationError))
        {
            status.Text =
                $"{context.Definition.AttributeName} is invalid: {validationError}";
            return;
        }

        var preservedSelection = context.SourceId;

        try
        {
            var working = new ForgeXamlDocument(sourceEditor.Text);
            working.SetAttributeById(
                context.SourceId,
                context.Definition.AttributeName,
                string.IsNullOrWhiteSpace(rawValue) ? null : rawValue);

            var result = renderService.Render(working.Text);
            if (!result.Succeeded || result.Document is null || result.Element is null)
            {
                diagnostics.Text = string.Join(Environment.NewLine, result.Diagnostics);
                diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                status.Text =
                    $"Rejected {context.Definition.AttributeName}: the edited XAML did not render.";
                return;
            }

            suppressSourceTextChanged = true;
            sourceEditor.Text = working.Text;
            suppressSourceTextChanged = false;

            ApplyValidRender(result, preservedSelection);
            status.Text = string.IsNullOrWhiteSpace(rawValue)
                ? $"Removed {context.Definition.AttributeName} from XAML."
                : $"Committed {context.Definition.AttributeName}=\"{rawValue}\" to XAML.";
        }
        catch (Exception ex)
        {
            status.Text = $"Could not commit {context.Definition.AttributeName}: {ex.Message}";
        }
    }

    void RebuildVisualTree()
    {
        rebuildingVisualTree = true;
        try
        {
            visualTree.RootNodes.Clear();
            treeNodesById.Clear();

            if (document is null) return;

            var mappedIds = coordinator.GetMappings()
                .Select(x => x.Source.Id)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var source in document.Elements.Where(x =>
                         mappedIds.Contains(x.Id) &&
                         NearestMappedParentId(x, mappedIds) is null))
            {
                visualTree.RootNodes.Add(BuildTreeNode(source, mappedIds));
            }
        }
        finally
        {
            rebuildingVisualTree = false;
        }
    }

    TreeViewNode BuildTreeNode(
        ForgeXamlElement source,
        IReadOnlySet<string> mappedIds)
    {
        var node = new TreeViewNode
        {
            Content = new VisualTreeEntry(source.Id, source.DisplayName),
            IsExpanded = source.Depth < 3
        };
        treeNodesById[source.Id] = node;

        if (document is not null)
        {
            foreach (var child in document.Elements.Where(x =>
                         mappedIds.Contains(x.Id) &&
                         string.Equals(
                             NearestMappedParentId(x, mappedIds),
                             source.Id,
                             StringComparison.Ordinal)))
            {
                node.Children.Add(BuildTreeNode(child, mappedIds));
            }
        }

        return node;
    }

    string? NearestMappedParentId(
        ForgeXamlElement source,
        IReadOnlySet<string> mappedIds)
    {
        if (document is null) return null;

        var parentId = source.ParentId;
        while (parentId is not null)
        {
            if (mappedIds.Contains(parentId))
                return parentId;

            parentId = document.FindById(parentId)?.ParentId;
        }

        return null;
    }

    void SyncVisualTreeSelection(string sourceId)
    {
        if (!treeNodesById.TryGetValue(sourceId, out var node))
            return;

        rebuildingVisualTree = true;
        try
        {
            visualTree.SelectedNode = node;
        }
        finally
        {
            rebuildingVisualTree = false;
        }
    }

    void SelectedElement_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSelection();

    void DrawSelection()
    {
        selectionLayer.Children.Clear();
        if (selectedFrameworkElement is null || previewContent.Content is null) return;

        try
        {
            var transform = selectedFrameworkElement.TransformToVisual(previewStage);
            var point = transform.TransformPoint(new Point(0, 0));
            var outline = new Border
            {
                Width = Math.Max(1, selectedFrameworkElement.ActualWidth),
                Height = Math.Max(1, selectedFrameworkElement.ActualHeight),
                BorderBrush = AccentBrush,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outline, point.X);
            Canvas.SetTop(outline, point.Y);
            selectionLayer.Children.Add(outline);
        }
        catch
        {
            selectionLayer.Children.Clear();
        }
    }

    void ClearSelection()
    {
        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedSourceId = null;
        selectedFrameworkElement = null;
        selectedType.Text = "Nothing selected";
        selectedName.Text = "";
        propertyForm.Children.Clear();
        selectionLayer.Children.Clear();
    }

    static Border SectionHeader(string title, string subtitle)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            TextWrapping = TextWrapping.Wrap,
            Foreground = MutedBrush
        });

        return new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 9, 12, 9),
            Child = stack
        };
    }

    static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromArgb(255, r, g, b));

    sealed record AuthoredHit(
        FrameworkElement Element,
        ForgeXamlElement Source,
        Rect Bounds,
        double Area);

    sealed record PropertyEditorContext(
        string SourceId,
        ForgePropertyDefinition Definition,
        TextBox Editor);

    sealed record VisualTreeEntry(string SourceId, string Label)
    {
        public override string ToString() => Label;
    }

    const string SampleXaml =
        """
        <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              x:Name="RootGrid"
              Background="#202327"
              Padding="32">
            <Grid.Resources>
                <SolidColorBrush x:Key="AccentBrush"
                                 Color="#F97419"/>
            </Grid.Resources>

            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="260"/>
            </Grid.ColumnDefinitions>

            <StackPanel x:Name="ContentStack"
                        Grid.Column="0"
                        Spacing="16"
                        VerticalAlignment="Center">
                <TextBlock x:Name="HeadingText"
                           Text="WinUI Forge"
                           FontSize="32"
                           FontWeight="SemiBold"/>
                <TextBlock x:Name="BodyText"
                           Text="This preview is rendered directly from the XAML source using WinUI 3 XamlReader."
                           MaxWidth="520"
                           TextWrapping="Wrap"
                           Opacity="0.72"/>
                <Button x:Name="ActionButton"
                        Content="Select me"
                        HorizontalAlignment="Left"
                        Padding="18,9"
                        Background="{StaticResource AccentBrush}"/>
            </StackPanel>

            <Border x:Name="InspectorCard"
                    Grid.Column="1"
                    Margin="20,0,0,0"
                    Padding="18"
                    BorderBrush="{StaticResource AccentBrush}"
                    BorderThickness="1"
                    CornerRadius="8">
                <StackPanel Spacing="8">
                    <TextBlock Text="Port proof"
                               FontSize="18"
                               FontWeight="SemiBold"/>
                    <TextBlock Text="Click any mapped authored element, then edit properties in the inspector."
                               TextWrapping="Wrap"
                               Opacity="0.72"/>
                </StackPanel>
            </Border>
        </Grid>
        """;
}
