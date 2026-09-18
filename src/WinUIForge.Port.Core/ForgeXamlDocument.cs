using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WinUIForge.Port.Core;

public sealed class ForgeXamlDocument
{
    public static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    XDocument document = null!;
    List<ForgeXamlElement> elements = [];
    Dictionary<string, ForgeXamlElement> elementsByName = new(StringComparer.Ordinal);

    public ForgeXamlDocument(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Reparse();
    }

    public string Text { get; private set; }

    public ForgeXamlElement Root { get; private set; } = null!;

    public IReadOnlyList<ForgeXamlElement> Elements => elements;

    public ForgeXamlElement? FindByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : elementsByName.GetValueOrDefault(name);

    public ForgeXamlElement? FindAtSourceIndex(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex > Text.Length) return null;

        return elements
            .Where(x => sourceIndex >= x.StartIndex && sourceIndex <= x.StartTagEndIndex)
            .OrderByDescending(x => x.Depth)
            .ThenByDescending(x => x.StartIndex)
            .FirstOrDefault();
    }

    public string? GetAttribute(string elementName, string attributeName) =>
        FindByName(elementName)?.Attributes
            .FirstOrDefault(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal))
            ?.Value;

    public bool HasAttribute(string elementName, string attributeName) =>
        FindByName(elementName)?.Attributes.Any(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal)) == true;

    public ForgeXamlEdit SetAttribute(string elementName, string attributeName, string? value)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new ArgumentException("Attribute name is required.", nameof(attributeName));

        var element = FindByName(elementName)
            ?? throw new InvalidOperationException($"No authored XAML element named '{elementName}' exists.");

        var existing = element.Attributes
            .FirstOrDefault(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal));

        var before = Text;
        ForgeXamlEdit edit;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is null)
                return new ForgeXamlEdit(elementName, attributeName, null, null, false, before, before);

            var removeStart = existing.StartIndex;
            while (removeStart > element.StartIndex &&
                   (Text[removeStart - 1] == ' ' || Text[removeStart - 1] == '\t'))
            {
                removeStart--;
            }

            // Preserve a multiline attribute's newline/indentation by removing only
            // horizontal whitespace plus the attribute itself.
            Text = Text.Remove(removeStart, existing.EndIndex - removeStart);
            edit = new ForgeXamlEdit(
                elementName,
                attributeName,
                existing.Value,
                null,
                true,
                before,
                Text);
        }
        else
        {
            var normalized = value.Trim();
            if (existing is not null)
            {
                Text = Text.Remove(existing.ValueStartIndex, existing.ValueEndIndex - existing.ValueStartIndex)
                           .Insert(existing.ValueStartIndex, EscapeAttributeValue(normalized));

                edit = new ForgeXamlEdit(
                    elementName,
                    attributeName,
                    existing.Value,
                    normalized,
                    true,
                    before,
                    Text);
            }
            else
            {
                var insertion = BuildAttributeInsertion(element, attributeName, normalized);
                var insertAt = element.StartTagCloseIndex;
                Text = Text.Insert(insertAt, insertion);

                edit = new ForgeXamlEdit(
                    elementName,
                    attributeName,
                    null,
                    normalized,
                    true,
                    before,
                    Text);
            }
        }

        Reparse();
        return edit;
    }

    void Reparse()
    {
        document = XDocument.Parse(
            Text,
            LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        if (document.Root is null)
            throw new InvalidOperationException("XAML document has no root element.");

        var lineStarts = BuildLineStarts(Text);
        var all = new List<ForgeXamlElement>();

        Root = BuildNode(document.Root, parent: null, lineStarts, all);
        elements = all.OrderBy(x => x.StartIndex).ToList();

        elementsByName = elements
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
    }

    ForgeXamlElement BuildNode(
        XElement element,
        ForgeXamlElement? parent,
        IReadOnlyList<int> lineStarts,
        List<ForgeXamlElement> all)
    {
        var lineInfo = (IXmlLineInfo)element;
        var startIndex = lineInfo.HasLineInfo()
            ? LineColumnToIndex(lineStarts, lineInfo.LineNumber, lineInfo.LinePosition, Text.Length)
            : 0;

        var tagEnd = FindStartTagEnd(Text, startIndex);
        var closeIndex = FindStartTagCloseIndex(Text, startIndex, tagEnd);
        var attributes = ParseAttributes(Text, startIndex, tagEnd);

        var node = new ForgeXamlElement(
            GetAuthoredName(element),
            element.Name.LocalName,
            lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
            lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0,
            startIndex,
            tagEnd,
            closeIndex,
            parent?.Depth + 1 ?? 0,
            parent,
            attributes);

        all.Add(node);

        foreach (var child in element.Elements())
            node.Children.Add(BuildNode(child, node, lineStarts, all));

        return node;
    }

    string BuildAttributeInsertion(ForgeXamlElement element, string attributeName, string value)
    {
        var escaped = EscapeAttributeValue(value);
        var tagText = Text[element.StartIndex..element.StartTagEndIndex];

        if (!tagText.Contains('\n'))
            return $" {attributeName}=\"{escaped}\"";

        var closeLineStart = Text.LastIndexOf('\n', Math.Max(element.StartIndex, element.StartTagCloseIndex - 1));
        var elementLineStart = Text.LastIndexOf('\n', Math.Max(0, element.StartIndex - 1)) + 1;

        var baseIndentLength = element.StartIndex - elementLineStart;
        var attributeIndent = new string(' ', Math.Max(0, baseIndentLength + 4));

        // If an existing attribute line has deeper indentation, preserve that style.
        var lastAttribute = element.Attributes.LastOrDefault();
        if (lastAttribute is not null)
        {
            var attrLineStart = Text.LastIndexOf('\n', Math.Max(0, lastAttribute.StartIndex - 1)) + 1;
            var whitespace = Text[attrLineStart..lastAttribute.StartIndex];
            if (whitespace.All(ch => ch is ' ' or '\t'))
                attributeIndent = whitespace;
        }

        return Environment.NewLine + attributeIndent + $"{attributeName}=\"{escaped}\"";
    }

    static IReadOnlyList<ForgeXamlAttribute> ParseAttributes(string text, int startIndex, int tagEnd)
    {
        var result = new List<ForgeXamlAttribute>();
        var i = startIndex + 1;

        while (i < tagEnd && !char.IsWhiteSpace(text[i]) && text[i] is not '>' and not '/')
            i++;

        while (i < tagEnd)
        {
            while (i < tagEnd && char.IsWhiteSpace(text[i])) i++;
            if (i >= tagEnd || text[i] is '>' or '/') break;

            var nameStart = i;
            while (i < tagEnd &&
                   !char.IsWhiteSpace(text[i]) &&
                   text[i] is not '=' and not '>' and not '/')
            {
                i++;
            }

            var name = text[nameStart..i];
            while (i < tagEnd && char.IsWhiteSpace(text[i])) i++;
            if (i >= tagEnd || text[i] != '=')
            {
                while (i < tagEnd && text[i] != '>' && !char.IsWhiteSpace(text[i])) i++;
                continue;
            }

            i++;
            while (i < tagEnd && char.IsWhiteSpace(text[i])) i++;
            if (i >= tagEnd || text[i] is not ('\'' or '"')) continue;

            var quote = text[i++];
            var valueStart = i;
            while (i < tagEnd && text[i] != quote) i++;
            var valueEnd = i;
            if (i < tagEnd) i++;

            result.Add(new ForgeXamlAttribute(
                name,
                UnescapeAttributeValue(text[valueStart..valueEnd]),
                nameStart,
                i,
                valueStart,
                valueEnd));
        }

        return result;
    }

    static string? GetAuthoredName(XElement element) =>
        element.Attribute(XamlNamespace + "Name")?.Value
        ?? element.Attribute("Name")?.Value;

    static string EscapeAttributeValue(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal);

    static string UnescapeAttributeValue(string value) =>
        value.Replace("&quot;", "\"", StringComparison.Ordinal)
             .Replace("&lt;", "<", StringComparison.Ordinal)
             .Replace("&amp;", "&", StringComparison.Ordinal);

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

    static int FindStartTagCloseIndex(string text, int startIndex, int tagEnd)
    {
        var i = tagEnd - 1;
        while (i > startIndex && char.IsWhiteSpace(text[i])) i--;
        if (i > startIndex && text[i] == '/') return i;
        return tagEnd;
    }
}

