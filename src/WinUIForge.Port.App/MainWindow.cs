using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
    readonly ForgeSourceEditor sourceEditor = new();

    readonly Grid previewStage = new();
    readonly ContentControl previewContent = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    readonly Canvas selectionLayer = new() { IsHitTestVisible = true };

    readonly ListView visualTree = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        IsItemClickEnabled = true
    };

    readonly StackPanel toolboxPanel = new() { Spacing = 6, Padding = new Thickness(8) };
    readonly StackPanel propertyPanel = new() { Spacing = 8 };
    readonly TextBlock diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.NoWrap };

    readonly Button undoButton = new() { Content = "Undo", IsEnabled = false };
    readonly Button redoButton = new() { Content = "Redo", IsEnabled = false };
    readonly Button deleteButton = new() { Content = "Delete", IsEnabled = false };
    readonly Button renderButton = new() { Content = "Render now" };

    readonly ForgeRenderService renderService = new();
    readonly ForgeVisualCoordinator coordinator = new();
    readonly ForgeEditHistory history = new();
    readonly ForgeControlKnowledgeCatalog controlCatalog = ForgeControlKnowledgeCatalog.Load();
    readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };

    Grid shellRoot = null!;
    ForgeXamlDocument? document;
    string? selectedElementIdentity;
    FrameworkElement? selectedFrameworkElement;
    bool suppressSourceTextChanged;
    bool suppressTreeSelection;

    double moveDeltaX;
    double moveDeltaY;
    double resizeDeltaX;
    double resizeDeltaY;
    double resizeStartWidth;
    double resizeStartHeight;

    static readonly SolidColorBrush WindowBrush = Brush(23, 27, 29);
    static readonly SolidColorBrush PanelBrush = Brush(34, 37, 42);
    static readonly SolidColorBrush ElevatedBrush = Brush(47, 50, 54);
    static readonly SolidColorBrush BorderBrush = Brush(65, 68, 72);
    static readonly SolidColorBrush AccentBrush = Brush(249, 116, 25);
    static readonly SolidColorBrush TextBrush = Brush(242, 243, 245);
    static readonly SolidColorBrush MutedBrush = Brush(179, 185, 193);

    public MainWindow()
    {
        Title = "WinUI Forge · Milestone 3";
        AppWindow.Resize(new SizeInt32(1760, 1000));
        AppWindow.TitleBar.BackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ForegroundColor = Color.FromArgb(255, 242, 243, 245);
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 242, 243, 245);

        Content = BuildShell();
        WireEvents();
        BuildToolbox();

        sourceEditor.Text = SampleXaml;
        RenderSource();
    }

    UIElement BuildShell()
    {
        shellRoot = new Grid
        {
            Background = WindowBrush,
            RequestedTheme = ElementTheme.Dark
        };
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            Text = "WinUI Forge · visual authoring milestone 3",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        headerText.Children.Add(new TextBlock
        {
            Text = "Transactions · stable authored identity · layout intelligence · Toolbox · move / resize / reparent",
            Foreground = MutedBrush
        });
        header.Children.Add(headerText);

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        undoButton.Click += (_, _) => Undo();
        redoButton.Click += (_, _) => Redo();
        deleteButton.Click += (_, _) => DeleteSelected();
        renderButton.Click += (_, _) => RenderSource();

        renderButton.Background = AccentBrush;
        renderButton.Foreground = WindowBrush;

        commands.Children.Add(undoButton);
        commands.Children.Add(redoButton);
        commands.Children.Add(deleteButton);
        commands.Children.Add(renderButton);
        Grid.SetColumn(commands, 1);
        header.Children.Add(commands);
        shellRoot.Children.Add(header);

        var workspace = new Grid { ColumnSpacing = 1, Background = BorderBrush };
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.86, GridUnitType.Star), MinWidth = 360 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.22, GridUnitType.Star), MinWidth = 500 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(410), MinWidth = 350 });
        Grid.SetRow(workspace, 1);
        shellRoot.Children.Add(workspace);

        var editorHost = new Grid { Background = PanelBrush };
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.Children.Add(SectionHeader(
            "XAML source",
            "The source remains authoritative. Forge authoring commands make deterministic source edits."));
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
            "WinUI 3 designer",
            "Orange handles edit the real WinUI element using its parent container semantics."));

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

        var tabs = new TabView
        {
            IsAddTabButtonVisible = false,
            Background = PanelBrush
        };

        var toolboxTab = new TabViewItem
        {
            Header = "Toolbox",
            IsClosable = false,
            Content = new ScrollViewer
            {
                Content = toolboxPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        var treeTab = new TabViewItem
        {
            Header = "Visual Tree",
            IsClosable = false,
            Content = visualTree
        };
        visualTree.Margin = new Thickness(8);

        var inspectorTab = new TabViewItem
        {
            Header = "Inspector",
            IsClosable = false,
            Content = new ScrollViewer
            {
                Content = propertyPanel,
                Padding = new Thickness(12),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        tabs.TabItems.Add(toolboxTab);
        tabs.TabItems.Add(treeTab);
        tabs.TabItems.Add(inspectorTab);
        tabs.SelectedIndex = 1;
        Grid.SetColumn(tabs, 2);
        workspace.Children.Add(tabs);

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
        shellRoot.Children.Add(statusBorder);

        return shellRoot;
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

            // WinUIEdit owns its own text undo stack. Structural Forge history is
            // intentionally reset after free-form source edits so stale snapshots
            // cannot overwrite newer manual work.
            history.Clear();
            UpdateHistoryButtons();

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

    void BuildToolbox()
    {
        toolboxPanel.Children.Clear();

        toolboxPanel.Children.Add(new TextBlock
        {
            Text = "WinUI controls",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        toolboxPanel.Children.Add(new TextBlock
        {
            Text = $"Guidance: Microsoft WinUI Gallery · {ForgeControlKnowledgeCatalog.GalleryRevision[..8]}",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var item in controlCatalog.Controls)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10),
                Tag = item
            };

            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TextBrush
            });
            row.Children.Add(new TextBlock
            {
                Text = item.Description,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            });
            if (item.RelatedControls.Count > 0)
            {
                row.Children.Add(new TextBlock
                {
                    Text = "Related: " + string.Join(", ", item.RelatedControls),
                    Foreground = MutedBrush,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            button.Content = row;
            button.Click += OnToolboxItemClick;
            toolboxPanel.Children.Add(button);
        }
    }

    void RenderSource()
    {
        renderTimer.Stop();
        var previousSelection = selectedElementIdentity;

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
        status.Text = "Source, preview, Visual Tree, Toolbox and inspector are synchronized.";

        RebuildVisualTree();

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            document.FindByIdentity(previousSelection) is not null)
        {
            SelectAuthoredElement(previousSelection, revealSource: false, selectTree: true);
        }
        else
        {
            ClearSelection();
        }

        UpdateHistoryButtons();
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
        var runtimeMapped =
            !string.IsNullOrWhiteSpace(node.Name) &&
            coordinator.TryGetVisual(node.Name, out _);

        var label = new TextBlock
        {
            Text = node.DisplayName + (runtimeMapped ? string.Empty : "  · source"),
            Margin = new Thickness(node.Depth * 16, 2, 4, 2),
            Foreground = runtimeMapped ? TextBrush : MutedBrush
        };

        visualTree.Items.Add(new ListViewItem
        {
            Content = label,
            Tag = node.Identity,
            IsEnabled = true
        });

        foreach (var child in node.Children)
            AddTreeRows(child);
    }

    void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressTreeSelection) return;
        if (visualTree.SelectedItem is not ListViewItem item) return;
        if (item.Tag is not string identity) return;

        SelectAuthoredElement(identity, revealSource: true, selectTree: false);
    }

    void OnSourcePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        sourceEditor.DispatcherQueue.TryEnqueue(() =>
        {
            if (document is null) return;
            var mapped = document.FindAtSourceIndex(sourceEditor.CaretUtf16Index);
            if (mapped is not null)
                SelectAuthoredElement(mapped.Identity, revealSource: false, selectTree: true);
        });
    }

    void OnPreviewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (document is null || previewContent.Content is not UIElement) return;

        var point = e.GetCurrentPoint(previewStage).Position;
        var source = coordinator.FindDeepestAuthoredAtPoint(previewStage, point, out var hitPath);
        if (source is null)
        {
            status.Text = $"Preview hit ({point.X:0},{point.Y:0}) did not intersect a named authored element.";
            return;
        }

        SelectAuthoredElement(source.Identity, revealSource: true, selectTree: true);
        status.Text =
            $"Selected {source.TypeName} '{source.DisplayName}' · bounds hits: {hitPath}";
    }

    void SelectAuthoredElement(string identity, bool revealSource, bool selectTree)
    {
        if (document?.FindByIdentity(identity) is not { } sourceElement) return;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedElementIdentity = sourceElement.Identity;
        selectedFrameworkElement = null;

        if (!string.IsNullOrWhiteSpace(sourceElement.Name) &&
            coordinator.TryGetVisual(sourceElement.Name, out var runtimeElement))
        {
            selectedFrameworkElement = runtimeElement;
            selectedFrameworkElement.SizeChanged += SelectedElement_SizeChanged;
        }

        if (revealSource)
            sourceEditor.GotoUtf16Index(sourceElement.StartIndex);

        if (selectTree)
            SelectTreeItem(sourceElement.Identity);

        RebuildPropertyInspector(sourceElement, selectedFrameworkElement);
        DrawSelection();
        UpdateCommandButtons();

        status.Text = selectedFrameworkElement is null
            ? $"Selected source node {sourceElement.DisplayName} · no direct FrameworkElement mapping."
            : $"Selected {sourceElement.TypeName} '{sourceElement.DisplayName}' · source ↔ runtime ↔ tree mapping active.";
    }

    void SelectTreeItem(string identity)
    {
        suppressTreeSelection = true;
        try
        {
            var item = visualTree.Items
                .OfType<ListViewItem>()
                .FirstOrDefault(x => string.Equals(x.Tag as string, identity, StringComparison.Ordinal));

            visualTree.SelectedItem = item;
            if (item is not null)
                visualTree.ScrollIntoView(item);
        }
        finally
        {
            suppressTreeSelection = false;
        }
    }

    void RebuildPropertyInspector(ForgeXamlElement sourceElement, FrameworkElement? runtimeElement)
    {
        propertyPanel.Children.Clear();

        propertyPanel.Children.Add(new TextBlock
        {
            Text = sourceElement.TypeName,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });

        var identityText = !string.IsNullOrWhiteSpace(sourceElement.Name)
            ? $"x:Name = {sourceElement.Name}"
            : !string.IsNullOrWhiteSpace(sourceElement.XKey)
                ? $"x:Key = {sourceElement.XKey}"
                : $"Forge identity = {sourceElement.Identity}";

        propertyPanel.Children.Add(new TextBlock
        {
            Text = identityText + $"
Source: line {sourceElement.Line}, column {sourceElement.Column}",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap
        });

        AddLayoutContext(sourceElement);
        AddAuthoringActions(sourceElement);

        if (runtimeElement is not null)
        {
            propertyPanel.Children.Add(new TextBlock
            {
                Text = "Live properties",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 8, 0, 0)
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

                propertyPanel.Children.Add(CreatePropertyEditor(
                    sourceElement,
                    runtimeElement,
                    definition));
            }
        }
        else
        {
            propertyPanel.Children.Add(new TextBlock
            {
                Text = "Source attributes",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 8, 0, 0)
            });

            foreach (var attribute in sourceElement.Attributes
                         .Where(x => !x.Name.StartsWith("xmlns", StringComparison.Ordinal) &&
                                     x.Name is not "x:Name" and not "Name" and not "x:Key"))
            {
                propertyPanel.Children.Add(CreateSourceAttributeEditor(sourceElement, attribute));
            }

            if (!sourceElement.Attributes.Any(x =>
                    !x.Name.StartsWith("xmlns", StringComparison.Ordinal) &&
                    x.Name is not "x:Name" and not "Name" and not "x:Key"))
            {
                propertyPanel.Children.Add(new TextBlock
                {
                    Text = "No editable source attributes are currently present.",
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }
    }

    void AddLayoutContext(ForgeXamlElement sourceElement)
    {
        var context = ForgeLayoutAdvisor.Describe(sourceElement);

        propertyPanel.Children.Add(new TextBlock
        {
            Text = "Layout context",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 10, 0, 0)
        });

        propertyPanel.Children.Add(new TextBlock
        {
            Text = $"Parent: {context.ParentLabel}
Mode: {context.Mode}",
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var note in context.Notes)
        {
            propertyPanel.Children.Add(new TextBlock
            {
                Text = "• " + note,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    void AddAuthoringActions(ForgeXamlElement sourceElement)
    {
        if (sourceElement.Parent is null) return;

        propertyPanel.Children.Add(new TextBlock
        {
            Text = "Structure",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 10, 0, 0)
        });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var up = new Button { Content = "Up", Tag = sourceElement.Identity };
        up.Click += (_, _) => ReorderSelected(-1);
        var down = new Button { Content = "Down", Tag = sourceElement.Identity };
        down.Click += (_, _) => ReorderSelected(1);
        var reparent = new Button { Content = "Reparent", Tag = sourceElement.Identity };
        reparent.Click += async (_, _) => await ReparentSelectedAsync();
        var delete = new Button { Content = "Delete", Tag = sourceElement.Identity };
        delete.Click += (_, _) => DeleteSelected();

        row.Children.Add(up);
        row.Children.Add(down);
        row.Children.Add(reparent);
        row.Children.Add(delete);
        propertyPanel.Children.Add(row);
    }

    FrameworkElement CreatePropertyEditor(
        ForgeXamlElement sourceElement,
        FrameworkElement runtimeElement,
        ForgePropertyDefinition definition)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var local = document?.GetAttributeByIdentity(sourceElement.Identity, definition.Name);
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
            Tag = new PropertyEditContext(sourceElement.Identity, definition.Name, definition),
            MinWidth = 120
        };
        editor.KeyDown += OnPropertyEditorKeyDown;
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        return grid;
    }

    FrameworkElement CreateSourceAttributeEditor(
        ForgeXamlElement sourceElement,
        ForgeXamlAttribute attribute)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock
        {
            Text = attribute.Name,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var editor = new TextBox
        {
            Text = attribute.Value,
            Tag = new PropertyEditContext(sourceElement.Identity, attribute.Name, null),
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
                context.Definition?.Validate(normalized);

            var beforeSelection = selectedElementIdentity;
            var edit = document.SetAttributeByIdentity(
                context.ElementIdentity,
                context.AttributeName,
                normalized);

            if (!edit.Changed)
            {
                status.Text = "No source change was required.";
                return;
            }

            history.Record(
                $"Set {context.AttributeName}",
                edit.Before,
                edit.After,
                beforeSelection,
                context.ElementIdentity);

            ApplyDocumentText(edit.After, context.ElementIdentity);

            status.Text = normalized is null
                ? $"Removed {context.AttributeName}."
                : $"Committed {context.AttributeName}="{normalized}".";
        }
        catch (Exception ex)
        {
            status.Text = $"Could not apply {context.AttributeName}: {ex.Message}";
        }
    }

    void OnToolboxItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ForgeControlKnowledge item } || document is null)
            return;

        try
        {
            var parent = ResolveInsertionParent()
                ?? throw new InvalidOperationException("Select a supported container or one of its children first.");

            var name = document.CreateUniqueName(item.TypeName);
            var edit = document.InsertChild(parent.Identity, item.CreateXaml(name));

            history.Record(
                $"Add {item.TypeName}",
                edit.Before,
                edit.After,
                selectedElementIdentity,
                edit.NextSelectionIdentity);

            ApplyDocumentText(edit.After, edit.NextSelectionIdentity);
            status.Text = $"Added {item.TypeName} '{name}' using Gallery-grounded Toolbox metadata.";
        }
        catch (Exception ex)
        {
            status.Text = "Could not add control: " + ex.Message;
        }
    }

    ForgeXamlElement? ResolveInsertionParent()
    {
        if (document is null) return null;

        var selected = document.FindByIdentity(selectedElementIdentity);
        for (var current = selected; current is not null; current = current.Parent)
        {
            if (!current.IsPropertyElement &&
                ForgeXamlDocument.CanContainDesignChildren(current.TypeName))
                return current;
        }

        return ForgeXamlDocument.CanContainDesignChildren(document.Root.TypeName)
            ? document.Root
            : null;
    }

    void ReorderSelected(int offset)
    {
        if (document is null || string.IsNullOrWhiteSpace(selectedElementIdentity)) return;

        try
        {
            var edit = document.ReorderElement(selectedElementIdentity, offset);
            if (!edit.Changed)
            {
                status.Text = "Element is already at that edge of its container.";
                return;
            }

            history.Record(
                offset < 0 ? "Move earlier" : "Move later",
                edit.Before,
                edit.After,
                selectedElementIdentity,
                edit.NextSelectionIdentity);

            ApplyDocumentText(edit.After, edit.NextSelectionIdentity);
        }
        catch (Exception ex)
        {
            status.Text = "Could not reorder element: " + ex.Message;
        }
    }

    async Task ReparentSelectedAsync()
    {
        if (document is null ||
            string.IsNullOrWhiteSpace(selectedElementIdentity) ||
            document.FindByIdentity(selectedElementIdentity) is not { } selected ||
            selected.Parent is null)
            return;

        var choices = document.Elements
            .Where(x =>
                !x.IsPropertyElement &&
                ForgeXamlDocument.CanContainDesignChildren(x.TypeName) &&
                !string.Equals(x.Identity, selected.Identity, StringComparison.Ordinal) &&
                !x.IsDescendantOf(selected))
            .Select(x => new ContainerChoice(x.Identity, x.DisplayName))
            .ToList();

        if (choices.Count == 0)
        {
            status.Text = "No valid target containers are available.";
            return;
        }

        var combo = new ComboBox
        {
            ItemsSource = choices,
            DisplayMemberPath = nameof(ContainerChoice.Label),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            XamlRoot = shellRoot.XamlRoot,
            Title = "Reparent element",
            Content = combo,
            PrimaryButtonText = "Move",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary ||
            combo.SelectedItem is not ContainerChoice choice)
            return;

        try
        {
            var edit = document.MoveElement(selectedElementIdentity, choice.Identity);
            if (!edit.Changed) return;

            history.Record(
                "Reparent element",
                edit.Before,
                edit.After,
                selectedElementIdentity,
                edit.NextSelectionIdentity);

            ApplyDocumentText(edit.After, edit.NextSelectionIdentity);
        }
        catch (Exception ex)
        {
            status.Text = "Could not reparent element: " + ex.Message;
        }
    }

    void DeleteSelected()
    {
        if (document is null ||
            string.IsNullOrWhiteSpace(selectedElementIdentity) ||
            document.FindByIdentity(selectedElementIdentity)?.Parent is null)
            return;

        try
        {
            var edit = document.RemoveElement(selectedElementIdentity);
            history.Record(
                "Delete element",
                edit.Before,
                edit.After,
                selectedElementIdentity,
                edit.NextSelectionIdentity);

            ApplyDocumentText(edit.After, edit.NextSelectionIdentity);
        }
        catch (Exception ex)
        {
            status.Text = "Could not delete element: " + ex.Message;
        }
    }

    void Undo()
    {
        var snapshot = history.Undo();
        if (snapshot is null) return;
        ApplyDocumentText(snapshot.Source, snapshot.SelectionIdentity);
        status.Text = "Undo applied.";
    }

    void Redo()
    {
        var snapshot = history.Redo();
        if (snapshot is null) return;
        ApplyDocumentText(snapshot.Source, snapshot.SelectionIdentity);
        status.Text = "Redo applied.";
    }

    void ApplyDocumentText(string source, string? selectionIdentity)
    {
        suppressSourceTextChanged = true;
        sourceEditor.Text = source;
        suppressSourceTextChanged = false;

        selectedElementIdentity = selectionIdentity;
        RenderSource();
        UpdateHistoryButtons();
    }

    void UpdateHistoryButtons()
    {
        undoButton.IsEnabled = history.CanUndo;
        redoButton.IsEnabled = history.CanRedo;
        undoButton.Content = history.UndoDescription is null ? "Undo" : $"Undo · {history.UndoDescription}";
        redoButton.Content = history.RedoDescription is null ? "Redo" : $"Redo · {history.RedoDescription}";
    }

    void UpdateCommandButtons()
    {
        deleteButton.IsEnabled =
            document?.FindByIdentity(selectedElementIdentity)?.Parent is not null;
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
            var width = Math.Max(1, selectedFrameworkElement.ActualWidth);
            var height = Math.Max(1, selectedFrameworkElement.ActualHeight);

            var outline = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = AccentBrush,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outline, point.X);
            Canvas.SetTop(outline, point.Y);
            selectionLayer.Children.Add(outline);

            var move = CreateHandle("Move / reorder");
            Canvas.SetLeft(move, point.X - 6);
            Canvas.SetTop(move, point.Y - 6);
            move.DragStarted += (_, _) =>
            {
                moveDeltaX = 0;
                moveDeltaY = 0;
            };
            move.DragDelta += (_, args) =>
            {
                moveDeltaX += args.HorizontalChange;
                moveDeltaY += args.VerticalChange;
                status.Text = $"Move preview Δ {moveDeltaX:0}, {moveDeltaY:0}";
            };
            move.DragCompleted += (_, _) => CommitMove(moveDeltaX, moveDeltaY);
            selectionLayer.Children.Add(move);

            var resize = CreateHandle("Resize");
            Canvas.SetLeft(resize, point.X + width - 6);
            Canvas.SetTop(resize, point.Y + height - 6);
            resize.DragStarted += (_, _) =>
            {
                resizeDeltaX = 0;
                resizeDeltaY = 0;
                resizeStartWidth = width;
                resizeStartHeight = height;
            };
            resize.DragDelta += (_, args) =>
            {
                resizeDeltaX += args.HorizontalChange;
                resizeDeltaY += args.VerticalChange;
                status.Text =
                    $"Resize preview {Math.Max(8, resizeStartWidth + resizeDeltaX):0} × {Math.Max(8, resizeStartHeight + resizeDeltaY):0}";
            };
            resize.DragCompleted += (_, _) => CommitResize();
            selectionLayer.Children.Add(resize);
        }
        catch
        {
            selectionLayer.Children.Clear();
        }
    }

    static Thumb CreateHandle(string tooltip)
    {
        var thumb = new Thumb
        {
            Width = 12,
            Height = 12,
            Background = AccentBrush,
            BorderBrush = WindowBrush,
            BorderThickness = new Thickness(1)
        };
        ToolTipService.SetToolTip(thumb, tooltip);
        return thumb;
    }

    void CommitResize()
    {
        if (document is null ||
            selectedFrameworkElement is null ||
            string.IsNullOrWhiteSpace(selectedElementIdentity))
            return;

        if (Math.Abs(resizeDeltaX) < 0.5 && Math.Abs(resizeDeltaY) < 0.5)
            return;

        try
        {
            var identity = selectedElementIdentity;
            var before = document.Text;
            var width = Math.Max(8, resizeStartWidth + resizeDeltaX);
            var height = Math.Max(8, resizeStartHeight + resizeDeltaY);

            document.SetAttributeByIdentity(identity, "Width", Number(width));
            document.SetAttributeByIdentity(identity, "Height", Number(height));

            history.Record(
                "Resize element",
                before,
                document.Text,
                identity,
                identity);

            ApplyDocumentText(document.Text, identity);
        }
        catch (Exception ex)
        {
            status.Text = "Could not resize element: " + ex.Message;
        }
    }

    void CommitMove(double deltaX, double deltaY)
    {
        if (document is null ||
            selectedFrameworkElement is null ||
            string.IsNullOrWhiteSpace(selectedElementIdentity) ||
            document.FindByIdentity(selectedElementIdentity) is not { } source ||
            source.Parent is null)
            return;

        if (Math.Abs(deltaX) < 1 && Math.Abs(deltaY) < 1)
            return;

        try
        {
            switch (source.Parent.TypeName)
            {
                case "StackPanel":
                {
                    var orientation = source.Parent.Attributes
                        .FirstOrDefault(x => x.Name == "Orientation")?.Value ?? "Vertical";
                    var primary = orientation == "Horizontal" ? deltaX : deltaY;
                    if (Math.Abs(primary) < 8) return;

                    var edit = document.ReorderElement(source.Identity, primary > 0 ? 1 : -1);
                    if (!edit.Changed) return;

                    history.Record(
                        "Reorder in StackPanel",
                        edit.Before,
                        edit.After,
                        source.Identity,
                        edit.NextSelectionIdentity);
                    ApplyDocumentText(edit.After, edit.NextSelectionIdentity);
                    break;
                }

                case "Canvas":
                    CommitCanvasMove(source, deltaX, deltaY);
                    break;

                case "Grid":
                    if (!CommitGridCellMove(source, deltaX, deltaY))
                        CommitMarginMove(source, deltaX, deltaY);
                    break;

                case "Border":
                    status.Text = "Border has one child; movement is controlled by the child's alignment/margin.";
                    break;

                default:
                    CommitMarginMove(source, deltaX, deltaY);
                    break;
            }
        }
        catch (Exception ex)
        {
            status.Text = "Could not move element: " + ex.Message;
        }
    }

    void CommitCanvasMove(ForgeXamlElement source, double deltaX, double deltaY)
    {
        if (document is null) return;

        var before = document.Text;
        var left = ReadDouble(source, "Canvas.Left", 0) + deltaX;
        var top = ReadDouble(source, "Canvas.Top", 0) + deltaY;

        document.SetAttributeByIdentity(source.Identity, "Canvas.Left", Number(left));
        document.SetAttributeByIdentity(source.Identity, "Canvas.Top", Number(top));

        history.Record("Move in Canvas", before, document.Text, source.Identity, source.Identity);
        ApplyDocumentText(document.Text, source.Identity);
    }

    bool CommitGridCellMove(ForgeXamlElement source, double deltaX, double deltaY)
    {
        if (document is null ||
            source.Parent?.Name is not { Length: > 0 } parentName ||
            !coordinator.TryGetVisual(parentName, out var parentVisual) ||
            parentVisual is not Grid grid)
            return false;

        var transform = selectedFrameworkElement!.TransformToVisual(grid);
        var topLeft = transform.TransformPoint(new Point(0, 0));
        var targetX = topLeft.X + selectedFrameworkElement.ActualWidth / 2 + deltaX;
        var targetY = topLeft.Y + selectedFrameworkElement.ActualHeight / 2 + deltaY;

        var currentColumn = Grid.GetColumn(selectedFrameworkElement);
        var currentRow = Grid.GetRow(selectedFrameworkElement);
        var targetColumn = FindColumn(grid, targetX);
        var targetRow = FindRow(grid, targetY);

        if (targetColumn == currentColumn && targetRow == currentRow)
            return false;

        var before = document.Text;
        document.SetAttributeByIdentity(source.Identity, "Grid.Column", targetColumn.ToString(CultureInfo.InvariantCulture));
        document.SetAttributeByIdentity(source.Identity, "Grid.Row", targetRow.ToString(CultureInfo.InvariantCulture));

        history.Record("Move between Grid cells", before, document.Text, source.Identity, source.Identity);
        ApplyDocumentText(document.Text, source.Identity);
        return true;
    }

    void CommitMarginMove(ForgeXamlElement source, double deltaX, double deltaY)
    {
        if (document is null || selectedFrameworkElement is null) return;

        var before = document.Text;
        var margin = selectedFrameworkElement.Margin;
        var moved = new Thickness(
            margin.Left + deltaX,
            margin.Top + deltaY,
            margin.Right,
            margin.Bottom);

        document.SetAttributeByIdentity(source.Identity, "Margin", FormatThickness(moved));
        history.Record("Move by Margin", before, document.Text, source.Identity, source.Identity);
        ApplyDocumentText(document.Text, source.Identity);
    }

    static int FindColumn(Grid grid, double x)
    {
        if (grid.ColumnDefinitions.Count == 0) return 0;

        var position = 0d;
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            position += grid.ColumnDefinitions[i].ActualWidth;
            if (x <= position) return i;
            position += grid.ColumnSpacing;
        }
        return grid.ColumnDefinitions.Count - 1;
    }

    static int FindRow(Grid grid, double y)
    {
        if (grid.RowDefinitions.Count == 0) return 0;

        var position = 0d;
        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            position += grid.RowDefinitions[i].ActualHeight;
            if (y <= position) return i;
            position += grid.RowSpacing;
        }
        return grid.RowDefinitions.Count - 1;
    }

    static double ReadDouble(ForgeXamlElement element, string attribute, double fallback)
    {
        var raw = element.Attributes.FirstOrDefault(x => x.Name == attribute)?.Value;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    static string Number(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    static string FormatThickness(Thickness value) =>
        string.Join(",",
            Number(value.Left),
            Number(value.Top),
            Number(value.Right),
            Number(value.Bottom));

    void ClearSelection()
    {
        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedElementIdentity = null;
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

        UpdateCommandButtons();
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
        string ElementIdentity,
        string AttributeName,
        ForgePropertyDefinition? Definition);

    sealed record ContainerChoice(string Identity, string Label);

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
                    <TextBlock Text="Use the Toolbox and orange designer handles to author real XAML."
                               TextWrapping="Wrap"
                               Opacity="0.72"/>
                </StackPanel>
            </Border>
        </Grid>
        """;
}
