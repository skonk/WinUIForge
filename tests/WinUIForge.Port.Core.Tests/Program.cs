using WinUIForge.Port.Core;

var tests = new List<(string Name, Action Run)>
{
    ("parse-named-elements", ParseNamedElements),
    ("source-index-mapping", SourceIndexMapping),
    ("write-property", WriteProperty),
    ("remove-property", RemoveProperty),
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

static void ParseNamedElements()
{
    var doc = new ForgeXamlDocument(Fixture());
    Check(doc.Elements.Count == 4, "expected four named authored elements");
    Check(doc.FindByName("ActionButton")?.TypeName == "Button", "button mapping");
    Check(doc.FindByName("HeadingText")?.Line > 0, "line info");
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
    Check(doc.FindAtSourceIndex(closingTagIndex) is null, "mapping is intentionally start-tag scoped in the first proof");
}

static void WriteProperty()
{
    var doc = new ForgeXamlDocument(Fixture());
    doc.SetAttribute("ActionButton", "Width", "160");

    Check(doc.GetAttribute("ActionButton", "Width") == "160", "width stored");
    Check(doc.Text.Contains("Width=\"160\"", StringComparison.Ordinal), "source updated");

    var reparsed = new ForgeXamlDocument(doc.Text);
    Check(reparsed.FindByName("ActionButton") is not null, "updated source remains parseable");
}

static void RemoveProperty()
{
    var doc = new ForgeXamlDocument(Fixture());
    doc.SetAttribute("ActionButton", "Width", "160");
    doc.SetAttribute("ActionButton", "Width", null);
    Check(doc.GetAttribute("ActionButton", "Width") is null, "blank value removes property");
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
                    Content="Select me"/>
        </StackPanel>
    </Grid>
    """;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
