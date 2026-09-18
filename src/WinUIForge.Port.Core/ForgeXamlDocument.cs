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
    Dictionary<string, ForgeXamlElement> elementsByIdentity = new(StringComparer.Ordinal);

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

    public ForgeXamlElement? FindByIdentity(string? identity) =>
        string.IsNullOrWhiteSpace(identity)
            ? null
            : elementsByIdentity.GetValueOrDefault(identity);

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

    public string? GetAttributeByIdentity(string identity, string attributeName) =>
        FindByIdentity(identity)?.Attributes
            .FirstOrDefault(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal))
            ?.Value;

    public bool HasAttribute(string elementName, string attributeName) =>
        FindByName(elementName)?.Attributes.Any(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal)) == true;

    public string CreateUniqueName(string typeName)
    {
        var prefix = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
        var i = 1;
        while (FindByName(prefix + i) is not null) i++;
        return prefix + i;
    }

    public ForgeXamlEdit SetAttribute(string elementName, string attributeName, string? value)
    {
        var element = FindByName(elementName)
            ?? throw new InvalidOperationException($"No authored XAML element named '{elementName}' exists.");
        return SetAttributeCore(element, attributeName, value);
    }

    public ForgeXamlEdit SetAttributeByIdentity(string identity, string attributeName, string? value)
    {
        var element = FindByIdentity(identity)
            ?? throw new InvalidOperationException($"No authored XAML element '{identity}' exists.");
        return SetAttributeCore(element, attributeName, value);
    }

    ForgeXamlEdit SetAttributeCore(ForgeXamlElement element, string attributeName, string? value)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new ArgumentException("Attribute name is required.", nameof(attributeName));

        var existing = element.Attributes
            .FirstOrDefault(x => string.Equals(x.Name, attributeName, StringComparison.Ordinal));

        var before = Text;
        ForgeXamlEdit edit;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is null)
                return new ForgeXamlEdit(element.Identity, attributeName, null, null, false, before, before);

            var removeStart = existing.StartIndex;
            while (removeStart > element.StartIndex &&
                   (Text[removeStart - 1] == ' ' || Text[removeStart - 1] == '\t'))
            {
                removeStart--;
            }

            Text = Text.Remove(removeStart, existing.EndIndex - removeStart);
            edit = new ForgeXamlEdit(
                element.Identity,
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
                    element.Identity,
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
                Text = Text.Insert(element.StartTagCloseIndex, insertion);

                edit = new ForgeXamlEdit(
                    element.Identity,
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

    public ForgeStructuralEdit InsertChild(string parentIdentity, string xamlFragment)
    {
        var parent = FindByIdentity(parentIdentity)
            ?? throw new InvalidOperationException($"Parent '{parentIdentity}' was not found.");

        if (!CanContainDesignChildren(parent.TypeName))
            throw new InvalidOperationException($"{parent.TypeName} is not a supported design container.");

        if (parent.IsSelfClosing)
            throw new InvalidOperationException($"Self-closing {parent.TypeName} must be expanded before adding children.");

        if (parent.TypeName == "Border" && parent.ContentChildren.Count > 0)
            throw new InvalidOperationException("Border can contain only one design child.");

        var fragmentDoc = new ForgeXamlDocument(WrapFragment(xamlFragment));
        var fragmentRoot = fragmentDoc.Root.ContentChildren.SingleOrDefault()
            ?? throw new InvalidOperationException("Toolbox fragment must contain exactly one root element.");

        var childName = fragmentRoot.Name;
        var childType = fragmentRoot.TypeName;
        var indent = GetPreferredChildIndent(parent);
        var formatted = ReindentFragment(xamlFragment.Trim(), indent);
        var insertion = Environment.NewLine + formatted + Environment.NewLine + GetIndentAt(parent.StartIndex);

        var before = Text;
        Text = Text.Insert(parent.EndTagStartIndex, insertion);
        Reparse();

        var inserted = !string.IsNullOrWhiteSpace(childName)
            ? FindByName(childName)
            : FindNewestChild(parentIdentity, childType);

        return new ForgeStructuralEdit(
            "Insert",
            before,
            Text,
            parentIdentity,
            inserted?.Identity,
            true);
    }

    public ForgeStructuralEdit RemoveElement(string identity)
    {
        var node = FindByIdentity(identity)
            ?? throw new InvalidOperationException($"Element '{identity}' was not found.");

        if (ReferenceEquals(node, Root))
            throw new InvalidOperationException("The root XAML element cannot be deleted.");

        var before = Text;
        Text = Text.Remove(node.StartIndex, node.FullEndIndex - node.StartIndex);
        Reparse();

        return new ForgeStructuralEdit("Delete", before, Text, identity, node.Parent?.Identity, true);
    }

    public ForgeStructuralEdit ReorderElement(string identity, int offset)
    {
        var node = FindByIdentity(identity)
            ?? throw new InvalidOperationException($"Element '{identity}' was not found.");
        var parent = node.Parent
            ?? throw new InvalidOperationException("The root element cannot be reordered.");

        var siblings = parent.ContentChildren.ToList();
        var current = siblings.IndexOf(node);
        if (current < 0) throw new InvalidOperationException("Element is not a design child of its parent.");

        var destination = Math.Clamp(current + offset, 0, siblings.Count - 1);
        if (destination == current)
            return new ForgeStructuralEdit("Reorder", Text, Text, identity, identity, false);

        var target = siblings[destination];
        var fragment = Text[node.StartIndex..node.FullEndIndex];
        var indent = GetIndentAt(node.StartIndex);
        var before = Text;
        var removedLength = node.FullEndIndex - node.StartIndex;

        Text = Text.Remove(node.StartIndex, removedLength);

        int insertAt;
        string insertion;

        if (destination < current)
        {
            insertAt = target.StartIndex;
            if (node.StartIndex < target.StartIndex) insertAt -= removedLength;
            insertion = fragment + Environment.NewLine + indent;
        }
        else
        {
            insertAt = target.FullEndIndex;
            if (node.StartIndex < target.FullEndIndex) insertAt -= removedLength;
            insertion = Environment.NewLine + indent + fragment;
        }

        Text = Text.Insert(insertAt, insertion);
        Reparse();

        var selectionIdentity = !string.IsNullOrWhiteSpace(node.Name)
            ? FindByName(node.Name)?.Identity
            : FindClosestIdentity(node.TypeName, node.Parent?.Identity);

        return new ForgeStructuralEdit("Reorder", before, Text, identity, selectionIdentity, true);
    }

    public ForgeStructuralEdit MoveElement(string identity, string newParentIdentity)
    {
        var node = FindByIdentity(identity)
            ?? throw new InvalidOperationException($"Element '{identity}' was not found.");
        var target = FindByIdentity(newParentIdentity)
            ?? throw new InvalidOperationException($"Target parent '{newParentIdentity}' was not found.");

        if (ReferenceEquals(node, Root))
            throw new InvalidOperationException("The root XAML element cannot be reparented.");
        if (!CanContainDesignChildren(target.TypeName))
            throw new InvalidOperationException($"{target.TypeName} is not a supported design container.");
        if (target.IsDescendantOf(node))
            throw new InvalidOperationException("An element cannot be moved into its own descendant.");
        if (target.TypeName == "Border" && target.ContentChildren.Count > 0 && !ReferenceEquals(node.Parent, target))
            throw new InvalidOperationException("Border can contain only one design child.");

        if (ReferenceEquals(node.Parent, target))
            return new ForgeStructuralEdit("Reparent", Text, Text, identity, identity, false);

        var fragment = Text[node.StartIndex..node.FullEndIndex];
        var targetStart = target.StartIndex;
        var targetIdentityWas = target.Identity;
        var before = Text;
        var removedLength = node.FullEndIndex - node.StartIndex;

        Text = Text.Remove(node.StartIndex, removedLength);
        Reparse();

        ForgeXamlElement? reparsedTarget = FindByIdentity(targetIdentityWas);
        if (reparsedTarget is null)
        {
            var adjustedStart = targetStart - (node.StartIndex < targetStart ? removedLength : 0);
            reparsedTarget = elements.FirstOrDefault(x => x.StartIndex == adjustedStart);
        }

        if (reparsedTarget is null)
            throw new InvalidOperationException("Target parent could not be resolved after source removal.");

        if (reparsedTarget.IsSelfClosing)
            throw new InvalidOperationException("Self-closing targets cannot receive children yet.");

        var indent = GetPreferredChildIndent(reparsedTarget);
        var formatted = ReindentFragment(fragment.Trim(), indent);
        var insertion = Environment.NewLine + formatted + Environment.NewLine + GetIndentAt(reparsedTarget.StartIndex);
        Text = Text.Insert(reparsedTarget.EndTagStartIndex, insertion);
        Reparse();

        var newIdentity = !string.IsNullOrWhiteSpace(node.Name)
            ? FindByName(node.Name)?.Identity
            : elements
                .Where(x => x.TypeName == node.TypeName && x.Parent?.Identity == reparsedTarget.Identity)
                .OrderByDescending(x => x.StartIndex)
                .FirstOrDefault()?.Identity;

        return new ForgeStructuralEdit("Reparent", before, Text, identity, newIdentity, true);
    }

    public static bool CanContainDesignChildren(string typeName) =>
        typeName is "Grid" or "StackPanel" or "Canvas" or "RelativePanel" or "Border" or "ScrollViewer";

    void Reparse()
    {
        document = XDocument.Parse(
            Text,
            LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        if (document.Root is null)
            throw new InvalidOperationException("XAML document has no root element.");

        var lineStarts = BuildLineStarts(Text);
        var spans = BuildElementSpans(Text, lineStarts)
            .ToDictionary(x => x.StartIndex);

        var all = new List<ForgeXamlElement>();
        Root = BuildNode(
            document.Root,
            parent: null,
            siblingOrdinal: 0,
            lineStarts,
            spans,
            all);

        elements = all.OrderBy(x => x.StartIndex).ToList();
        elementsByName = elements
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        elementsByIdentity = elements
            .GroupBy(x => x.Identity, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
    }

    ForgeXamlElement BuildNode(
        XElement element,
        ForgeXamlElement? parent,
        int siblingOrdinal,
        IReadOnlyList<int> lineStarts,
        IReadOnlyDictionary<int, ElementSpan> spans,
        List<ForgeXamlElement> all)
    {
        var lineInfo = (IXmlLineInfo)element;
        var startIndex = lineInfo.HasLineInfo()
            ? LineColumnToIndex(lineStarts, lineInfo.LineNumber, lineInfo.LinePosition, Text.Length)
            : 0;

        var span = spans.GetValueOrDefault(startIndex)
            ?? throw new InvalidOperationException($"Could not locate source span for {element.Name.LocalName}.");

        var attributes = ParseAttributes(Text, span.StartIndex, span.StartTagEndIndex);
        var name = GetAuthoredName(element);
        var xKey = element.Attribute(XamlNamespace + "Key")?.Value;
        var identity = BuildIdentity(parent, element.Name.LocalName, siblingOrdinal, name, xKey);

        var node = new ForgeXamlElement(
            identity,
            name,
            xKey,
            element.Name.LocalName,
            lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
            lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0,
            span.StartIndex,
            span.StartTagEndIndex,
            span.StartTagCloseIndex,
            span.EndTagStartIndex,
            span.FullEndIndex,
            span.IsSelfClosing,
            parent?.Depth + 1 ?? 0,
            parent,
            attributes);

        all.Add(node);

        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var child in element.Elements())
        {
            var localName = child.Name.LocalName;
            var ordinal = counters.GetValueOrDefault(localName);
            counters[localName] = ordinal + 1;
            node.Children.Add(BuildNode(child, node, ordinal, lineStarts, spans, all));
        }

        return node;
    }

    ForgeXamlElement? FindNewestChild(string parentIdentity, string typeName)
    {
        var parent = FindByIdentity(parentIdentity);
        return parent?.Children
            .Where(x => x.TypeName == typeName)
            .OrderByDescending(x => x.StartIndex)
            .FirstOrDefault();
    }

    string? FindClosestIdentity(string typeName, string? parentIdentity) =>
        elements
            .Where(x => x.TypeName == typeName && x.Parent?.Identity == parentIdentity)
            .OrderBy(x => x.StartIndex)
            .FirstOrDefault()?.Identity;

    string BuildAttributeInsertion(ForgeXamlElement element, string attributeName, string value)
    {
        var escaped = EscapeAttributeValue(value);
        var tagText = Text[element.StartIndex..element.StartTagEndIndex];

        if (!tagText.Contains('\n'))
            return $" {attributeName}=\"{escaped}\"";

        var elementLineStart = Text.LastIndexOf('\n', Math.Max(0, element.StartIndex - 1)) + 1;
        var baseIndentLength = element.StartIndex - elementLineStart;
        var attributeIndent = new string(' ', Math.Max(0, baseIndentLength + 4));

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

    string GetPreferredChildIndent(ForgeXamlElement parent)
    {
        var existing = parent.Children.FirstOrDefault();
        if (existing is not null)
            return GetIndentAt(existing.StartIndex);

        return GetIndentAt(parent.StartIndex) + "    ";
    }

    string GetIndentAt(int index)
    {
        var lineStart = Text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var length = Math.Max(0, index - lineStart);
        var prefix = Text.Substring(lineStart, length);
        return new string(prefix.TakeWhile(ch => ch is ' ' or '\t').ToArray());
    }

    static string ReindentFragment(string fragment, string indent)
    {
        var lines = fragment.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 1) return indent + lines[0].Trim();

        var nonEmpty = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var common = nonEmpty.Count == 0
            ? 0
            : nonEmpty.Min(x => x.TakeWhile(ch => ch is ' ' or '\t').Count());

        return string.Join(
            Environment.NewLine,
            lines.Select((line, i) =>
            {
                var normalized = line.Length >= common ? line[common..] : line.TrimStart();
                return indent + normalized;
            }));
    }

    static string WrapFragment(string fragment) =>
        "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
        "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
        fragment +
        "</Grid>";

    static string BuildIdentity(
        ForgeXamlElement? parent,
        string typeName,
        int ordinal,
        string? name,
        string? xKey)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return "name:" + name;

        var parentIdentity = parent?.Identity ?? "root";
        if (!string.IsNullOrWhiteSpace(xKey))
            return $"{parentIdentity}/key:{xKey}";

        return $"{parentIdentity}/{typeName}[{ordinal}]";
    }

    static IReadOnlyList<ElementSpan> BuildElementSpans(string text, IReadOnlyList<int> lineStarts)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = false,
            IgnoreWhitespace = false
        };

        var spans = new List<ElementSpan>();
        var stack = new Stack<int>();

        using var reader = XmlReader.Create(new StringReader(text), settings);
        var lineInfo = (IXmlLineInfo)reader;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var start = LineColumnToIndex(lineStarts, lineInfo.LineNumber, lineInfo.LinePosition, text.Length);
                var tagEnd = FindStartTagEnd(text, start);
                var close = FindStartTagCloseIndex(text, start, tagEnd);
                var span = new ElementSpan(
                    start,
                    tagEnd,
                    close,
                    reader.IsEmptyElement ? tagEnd : -1,
                    reader.IsEmptyElement ? tagEnd + 1 : -1,
                    reader.IsEmptyElement);

                spans.Add(span);
                if (!reader.IsEmptyElement)
                    stack.Push(spans.Count - 1);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && stack.Count > 0)
            {
                var index = stack.Pop();
                var endStart = LineColumnToIndex(lineStarts, lineInfo.LineNumber, lineInfo.LinePosition, text.Length);
                var endEnd = FindStartTagEnd(text, endStart);
                spans[index] = spans[index] with
                {
                    EndTagStartIndex = endStart,
                    FullEndIndex = endEnd + 1
                };
            }
        }

        return spans;
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

    sealed record ElementSpan(
        int StartIndex,
        int StartTagEndIndex,
        int StartTagCloseIndex,
        int EndTagStartIndex,
        int FullEndIndex,
        bool IsSelfClosing);
}

