using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Windows.Storage.Pickers;
using WinUIForge.Port.Core;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace WinUIForge.Port.App;

public sealed class MainWindow : Window
{
    readonly ForgeSourceEditor sourceEditor = new();

    readonly Grid previewStage = new()
    {
        Width = 1672,
        Height = 941
    };
    readonly ContentControl previewContent = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    readonly Image referenceOverlay = new()
    {
        Stretch = Stretch.Uniform,
        IsHitTestVisible = false,
        Opacity = 0.5,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    readonly Canvas allHighlightsLayer = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    readonly Canvas selectionLayer = new() { IsHitTestVisible = true };
    readonly Grid previewViewportShell = new();
    readonly Viewbox previewViewbox = new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.Both,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    readonly ScrollViewer previewScrollViewer = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Enabled,
        VerticalScrollMode = ScrollMode.Enabled,
        ZoomMode = ZoomMode.Disabled,
        Visibility = Visibility.Collapsed
    };

    readonly Button loadReferenceButton = new() { Content = "Load reference…" };
    readonly Button exportReviewPackageButton = new() { Content = "Export review package" };
    readonly CheckBox referenceVisibleCheckBox = new() { Content = "Overlay", IsEnabled = false };
    readonly CheckBox highlightAllCheckBox = new() { Content = "Highlight all" };
    readonly Slider referenceOpacitySlider = new()
    {
        Minimum = 0.1,
        Maximum = 0.9,
        Value = 0.5,
        StepFrequency = 0.05,
        Width = 120,
        IsEnabled = false
    };
    readonly TextBlock referenceInfo = new()
    {
        Text = "No reference image loaded.",
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    readonly TextBox viewportWidthBox = new()
    {
        Text = "1672",
        Width = 72,
        Header = "Width"
    };
    readonly TextBox viewportHeightBox = new()
    {
        Text = "941",
        Width = 72,
        Header = "Height"
    };
    readonly ComboBox viewportDisplayMode = new()
    {
        Width = 96,
        Header = "View"
    };
    readonly Button applyViewportButton = new()
    {
        Content = "Apply viewport",
        VerticalAlignment = VerticalAlignment.Bottom
    };
    readonly Button useReferenceSizeButton = new()
    {
        Content = "Use reference size",
        IsEnabled = false,
        VerticalAlignment = VerticalAlignment.Bottom
    };
    readonly TextBlock viewportInfo = new()
    {
        Text = "Logical viewport 1672 × 941 · Fit",
        TextWrapping = TextWrapping.Wrap
    };

    readonly TextBox visualTreeFilter = new()
    {
        PlaceholderText = "Filter Visual Tree…",
        MinWidth = 150
    };
    readonly CheckBox visualTreeNamedOnly = new()
    {
        Content = "Named only",
        VerticalAlignment = VerticalAlignment.Center
    };
    readonly TextBlock visualTreeCount = new()
    {
        Foreground = MutedBrush,
        VerticalAlignment = VerticalAlignment.Center
    };
    readonly ListView visualTree = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        IsItemClickEnabled = true
    };

