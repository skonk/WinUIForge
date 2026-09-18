using System.Text;

namespace WinUIForge.Port.Core;

public static class ForgeTextPosition
{
    public static int Utf8ByteOffsetToUtf16Index(string text, long byteOffset)
    {
        if (string.IsNullOrEmpty(text) || byteOffset <= 0) return 0;

        var bytes = Encoding.UTF8.GetBytes(text);
        var clamped = (int)Math.Clamp(byteOffset, 0, bytes.Length);

        while (clamped > 0 &&
               clamped < bytes.Length &&
               (bytes[clamped] & 0b1100_0000) == 0b1000_0000)
        {
            clamped--;
        }

        return Encoding.UTF8.GetString(bytes, 0, clamped).Length;
    }

    public static long Utf16IndexToUtf8ByteOffset(string text, int utf16Index)
    {
        if (string.IsNullOrEmpty(text) || utf16Index <= 0) return 0;

        var clamped = Math.Clamp(utf16Index, 0, text.Length);

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
