using System.Xml;
using System.Xml.Linq;

namespace WinUIForge.Port.Core;

public sealed class ForgeXamlDocument
{
    public static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    XDocument document = null!;
    List<ForgeXamlElement> elements = [];

    public ForgeXamlDocument(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Reparse();
    }

    public string Text { get; private set; }

    public IReadOnlyList<ForgeXamlElement> Elements => elements;

    public ForgeXamlElement? FindByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : elements.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    public ForgeXamlElement? FindAtSourceIndex(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex > Text.Length) return null;

        return elements
            .Where(x => sourceIndex >= x.StartIndex && sourceIndex <= x.StartTagEndIndex)
            .OrderByDescending(x => x.Depth)
            .ThenByDescending(x => x.StartIndex)
            .FirstOrDefault();
    }

    public string? GetAttribute(string elementName, string attributeName)
    {
        var element = FindXElement(elementName);
        return element?.Attribute(attributeName)?.Value;
    }

    public void SetAttribute(string elementName, string attributeName, string? value)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new ArgumentException("Attribute name is required.", nameof(attributeName));

        var element = FindXElement(elementName)
            ?? throw new InvalidOperationException($"No authored XAML element named '{elementName}' exists.");

        if (string.IsNullOrWhiteSpace(value))
            element.SetAttributeValue(attributeName, null);
        else
            element.SetAttributeValue(attributeName, value.Trim());

        Text = document.ToString(SaveOptions.DisableFormatting);
        Reparse();
    }

    XElement? FindXElement(string elementName) =>
        document
            .DescendantsAndSelf()
            .FirstOrDefault(x => string.Equals(GetAuthoredName(x), elementName, StringComparison.Ordinal));

    void Reparse()
    {
        document = XDocument.Parse(
            Text,
            LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        var lineStarts = BuildLineStarts(Text);
        var parsed = new List<ForgeXamlElement>();

        foreach (var element in document.DescendantsAndSelf())
        {
            var name = GetAuthoredName(element);
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (element is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo()) continue;

            var startIndex = LineColumnToIndex(lineStarts, lineInfo.LineNumber, lineInfo.LinePosition, Text.Length);
            var tagEnd = FindStartTagEnd(Text, startIndex);

            parsed.Add(new ForgeXamlElement(
                name,
                element.Name.LocalName,
                lineInfo.LineNumber,
                lineInfo.LinePosition,
                startIndex,
                tagEnd,
                element.Ancestors().Count()));
        }

        elements = parsed
            .OrderBy(x => x.StartIndex)
            .ToList();
    }

    static string? GetAuthoredName(XElement element) =>
        element.Attribute(XamlNamespace + "Name")?.Value
        ?? element.Attribute("Name")?.Value;

    static List<int> BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }
        return starts;
    }

    static int LineColumnToIndex(IReadOnlyList<int> lineStarts, int line, int column, int textLength)
    {
        if (line <= 0 || line > lineStarts.Count) return 0;
        var index = lineStarts[line - 1] + Math.Max(0, column - 1);
        return Math.Clamp(index, 0, textLength);
    }

    static int FindStartTagEnd(string text, int startIndex)
    {
        var quote = '\0';
        for (var i = Math.Clamp(startIndex, 0, text.Length); i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '>') return i;
        }

        return Math.Max(startIndex, text.Length - 1);
    }
}

public sealed record ForgeXamlElement(
    string Name,
    string TypeName,
    int Line,
    int Column,
    int StartIndex,
    int StartTagEndIndex,
    int Depth);

static class XDocumentExtensions
{
    public static IEnumerable<XElement> DescendantsAndSelf(this XDocument document)
    {
        if (document.Root is null) return [];
        return document.Root.DescendantsAndSelf();
    }
}