public sealed class ForgeXamlElement
{
    public ForgeXamlElement(
        string? name,
        string typeName,
        int line,
        int column,
        int startIndex,
        int startTagEndIndex,
        int startTagCloseIndex,
        int depth,
        ForgeXamlElement? parent,
        IReadOnlyList<ForgeXamlAttribute> attributes)
    {
        Name = name;
        TypeName = typeName;
        Line = line;
        Column = column;
        StartIndex = startIndex;
        StartTagEndIndex = startTagEndIndex;
        StartTagCloseIndex = startTagCloseIndex;
        Depth = depth;
        Parent = parent;
        Attributes = attributes;
    }

    public string? Name { get; }
    public string TypeName { get; }
    public int Line { get; }
    public int Column { get; }
    public int StartIndex { get; }
    public int StartTagEndIndex { get; }
    public int StartTagCloseIndex { get; }
    public int Depth { get; }
    public ForgeXamlElement? Parent { get; }
    public IReadOnlyList<ForgeXamlAttribute> Attributes { get; }
    public List<ForgeXamlElement> Children { get; } = [];
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? TypeName : $"{Name} [{TypeName}]";
}

public sealed record ForgeXamlAttribute(
    string Name,
    string Value,
    int StartIndex,
    int EndIndex,
    int ValueStartIndex,
    int ValueEndIndex);

public sealed record ForgeXamlEdit(
    string ElementName,
    string AttributeName,
    string? OldValue,
    string? NewValue,
    bool Changed,
    string Before,
    string After);
