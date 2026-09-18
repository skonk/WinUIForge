using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using WinUIForge.Port.Core;

namespace WinUIForge.Port.App;

/// <summary>
/// Curated WinUI property metadata, following XAML Studio's "curated first,
/// reflection later" approach. The catalog contains properties that are useful to
/// visual authoring and can round-trip safely as XAML attributes.
/// </summary>
internal static class ForgeDependencyPropertyCatalog
{
    static readonly IReadOnlyList<ForgePropertyDefinition> definitions =
    [
        // Layout
        D<FrameworkElement>(nameof(FrameworkElement.Width), FrameworkElement.WidthProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.Height), FrameworkElement.HeightProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.MinWidth), FrameworkElement.MinWidthProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.MinHeight), FrameworkElement.MinHeightProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.MaxWidth), FrameworkElement.MaxWidthProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.MaxHeight), FrameworkElement.MaxHeightProperty, typeof(double), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.Margin), FrameworkElement.MarginProperty, typeof(Thickness), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.HorizontalAlignment), FrameworkElement.HorizontalAlignmentProperty, typeof(HorizontalAlignment), "Layout"),
        D<FrameworkElement>(nameof(FrameworkElement.VerticalAlignment), FrameworkElement.VerticalAlignmentProperty, typeof(VerticalAlignment), "Layout"),
        A("Grid.Row", Grid.RowProperty, typeof(int), "Grid"),
        A("Grid.Column", Grid.ColumnProperty, typeof(int), "Grid"),
        A("Grid.RowSpan", Grid.RowSpanProperty, typeof(int), "Grid"),
        A("Grid.ColumnSpan", Grid.ColumnSpanProperty, typeof(int), "Grid"),

        // Common
        D<UIElement>(nameof(UIElement.Opacity), UIElement.OpacityProperty, typeof(double), "Common"),
        D<UIElement>(nameof(UIElement.Visibility), UIElement.VisibilityProperty, typeof(Visibility), "Common"),
        D<UIElement>(nameof(UIElement.IsHitTestVisible), UIElement.IsHitTestVisibleProperty, typeof(bool), "Common"),
        D<Control>(nameof(Control.IsEnabled), Control.IsEnabledProperty, typeof(bool), "Common"),

        // Appearance
        D<Control>(nameof(Control.Padding), Control.PaddingProperty, typeof(Thickness), "Appearance"),
        D<Control>(nameof(Control.Background), Control.BackgroundProperty, typeof(Brush), "Appearance"),
        D<Control>(nameof(Control.Foreground), Control.ForegroundProperty, typeof(Brush), "Appearance"),
        D<Control>(nameof(Control.BorderBrush), Control.BorderBrushProperty, typeof(Brush), "Appearance"),
        D<Control>(nameof(Control.BorderThickness), Control.BorderThicknessProperty, typeof(Thickness), "Appearance"),
        D<Control>(nameof(Control.CornerRadius), Control.CornerRadiusProperty, typeof(CornerRadius), "Appearance"),
        D<Control>(nameof(Control.FontSize), Control.FontSizeProperty, typeof(double), "Typography"),
        D<Control>(nameof(Control.HorizontalContentAlignment), Control.HorizontalContentAlignmentProperty, typeof(HorizontalAlignment), "Layout"),
        D<Control>(nameof(Control.VerticalContentAlignment), Control.VerticalContentAlignmentProperty, typeof(VerticalAlignment), "Layout"),

        D<Panel>(nameof(Panel.Background), Panel.BackgroundProperty, typeof(Brush), "Appearance"),
        D<Grid>(nameof(Grid.Padding), Grid.PaddingProperty, typeof(Thickness), "Appearance"),
        D<Grid>(nameof(Grid.RowSpacing), Grid.RowSpacingProperty, typeof(double), "Layout"),
        D<Grid>(nameof(Grid.ColumnSpacing), Grid.ColumnSpacingProperty, typeof(double), "Layout"),

        D<StackPanel>(nameof(StackPanel.Padding), StackPanel.PaddingProperty, typeof(Thickness), "Appearance"),
        D<StackPanel>(nameof(StackPanel.Spacing), StackPanel.SpacingProperty, typeof(double), "Layout"),
        D<StackPanel>(nameof(StackPanel.Orientation), StackPanel.OrientationProperty, typeof(Orientation), "Layout"),

        D<Border>(nameof(Border.Padding), Border.PaddingProperty, typeof(Thickness), "Appearance"),
        D<Border>(nameof(Border.Background), Border.BackgroundProperty, typeof(Brush), "Appearance"),
        D<Border>(nameof(Border.BorderBrush), Border.BorderBrushProperty, typeof(Brush), "Appearance"),
        D<Border>(nameof(Border.BorderThickness), Border.BorderThicknessProperty, typeof(Thickness), "Appearance"),
        D<Border>(nameof(Border.CornerRadius), Border.CornerRadiusProperty, typeof(CornerRadius), "Appearance"),

        // Content/text
        D<ContentControl>(nameof(ContentControl.Content), ContentControl.ContentProperty, typeof(object), "Content"),
        D<TextBlock>(nameof(TextBlock.Text), TextBlock.TextProperty, typeof(string), "Content"),
        D<TextBlock>(nameof(TextBlock.FontSize), TextBlock.FontSizeProperty, typeof(double), "Typography"),
        D<TextBlock>(nameof(TextBlock.FontWeight), TextBlock.FontWeightProperty, typeof(Microsoft.UI.Text.FontWeight), "Typography"),
        D<TextBlock>(nameof(TextBlock.Foreground), TextBlock.ForegroundProperty, typeof(Brush), "Typography"),
        D<TextBlock>(nameof(TextBlock.Padding), TextBlock.PaddingProperty, typeof(Thickness), "Appearance"),
        D<TextBlock>(nameof(TextBlock.TextWrapping), TextBlock.TextWrappingProperty, typeof(TextWrapping), "Content"),
        D<TextBox>(nameof(TextBox.Text), TextBox.TextProperty, typeof(string), "Content"),
        D<TextBox>(nameof(TextBox.PlaceholderText), TextBox.PlaceholderTextProperty, typeof(string), "Content"),
        D<Image>(nameof(Image.Stretch), Image.StretchProperty, typeof(Stretch), "Content")
    ];

    public static IReadOnlyList<ForgePropertyValue> Inspect(
        DependencyObject element,
        ForgeXamlElement source)
    {
        var runtimeType = element.GetType();

        return definitions
            .Where(x => x.Attached || x.OwnerType.IsAssignableFrom(runtimeType))
            .GroupBy(x => x.AttributeName, StringComparer.Ordinal)
            .Select(x => x
                .OrderByDescending(def => InheritanceDepth(def.OwnerType))
                .First())
            .Select(def =>
            {
                var local = element.ReadLocalValue(def.Property);
                var runtimeValue = local == DependencyProperty.UnsetValue
                    ? element.GetValue(def.Property)
                    : local;

                var sourceAttribute = source.Attributes
                    .FirstOrDefault(a => string.Equals(
                        a.Name,
                        def.AttributeName,
                        StringComparison.Ordinal));

                var editable = IsSafelyEditable(def.ValueType, runtimeValue, sourceAttribute?.Value);

                return new ForgePropertyValue(
                    def,
                    sourceAttribute?.Value,
                    FormatValue(runtimeValue),
                    local != DependencyProperty.UnsetValue,
                    editable);
            })
            .OrderByDescending(x => x.SourceValue is not null)
            .ThenBy(x => x.Definition.Group, StringComparer.Ordinal)
            .ThenBy(x => x.Definition.AttributeName, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TryValidateValue(
        ForgePropertyDefinition definition,
        string value,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (value.StartsWith('{'))
            return true; // validate markup extension during the real render pass.

        try
        {
            if (definition.ValueType == typeof(string) ||
                definition.ValueType == typeof(object))
                return true;

            _ = XamlBindingHelper.ConvertValue(definition.ValueType, value);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    static bool IsSafelyEditable(Type type, object? runtimeValue, string? sourceValue)
    {
        if (sourceValue?.StartsWith('{') == true)
            return true;

        return type == typeof(string) ||
               type == typeof(object) ||
               type == typeof(double) ||
               type == typeof(int) ||
               type == typeof(bool) ||
               type.IsEnum ||
               type == typeof(Thickness) ||
               type == typeof(CornerRadius) ||
               type == typeof(Brush) ||
               type == typeof(Microsoft.UI.Text.FontWeight);
    }

    static string FormatValue(object? value)
    {
        if (value is null || value == DependencyProperty.UnsetValue)
            return "";

        return value switch
        {
            double d when double.IsNaN(d) => "",
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            Thickness t => FormatThickness(t),
            CornerRadius c => $"{N(c.TopLeft)},{N(c.TopRight)},{N(c.BottomRight)},{N(c.BottomLeft)}",
            SolidColorBrush b => FormatColor(b.Color),
            Microsoft.UI.Text.FontWeight w => w.Weight.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }

    static string FormatThickness(Thickness t)
    {
        if (t.Left == t.Top && t.Top == t.Right && t.Right == t.Bottom)
            return N(t.Left);

        if (t.Left == t.Right && t.Top == t.Bottom)
            return $"{N(t.Left)},{N(t.Top)}";

        return $"{N(t.Left)},{N(t.Top)},{N(t.Right)},{N(t.Bottom)}";
    }

    static string FormatColor(Windows.UI.Color color) =>
        color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    static string N(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    static int InheritanceDepth(Type type)
    {
        var depth = 0;
        for (var current = type; current.BaseType is not null; current = current.BaseType)
            depth++;
        return depth;
    }

    static ForgePropertyDefinition D<TOwner>(
        string name,
        DependencyProperty property,
        Type valueType,
        string group)
        where TOwner : DependencyObject =>
        new(name, typeof(TOwner), property, valueType, group, false);

    static ForgePropertyDefinition A(
        string name,
        DependencyProperty property,
        Type valueType,
        string group) =>
        new(name, typeof(FrameworkElement), property, valueType, group, true);
}

internal sealed record ForgePropertyDefinition(
    string AttributeName,
    Type OwnerType,
    DependencyProperty Property,
    Type ValueType,
    string Group,
    bool Attached);

internal sealed record ForgePropertyValue(
    ForgePropertyDefinition Definition,
    string? SourceValue,
    string RuntimeValue,
    bool IsLocallySet,
    bool IsEditable)
{
    public string EditorValue => SourceValue ?? RuntimeValue;
}
