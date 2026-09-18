using System.Xml;
using System.Xml.Linq;

namespace WinUIForge.Port.Core;

/// <summary>
/// Parsed representation of the authored XAML text.
///
/// This deliberately keeps the original text authoritative. The XML DOM is used for
/// structure and line information, while property writes are applied as narrow text
/// edits to the existing start tag so unrelated formatting survives round-trips.
/// </summary>
public sealed class ForgeXamlDocument
{
    public static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    XDocument document = null!;
    List<ForgeXamlElement> elements = [];
    readonly Dictionary<string, XElement> xmlById = new(StringComparer.Ordinal);
    readonly Dictionary<string, ForgeXamlElement> elementById = new(StringComparer.Ordinal);
    readonly Dictionary<XElement, string> idByXml = new();
    readonly Dictionary<string, List<ForgeXamlElement>> childrenByParent = new(StringComparer.Ordinal);

    public ForgeXamlDocument(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Reparse();
    }

    public string Text { get; private set; }

    public IReadOnlyList<ForgeXamlElement> Elements => elements;

    public ForgeXamlElement RootElement =>
        elements.FirstOrDefault(x => x.ParentId is null)
        ?? throw new InvalidOperationException("The XAML document has no root element.");

    public ForgeXamlElement? FindById(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : elementById.GetValueOrDefault(id);

    public ForgeXamlElement? FindByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : elements.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ForgeXamlElement> GetChildren(string? parentId)
    {
        if (parentId is null)
            return elements.Where(x => x.ParentId is null).ToArray();

        return childrenByParent.TryGetValue(parentId, out var children)
            ? children
            : [];
    }

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
        var element = FindByName(elementName);
        return element is null ? null : GetAttributeById(element.Id, attributeName);
    }

    public string? GetAttributeById(string elementId, string attributeName)
    {
        var element = FindById(elementId)
            ?? throw new InvalidOperationException($"No authored XAML element '{elementId}' exists.");

        return element.Attributes
            .FirstOrDefault(x => AttributeNameEquals(x.Name, attributeName))
            ?.Value;
    }

    /// <summary>
    /// Compatibility helper for the first proof. Prefer SetAttributeById for new code.
    /// </summary>
    public void SetAttribute(string elementName, string attributeName, string? value)
    {
        var element = FindByName(elementName)
            ?? throw new InvalidOperationException($"No authored XAML element named '{elementName}' exists.");

        SetAttributeById(element.Id, attributeName, value);
    }

    /// <summary>
    /// Applies a minimal source edit to one attribute. Existing attribute values are
    /// replaced in place; missing attributes are inserted into the existing start tag;
    /// blank/null values remove the existing attribute. The rest of the XAML text is
    /// preserved byte-for-byte.
    /// </summary>
    public void SetAttributeById(string elementId, string attributeName, string? value)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new ArgumentException("Attribute name is required.", nameof(attributeName));

        var element = FindById(elementId)
            ?? throw new InvalidOperationException($"No authored XAML element '{elementId}' exists.");

        var existing = element.Attributes
            .FirstOrDefault(x => AttributeNameEquals(x.Name, attributeName));

        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var removeStart = existing.StartIndex;

                // Include indentation/spaces immediately before the attribute but never
                // consume the preceding line break or another attribute.
                while (removeStart > element.StartIndex)
                {
                    var ch = Text[removeStart - 1];
                    if (ch is ' ' or '\t')
                    {
                        removeStart--;
                        continue;
                    }
                    break;
                }

