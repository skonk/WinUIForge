param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$IconPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Executable was not found: $ExePath"
}

if (-not (Test-Path -LiteralPath $IconPath)) {
    throw "Icon was not found: $IconPath"
}

# Keep this source compatible with the C# compiler used by Windows PowerShell
# Add-Type. Avoid newer syntax such as interpolated strings and using declarations.
$source = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static class WorkshopExeIconPatcher
{
    const ushort RT_ICON = 3;
    const ushort RT_GROUP_ICON = 14;
    const ushort LANG_NEUTRAL = 0;
    const ushort IDI_APPLICATION = 32512;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr BeginUpdateResourceW(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool UpdateResourceW(
        IntPtr hUpdate,
        IntPtr lpType,
        IntPtr lpName,
        ushort wLanguage,
        byte[] lpData,
        uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool EndUpdateResourceW(IntPtr hUpdate, bool fDiscard);

    static IntPtr MakeIntResource(ushort value)
    {
        return (IntPtr)value;
    }

    sealed class IconEntry
    {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint BytesInRes;
        public uint ImageOffset;
        public ushort ResourceId;
        public byte[] ImageData = new byte[0];
    }

    public static void Patch(string exePath, string iconPath)
    {
        byte[] iconBytes = File.ReadAllBytes(iconPath);
        List<IconEntry> entries = ParseIcon(iconBytes);

        IntPtr update = BeginUpdateResourceW(exePath, false);
        if (update == IntPtr.Zero)
            ThrowLastWin32("BeginUpdateResourceW");

        bool completed = false;
        try
        {
            foreach (IconEntry entry in entries)
            {
                if (!UpdateResourceW(
                    update,
                    MakeIntResource(RT_ICON),
                    MakeIntResource(entry.ResourceId),
                    LANG_NEUTRAL,
                    entry.ImageData,
                    (uint)entry.ImageData.Length))
                {
                    ThrowLastWin32("UpdateResourceW RT_ICON " + entry.ResourceId);
                }
            }

            byte[] groupData = BuildGroupIcon(entries);

            if (!UpdateResourceW(
                update,
                MakeIntResource(RT_GROUP_ICON),
                MakeIntResource(IDI_APPLICATION),
                LANG_NEUTRAL,
                groupData,
                (uint)groupData.Length))
            {
                ThrowLastWin32("UpdateResourceW RT_GROUP_ICON IDI_APPLICATION");
            }

            // Explorer often chooses the lowest group-icon resource.
            if (!UpdateResourceW(
                update,
                MakeIntResource(RT_GROUP_ICON),
                MakeIntResource(1),
                LANG_NEUTRAL,
                groupData,
                (uint)groupData.Length))
            {
                ThrowLastWin32("UpdateResourceW RT_GROUP_ICON 1");
            }

            if (!EndUpdateResourceW(update, false))
                ThrowLastWin32("EndUpdateResourceW");

            completed = true;
        }
        finally
        {
            if (!completed && update != IntPtr.Zero)
                EndUpdateResourceW(update, true);
        }
    }

    static List<IconEntry> ParseIcon(byte[] bytes)
    {
        MemoryStream stream = new MemoryStream(bytes, false);
        try
        {
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                ushort reserved = reader.ReadUInt16();
                ushort type = reader.ReadUInt16();
                ushort count = reader.ReadUInt16();

                if (reserved != 0 || type != 1 || count == 0)
                    throw new InvalidDataException("The ICO file has an invalid ICONDIR header.");

                List<IconEntry> entries = new List<IconEntry>(count);
                ushort i;
                for (i = 0; i < count; i++)
                {
                    IconEntry entry = new IconEntry();
                    entry.Width = reader.ReadByte();
                    entry.Height = reader.ReadByte();
                    entry.ColorCount = reader.ReadByte();
                    entry.Reserved = reader.ReadByte();
                    entry.Planes = reader.ReadUInt16();
                    entry.BitCount = reader.ReadUInt16();
                    entry.BytesInRes = reader.ReadUInt32();
                    entry.ImageOffset = reader.ReadUInt32();
                    entry.ResourceId = checked((ushort)(i + 1));
                    entries.Add(entry);
                }

                foreach (IconEntry entry in entries)
                {
                    ulong end = (ulong)entry.ImageOffset + (ulong)entry.BytesInRes;
                    if (end > (ulong)bytes.Length)
                        throw new InvalidDataException("The ICO file contains an out-of-range image entry.");

                    entry.ImageData = new byte[checked((int)entry.BytesInRes)];
                    Buffer.BlockCopy(
                        bytes,
                        checked((int)entry.ImageOffset),
                        entry.ImageData,
                        0,
                        checked((int)entry.BytesInRes));
                }

                return entries;
            }
            finally
            {
                reader.Dispose();
            }
        }
        finally
        {
            stream.Dispose();
        }
    }

    static byte[] BuildGroupIcon(List<IconEntry> entries)
    {
        MemoryStream stream = new MemoryStream();
        try
        {
            BinaryWriter writer = new BinaryWriter(stream);
            try
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write(checked((ushort)entries.Count));

                foreach (IconEntry entry in entries)
                {
                    writer.Write(entry.Width);
                    writer.Write(entry.Height);
                    writer.Write(entry.ColorCount);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.Planes);
                    writer.Write(entry.BitCount);
                    writer.Write(entry.BytesInRes);
                    writer.Write(entry.ResourceId);
                }

                writer.Flush();
                return stream.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }
        finally
        {
            stream.Dispose();
        }
    }

    static void ThrowLastWin32(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(
            operation + " failed with Win32 error " + error + ".");
    }
}
'@

