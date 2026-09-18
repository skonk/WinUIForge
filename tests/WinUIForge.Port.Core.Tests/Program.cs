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
    ("unknown-element", UnknownElement),
    ("unnamed-node-identity", UnnamedNodeIdentity),
    ("unnamed-property-edit-preserves-identity", UnnamedPropertyEditPreservesIdentity),
    ("insert-delete-structural-edit", InsertDeleteStructuralEdit),
    ("reorder-structural-edit", ReorderStructuralEdit),
    ("reparent-structural-edit", ReparentStructuralEdit),
    ("history-undo-redo", HistoryUndoRedo),
    ("workshop-dashboard-benchmark-parses", WorkshopDashboardBenchmarkParses)
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


static void UnnamedNodeIdentity()
{
    var doc = new ForgeXamlDocument(StructuralFixture());
    var columns = doc.Elements.Where(x => x.TypeName == "ColumnDefinition").ToList();
    Check(columns.Count == 2, "column definitions discovered");
    Check(columns.All(x => !string.IsNullOrWhiteSpace(x.Identity)), "unnamed identities exist");
    Check(columns[0].Identity != columns[1].Identity, "unnamed identities are unique");
    Check(doc.FindByIdentity(columns[1].Identity)?.GetType() == typeof(ForgeXamlElement), "identity lookup");
}

static void UnnamedPropertyEditPreservesIdentity()
{
    var doc = new ForgeXamlDocument(StructuralFixture());
    var column = doc.Elements.Where(x => x.TypeName == "ColumnDefinition").ElementAt(1);
    var identity = column.Identity;

    var edit = doc.SetAttributeByIdentity(identity, "Width", "240");

    Check(edit.Changed, "unnamed property edit changed");
    Check(doc.FindByIdentity(identity) is not null, "unnamed identity survived attribute edit");
    Check(doc.GetAttributeByIdentity(identity, "Width") == "240", "unnamed attribute updated");
}

static void InsertDeleteStructuralEdit()
{
    var doc = new ForgeXamlDocument(StructuralFixture());
    var edit = doc.InsertChild(
        doc.FindByName("ContentStack")!.Identity,
        "<Button x:Name=\"GeneratedButton\" Content=\"Generated\"/>");

    Check(edit.Changed, "insert changed");
    Check(doc.FindByName("GeneratedButton") is not null, "inserted button discovered");
    Check(doc.Text.Contains("GeneratedButton", StringComparison.Ordinal), "insert source");

    var remove = doc.RemoveElement(doc.FindByName("GeneratedButton")!.Identity);
    Check(remove.Changed, "delete changed");
    Check(doc.FindByName("GeneratedButton") is null, "button removed");
    Check(new ForgeXamlDocument(doc.Text).Root.Name == "RootGrid", "document remains valid");
}

static void ReorderStructuralEdit()
{
    var doc = new ForgeXamlDocument(StructuralFixture());
    var button = doc.FindByName("ActionButton")!;
    var edit = doc.ReorderElement(button.Identity, -1);

    Check(edit.Changed, "reorder changed");
    var stack = doc.FindByName("ContentStack")!;
    Check(stack.ContentChildren[0].Name == "ActionButton", "button moved before heading");
    Check(stack.ContentChildren[1].Name == "HeadingText", "heading moved after button");
}

static void ReparentStructuralEdit()
{
    var doc = new ForgeXamlDocument(StructuralFixture());
    var edit = doc.MoveElement(
        doc.FindByName("ActionButton")!.Identity,
        doc.FindByName("TargetGrid")!.Identity);

    Check(edit.Changed, "reparent changed");
    Check(doc.FindByName("ActionButton")?.Parent?.Name == "TargetGrid", "button parent changed");
    Check(new ForgeXamlDocument(doc.Text).FindByName("ActionButton") is not null, "reparented source valid");
}

static void HistoryUndoRedo()
{
    var history = new ForgeEditHistory();
    history.Record("Change text", "A", "B", "name:A", "name:B");

    Check(history.CanUndo, "can undo");
    var before = history.Undo();
    Check(before?.Source == "A", "undo source");
    Check(before?.SelectionIdentity == "name:A", "undo selection");
    Check(history.CanRedo, "can redo");

    var after = history.Redo();
    Check(after?.Source == "B", "redo source");
    Check(after?.SelectionIdentity == "name:B", "redo selection");
}

static void WorkshopDashboardBenchmarkParses()
{
    var path = Path.Combine(
        AppContext.BaseDirectory,
        "Benchmark",
        "workshop-dashboard-v1",
        "Screen.xaml");

    Check(File.Exists(path), "benchmark fixture copied to test output");

    var source = File.ReadAllText(path);
    var doc = new ForgeXamlDocument(source);

    Check(doc.Root.Name == "WorkshopDashboardBenchmark", "benchmark root");
    Check(doc.FindByName("TopCommandBar") is not null, "top command bar");
    Check(doc.FindByName("LocalDashboardPane") is not null, "local dashboard pane");
    Check(doc.FindByName("DashboardMain") is not null, "main dashboard");
    Check(doc.FindByName("MetricCards") is not null, "metric cards");
    Check(doc.FindByName("RecentActivityPanel") is not null, "recent activity");
    Check(doc.FindByName("RecentProjectsPanel") is not null, "recent projects");
    Check(doc.FindByName("QuickActionsPanel") is not null, "quick actions");
    Check(doc.FindByName("WorkerStatusPanel") is not null, "worker status");
    Check(doc.FindByName("ProjectInspector") is not null, "project inspector");
    Check(doc.FindByName("ActivityConsole") is not null, "activity console");

    var workspace = doc.FindByName("DashboardWorkspace");
    Check(workspace is not null, "dashboard workspace");
    var columns = workspace!.Children
        .FirstOrDefault(x => x.TypeName == "Grid.ColumnDefinitions")
        ?.Children
        .Where(x => x.TypeName == "ColumnDefinition")
        .ToList();

    Check(columns?.Count == 3, "three workspace columns");
    Check(columns![0].Attributes.FirstOrDefault(x => x.Name == "Width")?.Value == "205", "local pane width");
    Check(columns[1].Attributes.FirstOrDefault(x => x.Name == "Width")?.Value == "*", "center is flexible");
    Check(columns[2].Attributes.FirstOrDefault(x => x.Name == "Width")?.Value == "259", "inspector width");
}

static string StructuralFixture() =>
    """
    <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          x:Name="RootGrid">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="200"/>
        </Grid.ColumnDefinitions>
        <StackPanel x:Name="ContentStack">
            <TextBlock x:Name="HeadingText" Text="Heading"/>
            <Button x:Name="ActionButton" Content="Go"/>
        </StackPanel>
        <Grid x:Name="TargetGrid">
        </Grid>
    </Grid>
    """;