                ReplaceRange(removeStart, existing.EndIndex + 1, "");
            }
            else
            {
                ReplaceRange(
                    existing.ValueStartIndex,
                    existing.ValueEndIndex,
                    EscapeAttributeValue(value.Trim()));
            }

            Reparse();
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
            return;

        var insertionIndex = element.StartTagEndIndex;
        if (insertionIndex > element.StartIndex && Text[insertionIndex - 1] == '/')
            insertionIndex--;

        var insertion = $" {attributeName}=\"{EscapeAttributeValue(value.Trim())}\"";
        ReplaceRange(insertionIndex, insertionIndex, insertion);
        Reparse();
    }

    void ReplaceRange(int start, int endExclusive, string replacement)
    {
        if (start < 0 || endExclusive < start || endExclusive > Text.Length)
            throw new InvalidOperationException("Calculated XAML edit range is invalid.");

        Text = string.Concat(
            Text.AsSpan(0, start),
            replacement,
            Text.AsSpan(endExclusive));
    }

    void Reparse()
    {
        document = XDocument.Parse(
            Text,
            LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        elements = [];
        xmlById.Clear();
        elementById.Clear();
        idByXml.Clear();
        childrenByParent.Clear();

        if (document.Root is null)
            return;

        var lineStarts = BuildLineStarts(Text);
        var duplicateIds = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            var parentId = element.Parent is null ? null : idByXml.GetValueOrDefault(element.Parent);
            var authoredName = GetAuthoredName(element);
            var generatedId = !string.IsNullOrWhiteSpace(authoredName)
                ? authoredName!
                : CreatePathId(element);

            if (duplicateIds.TryGetValue(generatedId, out var duplicateCount))
            {
                duplicateCount++;
                duplicateIds[generatedId] = duplicateCount;
                generatedId = $"{generatedId}#{duplicateCount}";
            }
            else
            {
                duplicateIds[generatedId] = 1;
            }

            idByXml[element] = generatedId;

            if (element is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
                continue;

            var startIndex = LineColumnToIndex(
                lineStarts,
                lineInfo.LineNumber,
                lineInfo.LinePosition,
                Text.Length);
            var tagEnd = FindStartTagEnd(Text, startIndex);
            var attributes = ParseAttributes(element, lineStarts, startIndex, tagEnd);

            var parsed = new ForgeXamlElement(
                generatedId,
                authoredName,
                element.Name.LocalName,
                lineInfo.LineNumber,
                lineInfo.LinePosition,
                startIndex,
                tagEnd,
                element.Ancestors().Count(),
                parentId,
                IsPropertyElement(element),
                attributes);

            elements.Add(parsed);
            xmlById[generatedId] = element;
            elementById[generatedId] = parsed;

            if (parentId is not null)
            {
                if (!childrenByParent.TryGetValue(parentId, out var children))
                    childrenByParent[parentId] = children = [];
                children.Add(parsed);
            }
        }

        elements = elements.OrderBy(x => x.StartIndex).ToList();
    }

    List<ForgeXamlAttribute> ParseAttributes(
        XElement element,
        IReadOnlyList<int> lineStarts,
        int elementStart,
        int tagEnd)
    {
        var result = new List<ForgeXamlAttribute>();

        foreach (var attribute in element.Attributes())
        {
            if (attribute is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
                continue;

            var start = LineColumnToIndex(
                lineStarts,
                lineInfo.LineNumber,
                lineInfo.LinePosition,
                Text.Length);
            start = Math.Clamp(start, elementStart, tagEnd);

            var equalsIndex = Text.IndexOf('=', start);
            if (equalsIndex < 0 || equalsIndex >= tagEnd)
                continue;

            var quoteIndex = equalsIndex + 1;
            while (quoteIndex < tagEnd && char.IsWhiteSpace(Text[quoteIndex]))
                quoteIndex++;

            if (quoteIndex >= tagEnd || Text[quoteIndex] is not ('"' or '\''))
                continue;

            var quote = Text[quoteIndex];
            var valueStart = quoteIndex + 1;
            var valueEnd = Text.IndexOf(quote, valueStart);
            if (valueEnd < 0 || valueEnd > tagEnd)
                continue;

            var fullName = attribute.Name.Namespace == XamlNamespace
                ? $"x:{attribute.Name.LocalName}"
                : attribute.Name.LocalName;

            result.Add(new ForgeXamlAttribute(
                fullName,
                attribute.Value,
                start,
                valueStart,
                valueEnd,
                valueEnd));
        }

        return result;
    }

    static bool AttributeNameEquals(string actual, string requested)
    {
        if (string.Equals(actual, requested, StringComparison.Ordinal))
            return true;

        if (requested.StartsWith("x:", StringComparison.Ordinal))
            return false;

        return actual.StartsWith("x:", StringComparison.Ordinal)
            ? string.Equals(actual[2..], requested, StringComparison.Ordinal)
            : false;
    }

    static bool IsPropertyElement(XElement element) =>
        element.Name.LocalName.Contains('.', StringComparison.Ordinal);

    static string? GetAuthoredName(XElement element) =>
        element.Attribute(XamlNamespace + "Name")?.Value
        ?? element.Attribute("Name")?.Value;

    static string CreatePathId(XElement element)
    {
        var segments = new Stack<string>();
        XElement? current = element;

        while (current is not null)
        {
            var sameTypeBefore = current
                .ElementsBeforeSelf(current.Name)
                .Count();

            segments.Push($"{current.Name.LocalName}[{sameTypeBefore}]");
            current = current.Parent;
        }

        return "/" + string.Join("/", segments);
    }

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

    static int LineColumnToIndex(
        IReadOnlyList<int> lineStarts,
        int line,
        int column,
        int textLength)
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

    static string EscapeAttributeValue(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}

public sealed record ForgeXamlElement(
    string Id,
    string? Name,
    string TypeName,
    int Line,
    int Column,
    int StartIndex,
    int StartTagEndIndex,
    int Depth,
    string? ParentId,
    bool IsPropertyElement,
    IReadOnlyList<ForgeXamlAttribute> Attributes)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name)
            ? TypeName
            : $"{Name} [{TypeName}]";
}

public sealed record ForgeXamlAttribute(
    string Name,
    string Value,
    int StartIndex,
    int ValueStartIndex,
    int ValueEndIndex,
    int EndIndex);
