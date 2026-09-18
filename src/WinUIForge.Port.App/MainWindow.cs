using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
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

    readonly TextBlock selectedType = new() { FontSize = 18 };
    readonly TextBlock selectedName = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBox widthEditor = new() { Header = "Width (blank = Auto)" };
    readonly Button applyWidth = new() { Content = "Apply Width", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.NoWrap };

    readonly Dictionary<string, FrameworkElement> renderedByName = new(StringComparer.Ordinal);
    readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };

    ForgeXamlDocument? document;
    string? selectedElementName;
    FrameworkElement? selectedFrameworkElement;
    bool suppressSourceSelection;
    bool suppressSourceTextChanged;

    static readonly SolidColorBrush WindowBrush = Brush(23, 27, 29);
    static readonly SolidColorBrush PanelBrush = Brush(34, 37, 42);
    static readonly SolidColorBrush ElevatedBrush = Brush(47, 50, 54);
    static readonly SolidColorBrush BorderBrush = Brush(65, 68, 72);
    static readonly SolidColorBrush AccentBrush = Brush(249, 116, 25);
    static readonly SolidColorBrush TextBrush = Brush(242, 243, 245);
    static readonly SolidColorBrush MutedBrush = Brush(179, 185, 193);

    public MainWindow()
    {
        Title = "WinUI Forge · WinUI 3 Port Proof";
        AppWindow.Resize(new SizeInt32(1600, 940));
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
                    Text = "WinUI Forge · WinUI 3 vertical slice",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = TextBrush
                },
                new TextBlock
                {
                    Text = "Real WinUI XAML → XamlReader → selectable runtime element → source edit",
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
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star), MinWidth = 360 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star), MinWidth = 420 });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320), MinWidth = 280 });
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);

        var editorHost = new Grid { Background = PanelBrush };
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorHost.Children.Add(SectionHeader("XAML source", "Edit the real WinUI XAML. Valid changes re-render after a short debounce."));
        Grid.SetRow(sourceEditor, 1);
        editorHost.Children.Add(sourceEditor);

        diagnostics.Margin = new Thickness(12, 8, 12, 10);
        diagnostics.Foreground = MutedBrush;
        diagnostics.MaxHeight = 72;
        Grid.SetRow(diagnostics, 2);
        editorHost.Children.Add(diagnostics);
        workspace.Children.Add(editorHost);

        var previewHost = new Grid { Background = WindowBrush };
        previewHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewHost.Children.Add(SectionHeader("WinUI 3 preview", "Click a named element to map it back to its source start tag."));

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

        var inspector = new Grid
        {
            Background = PanelBrush,
            Padding = new Thickness(16)
        };
        inspector.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inspector.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        inspector.Children.Add(SectionHeader("Live property proof", "FrameworkElement.Width is committed back into the XAML source."));

        var form = new StackPanel { Spacing = 12, Margin = new Thickness(0, 18, 0, 0) };
        selectedType.Text = "Nothing selected";
        selectedType.Foreground = TextBrush;
        form.Children.Add(selectedType);

        selectedName.Foreground = MutedBrush;
        form.Children.Add(selectedName);
        form.Children.Add(widthEditor);
        form.Children.Add(applyWidth);

        form.Children.Add(new Border
        {
            Height = 1,
            Background = BorderBrush,
            Margin = new Thickness(0, 8, 0, 8)
        });

        form.Children.Add(new TextBlock
        {
            Text = "Proof behaviour",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        form.Children.Add(new TextBlock
        {
            Text = "• Preview selection reveals the exact authored start tag.\n" +
                   "• Put the source caret inside a named start tag to select its rendered element.\n" +
                   "• Width edits rewrite XAML and re-render while preserving selection.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = MutedBrush
        });

        var inspectorScroll = new ScrollViewer { Content = form };
        Grid.SetRow(inspectorScroll, 1);
        inspector.Children.Add(inspectorScroll);
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

        sourceEditor.SelectionChanged += (_, _) =>
        {
            if (suppressSourceSelection || document is null) return;
            var mapped = document.FindAtSourceIndex(sourceEditor.SelectionStart);
            if (mapped is not null)
                SelectAuthoredElement(mapped.Name, revealSource: false);
        };

        applyWidth.Click += (_, _) => CommitWidth();
        previewStage.SizeChanged += (_, _) => DrawSelection();
    }

    void RenderSource()
    {
        renderTimer.Stop();
        var previousSelection = selectedElementName;

        try
        {
            var parsed = new ForgeXamlDocument(sourceEditor.Text);
            var loaded = XamlReader.Load(sourceEditor.Text);
            if (loaded is not UIElement root)
                throw new InvalidOperationException($"XAML root rendered as {loaded?.GetType().FullName ?? "null"}, not UIElement.");

            document = parsed;
            previewContent.Content = root;
            renderedByName.Clear();
            RegisterAuthoredElements(root);

            diagnostics.Text = $"Rendered successfully · {document.Elements.Count} named authored element(s) · {renderedByName.Count} mapped runtime element(s)";
            diagnostics.Foreground = MutedBrush;
            status.Text = "Source and preview are synchronized.";

            if (!string.IsNullOrWhiteSpace(previousSelection) && renderedByName.ContainsKey(previousSelection))
                SelectAuthoredElement(previousSelection, revealSource: false);
            else
                ClearSelection();
        }
        catch (Exception e)
        {
            diagnostics.Text = "Render error: " + e.Message;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Preview kept at the last valid XAML.";
        }
    }

    void RegisterAuthoredElements(DependencyObject root)
    {
        if (root is FrameworkElement element &&
            !string.IsNullOrWhiteSpace(element.Name) &&
            document?.FindByName(element.Name) is not null)
        {
            renderedByName[element.Name] = element;
            element.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler(OnAuthoredPointerPressed),
                true);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            RegisterAuthoredElements(VisualTreeHelper.GetChild(root, i));
    }

    void OnAuthoredPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || string.IsNullOrWhiteSpace(element.Name)) return;
        SelectAuthoredElement(element.Name, revealSource: true);
        e.Handled = true;
    }

    void SelectAuthoredElement(string name, bool revealSource)
    {
        if (document?.FindByName(name) is not { } sourceElement) return;
        if (!renderedByName.TryGetValue(name, out var runtimeElement)) return;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedElementName = name;
        selectedFrameworkElement = runtimeElement;
        selectedFrameworkElement.SizeChanged += SelectedElement_SizeChanged;

        selectedType.Text = sourceElement.TypeName;
        selectedName.Text = $"x:Name = {sourceElement.Name}\nSource: line {sourceElement.Line}, column {sourceElement.Column}";
        widthEditor.Text = document.GetAttribute(name, "Width") ?? "";

        if (revealSource)
        {
            suppressSourceSelection = true;
            try
            {
                sourceEditor.Select(
                    sourceElement.StartIndex,
                    Math.Max(0, sourceElement.StartTagEndIndex - sourceElement.StartIndex + 1));
            }
            finally
            {
                suppressSourceSelection = false;
            }
        }

        DrawSelection();
        status.Text = $"Selected {sourceElement.TypeName} '{name}' · source ↔ runtime mapping active.";
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

    void CommitWidth()
    {
        if (document is null || string.IsNullOrWhiteSpace(selectedElementName)) return;

        var raw = widthEditor.Text.Trim();
        string? normalized = null;

        if (!string.IsNullOrEmpty(raw))
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                double.IsNaN(width) ||
                double.IsInfinity(width) ||
                width < 0)
            {
                status.Text = "Width must be blank (Auto) or a non-negative number.";
                return;
            }
            normalized = width.ToString("0.###", CultureInfo.InvariantCulture);
        }

        try
        {
            var selected = selectedElementName;
            document.SetAttribute(selected, "Width", normalized);

            suppressSourceTextChanged = true;
            suppressSourceSelection = true;
            try
            {
                sourceEditor.Text = document.Text;
            }
            finally
            {
                suppressSourceSelection = false;
                suppressSourceTextChanged = false;
            }

            selectedElementName = selected;
            RenderSource();
            status.Text = normalized is null
                ? $"Removed Width from '{selected}' (Auto)."
                : $"Committed Width=\"{normalized}\" to '{selected}' in XAML.";
        }
        catch (Exception e)
        {
            status.Text = "Could not commit Width: " + e.Message;
        }
    }

    void ClearSelection()
    {
        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.SizeChanged -= SelectedElement_SizeChanged;

        selectedElementName = null;
        selectedFrameworkElement = null;
        selectedType.Text = "Nothing selected";
        selectedName.Text = "";
        widthEditor.Text = "";
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
                    <TextBlock Text="Click any named element, then edit Width in the host inspector."
                               TextWrapping="Wrap"
                               Opacity="0.72"/>
                </StackPanel>
            </Border>
        </Grid>
        """;
}
