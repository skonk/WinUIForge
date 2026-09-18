using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace WinUIForge.Port.App;

internal sealed record ForgePropertyDefinition(
    string Name,
    string Group,
    Type OwnerType,
    Type ValueType,
    DependencyProperty? DependencyProperty = null,
    Func<FrameworkElement, object?>? Getter = null,
    bool IsAttached = false)
{
    public object? ReadRuntimeValue(FrameworkElement element)
    {
        if (Getter is not null) return Getter(element);
        if (DependencyProperty is null) return null;

        try
        {
            return element.GetValue(DependencyProperty);
        }
        catch
        {
            return null;
        }
    }

    public bool CanApplyTo(FrameworkElement element) =>
        IsAttached || OwnerType.IsAssignableFrom(element.GetType());

    public string FormatRuntimeValue(FrameworkElement element) =>
        ForgePropertyCatalog.FormatValue(ReadRuntimeValue(element));

    public void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.TrimStart().StartsWith("{", StringComparison.Ordinal)) return;
        if (DependencyProperty is null && IsAttached) return;

        _ = XamlBindingHelper.ConvertValue(ValueType, value);
    }
}

internal static class ForgePropertyCatalog
{
    static readonly IReadOnlyList<ForgePropertyDefinition> Definitions =
    [
        new("Width", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.WidthProperty),
        new("Height", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.HeightProperty),
        new("MinWidth", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.MinWidthProperty),
        new("MinHeight", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.MinHeightProperty),
        new("MaxWidth", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.MaxWidthProperty),
        new("MaxHeight", "Layout", typeof(FrameworkElement), typeof(double), FrameworkElement.MaxHeightProperty),
        new("Margin", "Layout", typeof(FrameworkElement), typeof(Thickness), FrameworkElement.MarginProperty),
        new("HorizontalAlignment", "Layout", typeof(FrameworkElement), typeof(HorizontalAlignment), FrameworkElement.HorizontalAlignmentProperty),
        new("VerticalAlignment", "Layout", typeof(FrameworkElement), typeof(VerticalAlignment), FrameworkElement.VerticalAlignmentProperty),

        new("Canvas.Left", "Canvas", typeof(FrameworkElement), typeof(double), Getter: x => Canvas.GetLeft(x), IsAttached: true),
        new("Canvas.Top", "Canvas", typeof(FrameworkElement), typeof(double), Getter: x => Canvas.GetTop(x), IsAttached: true),

        new("Grid.Row", "Grid", typeof(FrameworkElement), typeof(int), Getter: x => Grid.GetRow(x), IsAttached: true),
        new("Grid.Column", "Grid", typeof(FrameworkElement), typeof(int), Getter: x => Grid.GetColumn(x), IsAttached: true),
        new("Grid.RowSpan", "Grid", typeof(FrameworkElement), typeof(int), Getter: x => Grid.GetRowSpan(x), IsAttached: true),
        new("Grid.ColumnSpan", "Grid", typeof(FrameworkElement), typeof(int), Getter: x => Grid.GetColumnSpan(x), IsAttached: true),

        new("Opacity", "Appearance", typeof(UIElement), typeof(double), UIElement.OpacityProperty),
        new("Visibility", "Appearance", typeof(UIElement), typeof(Visibility), UIElement.VisibilityProperty),
        new("IsHitTestVisible", "Interaction", typeof(UIElement), typeof(bool), UIElement.IsHitTestVisibleProperty),

        new("Padding", "Control", typeof(Control), typeof(Thickness), Control.PaddingProperty),
        new("Background", "Control", typeof(Control), typeof(Microsoft.UI.Xaml.Media.Brush), Control.BackgroundProperty),
        new("Foreground", "Control", typeof(Control), typeof(Microsoft.UI.Xaml.Media.Brush), Control.ForegroundProperty),
        new("BorderBrush", "Control", typeof(Control), typeof(Microsoft.UI.Xaml.Media.Brush), Control.BorderBrushProperty),
        new("BorderThickness", "Control", typeof(Control), typeof(Thickness), Control.BorderThicknessProperty),
        new("FontSize", "Typography", typeof(Control), typeof(double), Control.FontSizeProperty),
        new("IsEnabled", "Interaction", typeof(Control), typeof(bool), Control.IsEnabledProperty),
        new("HorizontalContentAlignment", "Control", typeof(Control), typeof(HorizontalAlignment), Control.HorizontalContentAlignmentProperty),
        new("VerticalContentAlignment", "Control", typeof(Control), typeof(VerticalAlignment), Control.VerticalContentAlignmentProperty),

        new("Content", "Content", typeof(ContentControl), typeof(object), ContentControl.ContentProperty),

        new("Text", "Content", typeof(TextBlock), typeof(string), TextBlock.TextProperty),
        new("FontSize", "Typography", typeof(TextBlock), typeof(double), TextBlock.FontSizeProperty),
        new("FontWeight", "Typography", typeof(TextBlock), typeof(Windows.UI.Text.FontWeight), TextBlock.FontWeightProperty),
        new("Foreground", "Typography", typeof(TextBlock), typeof(Microsoft.UI.Xaml.Media.Brush), TextBlock.ForegroundProperty),
        new("TextWrapping", "Typography", typeof(TextBlock), typeof(TextWrapping), TextBlock.TextWrappingProperty),
        new("Padding", "Layout", typeof(TextBlock), typeof(Thickness), TextBlock.PaddingProperty),

        new("Padding", "Border", typeof(Border), typeof(Thickness), Border.PaddingProperty),
        new("Background", "Border", typeof(Border), typeof(Microsoft.UI.Xaml.Media.Brush), Border.BackgroundProperty),
        new("BorderBrush", "Border", typeof(Border), typeof(Microsoft.UI.Xaml.Media.Brush), Border.BorderBrushProperty),
        new("BorderThickness", "Border", typeof(Border), typeof(Thickness), Border.BorderThicknessProperty),
        new("CornerRadius", "Border", typeof(Border), typeof(CornerRadius), Border.CornerRadiusProperty),

        new("Padding", "Grid", typeof(Grid), typeof(Thickness), Grid.PaddingProperty),
        new("Background", "Grid", typeof(Grid), typeof(Microsoft.UI.Xaml.Media.Brush), Panel.BackgroundProperty),
        new("ColumnSpacing", "Grid", typeof(Grid), typeof(double), Grid.ColumnSpacingProperty),
        new("RowSpacing", "Grid", typeof(Grid), typeof(double), Grid.RowSpacingProperty),

        new("Padding", "StackPanel", typeof(StackPanel), typeof(Thickness), StackPanel.PaddingProperty),
        new("Spacing", "StackPanel", typeof(StackPanel), typeof(double), StackPanel.SpacingProperty),
        new("Orientation", "StackPanel", typeof(StackPanel), typeof(Orientation), StackPanel.OrientationProperty)
    ];

    public static IReadOnlyList<ForgePropertyDefinition> For(FrameworkElement element)
    {
        var applicable = Definitions
            .Where(x => x.CanApplyTo(element))
            .OrderBy(x => x.Group, StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        // Prefer the most specific definition when the same XAML property appears
        // on both a base class and a concrete control.
        return applicable
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(x => InheritanceDepth(x.OwnerType))
                .First())
            .OrderBy(x => x.Group, StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            double d when double.IsNaN(d) => string.Empty,
            double d when double.IsPositiveInfinity(d) => string.Empty,
            Microsoft.UI.Xaml.Media.SolidColorBrush brush => brush.Color.ToString(),
            _ => value.ToString() ?? string.Empty
        };

    static int InheritanceDepth(Type type)
    {
        var depth = 0;
        for (var t = type; t.BaseType is not null; t = t.BaseType) depth++;
        return depth;
    }
}
