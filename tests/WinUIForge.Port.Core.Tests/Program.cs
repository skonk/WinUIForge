using System.Text;
using WinUIForge.Port.Core;

var tests = new List<(string Name, Action Run)>
{
    ("parse-authored-tree", ParseAuthoredTree),
    ("source-index-mapping", SourceIndexMapping),
    ("replace-property-preserves-formatting", ReplacePropertyPreservesFormatting),
    ("add-property-preserves-formatting", AddPropertyPreservesFormatting),
    ("remove-property-preserves-document", RemovePropertyPreservesDocument),
    ("attribute-escaping", AttributeEscaping),
    ("utf8-utf16-position-roundtrip", Utf8Utf16PositionRoundtrip),
    ("unknown-element", UnknownElement)
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception e)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {e}");
    }
}

return failed == 0 ? 0 : 1;

static void ParseAuthoredTree()
{
    var doc = new ForgeXamlDocument(Fixture());

    Check(doc.Root.Name == "RootGrid", "root name");
    Check(doc.Root.TypeName == "Grid", "root type");
    Check(doc.Root.Children.Count == 1, "root child count");
    Check(doc.Root.Children[0].Name == "ContentStack", "stack child");
    Check(doc.Root.Children[0].Children.Count == 2, "stack children");
    Check(doc.FindByName("ActionButton")?.TypeName == "Button", "button mapping");
    Check(doc.FindByName("HeadingText")?.Line > 0, "line info");
    Check(doc.FindByName("ActionButton")?.Parent?.Name == "ContentStack", "parent mapping");
    Check(doc.FindByName("ActionButton")?.Depth == 2, "depth");
}

static void SourceIndexMapping()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);
    var index = source.IndexOf("x:Name=\"ActionButton\"", StringComparison.Ordinal);
    Check(index >= 0, "fixture button source");
    Check(doc.FindAtSourceIndex(index)?.Name == "ActionButton", "caret maps to button start tag");

    var closingTagIndex = source.IndexOf("</StackPanel>", StringComparison.Ordinal);
    Check(closingTagIndex >= 0, "fixture closing tag");
    Check(doc.FindAtSourceIndex(closingTagIndex) is null, "mapping remains start-tag scoped");
}

static void ReplacePropertyPreservesFormatting()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);
    var beforePrefix = source[..source.IndexOf("Content=\"Select me\"", StringComparison.Ordinal)];
    var beforeSuffix = source[(source.IndexOf("Content=\"Select me\"", StringComparison.Ordinal) + "Content=\"Select me\"".Length)..];

    var edit = doc.SetAttribute("ActionButton", "Content", "Generate");

    Check(edit.Changed, "edit changed");
    Check(edit.OldValue == "Select me", "old value");
    Check(edit.NewValue == "Generate", "new value");
    Check(doc.GetAttribute("ActionButton", "Content") == "Generate", "new value stored");
    Check(doc.Text.StartsWith(beforePrefix, StringComparison.Ordinal), "prefix preserved");
    Check(doc.Text.EndsWith(beforeSuffix, StringComparison.Ordinal), "suffix preserved");
    Check(doc.Text.Contains("Content=\"Generate\"", StringComparison.Ordinal), "only value changed");
}

static void AddPropertyPreservesFormatting()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);
    doc.SetAttribute("ActionButton", "Width", "160");

    Check(doc.GetAttribute("ActionButton", "Width") == "160", "width stored");
    var widthLine = doc.Text.Split('\n').Single(x => x.Contains("Width=\"160\"", StringComparison.Ordinal));
    var alignmentLine = doc.Text.Split('\n').Single(x => x.Contains("HorizontalAlignment=\"Left\"", StringComparison.Ordinal));
    Check(
        widthLine[..widthLine.IndexOf("Width=", StringComparison.Ordinal)] ==
        alignmentLine[..alignmentLine.IndexOf("HorizontalAlignment=", StringComparison.Ordinal)],
        "multiline indentation preserved");
    Check(doc.Text.Contains("Content=\"Select me\"", StringComparison.Ordinal), "existing content preserved");
    Check(doc.Text.Contains("HorizontalAlignment=\"Left\"", StringComparison.Ordinal), "existing sibling property preserved");
}

static void RemovePropertyPreservesDocument()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);
    doc.SetAttribute("ActionButton", "HorizontalAlignment", null);

    Check(doc.GetAttribute("ActionButton", "HorizontalAlignment") is null, "attribute removed");
    Check(doc.Text.Contains("Content=\"Select me\"", StringComparison.Ordinal), "neighbor retained");
    Check(doc.Text.Contains("x:Name=\"ActionButton\"", StringComparison.Ordinal), "identity retained");
    Check(new ForgeXamlDocument(doc.Text).FindByName("ActionButton") is not null, "still parseable");
}

static void AttributeEscaping()
{
    var doc = new ForgeXamlDocument(Fixture());
    doc.SetAttribute("ActionButton", "Content", "A & B \"quoted\"");

    Check(doc.Text.Contains("A &amp; B &quot;quoted&quot;", StringComparison.Ordinal), "escaped source");
    Check(doc.GetAttribute("ActionButton", "Content") == "A & B \"quoted\"", "unescaped model value");
}


static void Utf8Utf16PositionRoundtrip()
{
    const string text = "Grid Δ café 😺 Button";

    for (var utf16 = 0; utf16 <= text.Length; utf16++)
    {
        if (utf16 > 0 &&
            utf16 < text.Length &&
            char.IsLowSurrogate(text[utf16]) &&
            char.IsHighSurrogate(text[utf16 - 1]))
        {
            continue;
        }

        var bytes = ForgeTextPosition.Utf16IndexToUtf8ByteOffset(text, utf16);
        var roundtrip = ForgeTextPosition.Utf8ByteOffsetToUtf16Index(text, bytes);
        Check(roundtrip == utf16, $"UTF roundtrip at {utf16}");
    }

    var emojiIndex = text.IndexOf("😺", StringComparison.Ordinal);
    var emojiByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, emojiIndex));
    Check(
        ForgeTextPosition.Utf8ByteOffsetToUtf16Index(text, emojiByte) == emojiIndex,
        "emoji boundary maps to UTF-16 source index");
}

static void UnknownElement()
{
    var doc = new ForgeXamlDocument(Fixture());
    try
    {
        doc.SetAttribute("MissingControl", "Width", "100");
        throw new InvalidOperationException("missing element should have failed");
    }
    catch (InvalidOperationException e) when (e.Message.Contains("MissingControl", StringComparison.Ordinal))
    {
    }
}

static string Fixture() =>
    """
    <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          x:Name="RootGrid">
        <StackPanel x:Name="ContentStack">
            <TextBlock x:Name="HeadingText"
                       Text="WinUI Forge"/>
            <Button x:Name="ActionButton"
                    Content="Select me"
                    HorizontalAlignment="Left"/>
        </StackPanel>
    </Grid>
    """;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
