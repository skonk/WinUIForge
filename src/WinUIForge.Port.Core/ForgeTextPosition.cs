using System.Text;

namespace WinUIForge.Port.Core;

/// <summary>
/// Converts between .NET UTF-16 source indices and UTF-8 byte offsets used by
/// Scintilla/WinUIEdit.
/// </summary>
public static class ForgeTextPosition
{
    public static long Utf16IndexToUtf8BytePosition(string text, int utf16Index)
    {
        ArgumentNullException.ThrowIfNull(text);
        utf16Index = Math.Clamp(utf16Index, 0, text.Length);
        return Encoding.UTF8.GetByteCount(text.AsSpan(0, utf16Index));
    }

    public static int Utf8BytePositionToUtf16Index(string text, long bytePosition)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = Encoding.UTF8.GetBytes(text);
        var clamped = (int)Math.Clamp(bytePosition, 0, bytes.LongLength);

        // Positions from Scintilla normally land on UTF-8 code-point boundaries.
        // Clamp backwards if a caller supplies a byte inside a continuation sequence.
        while (clamped > 0 &&
               clamped < bytes.Length &&
               (bytes[clamped] & 0b1100_0000) == 0b1000_0000)
        {
            clamped--;
        }

        return Encoding.UTF8.GetCharCount(bytes, 0, clamped);
    }
}