    readonly StackPanel toolboxPanel = new() { Spacing = 6, Padding = new Thickness(8) };
    readonly StackPanel propertyPanel = new() { Spacing = 8 };
    readonly StackPanel projectExplorerPanel = new() { Spacing = 2, Padding = new Thickness(6) };
    readonly Button addProjectFolderButton = new() { Content = "Add folder…" };
    readonly Button removeProjectFolderButton = new() { Content = "Remove folder", IsEnabled = false };
    readonly Button refreshProjectsButton = new() { Content = "Refresh" };
    readonly TextBlock projectExplorerInfo = new()
    {
        Foreground = MutedBrush,
        TextWrapping = TextWrapping.Wrap
    };
    readonly TextBlock diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.NoWrap };

    readonly ToggleButton projectsPaneToggle = new() { Content = "Projects", IsChecked = true };
    readonly ToggleButton sourcePaneToggle = new() { Content = "Source", IsChecked = true };
    readonly ToggleButton toolsPaneToggle = new() { Content = "Inspector / Tools", IsChecked = true };
    readonly AppBarButton openSourceFileButton = ToolbarButton("Open", "\uE8E5");
    readonly AppBarButton reloadSourceFileButton = ToolbarButton("Reload", "\uE72C", enabled: false);
    readonly AppBarButton saveSourceFileButton = ToolbarButton("Save", "\uE74E", enabled: false);
    readonly AppBarButton undoButton = ToolbarButton("Undo", "\uE7A7", enabled: false);
    readonly AppBarButton redoButton = ToolbarButton("Redo", "\uE7A6", enabled: false);
    readonly AppBarButton deleteButton = ToolbarButton("Delete", "\uE74D", enabled: false);
    readonly AppBarButton renderButton = ToolbarButton("Render", "\uE768");

    readonly ForgeUserSettings userSettings = ForgeUserSettings.Load();
    readonly ForgeRenderService renderService = new();
    readonly ForgeVisualCoordinator coordinator = new();
    readonly ForgeEditHistory history = new();
    readonly ForgeControlKnowledgeCatalog controlCatalog = ForgeControlKnowledgeCatalog.Load();
    readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(550) };

    Grid shellRoot = null!;
    Grid workspaceGrid = null!;
    ColumnDefinition projectsWorkspaceColumn = null!;
    ColumnDefinition projectsSplitterColumn = null!;
    ColumnDefinition sourceWorkspaceColumn = null!;
    ColumnDefinition sourceSplitterColumn = null!;
    ColumnDefinition previewWorkspaceColumn = null!;
    ColumnDefinition toolsSplitterColumn = null!;
    ColumnDefinition toolsWorkspaceColumn = null!;
    FrameworkElement projectsPaneHost = null!;
    FrameworkElement sourcePaneHost = null!;
    FrameworkElement toolsPaneHost = null!;
    TabView toolsTabs = null!;
    Thumb projectsSourceSplitter = null!;
    Thumb sourcePreviewSplitter = null!;
    Thumb previewToolsSplitter = null!;
    ForgeXamlDocument? document;
    string? selectedElementIdentity;
    FrameworkElement? selectedFrameworkElement;
    string? currentSourceFilePath;
    string? currentForgeSidecarPath;
    string? referenceSourceFilePath;
    string currentReviewCase = "winui-forge-review";
    string lastSavedSourceText = string.Empty;
    readonly Dictionary<string, string> semanticElements = new(StringComparer.Ordinal);
    readonly HashSet<string> expandedProjectDirectories = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, ForgeProjectScreen> projectScreensByPath = new(StringComparer.OrdinalIgnoreCase);
    string? selectedProjectRoot;
    bool currentSourceFileReadOnly;
    bool suppressSourceTextChanged;
    bool suppressTreeSelection;
    bool designerManipulating;
    int referencePixelWidth;
    int referencePixelHeight;

    Border? selectionOutline;
    Thumb? selectionMoveHandle;
    Thumb? selectionResizeHandle;

    double manipulationStartX;
    double manipulationStartY;
    double manipulationStartWidth;
    double manipulationStartHeight;

    Point movePointerStart;
    Point resizePointerStart;
    uint? movePointerId;
    uint? resizePointerId;

    double moveDeltaX;
    double moveDeltaY;
    Vector3 moveStartTranslation;

    double resizeDeltaX;
    double resizeDeltaY;
    double resizeStartWidth;
    double resizeStartHeight;
    double resizeOriginalWidth;
    double resizeOriginalHeight;
    HorizontalAlignment resizeOriginalHorizontalAlignment;
    VerticalAlignment resizeOriginalVerticalAlignment;
    bool resizePreviewAnchoredHorizontal;
    bool resizePreviewAnchoredVertical;
    bool resizeWidthActive;
    bool resizeHeightActive;

    nint nativeBigIcon;
    nint nativeSmallIcon;

    const uint ImageIcon = 1;
    const uint LoadFromFile = 0x10;
    const uint WmSetIcon = 0x0080;
    const int IconSmall = 0;
    const int IconBig = 1;
    const int IconSmall2 = 2;
    const int GclpHIcon = -14;
    const int GclpHIconSm = -34;

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint LoadImageW(nint hInstance, string name, uint type, int width, int height, uint loadFlags);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern nint SendMessageW(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
    static extern nint SetClassLongPtrW(nint hwnd, int index, nint value);

    const double ResizeAxisThreshold = 8;
    const double ResizeSecondAxisThreshold = 18;

    static readonly SolidColorBrush WindowBrush = Brush(23, 27, 29);
    static readonly SolidColorBrush PanelBrush = Brush(34, 37, 42);
    static readonly SolidColorBrush ElevatedBrush = Brush(47, 50, 54);
    static readonly SolidColorBrush BorderBrush = Brush(65, 68, 72);
    static readonly SolidColorBrush AccentBrush = Brush(249, 116, 25);
    static readonly SolidColorBrush TextBrush = Brush(242, 243, 245);
    static readonly SolidColorBrush MutedBrush = Brush(179, 185, 193);

    public MainWindow()
    {
        Title = "WinUI Forge · Milestone 4 benchmark";
        AppWindow.Resize(new SizeInt32(1760, 1000));
        AppWindow.TitleBar.BackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ForegroundColor = Color.FromArgb(255, 242, 243, 245);
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 23, 27, 29);
        AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 242, 243, 245);

        // Windows App SDK can retain a hidden system-icon state after title-bar
        // customization. Explicitly cycle it before applying the Workshop icon.
        AppWindow.TitleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.HideIconAndSystemMenu;
        AppWindow.TitleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.ShowIconAndSystemMenu;

        Activated += (_, _) => ApplyWindowIcons();

        viewportDisplayMode.ItemsSource = new[] { "Fit", "Fill", "1:1" };
        viewportDisplayMode.SelectedIndex = 0;

        projectsPaneToggle.IsChecked = userSettings.ProjectsPaneVisible;
        sourcePaneToggle.IsChecked = userSettings.SourcePaneVisible;
        toolsPaneToggle.IsChecked = userSettings.ToolsPaneVisible;

        Content = BuildShell();
        WireEvents();
        BuildToolbox();
        EnsureDefaultProjectFolders();
        RebuildProjectExplorer();
        ApplyWorkspacePanelVisibility(persist: false);

        sourceEditor.Text = SampleXaml;
        RenderSource();
        ApplyWindowIcons();
    }

    void ApplyWindowIcons()
    {
        var rootIconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Workshop.ico"));
        var linkedIconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Branding", "Workshop.ico"));
        var appIconPath = File.Exists(rootIconPath)
            ? rootIconPath
            : File.Exists(linkedIconPath)
                ? linkedIconPath
                : null;

        if (appIconPath is null)
        {
            Debug.WriteLine($"Workshop icon was not found under {AppContext.BaseDirectory}");
            return;
        }

        try
        {
            // Title-bar customization can leave the system icon hidden. Cycling
            // this state is required before applying the Workshop icon.
            AppWindow.TitleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.HideIconAndSystemMenu;
            AppWindow.TitleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.ShowIconAndSystemMenu;

            AppWindow.SetIcon(appIconPath);
            AppWindow.SetTitleBarIcon(appIconPath);
            AppWindow.SetTaskbarIcon(appIconPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppWindow icon API failed: {ex}");
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == 0)
        {
            Debug.WriteLine("Workshop icon could not be applied because the native window handle is unavailable.");
            return;
        }

        if (nativeBigIcon == 0)
        {
            nativeBigIcon = LoadImageW(0, appIconPath, ImageIcon, 32, 32, LoadFromFile);
            if (nativeBigIcon == 0)
                Debug.WriteLine($"Workshop 32px icon load failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        if (nativeSmallIcon == 0)
        {
            nativeSmallIcon = LoadImageW(0, appIconPath, ImageIcon, 16, 16, LoadFromFile);
            if (nativeSmallIcon == 0)
                Debug.WriteLine($"Workshop 16px icon load failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        if (nativeBigIcon != 0)
        {
            SendMessageW(hwnd, WmSetIcon, IconBig, nativeBigIcon);
            SetClassLongPtrW(hwnd, GclpHIcon, nativeBigIcon);
        }

        if (nativeSmallIcon != 0)
        {
            SendMessageW(hwnd, WmSetIcon, IconSmall, nativeSmallIcon);
            SendMessageW(hwnd, WmSetIcon, IconSmall2, nativeSmallIcon);
            SetClassLongPtrW(hwnd, GclpHIconSm, nativeSmallIcon);
        }
    }

    static AppBarButton ToolbarButton(string label, string glyph, bool enabled = true)
    {
        return new AppBarButton
        {
            Label = label,
            Icon = new FontIcon { Glyph = glyph, FontSize = 14 },
            IsEnabled = enabled
        };
    }

    static Button IconButton(string glyph, string tooltip)
    {
        var button = new Button
        {
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 13
            }
        };
        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    static Thumb Splitter() =>
        new()
        {
            Background = BorderBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

    void LoadNewDocument()
    {
        currentSourceFilePath = null;
        currentForgeSidecarPath = null;
        currentSourceFileReadOnly = false;
        referenceSourceFilePath = null;
        lastSavedSourceText = SampleXaml;
        semanticElements.Clear();
        history.Clear();
        selectedElementIdentity = null;

        suppressSourceTextChanged = true;
        try
        {
            sourceEditor.Text = SampleXaml;
        }
        finally
        {
            suppressSourceTextChanged = false;
        }

        ClearReference();
        RenderSource();
        UpdateHistoryButtons();
        UpdateSourceFileButtons();
        status.Text = "Created a new unsaved Forge UI document.";
    }

    UIElement BuildShell()
    {
        shellRoot = new Grid
        {
            Background = WindowBrush,
            RequestedTheme = ElementTheme.Dark
        };
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shellRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

        // Native Windows-style menu surface.
        var menuHost = new Border
        {
            Background = Brush(20, 23, 25),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var menuGrid = new Grid();
        menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var menuBar = new MenuBar
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var fileMenu = new MenuBarItem { Title = "File" };
        var newMenuItem = new MenuFlyoutItem { Text = "New UI" };
        newMenuItem.Click += (_, _) => LoadNewDocument();
        var openMenuItem = new MenuFlyoutItem { Text = "Open XAML…" };
        openMenuItem.Click += async (_, _) => await OpenSourceFileAsync();
        var addFolderMenuItem = new MenuFlyoutItem { Text = "Add Project Folder…" };
        addFolderMenuItem.Click += async (_, _) => await AddProjectFolderAsync();
        var saveMenuItem = new MenuFlyoutItem { Text = "Save" };
        saveMenuItem.Click += (_, _) => SaveCurrentSourceFile();
        var exitMenuItem = new MenuFlyoutItem { Text = "Exit" };
        exitMenuItem.Click += (_, _) => Close();
        fileMenu.Items.Add(newMenuItem);
        fileMenu.Items.Add(openMenuItem);
        fileMenu.Items.Add(addFolderMenuItem);
        fileMenu.Items.Add(new MenuFlyoutSeparator());
        fileMenu.Items.Add(saveMenuItem);
        fileMenu.Items.Add(new MenuFlyoutSeparator());
        fileMenu.Items.Add(exitMenuItem);

        var editMenu = new MenuBarItem { Title = "Edit" };
        var undoMenuItem = new MenuFlyoutItem { Text = "Undo" };
        undoMenuItem.Click += (_, _) => Undo();
        var redoMenuItem = new MenuFlyoutItem { Text = "Redo" };
        redoMenuItem.Click += (_, _) => Redo();
        var deleteMenuItem = new MenuFlyoutItem { Text = "Delete" };
        deleteMenuItem.Click += (_, _) => DeleteSelected();
        editMenu.Items.Add(undoMenuItem);
        editMenu.Items.Add(redoMenuItem);
        editMenu.Items.Add(new MenuFlyoutSeparator());
        editMenu.Items.Add(deleteMenuItem);

        var viewMenu = new MenuBarItem { Title = "View" };
        var projectsMenuItem = new ToggleMenuFlyoutItem
        {
            Text = "Projects",
            IsChecked = projectsPaneToggle.IsChecked == true
        };
        var sourceMenuItem = new ToggleMenuFlyoutItem
        {
            Text = "Source",
            IsChecked = sourcePaneToggle.IsChecked == true
        };
        var toolsMenuItem = new ToggleMenuFlyoutItem
        {
            Text = "Inspector / Tools",
            IsChecked = toolsPaneToggle.IsChecked == true
        };
        projectsMenuItem.Click += (_, _) => projectsPaneToggle.IsChecked = projectsMenuItem.IsChecked;
        sourceMenuItem.Click += (_, _) => sourcePaneToggle.IsChecked = sourceMenuItem.IsChecked;
        toolsMenuItem.Click += (_, _) => toolsPaneToggle.IsChecked = toolsMenuItem.IsChecked;
        viewMenu.Items.Add(projectsMenuItem);
        viewMenu.Items.Add(sourceMenuItem);
        viewMenu.Items.Add(toolsMenuItem);
        viewMenu.Items.Add(new MenuFlyoutSeparator());
        var fitMenuItem = new MenuFlyoutItem { Text = "Fit Designer" };
        fitMenuItem.Click += (_, _) => viewportDisplayMode.SelectedItem = "Fit";
        var actualMenuItem = new MenuFlyoutItem { Text = "Actual Size" };
        actualMenuItem.Click += (_, _) => viewportDisplayMode.SelectedItem = "1:1";
        viewMenu.Items.Add(fitMenuItem);
        viewMenu.Items.Add(actualMenuItem);

        var projectMenu = new MenuBarItem { Title = "Project" };
        var projectAddMenuItem = new MenuFlyoutItem { Text = "Add Folder…" };
        projectAddMenuItem.Click += async (_, _) => await AddProjectFolderAsync();
        var projectRefreshMenuItem = new MenuFlyoutItem { Text = "Refresh" };
        projectRefreshMenuItem.Click += (_, _) => RebuildProjectExplorer();
        var projectExplorerMenuItem = new MenuFlyoutItem { Text = "Open Selected Root in Explorer" };
        projectExplorerMenuItem.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(selectedProjectRoot) && Directory.Exists(selectedProjectRoot))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{selectedProjectRoot}\"",
                    UseShellExecute = true
                });
            }
        };
        projectMenu.Items.Add(projectAddMenuItem);
        projectMenu.Items.Add(projectRefreshMenuItem);
        projectMenu.Items.Add(projectExplorerMenuItem);

        var designerMenu = new MenuBarItem { Title = "Designer" };
        var renderMenuItem = new MenuFlyoutItem { Text = "Render Now" };
        renderMenuItem.Click += (_, _) => RenderSource();
        var referenceMenuItem = new MenuFlyoutItem { Text = "Load Reference…" };
        referenceMenuItem.Click += async (_, _) => await LoadReferenceAsync();
        var reviewMenuItem = new MenuFlyoutItem { Text = "Export Review Package…" };
        reviewMenuItem.Click += async (_, _) => await ExportReviewPackageAsync();
        designerMenu.Items.Add(renderMenuItem);
        designerMenu.Items.Add(referenceMenuItem);
        designerMenu.Items.Add(reviewMenuItem);

        var helpMenu = new MenuBarItem { Title = "Help" };
        var aboutMenuItem = new MenuFlyoutItem { Text = "About WinUI Forge" };
        aboutMenuItem.Click += (_, _) => status.Text = "WinUI Forge · visual XAML authoring and review.";
        helpMenu.Items.Add(aboutMenuItem);

        menuBar.Items.Add(fileMenu);
        menuBar.Items.Add(editMenu);
        menuBar.Items.Add(viewMenu);
        menuBar.Items.Add(projectMenu);
        menuBar.Items.Add(designerMenu);
        menuBar.Items.Add(helpMenu);
        menuGrid.Children.Add(menuBar);

        var productName = new TextBlock
        {
            Text = "WinUI Forge",
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0)
        };
        Grid.SetColumn(productName, 1);
        menuGrid.Children.Add(productName);
        menuHost.Child = menuGrid;
        shellRoot.Children.Add(menuHost);

        // Primary toolbar: left aligned, icon + text, native CommandBar behavior.
        var toolbarHost = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var commandBar = new CommandBar
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsOpen = false
        };

        var newButton = ToolbarButton("New", "\uE710");
        newButton.Click += (_, _) => LoadNewDocument();
        var referenceButton = ToolbarButton("Reference", "\uEB9F");
        referenceButton.Click += async (_, _) => await LoadReferenceAsync();
        var reviewButton = ToolbarButton("Review", "\uE8A5");
        reviewButton.Click += async (_, _) => await ExportReviewPackageAsync();

        commandBar.PrimaryCommands.Add(newButton);
        commandBar.PrimaryCommands.Add(openSourceFileButton);
        commandBar.PrimaryCommands.Add(saveSourceFileButton);
        commandBar.PrimaryCommands.Add(new AppBarSeparator());
        commandBar.PrimaryCommands.Add(undoButton);
        commandBar.PrimaryCommands.Add(redoButton);
        commandBar.PrimaryCommands.Add(deleteButton);
        commandBar.PrimaryCommands.Add(new AppBarSeparator());
        commandBar.PrimaryCommands.Add(referenceButton);
        commandBar.PrimaryCommands.Add(reviewButton);
        commandBar.PrimaryCommands.Add(renderButton);

        var resetWorkspace = new AppBarButton { Label = "Reset Workspace" };
        resetWorkspace.Click += (_, _) =>
        {
            projectsPaneToggle.IsChecked = true;
            sourcePaneToggle.IsChecked = true;
            toolsPaneToggle.IsChecked = true;
            userSettings.ProjectsPaneWidth = 300;
            userSettings.SourcePaneWidth = 430;
            userSettings.ToolsPaneWidth = 410;
            ApplyWorkspacePanelVisibility();
        };
        commandBar.SecondaryCommands.Add(resetWorkspace);
        toolbarHost.Child = commandBar;
        Grid.SetRow(toolbarHost, 1);
        shellRoot.Children.Add(toolbarHost);

        // Main development workspace: activity rail | projects | source | designer | tools.
        workspaceGrid = new Grid { Background = BorderBrush };
        workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        projectsWorkspaceColumn = new ColumnDefinition
        {
            Width = new GridLength(Math.Clamp(userSettings.ProjectsPaneWidth, 220, 700)),
            MinWidth = 220
        };
        projectsSplitterColumn = new ColumnDefinition { Width = new GridLength(6) };
        sourceWorkspaceColumn = new ColumnDefinition
        {
            Width = new GridLength(Math.Clamp(userSettings.SourcePaneWidth, 300, 900)),
            MinWidth = 300
        };
        sourceSplitterColumn = new ColumnDefinition { Width = new GridLength(6) };
        previewWorkspaceColumn = new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 500
        };
        toolsSplitterColumn = new ColumnDefinition { Width = new GridLength(6) };
        toolsWorkspaceColumn = new ColumnDefinition
        {
            Width = new GridLength(Math.Clamp(userSettings.ToolsPaneWidth, 280, 900)),
            MinWidth = 280
        };

        workspaceGrid.ColumnDefinitions.Add(projectsWorkspaceColumn);
        workspaceGrid.ColumnDefinitions.Add(projectsSplitterColumn);
        workspaceGrid.ColumnDefinitions.Add(sourceWorkspaceColumn);
        workspaceGrid.ColumnDefinitions.Add(sourceSplitterColumn);
        workspaceGrid.ColumnDefinitions.Add(previewWorkspaceColumn);
        workspaceGrid.ColumnDefinitions.Add(toolsSplitterColumn);
        workspaceGrid.ColumnDefinitions.Add(toolsWorkspaceColumn);
        Grid.SetRow(workspaceGrid, 2);
        shellRoot.Children.Add(workspaceGrid);

        // Activity rail.
        var activityRail = new Border
        {
            Background = Brush(23, 26, 29),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        var railStack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(5, 8, 5, 8)
        };

        Button RailButton(string glyph, string tooltip, bool active = false)
        {
            var button = new Button
            {
                Width = 38,
                Height = 38,
                Padding = new Thickness(0),
                Background = active ? ElevatedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            button.Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 14,
                Foreground = active ? AccentBrush : TextBrush
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        var projectsRailButton = RailButton("\uE8B7", "Projects", projectsPaneToggle.IsChecked == true);
        projectsRailButton.Click += (_, _) => projectsPaneToggle.IsChecked = !(projectsPaneToggle.IsChecked == true);
        var sourceRailButton = RailButton("\uE943", "Source", sourcePaneToggle.IsChecked == true);
        sourceRailButton.Click += (_, _) => sourcePaneToggle.IsChecked = !(sourcePaneToggle.IsChecked == true);
        var searchRailButton = RailButton("\uE721", "Search");
        searchRailButton.Click += (_, _) => status.Text = "Workspace search is planned for a later Forge milestone.";
        var focusRailButton = RailButton("\uE790", "Designer focus");
        focusRailButton.Click += (_, _) =>
        {
            projectsPaneToggle.IsChecked = false;
            sourcePaneToggle.IsChecked = false;
            toolsPaneToggle.IsChecked = false;
        };

        railStack.Children.Add(projectsRailButton);
        railStack.Children.Add(sourceRailButton);
        railStack.Children.Add(searchRailButton);
        railStack.Children.Add(focusRailButton);
        activityRail.Child = railStack;
        workspaceGrid.Children.Add(activityRail);

        // Projects panel.
        var projectHost = new Grid { Background = Brush(29, 32, 36) };
        projectHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        projectHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        projectHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        projectHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        projectHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

        var projectTitleBorder = new Border
        {
            Background = PanelBrush,
            Padding = new Thickness(10, 8, 10, 3)
        };
        projectTitleBorder.Child = new TextBlock
        {
            Text = "PROJECTS",
            Foreground = TextBrush,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        projectHost.Children.Add(projectTitleBorder);

        var projectDescriptionBorder = new Border
        {
            Background = PanelBrush,
            Padding = new Thickness(10, 0, 10, 7)
        };
        var projectDescription = new StackPanel { Spacing = 2 };
        projectDescription.Children.Add(new TextBlock
        {
            Text = "Open UI files and paired references from project folders.",
            Foreground = MutedBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });
        projectExplorerInfo.FontSize = 10;
        projectExplorerInfo.Opacity = 0.78;
        projectDescription.Children.Add(projectExplorerInfo);
        projectDescriptionBorder.Child = projectDescription;
        Grid.SetRow(projectDescriptionBorder, 1);
        projectHost.Children.Add(projectDescriptionBorder);

        var projectToolbar = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 5, 8, 5)
        };
        var projectToolbarStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        addProjectFolderButton.Content = "＋  Add folder";
        removeProjectFolderButton.Content = "⌫  Remove";
        refreshProjectsButton.Content = "↻";
        addProjectFolderButton.Height = 28;
        removeProjectFolderButton.Height = 28;
        refreshProjectsButton.Width = 34;
        refreshProjectsButton.Height = 28;
        projectToolbarStack.Children.Add(addProjectFolderButton);
        projectToolbarStack.Children.Add(removeProjectFolderButton);
        projectToolbarStack.Children.Add(refreshProjectsButton);
        projectToolbar.Child = projectToolbarStack;
        Grid.SetRow(projectToolbar, 2);
        projectHost.Children.Add(projectToolbar);

        var projectScroll = new ScrollViewer
        {
            Content = projectExplorerPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(projectScroll, 3);
        projectHost.Children.Add(projectScroll);

        var projectFooter = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 0, 8, 0)
        };
        projectFooter.Child = new TextBlock
        {
            Text = "Project files remain normal files on disk",
            Foreground = MutedBrush,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(projectFooter, 4);
        projectHost.Children.Add(projectFooter);

        projectsPaneHost = projectHost;
        Grid.SetColumn(projectHost, 1);
        workspaceGrid.Children.Add(projectHost);

        projectsSourceSplitter = Splitter();
        Grid.SetColumn(projectsSourceSplitter, 2);
        workspaceGrid.Children.Add(projectsSourceSplitter);

        // Source editor.
        var editorHost = new Grid { Background = Brush(27, 30, 33) };
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        editorHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

        var sourceHeader = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 0, 8, 0)
        };
        var sourceHeaderGrid = new Grid();
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var sourceTitle = new TextBlock
        {
            Text = "SOURCE",
            Foreground = MutedBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        sourceHeaderGrid.Children.Add(sourceTitle);

        var sourceActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var sourceReload = IconButton("\uE72C", "Reload XAML");
        sourceReload.Click += (_, _) => ReloadCurrentSourceFile();
        var sourceSave = IconButton("\uE74E", "Save XAML");
        sourceSave.Click += (_, _) => SaveCurrentSourceFile();
        sourceActions.Children.Add(sourceReload);
        sourceActions.Children.Add(sourceSave);
        Grid.SetColumn(sourceActions, 1);
        sourceHeaderGrid.Children.Add(sourceActions);
        sourceHeader.Child = sourceHeaderGrid;
        editorHost.Children.Add(sourceHeader);

        Grid.SetRow(sourceEditor, 1);
        editorHost.Children.Add(sourceEditor);

        var sourceFooter = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 0, 8, 0)
        };
        diagnostics.Margin = new Thickness(0);
        diagnostics.Foreground = MutedBrush;
        diagnostics.FontSize = 10;
        diagnostics.MaxHeight = 30;
        sourceFooter.Child = diagnostics;
        Grid.SetRow(sourceFooter, 2);
        editorHost.Children.Add(sourceFooter);

        sourcePaneHost = editorHost;
        Grid.SetColumn(editorHost, 3);
        workspaceGrid.Children.Add(editorHost);

        sourcePreviewSplitter = Splitter();
        Grid.SetColumn(sourcePreviewSplitter, 4);
        workspaceGrid.Children.Add(sourcePreviewSplitter);

        // Designer.
        var previewHost = new Grid { Background = WindowBrush };
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        var designerHeader = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 0, 8, 0)
        };
        var designerHeaderGrid = new Grid();
        designerHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        designerHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        designerHeaderGrid.Children.Add(new TextBlock
        {
            Text = "DESIGNER",
            Foreground = MutedBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var designerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var fitButton = IconButton("\uE9A6", "Fit");
        fitButton.Click += (_, _) => viewportDisplayMode.SelectedItem = "Fit";
        var actualButton = IconButton("\uE8A3", "Actual size");
        actualButton.Click += (_, _) => viewportDisplayMode.SelectedItem = "1:1";
        var overlayButton = IconButton("\uE7B3", "Toggle reference");
        overlayButton.Click += (_, _) =>
        {
            if (referenceVisibleCheckBox.IsEnabled)
                referenceVisibleCheckBox.IsChecked = !(referenceVisibleCheckBox.IsChecked == true);
        };
        designerActions.Children.Add(fitButton);
        designerActions.Children.Add(actualButton);
        designerActions.Children.Add(overlayButton);
        Grid.SetColumn(designerActions, 1);
        designerHeaderGrid.Children.Add(designerActions);
        designerHeader.Child = designerHeaderGrid;
        previewHost.Children.Add(designerHeader);

        var designerToolbar = new Grid
        {
            Background = Brush(29, 32, 36),
            Padding = new Thickness(8, 4, 8, 4),
            ColumnSpacing = 8
        };
        designerToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        designerToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        designerToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        designerToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        viewportInfo.FontSize = 10;
        viewportInfo.VerticalAlignment = VerticalAlignment.Center;
        designerToolbar.Children.Add(viewportInfo);

        viewportDisplayMode.Header = null;
        viewportDisplayMode.Width = 84;
        Grid.SetColumn(viewportDisplayMode, 1);
        designerToolbar.Children.Add(viewportDisplayMode);

        referenceVisibleCheckBox.Content = "Overlay";
        Grid.SetColumn(referenceVisibleCheckBox, 2);
        designerToolbar.Children.Add(referenceVisibleCheckBox);

        var opacityRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        opacityRow.Children.Add(new TextBlock
        {
            Text = "Opacity",
            Foreground = MutedBrush,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        });
        referenceOpacitySlider.Width = 96;
        opacityRow.Children.Add(referenceOpacitySlider);
        Grid.SetColumn(opacityRow, 3);
        designerToolbar.Children.Add(opacityRow);

        Grid.SetRow(designerToolbar, 1);
        previewHost.Children.Add(designerToolbar);

        var previewFrame = new Border
        {
            Margin = new Thickness(16),
            Background = ElevatedBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1)
        };
        previewStage.Background = ElevatedBrush;
        previewStage.Children.Add(previewContent);
        previewStage.Children.Add(referenceOverlay);
        previewStage.Children.Add(allHighlightsLayer);
        previewStage.Children.Add(selectionLayer);

        previewViewbox.Child = previewStage;
        previewViewportShell.Children.Add(previewViewbox);
        previewViewportShell.Children.Add(previewScrollViewer);
        previewFrame.Child = previewViewportShell;
        Grid.SetRow(previewFrame, 2);
        previewHost.Children.Add(previewFrame);

        var designerFooter = new Border
        {
            Background = PanelBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 0, 8, 0)
        };
        referenceInfo.FontSize = 10;
        referenceInfo.VerticalAlignment = VerticalAlignment.Center;
        designerFooter.Child = referenceInfo;
        Grid.SetRow(designerFooter, 3);
        previewHost.Children.Add(designerFooter);

        Grid.SetColumn(previewHost, 5);
        workspaceGrid.Children.Add(previewHost);

        previewToolsSplitter = Splitter();
        Grid.SetColumn(previewToolsSplitter, 6);
        workspaceGrid.Children.Add(previewToolsSplitter);

        // Toolbox / Visual Tree / Inspector.
        toolsTabs = new TabView
        {
            IsAddTabButtonVisible = false,
            Background = PanelBrush
        };
        toolsPaneHost = toolsTabs;

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

        var treeHost = new Grid { Background = PanelBrush };
        treeHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        treeHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var treeTools = new Grid
        {
            Padding = new Thickness(8, 6, 8, 6),
            ColumnSpacing = 8
        };
        treeTools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        treeTools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        treeTools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        visualTreeFilter.HorizontalAlignment = HorizontalAlignment.Stretch;
        treeTools.Children.Add(visualTreeFilter);
        Grid.SetColumn(visualTreeNamedOnly, 1);
        treeTools.Children.Add(visualTreeNamedOnly);
        Grid.SetColumn(visualTreeCount, 2);
        treeTools.Children.Add(visualTreeCount);
        treeHost.Children.Add(treeTools);

        visualTree.Margin = new Thickness(8, 0, 8, 8);
        Grid.SetRow(visualTree, 1);
        treeHost.Children.Add(visualTree);

        var treeTab = new TabViewItem
        {
            Header = "Visual Tree",
            IsClosable = false,
            Content = treeHost
        };

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

        toolsTabs.TabItems.Add(toolboxTab);
        toolsTabs.TabItems.Add(treeTab);
        toolsTabs.TabItems.Add(inspectorTab);
        toolsTabs.SelectedIndex = Math.Clamp(userSettings.ToolsTabIndex, 0, 2);
        Grid.SetColumn(toolsTabs, 7);
        workspaceGrid.Children.Add(toolsTabs);

        // Global status bar.
        var statusBorder = new Border
        {
            Background = Brush(20, 23, 25),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 0, 8, 0)
        };
        var statusGrid = new Grid();
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        status.Foreground = MutedBrush;
        status.FontSize = 10;
        status.VerticalAlignment = VerticalAlignment.Center;
        statusGrid.Children.Add(status);
        var statusRight = new TextBlock
        {
            Text = "WinUI 3 · Forge",
            Foreground = MutedBrush,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusRight, 1);
        statusGrid.Children.Add(statusRight);
        statusBorder.Child = statusGrid;
        Grid.SetRow(statusBorder, 3);
        shellRoot.Children.Add(statusBorder);

        // Keep View menu checkmarks synchronized with rail/menu toggles.
        projectsPaneToggle.Checked += (_, _) => projectsMenuItem.IsChecked = true;
        projectsPaneToggle.Unchecked += (_, _) => projectsMenuItem.IsChecked = false;
        sourcePaneToggle.Checked += (_, _) => sourceMenuItem.IsChecked = true;
        sourcePaneToggle.Unchecked += (_, _) => sourceMenuItem.IsChecked = false;
        toolsPaneToggle.Checked += (_, _) => toolsMenuItem.IsChecked = true;
        toolsPaneToggle.Unchecked += (_, _) => toolsMenuItem.IsChecked = false;

        return shellRoot;
    }

    void WireEvents()
    {
        projectsPaneToggle.Checked += (_, _) => ApplyWorkspacePanelVisibility();
        projectsPaneToggle.Unchecked += (_, _) => ApplyWorkspacePanelVisibility();
        sourcePaneToggle.Checked += (_, _) => ApplyWorkspacePanelVisibility();
        sourcePaneToggle.Unchecked += (_, _) => ApplyWorkspacePanelVisibility();
        toolsPaneToggle.Checked += (_, _) => ApplyWorkspacePanelVisibility();
        toolsPaneToggle.Unchecked += (_, _) => ApplyWorkspacePanelVisibility();

        projectsSourceSplitter.DragDelta += (_, e) => ResizeProjectsPane(e.HorizontalChange);
        projectsSourceSplitter.DragCompleted += (_, _) => SaveWorkspaceSettings();
        sourcePreviewSplitter.DragDelta += (_, e) => ResizeSourcePane(e.HorizontalChange);
        sourcePreviewSplitter.DragCompleted += (_, _) => SaveWorkspaceSettings();
        previewToolsSplitter.DragDelta += (_, e) => ResizeToolsPane(e.HorizontalChange);
        previewToolsSplitter.DragCompleted += (_, _) => SaveWorkspaceSettings();

        toolsTabs.SelectionChanged += (_, _) =>
        {
            userSettings.ToolsTabIndex = toolsTabs.SelectedIndex;
            userSettings.Save();
        };

        Closed += (_, _) => SaveWorkspaceSettings();

        addProjectFolderButton.Click += async (_, _) => await AddProjectFolderAsync();
        removeProjectFolderButton.Click += (_, _) => RemoveSelectedProjectFolder();
        refreshProjectsButton.Click += (_, _) => RebuildProjectExplorer();

        undoButton.Click += (_, _) => Undo();
        redoButton.Click += (_, _) => Redo();
        deleteButton.Click += (_, _) => DeleteSelected();
        renderButton.Click += (_, _) => RenderSource();

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
            UpdateSourceFileButtons();

            renderTimer.Stop();
            renderTimer.Start();
        };

        sourceEditor.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnSourcePointerReleased),
            true);

        visualTree.SelectionChanged += OnTreeSelectionChanged;
        visualTreeFilter.TextChanged += (_, _) => RebuildVisualTree();
        visualTreeNamedOnly.Checked += (_, _) => RebuildVisualTree();
        visualTreeNamedOnly.Unchecked += (_, _) => RebuildVisualTree();

        openSourceFileButton.Click += async (_, _) => await OpenSourceFileAsync();
        reloadSourceFileButton.Click += (_, _) => ReloadCurrentSourceFile();
        saveSourceFileButton.Click += (_, _) => SaveCurrentSourceFile();

        loadReferenceButton.Click += async (_, _) => await LoadReferenceAsync();
        exportReviewPackageButton.Click += async (_, _) => await ExportReviewPackageAsync();
        referenceVisibleCheckBox.Checked += (_, _) => UpdateReferenceOverlay();
        referenceVisibleCheckBox.Unchecked += (_, _) => UpdateReferenceOverlay();
        highlightAllCheckBox.Checked += (_, _) => DrawAllHighlights();
        highlightAllCheckBox.Unchecked += (_, _) => DrawAllHighlights();
        referenceOpacitySlider.ValueChanged += (_, _) => UpdateReferenceOverlay();
        applyViewportButton.Click += (_, _) => ApplyViewportFromInputs();
        useReferenceSizeButton.Click += (_, _) => UseReferenceViewport();
        viewportDisplayMode.SelectionChanged += (_, _) => ApplyViewportDisplayMode();

        previewStage.SizeChanged += (_, _) =>
        {
            DrawAllHighlights();
            DrawSelection();
        };
        previewStage.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPreviewPointerPressed),
            true);
    }

    void ApplyWorkspacePanelVisibility(bool persist = true)
    {
        if (projectsPaneHost is null ||
            sourcePaneHost is null ||
            toolsPaneHost is null ||
            projectsSourceSplitter is null ||
            sourcePreviewSplitter is null ||
            previewToolsSplitter is null)
            return;

        var showProjects = projectsPaneToggle.IsChecked == true;
        var showSource = sourcePaneToggle.IsChecked == true;
        var showTools = toolsPaneToggle.IsChecked == true;

        projectsPaneHost.Visibility = showProjects ? Visibility.Visible : Visibility.Collapsed;
        projectsSourceSplitter.Visibility = showProjects ? Visibility.Visible : Visibility.Collapsed;
        projectsWorkspaceColumn.MinWidth = showProjects ? 220 : 0;
        projectsWorkspaceColumn.Width = showProjects
            ? new GridLength(Math.Clamp(userSettings.ProjectsPaneWidth, 220, 700))
            : new GridLength(0);
        projectsSplitterColumn.Width = showProjects
            ? new GridLength(6)
            : new GridLength(0);

        sourcePaneHost.Visibility = showSource ? Visibility.Visible : Visibility.Collapsed;
        sourcePreviewSplitter.Visibility = showSource ? Visibility.Visible : Visibility.Collapsed;
        sourceWorkspaceColumn.MinWidth = showSource ? 300 : 0;
        sourceWorkspaceColumn.Width = showSource
            ? new GridLength(Math.Clamp(userSettings.SourcePaneWidth, 300, 900))
            : new GridLength(0);
        sourceSplitterColumn.Width = showSource
            ? new GridLength(6)
            : new GridLength(0);

        toolsPaneHost.Visibility = showTools ? Visibility.Visible : Visibility.Collapsed;
        previewToolsSplitter.Visibility = showTools ? Visibility.Visible : Visibility.Collapsed;
        toolsWorkspaceColumn.MinWidth = showTools ? 280 : 0;
        toolsWorkspaceColumn.Width = showTools
            ? new GridLength(Math.Clamp(userSettings.ToolsPaneWidth, 280, 900))
            : new GridLength(0);
        toolsSplitterColumn.Width = showTools
            ? new GridLength(6)
            : new GridLength(0);

        previewWorkspaceColumn.MinWidth = 500;
        previewWorkspaceColumn.Width = new GridLength(1, GridUnitType.Star);

        if (persist)
        {
            userSettings.ProjectsPaneVisible = showProjects;
            userSettings.SourcePaneVisible = showSource;
            userSettings.ToolsPaneVisible = showTools;
            userSettings.Save();
        }

        previewStage.DispatcherQueue.TryEnqueue(() =>
        {
            previewStage.UpdateLayout();
            DrawAllHighlights();
            DrawSelection();
        });

        status.Text = $"Projects {(showProjects ? "visible" : "hidden")} · " +
                      $"Source {(showSource ? "visible" : "hidden")} · " +
                      $"Inspector/Tools {(showTools ? "visible" : "hidden")}.";
    }

    void ResizeProjectsPane(double horizontalChange)
    {
        if (projectsPaneToggle.IsChecked != true || Math.Abs(horizontalChange) < 0.01)
            return;

        var width = Math.Clamp(
            projectsWorkspaceColumn.ActualWidth + horizontalChange,
            220,
            700);

        projectsWorkspaceColumn.Width = new GridLength(width);
        userSettings.ProjectsPaneWidth = width;
    }

    void ResizeSourcePane(double horizontalChange)
    {
        if (sourcePaneToggle.IsChecked != true || Math.Abs(horizontalChange) < 0.01)
            return;

        var projectsWidth = projectsPaneToggle.IsChecked == true
            ? projectsWorkspaceColumn.ActualWidth + projectsSplitterColumn.ActualWidth
            : 0;
        var toolsWidth = toolsPaneToggle.IsChecked == true
            ? toolsWorkspaceColumn.ActualWidth + toolsSplitterColumn.ActualWidth
            : 0;
        var maximum = Math.Max(
            300,
            Math.Min(900, workspaceGrid.ActualWidth - projectsWidth - toolsWidth - previewWorkspaceColumn.MinWidth - 18));
        var width = Math.Clamp(
            sourceWorkspaceColumn.ActualWidth + horizontalChange,
            300,
            maximum);

        sourceWorkspaceColumn.Width = new GridLength(width);
        userSettings.SourcePaneWidth = width;
    }

    void ResizeToolsPane(double horizontalChange)
    {
        if (toolsPaneToggle.IsChecked != true || Math.Abs(horizontalChange) < 0.01)
            return;

        var projectsWidth = projectsPaneToggle.IsChecked == true
            ? projectsWorkspaceColumn.ActualWidth + projectsSplitterColumn.ActualWidth
            : 0;
        var sourceWidth = sourcePaneToggle.IsChecked == true
            ? sourceWorkspaceColumn.ActualWidth + sourceSplitterColumn.ActualWidth
            : 0;
        var maximum = Math.Max(
            280,
            Math.Min(900, workspaceGrid.ActualWidth - projectsWidth - sourceWidth - previewWorkspaceColumn.MinWidth - 18));
        var width = Math.Clamp(
            toolsWorkspaceColumn.ActualWidth - horizontalChange,
            280,
            maximum);

        toolsWorkspaceColumn.Width = new GridLength(width);
        userSettings.ToolsPaneWidth = width;
    }

    void SaveWorkspaceSettings()
    {
        userSettings.ProjectsPaneVisible = projectsPaneToggle.IsChecked == true;
        userSettings.SourcePaneVisible = sourcePaneToggle.IsChecked == true;
        userSettings.ToolsPaneVisible = toolsPaneToggle.IsChecked == true;
        userSettings.ToolsTabIndex = toolsTabs?.SelectedIndex ?? userSettings.ToolsTabIndex;

        if (projectsPaneToggle.IsChecked == true && projectsWorkspaceColumn.ActualWidth >= 220)
            userSettings.ProjectsPaneWidth = projectsWorkspaceColumn.ActualWidth;

        if (sourcePaneToggle.IsChecked == true && sourceWorkspaceColumn.ActualWidth >= 300)
            userSettings.SourcePaneWidth = sourceWorkspaceColumn.ActualWidth;

        if (toolsPaneToggle.IsChecked == true && toolsWorkspaceColumn.ActualWidth >= 280)
            userSettings.ToolsPaneWidth = toolsWorkspaceColumn.ActualWidth;

        userSettings.Save();
    }

    void EnsureDefaultProjectFolders()
    {
        userSettings.ProjectFolders = userSettings.ProjectFolders
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userSettings.ProjectFolders.Count == 0)
        {
            var descriptor = FindRepositoryFile("benchmarks", "Workshop UI.forgeproject");
            var benchmarkRoot = descriptor is null ? null : Path.GetDirectoryName(descriptor);
            if (!string.IsNullOrWhiteSpace(benchmarkRoot) && Directory.Exists(benchmarkRoot))
                userSettings.ProjectFolders.Add(benchmarkRoot);
        }

        foreach (var root in userSettings.ProjectFolders)
            expandedProjectDirectories.Add(Path.GetFullPath(root));

        userSettings.Save();
    }

    async Task AddProjectFolderAsync()
    {
        try
        {
            var picker = new FolderPicker(AppWindow.Id)
            {
                Title = "Add Forge project folder"
            };

            var result = await picker.PickSingleFolderAsync();
            if (result is null)
                return;

            var path = Path.GetFullPath(result.Path);
            if (!userSettings.ProjectFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
                userSettings.ProjectFolders.Add(path);

            expandedProjectDirectories.Add(path);
            selectedProjectRoot = path;
            userSettings.Save();
            RebuildProjectExplorer();

            status.Text = $"Added project folder: {path}";
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Add project folder error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not add project folder: " + ex.Message;
        }
    }

    void RemoveSelectedProjectFolder()
    {
        if (string.IsNullOrWhiteSpace(selectedProjectRoot))
            return;

        userSettings.ProjectFolders.RemoveAll(path =>
            string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(selectedProjectRoot),
                StringComparison.OrdinalIgnoreCase));

        expandedProjectDirectories.RemoveWhere(path =>
            path.StartsWith(
                Path.GetFullPath(selectedProjectRoot),
                StringComparison.OrdinalIgnoreCase));

        status.Text = $"Removed project folder: {selectedProjectRoot}";
        selectedProjectRoot = null;
        userSettings.Save();
        RebuildProjectExplorer();
    }

    void RebuildProjectExplorer()
    {
        projectExplorerPanel.Children.Clear();
        projectScreensByPath.Clear();

        var roots = userSettings.ProjectFolders
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFullPath)
            .ToList();

        userSettings.ProjectFolders = roots;
        userSettings.Save();

        if (roots.Count == 0)
        {
            projectExplorerInfo.Text =
                "No project folders yet. Add a folder containing XAML, Forge sidecars and reference images.";
            removeProjectFolderButton.IsEnabled = false;
            return;
        }

        projectExplorerInfo.Text =
            $"{roots.Count} project folder{(roots.Count == 1 ? string.Empty : "s")} · " +
            "UI files and references are paired by .forgeproject metadata when available.";

        foreach (var root in roots)
        {
            LoadProjectDescriptors(root);
            AddProjectDirectoryRows(root, root, depth: 0);
        }

        removeProjectFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(selectedProjectRoot);
    }

    void LoadProjectDescriptors(string root)
    {
        IEnumerable<string> descriptors;
        try
        {
            descriptors = Directory.EnumerateFiles(root, "*.forgeproject", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (var descriptorPath in descriptors)
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(descriptorPath));
                var descriptorRoot = json.RootElement;

                if (!descriptorRoot.TryGetProperty("screens", out var screens) ||
                    screens.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var screen in screens.EnumerateArray())
                {
                    if (!screen.TryGetProperty("xaml", out var xamlElement) ||
                        xamlElement.ValueKind != JsonValueKind.String)
                        continue;

                    var xamlRelative = xamlElement.GetString();
                    if (string.IsNullOrWhiteSpace(xamlRelative))
                        continue;

                    var name = screen.TryGetProperty("name", out var nameElement) &&
                               nameElement.ValueKind == JsonValueKind.String
                        ? nameElement.GetString() ?? Path.GetFileNameWithoutExtension(xamlRelative)
                        : Path.GetFileNameWithoutExtension(xamlRelative);

                    var xamlPath = Path.GetFullPath(Path.Combine(root, xamlRelative));
                    string? referencePath = null;
                    if (screen.TryGetProperty("reference", out var referenceElement) &&
                        referenceElement.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(referenceElement.GetString()))
                    {
                        referencePath = Path.GetFullPath(
                            Path.Combine(root, referenceElement.GetString()!));
                    }

                    double? viewportWidth = null;
                    double? viewportHeight = null;
                    if (screen.TryGetProperty("viewport", out var viewport) &&
                        viewport.ValueKind == JsonValueKind.Object)
                    {
                        if (viewport.TryGetProperty("width", out var width) && width.TryGetDouble(out var parsedWidth))
                            viewportWidth = parsedWidth;
                        if (viewport.TryGetProperty("height", out var height) && height.TryGetDouble(out var parsedHeight))
                            viewportHeight = parsedHeight;
                    }

                    var projectScreen = new ForgeProjectScreen(
                        name,
                        xamlPath,
                        referencePath,
                        viewportWidth,
                        viewportHeight);

                    projectScreensByPath[xamlPath] = projectScreen;
                    if (!string.IsNullOrWhiteSpace(referencePath))
                        projectScreensByPath[referencePath] = projectScreen;
                }
            }
            catch (Exception ex)
            {
                diagnostics.Text = $"Forge project warning ({Path.GetFileName(descriptorPath)}): {ex.Message}";
                diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            }
        }
    }

    void AddProjectDirectoryRows(string root, string directory, int depth)
    {
        if (IsIgnoredProjectDirectory(directory))
            return;

        var fullDirectory = Path.GetFullPath(directory);
        var expanded = expandedProjectDirectories.Contains(fullDirectory);
        var isRoot = string.Equals(
            Path.GetFullPath(root),
            fullDirectory,
            StringComparison.OrdinalIgnoreCase);

        var folderEntry = new ForgeProjectEntry(
            root,
            fullDirectory,
            ForgeProjectFileKind.Folder);

        projectExplorerPanel.Children.Add(CreateProjectRow(
            folderEntry,
            depth,
            isRoot ? $"▣ {Path.GetFileName(fullDirectory)}" : $"{(expanded ? "▾" : "▸")} {Path.GetFileName(fullDirectory)}",
            isRoot ? AccentBrush : TextBrush));

        if (!expanded)
            return;

        string[] directories;
        string[] files;
        try
        {
            directories = Directory.GetDirectories(fullDirectory)
                .Where(path => !IsIgnoredProjectDirectory(path))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            files = Directory.GetFiles(fullDirectory)
                .Where(IsVisibleProjectFile)
                .OrderBy(ProjectFileSortOrder)
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            projectExplorerPanel.Children.Add(new TextBlock
            {
                Text = $"Cannot read {fullDirectory}: {ex.Message}",
                Margin = new Thickness(12 + depth * 16, 4, 6, 4),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var childDirectory in directories)
            AddProjectDirectoryRows(root, childDirectory, depth + 1);

        foreach (var file in files)
        {
            var kind = ProjectFileKind(file);
            var entry = new ForgeProjectEntry(root, file, kind);
            var (prefix, brush) = ProjectFilePresentation(kind);

            projectExplorerPanel.Children.Add(CreateProjectRow(
                entry,
                depth + 1,
                $"{prefix} {Path.GetFileName(file)}",
                brush));
        }
    }

    Button CreateProjectRow(
        ForgeProjectEntry entry,
        int depth,
        string text,
        Brush foreground)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8 + depth * 16, 5, 8, 5),
            Tag = entry,
            Content = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        button.Click += async (_, _) => await OpenProjectEntryAsync(entry);
        return button;
    }

    async Task OpenProjectEntryAsync(ForgeProjectEntry entry)
    {
        selectedProjectRoot = entry.RootPath;
        removeProjectFolderButton.IsEnabled = true;

        if (entry.Kind == ForgeProjectFileKind.Folder)
        {
            if (!expandedProjectDirectories.Add(entry.FullPath))
                expandedProjectDirectories.Remove(entry.FullPath);

            RebuildProjectExplorer();
            return;
        }

        try
        {
            switch (entry.Kind)
            {
                case ForgeProjectFileKind.Xaml:
                    await OpenProjectXamlAsync(entry.FullPath);
                    break;

                case ForgeProjectFileKind.ReferenceImage:
                    await OpenProjectReferenceAsync(entry.FullPath);
                    break;

                case ForgeProjectFileKind.Sidecar:
                    status.Text =
                        $"{Path.GetFileName(entry.FullPath)} is Forge metadata and is loaded automatically with its XAML.";
                    break;

                case ForgeProjectFileKind.Project:
                    status.Text =
                        $"{Path.GetFileName(entry.FullPath)} defines XAML/reference pairings for this project.";
                    break;

                default:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = entry.FullPath,
                        UseShellExecute = true
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Project open error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not open project item: " + ex.Message;
        }
    }

    async Task OpenProjectXamlAsync(string xamlPath)
    {
        LoadSourceFile(xamlPath, readOnly: false);

        if (projectScreensByPath.TryGetValue(Path.GetFullPath(xamlPath), out var screen))
        {
            if (!string.IsNullOrWhiteSpace(screen.ReferencePath) && File.Exists(screen.ReferencePath))
            {
                await LoadReferenceFromPathAsync(screen.ReferencePath, adoptViewport: false);
            }
            else
            {
                ClearReference();
            }

            if (screen.ViewportWidth is > 0 && screen.ViewportHeight is > 0)
                SetBenchmarkViewport(screen.ViewportWidth.Value, screen.ViewportHeight.Value);

            viewportDisplayMode.SelectedItem = "Fit";
            status.Text = !string.IsNullOrWhiteSpace(screen.ReferencePath) && File.Exists(screen.ReferencePath)
                ? $"Loaded {screen.Name} · XAML + paired reference."
                : $"Loaded {screen.Name} · reference image is not present in the project folder yet.";
            return;
        }

        var siblingReference = FindSiblingReference(xamlPath);
        if (siblingReference is not null)
            await LoadReferenceFromPathAsync(siblingReference);
        else
            ClearReference();

        status.Text = $"Loaded {Path.GetFileName(xamlPath)} from project.";
    }

    async Task OpenProjectReferenceAsync(string referencePath)
    {
        if (projectScreensByPath.TryGetValue(Path.GetFullPath(referencePath), out var screen) &&
            File.Exists(screen.XamlPath))
        {
            LoadSourceFile(screen.XamlPath, readOnly: false);
            await LoadReferenceFromPathAsync(referencePath, adoptViewport: false);

            if (screen.ViewportWidth is > 0 && screen.ViewportHeight is > 0)
                SetBenchmarkViewport(screen.ViewportWidth.Value, screen.ViewportHeight.Value);

            viewportDisplayMode.SelectedItem = "Fit";
            status.Text = $"Loaded {screen.Name} · paired reference + XAML.";
            return;
        }

        await LoadReferenceFromPathAsync(referencePath);
        status.Text = $"Loaded reference image: {Path.GetFileName(referencePath)}";
    }

    string? FindSiblingReference(string xamlPath)
    {
        var directory = Path.GetDirectoryName(xamlPath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var stem = Path.GetFileNameWithoutExtension(xamlPath);
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" })
        {
            var candidate = Path.Combine(directory, stem + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    static bool IsIgnoredProjectDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name is ".git" or ".vs" or "bin" or "obj" or "node_modules";
    }

    static bool IsVisibleProjectFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        return extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".forge.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".forgeproject", StringComparison.OrdinalIgnoreCase);
    }

    static int ProjectFileSortOrder(string path) =>
        ProjectFileKind(path) switch
        {
            ForgeProjectFileKind.Xaml => 0,
            ForgeProjectFileKind.ReferenceImage => 1,
            ForgeProjectFileKind.Sidecar => 2,
            ForgeProjectFileKind.Project => 3,
            _ => 4
        };

    static ForgeProjectFileKind ProjectFileKind(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        if (extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase))
            return ForgeProjectFileKind.Xaml;

        if (extension is not null &&
            new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase))
            return ForgeProjectFileKind.ReferenceImage;

        if (fileName.EndsWith(".forge.json", StringComparison.OrdinalIgnoreCase))
            return ForgeProjectFileKind.Sidecar;

        if (fileName.EndsWith(".forgeproject", StringComparison.OrdinalIgnoreCase))
            return ForgeProjectFileKind.Project;

        return ForgeProjectFileKind.Other;
    }

    static (string Prefix, Brush Foreground) ProjectFilePresentation(ForgeProjectFileKind kind) =>
        kind switch
        {
            ForgeProjectFileKind.Xaml => ("◇ UI", AccentBrush),
            ForgeProjectFileKind.ReferenceImage => ("▣ REF", TextBrush),
            ForgeProjectFileKind.Sidecar => ("· META", MutedBrush),
            ForgeProjectFileKind.Project => ("◆ PROJECT", AccentBrush),
            _ => ("·", MutedBrush)
        };

    async Task OpenSourceFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker(AppWindow.Id)
            {
                Title = "Open WinUI XAML"
            };
            picker.FileTypeFilter.Add(".xaml");

            var result = await picker.PickSingleFileAsync();
            if (result is null)
                return;

            LoadSourceFile(result.Path, readOnly: false);
            status.Text = $"Loaded {Path.GetFileName(result.Path)} directly from disk.";
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Open XAML error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not open XAML: " + ex.Message;
        }
    }

    void LoadSourceFile(string path, bool readOnly)
    {
        var source = File.ReadAllText(path);

        currentSourceFilePath = path;
        currentSourceFileReadOnly = readOnly;
        lastSavedSourceText = source;
        LoadForgeSidecar(path);

        history.Clear();
        selectedElementIdentity = null;
        ApplyDocumentText(source, null);
        UpdateHistoryButtons();
        UpdateSourceFileButtons();
    }

    void ReloadCurrentSourceFile()
    {
        if (string.IsNullOrWhiteSpace(currentSourceFilePath))
            return;

        try
        {
            var source = File.ReadAllText(currentSourceFilePath);
            lastSavedSourceText = source;
            LoadForgeSidecar(currentSourceFilePath);

            history.Clear();
            selectedElementIdentity = null;
            ApplyDocumentText(source, null);
            UpdateHistoryButtons();
            UpdateSourceFileButtons();

            status.Text = $"Reloaded {Path.GetFileName(currentSourceFilePath)} from disk.";
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Reload XAML error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not reload XAML: " + ex.Message;
        }
    }

    void SaveCurrentSourceFile()
    {
        if (string.IsNullOrWhiteSpace(currentSourceFilePath) || currentSourceFileReadOnly)
            return;

        try
        {
            File.WriteAllText(currentSourceFilePath, sourceEditor.Text);
            lastSavedSourceText = sourceEditor.Text;
            UpdateSourceFileButtons();

            status.Text = $"Saved {Path.GetFileName(currentSourceFilePath)}.";
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Save XAML error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not save XAML: " + ex.Message;
        }
    }

    void UpdateSourceFileButtons()
    {
        var hasPath = !string.IsNullOrWhiteSpace(currentSourceFilePath);
        var dirty = hasPath &&
                    !string.Equals(sourceEditor.Text, lastSavedSourceText, StringComparison.Ordinal);

        reloadSourceFileButton.IsEnabled = hasPath;
        saveSourceFileButton.IsEnabled = hasPath && !currentSourceFileReadOnly && dirty;
        saveSourceFileButton.Label = dirty ? "Save *" : "Save";
    }

    static string? FindRepositoryFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var depth = 0; directory is not null && depth < 12; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(relativeSegments).ToArray());

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    void LoadForgeSidecar(string xamlPath)
    {
        semanticElements.Clear();
        currentForgeSidecarPath = null;
        currentReviewCase = Path.GetFileNameWithoutExtension(xamlPath);

        var directory = Path.GetDirectoryName(xamlPath);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var sidecarPath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(xamlPath) + ".forge.json");

        if (!File.Exists(sidecarPath))
            return;

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(sidecarPath));
            var root = json.RootElement;

            if (root.TryGetProperty("case", out var caseElement) &&
                caseElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(caseElement.GetString()))
            {
                currentReviewCase = caseElement.GetString()!;
            }

            if (root.TryGetProperty("semanticElements", out var semantic) &&
                semantic.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in semantic.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        continue;

                    var name = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        semanticElements[property.Name] = name!;
                }
            }

            currentForgeSidecarPath = sidecarPath;
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Forge sidecar warning: " + ex.Message;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
        }
    }

    void DrawAllHighlights(bool force = false)
    {
        allHighlightsLayer.Children.Clear();

        var enabled = force || highlightAllCheckBox.IsChecked == true;
        allHighlightsLayer.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!enabled || document is null)
            return;

        IEnumerable<ForgeXamlElement> candidates;

        if (semanticElements.Count > 0)
        {
            var names = semanticElements.Values
                .ToHashSet(StringComparer.Ordinal);

            candidates = document.Elements.Where(x =>
                !string.IsNullOrWhiteSpace(x.Name) &&
                names.Contains(x.Name!));
        }
        else
        {
            candidates = document.Elements.Where(x =>
                !string.IsNullOrWhiteSpace(x.Name) &&
                x.TypeName is "Grid" or "Border" or "StackPanel" or "Canvas" or "ScrollViewer");
        }

        var highlightBrush = new SolidColorBrush(Color.FromArgb(190, 249, 116, 25));
        var labelBackground = new SolidColorBrush(Color.FromArgb(220, 23, 27, 29));

        foreach (var source in candidates.Take(64))
        {
            if (string.Equals(source.Identity, document.Root.Identity, StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(source.Name) ||
                !coordinator.TryGetVisual(source.Name, out var element))
                continue;

            try
            {
                var transform = element.TransformToVisual(previewStage);
                var point = transform.TransformPoint(new Point(0, 0));
                var width = Math.Max(1, element.ActualWidth);
                var height = Math.Max(1, element.ActualHeight);

                if (width < 18 || height < 14)
                    continue;

                var outline = new Border
                {
                    Width = width,
                    Height = height,
                    BorderBrush = highlightBrush,
                    BorderThickness = new Thickness(1.5),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(outline, point.X);
                Canvas.SetTop(outline, point.Y);
                allHighlightsLayer.Children.Add(outline);

                if (width >= 70 && height >= 26)
                {
                    var label = new Border
                    {
                        Background = labelBackground,
                        BorderBrush = highlightBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        IsHitTestVisible = false,
                        Child = new TextBlock
                        {
                            Text = source.Name,
                            Foreground = AccentBrush,
                            FontSize = 10
                        }
                    };

                    Canvas.SetLeft(label, Math.Max(0, point.X + 3));
                    Canvas.SetTop(label, Math.Max(0, point.Y + 3));
                    allHighlightsLayer.Children.Add(label);
                }
            }
            catch
            {
                // A transient layout pass should not prevent other semantic
                // regions from being highlighted.
            }
        }
    }

    async Task ExportReviewPackageAsync()
    {
        if (document is null || previewContent.Content is not UIElement)
        {
            status.Text = "Render a valid XAML design before exporting a review package.";
            return;
        }

        exportReviewPackageButton.IsEnabled = false;

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "WinUIForge",
            "review-" + Guid.NewGuid().ToString("N"));

        try
        {
            status.Text = "Exporting review package…";
            previewStage.UpdateLayout();
            Directory.CreateDirectory(tempRoot);

            var sourceDirectory = Path.Combine(tempRoot, "source");
            var referenceDirectory = Path.Combine(tempRoot, "reference");
            var renderDirectory = Path.Combine(tempRoot, "render");
            var geometryDirectory = Path.Combine(tempRoot, "geometry");
            var diagnosticsDirectory = Path.Combine(tempRoot, "diagnostics");

            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(referenceDirectory);
            Directory.CreateDirectory(renderDirectory);
            Directory.CreateDirectory(geometryDirectory);
            Directory.CreateDirectory(diagnosticsDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(sourceDirectory, "Screen.xaml"),
                sourceEditor.Text);

            if (!string.IsNullOrWhiteSpace(currentForgeSidecarPath) &&
                File.Exists(currentForgeSidecarPath))
            {
                File.Copy(
                    currentForgeSidecarPath,
                    Path.Combine(sourceDirectory, "Screen.forge.json"),
                    overwrite: true);
            }

            string? originalReferenceRelativePath = null;
            if (!string.IsNullOrWhiteSpace(referenceSourceFilePath) &&
                File.Exists(referenceSourceFilePath))
            {
                var extension = Path.GetExtension(referenceSourceFilePath);
                if (string.IsNullOrWhiteSpace(extension))
                    extension = ".png";

                var destinationName = "reference-original" + extension.ToLowerInvariant();
                File.Copy(
                    referenceSourceFilePath,
                    Path.Combine(referenceDirectory, destinationName),
                    overwrite: true);

                originalReferenceRelativePath = "reference/" + destinationName;
            }

            await CaptureReviewImagesAsync(tempRoot);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            await File.WriteAllTextAsync(
                Path.Combine(geometryDirectory, "layout.json"),
                JsonSerializer.Serialize(BuildLayoutSnapshot(), jsonOptions));

            await File.WriteAllTextAsync(
                Path.Combine(geometryDirectory, "hierarchy.json"),
                JsonSerializer.Serialize(BuildHierarchySnapshot(), jsonOptions));

            await File.WriteAllTextAsync(
                Path.Combine(geometryDirectory, "bounds.svg"),
                BuildBoundsSvg());

            await File.WriteAllTextAsync(
                Path.Combine(diagnosticsDirectory, "render-diagnostics.json"),
                JsonSerializer.Serialize(new
                {
                    renderedAtUtc = DateTimeOffset.UtcNow,
                    authoredElementCount = document.Elements.Count,
                    namedRuntimeMappings = coordinator.MappedCount,
                    diagnostics = diagnostics.Text,
                    referenceLoaded = referenceOverlay.Source is not null,
                    semanticRegionCount = semanticElements.Count
                }, jsonOptions));

            await File.WriteAllTextAsync(
                Path.Combine(diagnosticsDirectory, "environment.json"),
                JsonSerializer.Serialize(new
                {
                    operatingSystem = Environment.OSVersion.ToString(),
                    dotnet = Environment.Version.ToString(),
                    viewport = new
                    {
                        width = previewStage.Width,
                        height = previewStage.Height
                    },
                    theme = shellRoot.ActualTheme.ToString()
                }, jsonOptions));

            var dirty = !string.IsNullOrWhiteSpace(currentSourceFilePath) &&
                        !string.Equals(sourceEditor.Text, lastSavedSourceText, StringComparison.Ordinal);

            var manifest = new
            {
                schema = 1,
                tool = "WinUIForge",
                packageType = "forge-review",
                @case = currentReviewCase,
                exportedAtUtc = DateTimeOffset.UtcNow,
                viewport = new
                {
                    logicalWidth = previewStage.Width,
                    logicalHeight = previewStage.Height,
                    displayMode = viewportDisplayMode.SelectedItem as string ?? "Fit"
                },
                source = new
                {
                    filename = "source/Screen.xaml",
                    sidecar = File.Exists(Path.Combine(sourceDirectory, "Screen.forge.json"))
                        ? "source/Screen.forge.json"
                        : null,
                    hasUnsavedChanges = dirty
                },
                reference = new
                {
                    original = originalReferenceRelativePath,
                    normalized = referenceOverlay.Source is not null
                        ? "reference/reference-normalized.png"
                        : null,
                    originalWidth = referencePixelWidth,
                    originalHeight = referencePixelHeight
                },
                renders = new
                {
                    clean = "render/render-clean.png",
                    highlighted = "render/render-highlighted.png",
                    comparison50 = referenceOverlay.Source is not null
                        ? "render/comparison-50pct.png"
                        : null
                },
                geometry = new
                {
                    layout = "geometry/layout.json",
                    hierarchy = "geometry/hierarchy.json",
                    bounds = "geometry/bounds.svg"
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, jsonOptions));

            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "README.md"),
                BuildReviewReadme());

            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "WinUIForge Reviews");
            Directory.CreateDirectory(downloads);

            var safeCase = SanitizeFileName(currentReviewCase);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var zipPath = Path.Combine(
                downloads,
                $"{safeCase}_{stamp}.forge-review.zip");

            ZipFile.CreateFromDirectory(
                tempRoot,
                zipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            status.Text = $"Review package exported: {Path.GetFileName(zipPath)}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{zipPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception explorerError)
            {
                diagnostics.Text =
                    $"Review package exported successfully. Explorer could not be opened: {explorerError.Message}";
                diagnostics.Foreground = MutedBrush;
            }
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Review export error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not export review package: " + ex.Message;
        }
        finally
        {
            exportReviewPackageButton.IsEnabled = true;

            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Temporary cleanup failure must not hide an otherwise successful
                // review export.
            }
        }
    }

    async Task CaptureReviewImagesAsync(string packageRoot)
    {
        var renderDirectory = Path.Combine(packageRoot, "render");
        var referenceDirectory = Path.Combine(packageRoot, "reference");

        var oldReferenceVisibility = referenceOverlay.Visibility;
        var oldReferenceOpacity = referenceOverlay.Opacity;
        var oldHighlightsVisibility = allHighlightsLayer.Visibility;
        var oldSelectionVisibility = selectionLayer.Visibility;
        var oldContentVisibility = previewContent.Visibility;

        try
        {
            selectionLayer.Visibility = Visibility.Collapsed;
            previewContent.Visibility = Visibility.Visible;

            referenceOverlay.Visibility = Visibility.Collapsed;
            allHighlightsLayer.Visibility = Visibility.Collapsed;
            previewStage.UpdateLayout();
            await SaveElementPngAsync(
                previewStage,
                Path.Combine(renderDirectory, "render-clean.png"));

            DrawAllHighlights(force: true);
            referenceOverlay.Visibility = Visibility.Collapsed;
            allHighlightsLayer.Visibility = Visibility.Visible;
            previewStage.UpdateLayout();
            await SaveElementPngAsync(
                previewStage,
                Path.Combine(renderDirectory, "render-highlighted.png"));

            if (referenceOverlay.Source is not null)
            {
                previewContent.Visibility = Visibility.Collapsed;
                allHighlightsLayer.Visibility = Visibility.Collapsed;
                referenceOverlay.Visibility = Visibility.Visible;
                referenceOverlay.Opacity = 1;
                previewStage.UpdateLayout();

                await SaveElementPngAsync(
                    previewStage,
                    Path.Combine(referenceDirectory, "reference-normalized.png"));

                previewContent.Visibility = Visibility.Visible;
                referenceOverlay.Opacity = 0.5;
                previewStage.UpdateLayout();

                await SaveElementPngAsync(
                    previewStage,
                    Path.Combine(renderDirectory, "comparison-50pct.png"));
            }
        }
        finally
        {
            previewContent.Visibility = oldContentVisibility;
            referenceOverlay.Visibility = oldReferenceVisibility;
            referenceOverlay.Opacity = oldReferenceOpacity;
            allHighlightsLayer.Visibility = oldHighlightsVisibility;
            selectionLayer.Visibility = oldSelectionVisibility;

            DrawAllHighlights();
            DrawSelection();
        }
    }

    async Task SaveElementPngAsync(UIElement element, string path)
    {
        var width = Math.Max(1, (int)Math.Round(previewStage.Width));
        var height = Math.Max(1, (int)Math.Round(previewStage.Height));

        var target = new RenderTargetBitmap();
        await target.RenderAsync(element, width, height);

        var buffer = await target.GetPixelsAsync();
        var bytes = new byte[checked((int)buffer.Length)];
        using (var reader = DataReader.FromBuffer(buffer))
            reader.ReadBytes(bytes);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());

        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        stream.Size = 0;

        var encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.PngEncoderId,
            stream);

        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)target.PixelWidth,
            (uint)target.PixelHeight,
            96,
            96,
            bytes);

        await encoder.FlushAsync();
    }

    object BuildLayoutSnapshot()
    {
        if (document is null)
            return Array.Empty<object>();

        var entries = new List<object>();

        foreach (var source in document.Elements.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (string.IsNullOrWhiteSpace(source.Name) ||
                !coordinator.TryGetVisual(source.Name, out var element))
                continue;

            try
            {
                var transform = element.TransformToVisual(previewStage);
                var point = transform.TransformPoint(new Point(0, 0));

                object? text = null;
                if (element is TextBlock textBlock)
                {
                    text = new
                    {
                        value = textBlock.Text,
                        fontSize = textBlock.FontSize,
                        fontWeight = textBlock.FontWeight.Weight,
                        wrapping = textBlock.TextWrapping.ToString(),
                        trimming = textBlock.TextTrimming.ToString(),
                        foreground = BrushValue(textBlock.Foreground)
                    };
                }
                else if (element is Control control)
                {
                    text = new
                    {
                        fontSize = control.FontSize,
                        fontWeight = control.FontWeight.Weight,
                        foreground = BrushValue(control.Foreground)
                    };
                }

                object appearance = element switch
                {
                    Border border => new
                    {
                        background = BrushValue(border.Background),
                        borderBrush = BrushValue(border.BorderBrush),
                        borderThickness = ThicknessValue(border.BorderThickness),
                        cornerRadius = new
                        {
                            topLeft = border.CornerRadius.TopLeft,
                            topRight = border.CornerRadius.TopRight,
                            bottomRight = border.CornerRadius.BottomRight,
                            bottomLeft = border.CornerRadius.BottomLeft
                        },
                        opacity = border.Opacity
                    },
                    Panel panel => new
                    {
                        background = BrushValue(panel.Background),
                        opacity = panel.Opacity
                    },
                    Control control => new
                    {
                        background = BrushValue(control.Background),
                        foreground = BrushValue(control.Foreground),
                        opacity = control.Opacity
                    },
                    _ => new
                    {
                        opacity = element.Opacity
                    }
                };

                entries.Add(new
                {
                    identity = source.Identity,
                    name = source.Name,
                    semanticKeys = semanticElements
                        .Where(x => string.Equals(x.Value, source.Name, StringComparison.Ordinal))
                        .Select(x => x.Key)
                        .ToArray(),
                    type = source.TypeName,
                    parent = new
                    {
                        identity = source.Parent?.Identity,
                        name = source.Parent?.Name,
                        type = source.Parent?.TypeName
                    },
                    source = new
                    {
                        line = source.Line,
                        column = source.Column
                    },
                    bounds = new
                    {
                        x = point.X,
                        y = point.Y,
                        width = element.ActualWidth,
                        height = element.ActualHeight
                    },
                    desiredSize = new
                    {
                        width = element.DesiredSize.Width,
                        height = element.DesiredSize.Height
                    },
                    layout = new
                    {
                        width = double.IsNaN(element.Width) ? (double?)null : element.Width,
                        height = double.IsNaN(element.Height) ? (double?)null : element.Height,
                        minWidth = element.MinWidth,
                        minHeight = element.MinHeight,
                        maxWidth = double.IsInfinity(element.MaxWidth) ? (double?)null : element.MaxWidth,
                        maxHeight = double.IsInfinity(element.MaxHeight) ? (double?)null : element.MaxHeight,
                        margin = ThicknessValue(element.Margin),
                        horizontalAlignment = element.HorizontalAlignment.ToString(),
                        verticalAlignment = element.VerticalAlignment.ToString(),
                        grid = new
                        {
                            row = Grid.GetRow(element),
                            column = Grid.GetColumn(element),
                            rowSpan = Grid.GetRowSpan(element),
                            columnSpan = Grid.GetColumnSpan(element)
                        },
                        canvas = new
                        {
                            left = double.IsNaN(Canvas.GetLeft(element)) ? (double?)null : Canvas.GetLeft(element),
                            top = double.IsNaN(Canvas.GetTop(element)) ? (double?)null : Canvas.GetTop(element)
                        }
                    },
                    appearance,
                    text
                });
            }
            catch
            {
                // A single transient or non-transformable element should not
                // invalidate the full review package.
            }
        }

        return entries;
    }

    object BuildHierarchySnapshot()
    {
        if (document is null)
            return Array.Empty<object>();

        return document.Elements.Select(source => new
        {
            identity = source.Identity,
            name = source.Name,
            key = source.XKey,
            type = source.TypeName,
            depth = source.Depth,
            parentIdentity = source.Parent?.Identity,
            parentName = source.Parent?.Name,
            line = source.Line,
            column = source.Column
        }).ToArray();
    }

    string BuildBoundsSvg()
    {
        var width = Math.Max(1, previewStage.Width);
        var height = Math.Max(1, previewStage.Height);
        var builder = new StringBuilder();

        builder.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(width)}\" height=\"{Number(height)}\" viewBox=\"0 0 {Number(width)} {Number(height)}\">");
        builder.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"none\"/>");

        if (document is not null)
        {
            IEnumerable<ForgeXamlElement> candidates;
            if (semanticElements.Count > 0)
            {
                var names = semanticElements.Values.ToHashSet(StringComparer.Ordinal);
                candidates = document.Elements.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Name) &&
                    names.Contains(x.Name!));
            }
            else
            {
                candidates = document.Elements.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Name));
            }

            foreach (var source in candidates.Take(128))
            {
                if (string.IsNullOrWhiteSpace(source.Name) ||
                    !coordinator.TryGetVisual(source.Name, out var element))
                    continue;

                try
                {
                    var transform = element.TransformToVisual(previewStage);
                    var point = transform.TransformPoint(new Point(0, 0));
                    var elementWidth = Math.Max(1, element.ActualWidth);
                    var elementHeight = Math.Max(1, element.ActualHeight);
                    var name = XmlEscape(source.Name);

                    builder.AppendLine(
                        $"  <rect x=\"{Number(point.X)}\" y=\"{Number(point.Y)}\" width=\"{Number(elementWidth)}\" height=\"{Number(elementHeight)}\" fill=\"none\" stroke=\"#F97419\" stroke-width=\"1.5\"/>");
                    builder.AppendLine(
                        $"  <text x=\"{Number(point.X + 3)}\" y=\"{Number(point.Y + 12)}\" fill=\"#F97419\" font-size=\"10\" font-family=\"Segoe UI, sans-serif\">{name}</text>");
                }
                catch
                {
                }
            }
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    string BuildReviewReadme() =>
        $"""
        # WinUI Forge review package

        Case: {currentReviewCase}
        Viewport: {previewStage.Width:0} x {previewStage.Height:0}

        This archive is intended for visual + structural review.

        ## Primary files

        - `source/Screen.xaml` — authoritative XAML at export time.
        - `source/Screen.forge.json` — semantic sidecar when available.
        - `reference/reference-original.*` — untouched reference image when available.
        - `reference/reference-normalized.png` — reference rendered into the exact Forge viewport.
        - `render/render-clean.png` — clean WinUI render without designer chrome.
        - `render/render-highlighted.png` — WinUI render with semantic Highlight All frames.
        - `render/comparison-50pct.png` — exact 50% reference overlay when a reference is loaded.
        - `geometry/layout.json` — runtime bounds and resolved layout properties for named elements.
        - `geometry/hierarchy.json` — authored XAML hierarchy.
        - `geometry/bounds.svg` — exact semantic bounds in viewport coordinates.
        - `diagnostics/*` — render and environment context.

        The reference image is validation input only. It is not embedded into the authored UI.
        """;

    static object ThicknessValue(Thickness value) => new
    {
        left = value.Left,
        top = value.Top,
        right = value.Right,
        bottom = value.Bottom
    };

    static string? BrushValue(Brush? brush) =>
        brush is SolidColorBrush solid
            ? $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}"
            : brush?.ToString();

    static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
            builder.Append(invalid.Contains(character) ? '-' : character);

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result)
            ? "winui-forge-review"
            : result;
    }

    static string XmlEscape(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    async Task LoadReferenceAsync()
    {
        try
        {
            status.Text = "Opening reference image picker…";

            var picker = new FileOpenPicker(AppWindow.Id)
            {
                Title = "Choose a visual reference image"
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");

            var result = await picker.PickSingleFileAsync();
            if (result is null)
            {
                status.Text = "Reference image selection cancelled.";
                return;
            }

            await LoadReferenceFromPathAsync(result.Path);
            status.Text =
                "Reference image loaded. Overlay is visual-only and does not modify authored XAML.";
        }
        catch (Exception ex)
        {
            diagnostics.Text = "Reference picker error: " + ex;
            diagnostics.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            status.Text = "Could not load reference image: " + ex.Message;
        }
    }

    async Task LoadReferenceFromPathAsync(string path, bool adoptViewport = true)
    {
        referenceSourceFilePath = Path.GetFullPath(path);
        var file = await StorageFile.GetFileFromPathAsync(referenceSourceFilePath);
        var bitmap = new BitmapImage();
        using (var stream = await file.OpenReadAsync())
            await bitmap.SetSourceAsync(stream);

        referenceOverlay.Source = bitmap;
        referencePixelWidth = bitmap.PixelWidth;
        referencePixelHeight = bitmap.PixelHeight;
        referenceVisibleCheckBox.IsEnabled = true;
        referenceOpacitySlider.IsEnabled = true;
        useReferenceSizeButton.IsEnabled = referencePixelWidth > 0 && referencePixelHeight > 0;
        referenceVisibleCheckBox.IsChecked = true;

        referenceInfo.Text =
            $"{file.Name} · {referencePixelWidth} × {referencePixelHeight} · project reference";

        if (adoptViewport && referencePixelWidth > 0 && referencePixelHeight > 0)
            SetBenchmarkViewport(referencePixelWidth, referencePixelHeight);

        UpdateReferenceOverlay();
    }

    void ClearReference()
    {
        referenceSourceFilePath = null;
        referenceOverlay.Source = null;
        referencePixelWidth = 0;
        referencePixelHeight = 0;
        referenceVisibleCheckBox.IsChecked = false;
        referenceVisibleCheckBox.IsEnabled = false;
        referenceOpacitySlider.IsEnabled = false;
        useReferenceSizeButton.IsEnabled = false;
        referenceInfo.Text = "No reference image loaded.";
        UpdateReferenceOverlay();
    }

    void UpdateReferenceOverlay()
    {
        referenceOverlay.Opacity = referenceOpacitySlider.Value;
        referenceOverlay.Visibility =
            referenceOverlay.Source is not null &&
            referenceVisibleCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    void ApplyViewportFromInputs()
    {
        if (!double.TryParse(viewportWidthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
            !double.TryParse(viewportHeightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            status.Text = "Viewport width and height must be numeric.";
            return;
        }

        SetBenchmarkViewport(width, height);
    }

    void UseReferenceViewport()
    {
        if (referencePixelWidth <= 0 || referencePixelHeight <= 0)
            return;

        SetBenchmarkViewport(referencePixelWidth, referencePixelHeight);
    }

    void SetBenchmarkViewport(double width, double height)
    {
        if (width < 64 || height < 64 || width > 7680 || height > 4320)
        {
            status.Text = "Benchmark viewport must be between 64 × 64 and 7680 × 4320.";
            return;
        }

        previewStage.Width = width;
        previewStage.Height = height;
        viewportWidthBox.Text = Number(width);
        viewportHeightBox.Text = Number(height);
        UpdateViewportInfo();

        previewStage.DispatcherQueue.TryEnqueue(() =>
        {
            previewStage.UpdateLayout();
            DrawAllHighlights();
            DrawSelection();
        });

        status.Text = $"Benchmark viewport set to {width:0} × {height:0}.";
    }

    void ApplyViewportDisplayMode()
    {
        var mode = viewportDisplayMode.SelectedItem as string ?? "Fit";

        if (mode == "1:1")
        {
            if (ReferenceEquals(previewViewbox.Child, previewStage))
                previewViewbox.Child = null;

            if (!ReferenceEquals(previewScrollViewer.Content, previewStage))
                previewScrollViewer.Content = previewStage;

            previewViewbox.Visibility = Visibility.Collapsed;
            previewScrollViewer.Visibility = Visibility.Visible;
        }
        else
        {
            if (ReferenceEquals(previewScrollViewer.Content, previewStage))
                previewScrollViewer.Content = null;

            if (!ReferenceEquals(previewViewbox.Child, previewStage))
                previewViewbox.Child = previewStage;

            previewViewbox.Stretch = mode == "Fill" ? Stretch.UniformToFill : Stretch.Uniform;
            previewViewbox.Visibility = Visibility.Visible;
            previewScrollViewer.Visibility = Visibility.Collapsed;
        }

        UpdateViewportInfo();
        previewStage.DispatcherQueue.TryEnqueue(DrawSelection);
    }

    void UpdateViewportInfo()
    {
        var mode = viewportDisplayMode.SelectedItem as string ?? "Fit";
        viewportInfo.Text =
            $"Logical viewport {previewStage.Width:0} × {previewStage.Height:0} · {mode} · WinUI is measured at logical size";
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
        DrawAllHighlights();

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

            if (document is null)
            {
                visualTreeCount.Text = string.Empty;
                return;
            }

            var query = visualTreeFilter.Text?.Trim() ?? string.Empty;
            var namedOnly = visualTreeNamedOnly.IsChecked == true;
            var shown = 0;

            if (namedOnly)
                AddNamedTreeRows(document.Root, namedDepth: 0, query, ref shown);
            else
                AddTreeRows(document.Root, query, ref shown);

            visualTreeCount.Text = $"{shown}/{document.Elements.Count}";
        }
        finally
        {
            suppressTreeSelection = false;
        }
    }

    void AddTreeRows(
        ForgeXamlElement node,
        string query,
        ref int shown)
    {
        if (MatchesTreeFilter(node, query))
        {
            AddTreeRow(node, node.Depth);
            shown++;
        }

        foreach (var child in node.Children)
            AddTreeRows(child, query, ref shown);
    }

    void AddNamedTreeRows(
        ForgeXamlElement node,
        int namedDepth,
        string query,
        ref int shown)
    {
        var isNamed = !string.IsNullOrWhiteSpace(node.Name) ||
                      !string.IsNullOrWhiteSpace(node.XKey);

        var childNamedDepth = namedDepth;
        if (isNamed)
        {
            if (MatchesTreeFilter(node, query))
            {
                AddTreeRow(node, namedDepth);
                shown++;
            }

            childNamedDepth++;
        }

        foreach (var child in node.Children)
            AddNamedTreeRows(child, childNamedDepth, query, ref shown);
    }

    bool MatchesTreeFilter(ForgeXamlElement node, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return node.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               node.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(node.Name) &&
                node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(node.XKey) &&
                node.XKey.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    void AddTreeRow(ForgeXamlElement node, int displayDepth)
    {
        var runtimeMapped =
            !string.IsNullOrWhiteSpace(node.Name) &&
            coordinator.TryGetVisual(node.Name, out _);

        var label = new TextBlock
        {
            Text = node.DisplayName + (runtimeMapped ? string.Empty : "  · source"),
            Margin = new Thickness(displayDepth * 16, 2, 4, 2),
            Foreground = runtimeMapped ? TextBrush : MutedBrush
        };

        visualTree.Items.Add(new ListViewItem
        {
            Content = label,
            Tag = node.Identity,
            IsEnabled = true
        });
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

        // Designer Thumb input bubbles through previewStage. Do not re-run preview
        // hit-testing for a handle press: SelectAuthoredElement redraws the chrome,
        // which would remove the active Thumb before its drag can begin.
        if (IsDesignerHandleSource(e.OriginalSource))
            return;

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

    static bool IsDesignerHandleSource(object? originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Thumb)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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
            Text = identityText + $"\nSource: line {sourceElement.Line}, column {sourceElement.Column}",
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
            Text = $"Parent: {context.ParentLabel}\nMode: {context.Mode}",
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
                : $"Committed {context.AttributeName}=\"{normalized}\".";
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
        UpdateSourceFileButtons();
    }

    void UpdateHistoryButtons()
    {
        undoButton.IsEnabled = history.CanUndo;
        redoButton.IsEnabled = history.CanRedo;
        undoButton.Label = history.UndoDescription is null ? "Undo" : $"Undo · {history.UndoDescription}";
        redoButton.Label = history.RedoDescription is null ? "Redo" : $"Redo · {history.RedoDescription}";
    }

    void UpdateCommandButtons()
    {
        deleteButton.IsEnabled =
            document?.FindByIdentity(selectedElementIdentity)?.Parent is not null;
    }

    void SelectedElement_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!designerManipulating)
            DrawSelection();
    }

    void DrawSelection()
    {
        selectionLayer.Children.Clear();
        selectionOutline = null;
        selectionMoveHandle = null;
        selectionResizeHandle = null;

        if (selectedFrameworkElement is null || previewContent.Content is null) return;

        try
        {
            var transform = selectedFrameworkElement.TransformToVisual(previewStage);
            var point = transform.TransformPoint(new Point(0, 0));
            var width = Math.Max(1, selectedFrameworkElement.ActualWidth);
            var height = Math.Max(1, selectedFrameworkElement.ActualHeight);

            selectionOutline = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = AccentBrush,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false
            };
            selectionLayer.Children.Add(selectionOutline);

            selectionMoveHandle = CreateHandle("Move / reorder");
            selectionMoveHandle.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler((_, args) =>
                    BeginMovePointerDrag(selectionMoveHandle, args, point.X, point.Y, width, height)),
                true);
            selectionMoveHandle.AddHandler(
                UIElement.PointerMovedEvent,
                new PointerEventHandler((_, args) => ContinueMovePointerDrag(args)),
                true);
            selectionMoveHandle.AddHandler(
                UIElement.PointerCanceledEvent,
                new PointerEventHandler((_, args) => CancelMovePointerDrag(selectionMoveHandle, args)),
                true);
            selectionMoveHandle.DragCompleted += (_, _) =>
            {
                if (movePointerId is null) return;
                movePointerId = null;
                CompleteMovePreview();
            };
            selectionLayer.Children.Add(selectionMoveHandle);

            selectionResizeHandle = CreateHandle("Resize");
            selectionResizeHandle.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler((_, args) =>
                    BeginResizePointerDrag(selectionResizeHandle, args, width, height)),
                true);
            selectionResizeHandle.AddHandler(
                UIElement.PointerMovedEvent,
                new PointerEventHandler((_, args) => ContinueResizePointerDrag(args)),
                true);
            selectionResizeHandle.AddHandler(
                UIElement.PointerCanceledEvent,
                new PointerEventHandler((_, args) => CancelResizePointerDrag(selectionResizeHandle, args)),
                true);
            selectionResizeHandle.DragCompleted += (_, _) =>
            {
                if (resizePointerId is null) return;
                resizePointerId = null;
                CompleteResizePreview();
            };
            selectionLayer.Children.Add(selectionResizeHandle);

            UpdateSelectionChrome(point.X, point.Y, width, height);
        }
        catch
        {
            selectionLayer.Children.Clear();
            selectionOutline = null;
            selectionMoveHandle = null;
            selectionResizeHandle = null;
        }
    }

    void UpdateSelectionChrome(
        double x,
        double y,
        double width,
        double height,
        bool preserveResizeHandle = false)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (selectionOutline is not null)
        {
            selectionOutline.Width = width;
            selectionOutline.Height = height;
            Canvas.SetLeft(selectionOutline, x);
            Canvas.SetTop(selectionOutline, y);
        }

        if (selectionMoveHandle is not null)
        {
            Canvas.SetLeft(selectionMoveHandle, x - 6);
            Canvas.SetTop(selectionMoveHandle, y - 6);
        }

        if (selectionResizeHandle is not null)
        {
            Canvas.SetLeft(selectionResizeHandle, x + width - 6);
            Canvas.SetTop(selectionResizeHandle, y + height - 6);
        }
    }

    void UpdateSelectionChromeFromRuntime(
        double? widthOverride = null,
        double? heightOverride = null)
    {
        if (selectedFrameworkElement is null) return;

        try
        {
            var transform = selectedFrameworkElement.TransformToVisual(previewStage);
            var point = transform.TransformPoint(new Point(0, 0));
            UpdateSelectionChrome(
                point.X,
                point.Y,
                Math.Max(1, widthOverride ?? selectedFrameworkElement.ActualWidth),
                Math.Max(1, heightOverride ?? selectedFrameworkElement.ActualHeight));
        }
        catch
        {
            // Keep the last valid live-preview chrome if layout is between passes.
        }
    }

    void BeginMovePointerDrag(
        Thumb handle,
        PointerRoutedEventArgs args,
        double x,
        double y,
        double width,
        double height)
    {
        if (selectedFrameworkElement is null) return;

        movePointerId = args.Pointer.PointerId;
        movePointerStart = args.GetCurrentPoint(previewStage).Position;
        // Thumb already captures the pointer for its built-in drag behavior.
        // We only observe the routed pointer stream here.
        args.Handled = true;

        BeginMovePreview(x, y, width, height);
    }

    void ContinueMovePointerDrag(PointerRoutedEventArgs args)
    {
        if (movePointerId != args.Pointer.PointerId) return;

        var current = args.GetCurrentPoint(previewStage).Position;
        moveDeltaX = current.X - movePointerStart.X;
        moveDeltaY = current.Y - movePointerStart.Y;
        UpdateMovePreview();
        args.Handled = true;
    }

    void EndMovePointerDrag(Thumb handle, PointerRoutedEventArgs args)
    {
        if (movePointerId != args.Pointer.PointerId) return;

        var current = args.GetCurrentPoint(previewStage).Position;
        moveDeltaX = current.X - movePointerStart.X;
        moveDeltaY = current.Y - movePointerStart.Y;

        movePointerId = null;
        args.Handled = true;
        CompleteMovePreview();
    }

    void CancelMovePointerDrag(Thumb? handle, PointerRoutedEventArgs? args)
    {
        if (movePointerId is null) return;

        movePointerId = null;

        if (selectedFrameworkElement is not null)
            selectedFrameworkElement.Translation = moveStartTranslation;

        designerManipulating = false;
        DrawSelection();
    }

    void BeginMovePreview(double x, double y, double width, double height)
    {
        if (selectedFrameworkElement is null) return;

        designerManipulating = true;
        moveDeltaX = 0;
        moveDeltaY = 0;
        moveStartTranslation = selectedFrameworkElement.Translation;

        manipulationStartX = x;
        manipulationStartY = y;
        manipulationStartWidth = width;
        manipulationStartHeight = height;
    }

    void UpdateMovePreview()
    {
        if (selectedFrameworkElement is null ||
            document?.FindByIdentity(selectedElementIdentity) is not { } source)
            return;

        var parentType = source.Parent?.TypeName ?? string.Empty;

        // Translation is a runtime-only rendering offset. It gives immediate
        // feedback without mutating layout or authoritative XAML mid-drag.
        // Border is intentionally excluded because its child has no free X/Y
        // positioning semantics; the moving outline acts as a target ghost.
        if (parentType != "Border")
        {
            selectedFrameworkElement.Translation =
                moveStartTranslation +
                new Vector3((float)moveDeltaX, (float)moveDeltaY, 0);
        }

        UpdateSelectionChrome(
            manipulationStartX + moveDeltaX,
            manipulationStartY + moveDeltaY,
            manipulationStartWidth,
            manipulationStartHeight);

        status.Text = parentType switch
        {
            "StackPanel" =>
                $"Live reorder preview Δ {moveDeltaX:0}, {moveDeltaY:0} · release to reorder",
            "Canvas" =>
                $"Live Canvas preview Δ {moveDeltaX:0}, {moveDeltaY:0}",
            "Grid" =>
                $"Live Grid preview Δ {moveDeltaX:0}, {moveDeltaY:0} · release to resolve cell/margin",
            "Border" =>
                "Border constrains its child; orange outline shows the attempted position.",
            _ =>
                $"Live move preview Δ {moveDeltaX:0}, {moveDeltaY:0}"
        };
    }

    void CompleteMovePreview()
    {
        var element = selectedFrameworkElement;
        if (element is not null)
            element.Translation = moveStartTranslation;

        designerManipulating = false;
        CommitMove(moveDeltaX, moveDeltaY);

        // CommitMove may intentionally make no source change (small drag,
        // constrained Border, edge of StackPanel). In those cases restore the
        // chrome to the actual runtime bounds immediately.
        DrawSelection();
    }

    void BeginResizePointerDrag(
        Thumb handle,
        PointerRoutedEventArgs args,
        double width,
        double height)
    {
        if (selectedFrameworkElement is null) return;

        resizePointerId = args.Pointer.PointerId;
        resizePointerStart = args.GetCurrentPoint(previewStage).Position;
        // Thumb owns pointer capture; Forge observes the handled routed events.
        args.Handled = true;

        BeginResizePreview(width, height);
    }

    void ContinueResizePointerDrag(PointerRoutedEventArgs args)
    {
        if (resizePointerId != args.Pointer.PointerId) return;

        var current = args.GetCurrentPoint(previewStage).Position;
        resizeDeltaX = current.X - resizePointerStart.X;
        resizeDeltaY = current.Y - resizePointerStart.Y;
        UpdateResizePreview();
        args.Handled = true;
    }

    void EndResizePointerDrag(Thumb handle, PointerRoutedEventArgs args)
    {
        if (resizePointerId != args.Pointer.PointerId) return;

        var current = args.GetCurrentPoint(previewStage).Position;
        resizeDeltaX = current.X - resizePointerStart.X;
        resizeDeltaY = current.Y - resizePointerStart.Y;
        UpdateResizePreview();

        resizePointerId = null;
        args.Handled = true;
        CompleteResizePreview();
    }

    void CancelResizePointerDrag(Thumb? handle, PointerRoutedEventArgs? args)
    {
        if (resizePointerId is null) return;

        resizePointerId = null;

        designerManipulating = false;
        RestoreResizePreview(selectedFrameworkElement);
        DrawSelection();
    }

    void BeginResizePreview(double width, double height)
    {
        if (selectedFrameworkElement is null) return;

        designerManipulating = true;
        resizeDeltaX = 0;
        resizeDeltaY = 0;
        resizeStartWidth = width;
        resizeStartHeight = height;
        resizeOriginalWidth = selectedFrameworkElement.Width;
        resizeOriginalHeight = selectedFrameworkElement.Height;
        resizeOriginalHorizontalAlignment = selectedFrameworkElement.HorizontalAlignment;
        resizeOriginalVerticalAlignment = selectedFrameworkElement.VerticalAlignment;
        resizePreviewAnchoredHorizontal = false;
        resizePreviewAnchoredVertical = false;
        resizeWidthActive = false;
        resizeHeightActive = false;
    }

    void UpdateResizeAxes()
    {
        var x = Math.Abs(resizeDeltaX);
        var y = Math.Abs(resizeDeltaY);

        if (!resizeWidthActive && !resizeHeightActive)
        {
            if (x < ResizeAxisThreshold && y < ResizeAxisThreshold)
                return;

            // Pick a dominant axis when the drag clearly starts horizontally or
            // vertically. A genuinely diagonal drag activates both axes.
            if (x >= y * 1.35)
                resizeWidthActive = true;
            else if (y >= x * 1.35)
                resizeHeightActive = true;
            else
            {
                resizeWidthActive = true;
                resizeHeightActive = true;
            }

            return;
        }

        // Once the user has established one axis, require a much larger,
        // intentional excursion before enabling the second one. This prevents
        // normal mouse jitter from turning a width resize into a height resize.
        if (resizeWidthActive &&
            !resizeHeightActive &&
            y >= ResizeSecondAxisThreshold &&
            y >= x * 0.45)
        {
            resizeHeightActive = true;
        }

        if (resizeHeightActive &&
            !resizeWidthActive &&
            x >= ResizeSecondAxisThreshold &&
            x >= y * 0.45)
        {
            resizeWidthActive = true;
        }
    }

    void UpdateResizePreview()
    {
        if (selectedFrameworkElement is null ||
            document?.FindByIdentity(selectedElementIdentity) is not { } source)
            return;

        UpdateResizeAxes();

        var keepAutoHeight = ShouldKeepAutoHeightOnResize(source);
        var widthChanged = resizeWidthActive;
        var heightChanged = resizeHeightActive && !keepAutoHeight;

        // TextBlock is content-sized vertically by default. Unless the author
        // already supplied Height, resizing changes its width and lets wrapping
        // determine the natural height instead of clipping the text.
        if (keepAutoHeight)
        {
            resizeHeightActive = false;
            selectedFrameworkElement.Height = double.NaN;
        }

        // A bottom-right handle anchors the visual top-left. WinUI can reposition
        // an implicitly Stretch-aligned child as soon as Width/Height becomes
        // explicit, so temporarily make the cross-axis anchor explicit where
        // required. The same property is committed with the final resize.
        if (widthChanged &&
            !resizePreviewAnchoredHorizontal &&
            ShouldAnchorHorizontalOnResize(source))
        {
            selectedFrameworkElement.HorizontalAlignment = HorizontalAlignment.Left;
            resizePreviewAnchoredHorizontal = true;
        }

        if (heightChanged &&
            !resizePreviewAnchoredVertical &&
            ShouldAnchorVerticalOnResize(source))
        {
            selectedFrameworkElement.VerticalAlignment = VerticalAlignment.Top;
            resizePreviewAnchoredVertical = true;
        }

        var previewWidth = widthChanged
            ? Math.Max(8, resizeStartWidth + resizeDeltaX)
            : (double?)null;
        var previewHeight = heightChanged
            ? Math.Max(8, resizeStartHeight + resizeDeltaY)
            : (double?)null;

        if (previewWidth is { } width)
            selectedFrameworkElement.Width = width;
        else
            selectedFrameworkElement.Width = resizeOriginalWidth;

        if (previewHeight is { } height)
            selectedFrameworkElement.Height = height;
        else
            selectedFrameworkElement.Height = keepAutoHeight
                ? double.NaN
                : resizeOriginalHeight;

        previewStage.UpdateLayout();

        // During an active resize, the target dimension is authoritative for
        // designer chrome. Some WinUI elements (notably TextBlock in a
        // StackPanel) can keep reporting the previous arranged ActualWidth for
        // part of the layout pass even though Width and clipping have already
        // changed. Using the drag target prevents the orange frame from lagging
        // behind the content.
        UpdateSelectionChromeFromRuntime(
            widthOverride: previewWidth,
            heightOverride: previewHeight);

        var widthText = previewWidth is { } widthValue
            ? widthValue.ToString("0", CultureInfo.InvariantCulture)
            : "Auto";
        var heightText = previewHeight is { } heightValue
            ? heightValue.ToString("0", CultureInfo.InvariantCulture)
            : "Auto";

        status.Text = $"Live resize preview {widthText} × {heightText}";
    }

    void CompleteResizePreview()
    {
        var previewElement = selectedFrameworkElement;
        designerManipulating = false;

        if (!HasResizeAxisChange())
        {
            RestoreResizePreview(previewElement);
            DrawSelection();
            return;
        }

        if (!CommitResize())
        {
            RestoreResizePreview(previewElement);
            DrawSelection();
        }
    }

    bool HasResizeAxisChange() =>
        resizeWidthActive || resizeHeightActive;

    void RestoreResizePreview(FrameworkElement? element)
    {
        if (element is null) return;

        element.Width = resizeOriginalWidth;
        element.Height = resizeOriginalHeight;
        element.HorizontalAlignment = resizeOriginalHorizontalAlignment;
        element.VerticalAlignment = resizeOriginalVerticalAlignment;
        resizePreviewAnchoredHorizontal = false;
        resizePreviewAnchoredVertical = false;
        resizeWidthActive = false;
        resizeHeightActive = false;
        previewStage.UpdateLayout();
    }

    static bool ShouldKeepAutoHeightOnResize(ForgeXamlElement source) =>
        string.Equals(source.TypeName, "TextBlock", StringComparison.Ordinal) &&
        !source.Attributes.Any(x => string.Equals(x.Name, "Height", StringComparison.Ordinal));

    static bool ShouldAnchorHorizontalOnResize(ForgeXamlElement source)
    {
        if (HasAuthoredNonStretchAlignment(source, "HorizontalAlignment"))
            return false;

        if (source.Parent is null)
            return false;

        if (source.Parent.TypeName == "StackPanel")
        {
            var orientation = source.Parent.Attributes
                .FirstOrDefault(x => x.Name == "Orientation")?.Value ?? "Vertical";
            return !string.Equals(orientation, "Horizontal", StringComparison.OrdinalIgnoreCase);
        }

        return source.Parent.TypeName is
            "Grid" or
            "Border" or
            "ScrollViewer" or
            "RelativePanel";
    }

    static bool ShouldAnchorVerticalOnResize(ForgeXamlElement source)
    {
        if (HasAuthoredNonStretchAlignment(source, "VerticalAlignment"))
            return false;

        if (source.Parent is null)
            return false;

        if (source.Parent.TypeName == "StackPanel")
        {
            var orientation = source.Parent.Attributes
                .FirstOrDefault(x => x.Name == "Orientation")?.Value ?? "Vertical";
            return string.Equals(orientation, "Horizontal", StringComparison.OrdinalIgnoreCase);
        }

        return source.Parent.TypeName is
            "Grid" or
            "Border" or
            "ScrollViewer" or
            "RelativePanel";
    }

    static bool HasAuthoredNonStretchAlignment(ForgeXamlElement source, string attributeName)
    {
        var value = source.Attributes
            .FirstOrDefault(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal))
            ?.Value;

        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value, "Stretch", StringComparison.OrdinalIgnoreCase);
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

    bool CommitResize()
    {
        if (document is null ||
            selectedFrameworkElement is null ||
            string.IsNullOrWhiteSpace(selectedElementIdentity) ||
            document.FindByIdentity(selectedElementIdentity) is not { } source)
            return false;

        var keepAutoHeight = ShouldKeepAutoHeightOnResize(source);
        var widthChanged = resizeWidthActive;
        var heightChanged = resizeHeightActive && !keepAutoHeight;
        if (!widthChanged && !heightChanged)
            return false;

        try
        {
            var identity = selectedElementIdentity;
            var before = document.Text;

            if (widthChanged)
            {
                var width = Math.Max(8, resizeStartWidth + resizeDeltaX);
                document.SetAttributeByIdentity(identity, "Width", Number(width));

                if (ShouldAnchorHorizontalOnResize(source))
                    document.SetAttributeByIdentity(identity, "HorizontalAlignment", "Left");
            }

            // Re-resolve after a source edit because ForgeXamlDocument reparses
            // and replaces its element instances after each attribute mutation.
            source = document.FindByIdentity(identity) ?? source;

            if (heightChanged)
            {
                var height = Math.Max(8, resizeStartHeight + resizeDeltaY);
                document.SetAttributeByIdentity(identity, "Height", Number(height));

                source = document.FindByIdentity(identity) ?? source;
                if (ShouldAnchorVerticalOnResize(source))
                    document.SetAttributeByIdentity(identity, "VerticalAlignment", "Top");
            }

            history.Record(
                "Resize element",
                before,
                document.Text,
                identity,
                identity);

            ApplyDocumentText(document.Text, identity);
            return true;
        }
        catch (Exception ex)
        {
            status.Text = "Could not resize element: " + ex.Message;
            return false;
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
        designerManipulating = false;
        movePointerId = null;
        resizePointerId = null;
        selectionOutline = null;
        selectionMoveHandle = null;
        selectionResizeHandle = null;
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

    enum ForgeProjectFileKind
    {
        Folder,
        Xaml,
        ReferenceImage,
        Sidecar,
        Project,
        Other
    }

    sealed record ForgeProjectEntry(
        string RootPath,
        string FullPath,
        ForgeProjectFileKind Kind);

    sealed record ForgeProjectScreen(
        string Name,
        string XamlPath,
        string? ReferencePath,
        double? ViewportWidth,
        double? ViewportHeight);

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
