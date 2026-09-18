using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text;
using WinUIEditor;

namespace WinUIForge.Port.App;

/// <summary>
/// Forge-facing source editor adapter around WinUIEdit/Scintilla.
///
/// Scintilla caret positions are UTF-8 byte offsets while ForgeXamlDocument uses
/// .NET UTF-16 string indices. This class owns that translation so the rest of Forge
/// can remain editor-implementation agnostic.
/// </summary>
internal sealed class ForgeSourceEditor : Grid
{
    readonly CodeEditorControl control = new()
    {
        HighlightingLanguage = "xml",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    string cachedText = "";
    bool settingText;
    bool suppressCaretNotification;
    bool defaultsApplied;

    public ForgeSourceEditor()
    {
        Children.Add(control);

        control.Editor.Modified += (_, _) => OnEditorModified();
        control.Editor.UpdateUI += (_, _) => OnEditorUpdated();
        control.Loaded += (_, _) => ApplyDefaultsOnce();
    }

    public event EventHandler? TextChanged;
    public event EventHandler? CaretMoved;

    public string Text
    {
        get => ReadText();
        set => SetText(value ?? "");
    }

    public int CaretIndex
    {
        get
        {
            var text = ReadText();
            return Utf8BytePositionToUtf16Index(
                text,
                control.Editor.CurrentPos);
        }
    }

    public void RevealIndex(int utf16Index)
    {
        var text = ReadText();
        var clamped = Math.Clamp(utf16Index, 0, text.Length);
        var bytePosition = Utf16IndexToUtf8BytePosition(text, clamped);

        suppressCaretNotification = true;
        control.Editor.GotoPos(bytePosition);

        if (!DispatcherQueue.TryEnqueue(() => suppressCaretNotification = false))
            suppressCaretNotification = false;
    }

    void SetText(string value)
    {
        if (string.Equals(value, cachedText, StringComparison.Ordinal) &&
            string.Equals(value, ReadText(), StringComparison.Ordinal))
            return;

        settingText = true;
        try
        {
            control.Editor.SetText(value);
            cachedText = value;
        }
        finally
        {
            settingText = false;
        }
    }

    string ReadText()
    {
        var length = Math.Max(0, control.Editor.TextLength);
        var value = control.Editor.GetText(length + 1);

        // The WinUIEdit wrapper requests a NUL-terminated Scintilla buffer. Depending
        // on projection/version, the terminal NUL can be observable in the managed
        // string, so normalize it here.
        return value.TrimEnd('\0');
    }

    void OnEditorModified()
    {
        if (settingText) return;

        var current = ReadText();
        if (string.Equals(current, cachedText, StringComparison.Ordinal))
            return;

        cachedText = current;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnEditorUpdated()
    {
        if (settingText || suppressCaretNotification)
            return;

        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    void ApplyDefaultsOnce()
    {
        if (defaultsApplied) return;
        defaultsApplied = true;

        try
        {
            control.ApplyDefaultsToDocument();
        }
        catch
        {
            // The editor remains usable if a prerelease package changes this optional
            // defaults helper. Core text/caret APIs are the required contract.
        }
    }

    internal static long Utf16IndexToUtf8BytePosition(string text, int utf16Index)
    {
        utf16Index = Math.Clamp(utf16Index, 0, text.Length);
        return Encoding.UTF8.GetByteCount(text.AsSpan(0, utf16Index));
    }

    internal static int Utf8BytePositionToUtf16Index(string text, long bytePosition)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var clamped = (int)Math.Clamp(bytePosition, 0, bytes.LongLength);

        // Scintilla gives valid code-point boundaries. Be defensive if a future editor
        // hands us a byte offset in the middle of a UTF-8 sequence.
        while (clamped > 0 &&
               clamped < bytes.Length &&
               (bytes[clamped] & 0b1100_0000) == 0b1000_0000)
        {
            clamped--;
        }

        return Encoding.UTF8.GetCharCount(bytes, 0, clamped);
    }
}
