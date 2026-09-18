using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using System.Text.RegularExpressions;
using System.Xml;
using WinUIForge.Port.Core;

namespace WinUIForge.Port.App;

/// <summary>
/// WinUI 3 adaptation of XAML Studio's XamlRenderService boundary.
///
/// The authored text is parsed first so source diagnostics and element spans are
/// available even before WinUI creates the runtime tree. A failed render never asks
/// callers to discard the previous valid preview.
/// </summary>
internal sealed class ForgeXamlRenderService
{
    public ForgeXamlRenderResult Render(string source)
    {
        ForgeXamlDocument document;

        try
        {
            document = new ForgeXamlDocument(source);
        }
        catch (XmlException e)
        {
            return ForgeXamlRenderResult.Failure(
                new ForgeRenderDiagnostic(
                    "XML",
                    e.Message,
                    e.LineNumber > 0 ? e.LineNumber : null,
                    e.LinePosition > 0 ? e.LinePosition : null));
        }
        catch (Exception e)
        {
            return ForgeXamlRenderResult.Failure(
                new ForgeRenderDiagnostic("XML", e.Message, null, null));
        }

        try
        {
            var loaded = XamlReader.Load(source);
            if (loaded is not UIElement element)
            {
                return ForgeXamlRenderResult.Failure(
                    new ForgeRenderDiagnostic(
                        "XAML",
                        $"XAML root rendered as {loaded?.GetType().FullName ?? "null"}, not UIElement.",
                        null,
                        null),
                    document);
            }

            return ForgeXamlRenderResult.Success(document, element);
        }
        catch (Exception e)
        {
            var (line, column) = TryExtractLineColumn(e);
            return ForgeXamlRenderResult.Failure(
                new ForgeRenderDiagnostic("XAML", e.Message, line, column),
                document);
        }
    }

    static (int? Line, int? Column) TryExtractLineColumn(Exception error)
    {
        // WinUI XAML exception text varies between SDK versions. Keep this intentionally
        // permissive rather than binding the migration to one exception implementation.
        var message = error.ToString();
        var match = Regex.Match(
            message,
            @"(?:line|Line)\s*(?<line>\d+).{0,24}?(?:position|column|Position|Column)\s*(?<column>\d+)",
            RegexOptions.Singleline);

        if (!match.Success)
            return (null, null);

        return (
            int.TryParse(match.Groups["line"].Value, out var line) ? line : null,
            int.TryParse(match.Groups["column"].Value, out var column) ? column : null);
    }
}

internal sealed record ForgeRenderDiagnostic(
    string Stage,
    string Message,
    int? Line,
    int? Column)
{
    public override string ToString()
    {
        var location = Line is null
            ? ""
            : $" · line {Line}" + (Column is null ? "" : $", column {Column}");

        return $"{Stage}: {Message}{location}";
    }
}

internal sealed class ForgeXamlRenderResult
{
    ForgeXamlRenderResult(
        bool succeeded,
        ForgeXamlDocument? document,
        UIElement? element,
        IReadOnlyList<ForgeRenderDiagnostic> diagnostics)
    {
        Succeeded = succeeded;
        Document = document;
        Element = element;
        Diagnostics = diagnostics;
    }

    public bool Succeeded { get; }
    public ForgeXamlDocument? Document { get; }
    public UIElement? Element { get; }
    public IReadOnlyList<ForgeRenderDiagnostic> Diagnostics { get; }

    public static ForgeXamlRenderResult Success(
        ForgeXamlDocument document,
        UIElement element) =>
        new(true, document, element, []);

    public static ForgeXamlRenderResult Failure(
        ForgeRenderDiagnostic diagnostic,
        ForgeXamlDocument? document = null) =>
        new(false, document, null, [diagnostic]);
}