public sealed class ForgeXamlElement
{
    public ForgeXamlElement(
        string identity,
        string? name,
        string? xKey,
        string typeName,
        int line,
        int column,
        int startIndex,
        int startTagEndIndex,
        int startTagCloseIndex,
        int endTagStartIndex,
        int fullEndIndex,
        bool isSelfClosing,
        int depth,
        ForgeXamlElement? parent,
        IReadOnlyList<ForgeXamlAttribute> attributes)
    {
        Identity = identity;
        Name = name;
        XKey = xKey;
        TypeName = typeName;
        Line = line;
        Column = column;
        StartIndex = startIndex;
        StartTagEndIndex = startTagEndIndex;
        StartTagCloseIndex = startTagCloseIndex;
        EndTagStartIndex = endTagStartIndex;
        FullEndIndex = fullEndIndex;
        IsSelfClosing = isSelfClosing;
        Depth = depth;
        Parent = parent;
        Attributes = attributes;
    }

    public string Identity { get; }
    public string? Name { get; }
    public string? XKey { get; }
    public string TypeName { get; }
    public int Line { get; }
    public int Column { get; }
    public int StartIndex { get; }
    public int StartTagEndIndex { get; }
    public int StartTagCloseIndex { get; }
    public int EndTagStartIndex { get; }
    public int FullEndIndex { get; }
    public bool IsSelfClosing { get; }
    public int Depth { get; }
    public ForgeXamlElement? Parent { get; }
    public IReadOnlyList<ForgeXamlAttribute> Attributes { get; }
    public List<ForgeXamlElement> Children { get; } = [];
    public IReadOnlyList<ForgeXamlElement> ContentChildren =>
        Children.Where(x => !x.IsPropertyElement).ToList();
    public bool IsPropertyElement => TypeName.Contains('.', StringComparison.Ordinal);
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name)
            ? $"{Name} [{TypeName}]"
            : !string.IsNullOrWhiteSpace(XKey)
                ? $"{XKey} [{TypeName}]"
                : TypeName;

    public bool IsDescendantOf(ForgeXamlElement possibleAncestor)
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, possibleAncestor) ||
                string.Equals(current.Identity, possibleAncestor.Identity, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

public sealed record ForgeXamlAttribute(
    string Name,
    string Value,
    int StartIndex,
    int EndIndex,
    int ValueStartIndex,
    int ValueEndIndex);

public sealed record ForgeXamlEdit(
    string ElementIdentity,
    string AttributeName,
    string? OldValue,
    string? NewValue,
    bool Changed,
    string Before,
    string After);

public sealed record ForgeStructuralEdit(
    string Kind,
    string Before,
    string After,
    string? PreviousSelectionIdentity,
    string? NextSelectionIdentity,
    bool Changed);
