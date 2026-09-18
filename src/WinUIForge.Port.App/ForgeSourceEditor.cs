using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text;
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
                editorControl.Editor.GotoPos(Utf16IndexToUtf8ByteOffset(pendingText, Math.Min(oldCaret, pendingText.Length)));
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
            return Utf8ByteOffsetToUtf16Index(Text, editorControl.Editor.CurrentPos);
        }
    }

    public void GotoUtf16Index(int index)
    {
        if (!ready) return;

        var text = Text;
        var clamped = Math.Clamp(index, 0, text.Length);
        editorControl.Editor.GotoPos(Utf16IndexToUtf8ByteOffset(text, clamped));
        editorControl.Focus(FocusState.Programmatic);
    }

    void OnModified(Editor sender, ModifiedEventArgs args)
    {
        if (suppressEditorEvents) return;
        pendingText = Text;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnUpdateUi(Editor sender, UpdateUIEventArgs args)
    {
        if (suppressEditorEvents) return;
        CaretChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static int Utf8ByteOffsetToUtf16Index(string text, long byteOffset)
    {
        if (string.IsNullOrEmpty(text) || byteOffset <= 0) return 0;

        var bytes = Encoding.UTF8.GetBytes(text);
        var clamped = (int)Math.Clamp(byteOffset, 0, bytes.Length);

        // Scintilla positions are UTF-8 byte offsets. Decode the prefix to recover
        // the .NET UTF-16 index used by our XAML source spans.
        return Encoding.UTF8.GetString(bytes, 0, clamped).Length;
    }

    internal static long Utf16IndexToUtf8ByteOffset(string text, int utf16Index)
    {
        if (string.IsNullOrEmpty(text) || utf16Index <= 0) return 0;

        var clamped = Math.Clamp(utf16Index, 0, text.Length);

        // Avoid splitting a surrogate pair if a caller supplied an arbitrary index.
        if (clamped < text.Length &&
            clamped > 0 &&
            char.IsLowSurrogate(text[clamped]) &&
            char.IsHighSurrogate(text[clamped - 1]))
        {
            clamped--;
        }

        return Encoding.UTF8.GetByteCount(text.AsSpan(0, clamped));
    }
}