Add-Type -TypeDefinition $source -Language CSharp

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$resolvedIcon = (Resolve-Path -LiteralPath $IconPath).Path

# The runtime ICO is accepted by Win32 LoadImage even though System.Drawing
# rejects its original directory metadata. Ask Win32 to decode the icon first,
# then serialize that native HICON back into a conventional single-image ICO.
Add-Type -AssemblyName System.Drawing

$nativeLoaderSource = @'
using System;
using System.Runtime.InteropServices;

public static class WorkshopNativeIconLoader
{
    const uint IMAGE_ICON = 1;
    const uint LR_LOADFROMFILE = 0x10;

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadImageW(
        IntPtr hInstance,
        string name,
        uint type,
        int width,
        int height,
        uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr hIcon);

    public static IntPtr LoadIcon(string path, int size)
    {
        return LoadImageW(IntPtr.Zero, path, IMAGE_ICON, size, size, LR_LOADFROMFILE);
    }

    public static void ReleaseIcon(IntPtr icon)
    {
        if (icon != IntPtr.Zero)
            DestroyIcon(icon);
    }
}
'@

if (-not ("WorkshopNativeIconLoader" -as [type])) {
    Add-Type -TypeDefinition $nativeLoaderSource -Language CSharp
}

$normalizedIconPath = Join-Path ([System.IO.Path]::GetTempPath()) "WinUIForge-Workshop-normalized.ico"
$nativeIcon = [WorkshopNativeIconLoader]::LoadIcon($resolvedIcon, 256)
if ($nativeIcon -eq [IntPtr]::Zero) {
    throw "Win32 could not decode Workshop.ico. Error: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}

try {
    $borrowedIcon = [System.Drawing.Icon]::FromHandle($nativeIcon)
    $iconObject = $borrowedIcon.Clone()
    try {
        $stream = [System.IO.File]::Create($normalizedIconPath)
        try {
            $iconObject.Save($stream)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $iconObject.Dispose()
    }
}
finally {
    [WorkshopNativeIconLoader]::ReleaseIcon($nativeIcon)
}

try {
    [WorkshopExeIconPatcher]::Patch($resolvedExe, $normalizedIconPath)
}
finally {
    Remove-Item -LiteralPath $normalizedIconPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Stamped Workshop icon resources into:" -ForegroundColor Green
Write-Host "  $resolvedExe"
