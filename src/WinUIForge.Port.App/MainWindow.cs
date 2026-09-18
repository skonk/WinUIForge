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

    readonly Grid previewStage = new();
    readonly ContentControl previewContent = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    readonly Canvas selectionLayer = new() { IsHitTestVisible = false };

    readonly ListView visualTree = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        IsItemClickEnabled = true
    };

    readonly StackPanel propertyPanel = new() { Spacing = 8 };
    readonly TextBlock diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.NoWrap };

    readonly ForgeRenderService renderService = new();
    readonly ForgeVisualCoordinator coordinator = new();
    readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };

    ForgeXamlDocument? document;
    string? selectedElementName;
    FrameworkElement? selectedFrameworkElement;
    bool suppressSourceTextChanged;
    bool suppressTreeSelection;

    static readonly SolidColorBrush WindowBrush = Brush(23, 27, 29);
    static readonly SolidColorBrush PanelBrush = Brush(34, 37, 42);
    static readonly SolidColorBrush ElevatedBrush = Brush(47, 50, 54);
    static readonly SolidColorBrush BorderBrush = Brush(65, 68, 72);
    static readonly SolidColorBrush AccentBrush = Brush(249, 116, 25);
    static readonly SolidColorBrush TextBrush = Brush(242, 243, 245);
    static readonly SolidColorBrush MutedBrush = Brush(179, 185, 193);

    public MainWindow()
    {
        Title = "WinUI Forge · Milestone 2";
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

        var headerText = new StackPanel { Spacing = 2 };
        headerText.Children.Add(new TextBlock
        {
            Text = "WinUI Forge · XAML Studio migration milestone 2",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        headerText.Children.Add(new TextBlock
        {
            Text = "Real XAML · render service · authored coordinator · Visual Tree · live property inspector",
            Foreground = MutedBrush
        });
        header.Children.Add(headerText);

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
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star), MinWidth = 380 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star), MinWidth = 460 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360), MinWidth = 320 });
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);

        var editorHost = new Grid { Background = PanelBrush };
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.Children.Add(SectionHeader(
            "XAML source",
            "The source is authoritative. Property edits mutate only the affected attribute."));
        Grid.SetRow(sourceEditor, 1);
        editorHost.Children.Add(sourceEditor);

        diagnostics.Margin = new Thickness(12, 8, 12, 10);
        diagnostics.Foreground = MutedBrush;
        diagnostics.MaxHeight = 86;
        Grid.SetRow(diagnostics, 2);
        editorHost.Children.Add(diagnostics);
        workspace.Children.Add(editorHost);

        var previewHost = new Grid { Background = WindowBrush };
        previewHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewHost.Children.Add(SectionHeader(
            "WinUI 3 preview",
            "Selection is resolved from real authored element bounds."));

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

        var right = new Grid { Background = PanelBrush };
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.38, GridUnitType.Star), MinHeight = 180 });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.62, GridUnitType.Star), MinHeight = 260 });

        var treeHost = new Grid();
        treeHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        treeHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        treeHost.Children.Add(SectionHeader(
            "Authored Visual Tree",
            "Tree, preview and source share one selection."));
        visualTree.Margin = new Thickness(8);
        Grid.SetRow(visualTree, 1);
        treeHost.Children.Add(visualTree);
        right.Children.Add(treeHost);

        var inspectorHost = new Grid();
        inspectorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inspectorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inspectorHost.Children.Add(SectionHeader(
            "Live Properties",
            "Press Enter to commit. Blank removes the local XAML value."));

        var propertyScroll = new ScrollViewer
        {
            Content = propertyPanel,
            Padding = new Thickness(12),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(propertyScroll, 1);
        inspectorHost.Children.Add(propertyScroll);
        Grid.SetRow(inspectorHost, 1);
        right.Children.Add(inspectorHost);

        Grid.SetColumn(right, 2);
        workspace.Children.Add(right);

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

        visualTree.SelectionChanged += OnTreeSelectionChanged;

        previewStage.SizeChanged += (_, _) => DrawSelection();
        previewStage.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPreviewPointerPressed),
            true);
    }

    void RenderSource()
    {
        renderTimer.Stop();
        var previousSelection = selectedElementName;

        var result = renderService.Render(sourceEditor.Text);
        if (!result.Succeeded || result.Document is null || result.Root is null)
        {
            diagnostics.Text = "Render error: " + result.Error;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Source is invalid; preview remains on the last valid render.";
            return;
        }

        document = result.Document;
        previewContent.Content = result.Root;
        coordinator.Initialize(document, result.Root);

        diagnostics.Text =
            $"Rendered successfully · {document.Elements.Count} authored element(s) · " +
            $"{coordinator.MappedCount} named runtime mapping(s)";
        diagnostics.Foreground = MutedBrush;
        status.Text = "Source, preview, Visual Tree and inspector are synchronized.";

        RebuildVisualTree();

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            coordinator.TryGetVisual(previousSelection, out _))
        {
            SelectAuthoredElement(previousSelection, revealSource: false, selectTree: true);
        }
        else
        {
            ClearSelection();
        }
    }

    void RebuildVisualTree()
    {
        suppressTreeSelection = true;
        try
        {
            visualTree.Items.Clear();
            if (document is null) return;
            AddTreeRows(document.Root);
        }
        finally
        {
            suppressTreeSelection = false;
        }
    }

    void AddTreeRows(ForgeXamlElement node)
    {
        var label = new TextBlock
        {
            Text = node.DisplayName,
            Margin = new Thickness(node.Depth * 16, 2, 4, 2),
            Foreground = string.IsNullOrWhiteSpace(node.Name) ? MutedBrush : TextBrush
        };

        visualTree.Items.Add(new ListViewItem
        {
            Content = label,
            Tag = node.Name,
            IsEnabled = !string.IsNullOrWhiteSpace(node.Name)
        });

        foreach (var child in node.Children)
            AddTreeRows(child);
    }

    void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressTreeSelection) return;
        if (visualTree.SelectedItem is not ListViewItem item) return;
        if (item.Tag is not string name || string.IsNullOrWhiteSpace(name)) return;

        SelectAuthoredElement(name, revealSource: true, selectTree: false);
    }

    void OnSourcePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        sourceEditor.DispatcherQueue.TryEnqueue(() =>
        {
            if (document is null) return;
            var mapped = document.FindAtSourceIndex(sourceEditor.SelectionStart);
            if (!string.IsNullOrWhiteSpace(mapped?.Name))
                SelectAuthoredElement(mapped.Name!, revealSource: false, selectTree: true);
        });
    }

    void OnPreviewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (document is null || previewContent.Content is not UIElement) return;

        var point = e.GetCurrentPoint(previewStage).Position;
        var source = coordinator.FindDeepestAuthoredAtPoint(previewStage, point, out var hitPath);
        if (string.IsNullOrWhiteSpace(source?.Name))
        {
            status.Text = $"Preview hit ({point.X:0},{point.Y:0}) did not intersect a named authored element.";
            return;
        }

        SelectAuthoredElement(source.Name!, revealSource: true, selectTree: true);
        status.Text =
            $"Selected {source.TypeName} '{source.Name}' · bounds hits: {hitPath}";
    }

    void SelectAuthoredElement(string name, bool revealSource, bool selectTree)
    {
        if (document?.FindByName(name) is not { } sourceElement) return;
        if (!coordinator.TryGetVisual(name, out var runtimeElement)) return;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedElementName = name;
        selectedFrameworkElement = runtimeElement;
        selectedFrameworkElement.SizeChanged += SelectedElement_SizeChanged;

        if (revealSource)
            sourceEditor.Select(sourceElement.StartIndex, 0);

        if (selectTree)
            SelectTreeItem(name);

        RebuildPropertyInspector(sourceElement, runtimeElement);
        DrawSelection();

        status.Text =
            $"Selected {sourceElement.TypeName} '{name}' · source ↔ runtime ↔ tree mapping active.";
    }

    void SelectTreeItem(string name)
    {
        suppressTreeSelection = true;
        try
        {
            var item = visualTree.Items
                .OfType<ListViewItem>()
                .FirstOrDefault(x => string.Equals(x.Tag as string, name, StringComparison.Ordinal));

            visualTree.SelectedItem = item;
            if (item is not null)
                visualTree.ScrollIntoView(item);
        }
        finally
        {
            suppressTreeSelection = false;
        }
    }

    void RebuildPropertyInspector(ForgeXamlElement sourceElement, FrameworkElement runtimeElement)
    {
        propertyPanel.Children.Clear();

        propertyPanel.Children.Add(new TextBlock
        {
            Text = sourceElement.TypeName,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });

        propertyPanel.Children.Add(new TextBlock
        {
            Text = $"x:Name = {sourceElement.Name}\nSource: line {sourceElement.Line}, column {sourceElement.Column}",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap
        });

        propertyPanel.Children.Add(new TextBlock
        {
            Text = "Local XAML values are shown in the field. Unset properties show the runtime value as a placeholder.",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        string? lastGroup = null;
        foreach (var definition in ForgePropertyCatalog.For(runtimeElement))
        {
            if (!string.Equals(lastGroup, definition.Group, StringComparison.Ordinal))
            {
                lastGroup = definition.Group;
                propertyPanel.Children.Add(new TextBlock
                {
                    Text = definition.Group,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = TextBrush,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            propertyPanel.Children.Add(CreatePropertyEditor(sourceElement, runtimeElement, definition));
        }
    }

    FrameworkElement CreatePropertyEditor(
        ForgeXamlElement sourceElement,
        FrameworkElement runtimeElement,
        ForgePropertyDefinition definition)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var local = document?.GetAttribute(sourceElement.Name!, definition.Name);
        var runtime = definition.FormatRuntimeValue(runtimeElement);

        var label = new TextBlock
        {
            Text = definition.Name,
            Foreground = local is null ? MutedBrush : TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(label);

        var editor = new TextBox
        {
            Text = local ?? string.Empty,
            PlaceholderText = string.IsNullOrWhiteSpace(runtime) ? "(unset)" : $"runtime: {runtime}",
            Tag = new PropertyEditContext(sourceElement.Name!, definition),
            MinWidth = 120
        };
        editor.KeyDown += OnPropertyEditorKeyDown;
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        return grid;
    }

    void OnPropertyEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        if (sender is not TextBox editor || editor.Tag is not PropertyEditContext context) return;

        CommitProperty(context, editor.Text);
        e.Handled = true;
    }

    void CommitProperty(PropertyEditContext context, string rawValue)
    {
        if (document is null) return;

        try
        {
            var normalized = string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
            if (normalized is not null)
                context.Definition.Validate(normalized);

            var selected = selectedElementName;
            var edit = document.SetAttribute(context.ElementName, context.Definition.Name, normalized);

            if (!edit.Changed)
            {
                status.Text = "No source change was required.";
                return;
            }

            suppressSourceTextChanged = true;
            sourceEditor.Text = document.Text;
            suppressSourceTextChanged = false;

            selectedElementName = selected;
            RenderSource();

            status.Text = normalized is null
                ? $"Removed {context.Definition.Name} from '{context.ElementName}'."
                : $"Committed {context.Definition.Name}=\"{normalized}\" to '{context.ElementName}'.";
        }
        catch (Exception ex)
        {
            status.Text = $"Could not apply {context.Definition.Name}: {ex.Message}";
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

        selectedElementName = null;
        selectedFrameworkElement = null;
        selectionLayer.Children.Clear();

        suppressTreeSelection = true;
        try
        {
            visualTree.SelectedItem = null;
        }
        finally
        {
            suppressTreeSelection = false;
        }

        propertyPanel.Children.Clear();
        propertyPanel.Children.Add(new TextBlock
        {
            Text = "Nothing selected",
            FontSize = 18,
            Foreground = MutedBrush
        });
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

    sealed record PropertyEditContext(
        string ElementName,
        ForgePropertyDefinition Definition);

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
                    <TextBlock Text="Click any named element, then edit properties in the inspector."
                               TextWrapping="Wrap"
                               Opacity="0.72"/>
                </StackPanel>
            </Border>
        </Grid>
        """;
}
