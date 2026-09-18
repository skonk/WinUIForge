using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIForge.Port.Core;
using WinUIEditor;

namespace WinUIForge.Port.App;

internal sealed class ForgeSourceEditor : Grid
{
    readonly CodeEditorControl editorControl = new()
    {
        HighlightingLanguage = "xml",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    string pendingText = string.Empty;
    bool ready;
    bool suppressEditorEvents;

    public ForgeSourceEditor()
    {
        Children.Add(editorControl);

        editorControl.Loaded += (_, _) =>
        {
            if (ready) return;
            ready = true;

            editorControl.Editor.Modified += OnModified;
            editorControl.Editor.UpdateUI += OnUpdateUi;

            suppressEditorEvents = true;
            try
            {
                editorControl.Editor.SetText(pendingText);
                editorControl.Editor.EmptyUndoBuffer();
            }
            finally
            {
                suppressEditorEvents = false;
            }

            EditorReady?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? TextChanged;

    public event EventHandler? CaretChanged;

    public event EventHandler? EditorReady;

    public string Text
    {
        get
        {
            if (!ready) return pendingText;
            var length = editorControl.Editor.Length;
            return editorControl.Editor.GetText(length + 1);
        }
        set
        {
            pendingText = value ?? string.Empty;
            if (!ready) return;

            suppressEditorEvents = true;
            try
            {
                var oldCaret = CaretUtf16Index;
                editorControl.Editor.SetText(pendingText);
                editorControl.Editor.GotoPos(ForgeTextPosition.Utf16IndexToUtf8ByteOffset(pendingText, Math.Min(oldCaret, pendingText.Length)));
            }
            finally
            {
                suppressEditorEvents = false;
            }
        }
    }

    public int CaretUtf16Index
    {
        get
        {
            if (!ready) return 0;
            return ForgeTextPosition.Utf8ByteOffsetToUtf16Index(Text, editorControl.Editor.CurrentPos);
        }
    }

    public void GotoUtf16Index(int index)
    {
        if (!ready) return;

        var text = Text;
        var clamped = Math.Clamp(index, 0, text.Length);
        editorControl.Editor.GotoPos(ForgeTextPosition.Utf16IndexToUtf8ByteOffset(text, clamped));
        editorControl.Focus(FocusState.Programmatic);
    }

    void OnModified(Editor sender, ModifiedEventArgs args)
    {
        if (suppressEditorEvents) return;

        // WinUIEdit/Scintilla can deliver a Modified notification after SetText has
        // returned. Forge assigns pendingText before programmatic source updates,
        // so only surface this as a user edit when the live editor text actually
        // differs from the last value Forge supplied. Otherwise a delayed
        // programmatic notification would clear ForgeEditHistory immediately
        // after every visual authoring transaction.
        var currentText = Text;
        if (string.Equals(currentText, pendingText, StringComparison.Ordinal))
            return;

        pendingText = currentText;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnUpdateUi(Editor sender, UpdateUIEventArgs args)
    {
        if (suppressEditorEvents) return;
        CaretChanged?.Invoke(this, EventArgs.Empty);
    }

}
