using WinUIForge.Port.Core;

var tests = new List<(string Name, Action Run)>
{
    ("parse-authored-tree", ParseAuthoredTree),
    ("source-index-mapping", SourceIndexMapping),
    ("minimal-existing-property-write", MinimalExistingPropertyWrite),
    ("minimal-property-add", MinimalPropertyAdd),
    ("minimal-property-remove", MinimalPropertyRemove),
    ("unnamed-elements-get-stable-paths", UnnamedElementsGetStablePaths),
    ("utf8-editor-position-roundtrip", Utf8EditorPositionRoundtrip),
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
    Check(doc.Elements.Count >= 8, "all XML elements are represented, not only named elements");
    Check(doc.FindByName("ActionButton")?.TypeName == "Button", "button mapping");
    Check(doc.FindByName("HeadingText")?.Line > 0, "line info");
    Check(doc.GetChildren("ContentStack").Any(x => x.Name == "ActionButton"), "parent/child structure");
    Check(doc.Elements.Any(x => x.IsPropertyElement && x.TypeName == "Grid.Resources"), "property element flagged");
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

static void MinimalExistingPropertyWrite()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);
    doc.SetAttribute("HeadingText", "Text", "Forge & Friends");

    var expected = source.Replace(
        "Text=\"WinUI Forge\"",
        "Text=\"Forge &amp; Friends\"",
        StringComparison.Ordinal);

    Check(doc.GetAttribute("HeadingText", "Text") == "Forge & Friends", "decoded value stored");
    Check(doc.Text == expected, "only the existing attribute value changed");
}

static void MinimalPropertyAdd()
{
    var source = Fixture();
    var doc = new ForgeXamlDocument(source);

    doc.SetAttribute("ActionButton", "Width", "160");

    Check(doc.GetAttribute("ActionButton", "Width") == "160", "width stored");
    Check(doc.Text.Contains("Background=\"{StaticResource AccentBrush}\" Width=\"160\"/>", StringComparison.Ordinal), "attribute inserted in existing tag");
    Check(doc.Text.Count(c => c == '\n') == source.Count(c => c == '\n'), "adding property does not reformat document");
}

static void MinimalPropertyRemove()
{
    var doc = new ForgeXamlDocument(Fixture());
    doc.SetAttribute("ActionButton", "Width", "160");
    var withWidth = doc.Text;
    doc.SetAttribute("ActionButton", "Width", null);

    Check(doc.GetAttribute("ActionButton", "Width") is null, "property removed");
    Check(!doc.Text.Contains("Width=\"160\"", StringComparison.Ordinal), "source property removed");
    Check(doc.Text.Length < withWidth.Length, "narrow removal applied");
}

static void UnnamedElementsGetStablePaths()
{
    var doc = new ForgeXamlDocument(Fixture());
    var unnamedText = doc.Elements.First(x =>
        x.TypeName == "TextBlock" &&
        x.Name is null &&
        x.Attributes.Any(a => a.Name == "Text" && a.Value == "Port proof"));

    Check(unnamedText.Id.Contains("TextBlock[", StringComparison.Ordinal), "path identity");
    Check(doc.FindById(unnamedText.Id) == unnamedText, "id lookup");
}

static void Utf8EditorPositionRoundtrip()
{
    const string text = "<TextBlock Text=\"Café 🛠 ruins\"/>";
    for (var i = 0; i <= text.Length; i++)
    {
        // Do not test the interior UTF-16 code unit of a surrogate pair because no
        // valid editor caret can land there.
        if (i > 0 && i < text.Length &&
            char.IsHighSurrogate(text[i - 1]) &&
            char.IsLowSurrogate(text[i]))
        {
            continue;
        }

        var bytes = ForgeTextPosition.Utf16IndexToUtf8BytePosition(text, i);
        var roundtrip = ForgeTextPosition.Utf8BytePositionToUtf16Index(text, bytes);
        Check(roundtrip == i, $"UTF position roundtrip at {i}");
    }

    var emojiIndex = text.IndexOf("🛠", StringComparison.Ordinal);
    var emojiByte = ForgeTextPosition.Utf16IndexToUtf8BytePosition(text, emojiIndex);
    Check(
        ForgeTextPosition.Utf8BytePositionToUtf16Index(text, emojiByte + 1) == emojiIndex,
        "mid-sequence byte clamps to emoji start");
}

static void UnknownElement()
{
    var doc = new ForgeXamlDocument(Fixture());
    try
    {
        doc.SetAttributeById("MissingControl", "Width", "100");
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
        <Grid.Resources>
            <SolidColorBrush x:Key="AccentBrush" Color="#F97419"/>
        </Grid.Resources>
        <StackPanel x:Name="ContentStack">
            <TextBlock x:Name="HeadingText"
                       Text="WinUI Forge"
                       FontSize="32"
                       FontWeight="SemiBold"/>
            <Button x:Name="ActionButton"
                    Content="Select me"
                    Background="{StaticResource AccentBrush}"/>
        </StackPanel>
        <Border x:Name="InspectorCard">
            <StackPanel>
                <TextBlock Text="Port proof"/>
                <TextBlock Text="Unnamed authored child"/>
            </StackPanel>
        </Border>
    </Grid>
    """;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
