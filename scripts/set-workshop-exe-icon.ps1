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

$source = @"
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

    static IntPtr MakeIntResource(ushort value) => (IntPtr)value;

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
        public byte[] ImageData = Array.Empty<byte>();
    }

    public static void Patch(string exePath, string iconPath)
    {
        var iconBytes = File.ReadAllBytes(iconPath);
        var entries = ParseIcon(iconBytes);

        IntPtr update = BeginUpdateResourceW(exePath, false);
        if (update == IntPtr.Zero)
            ThrowLastWin32("BeginUpdateResourceW");

        bool completed = false;
        try
        {
            foreach (var entry in entries)
            {
                if (!UpdateResourceW(
                    update,
                    MakeIntResource(RT_ICON),
                    MakeIntResource(entry.ResourceId),
                    LANG_NEUTRAL,
                    entry.ImageData,
                    (uint)entry.ImageData.Length))
                {
                    ThrowLastWin32($"UpdateResourceW RT_ICON {entry.ResourceId}");
                }
            }

            var groupData = BuildGroupIcon(entries);

            // IDI_APPLICATION is what the .NET apphost itself looks for.
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

            // Also add resource ID 1. Explorer commonly selects the lowest
            // group-icon resource when displaying an executable.
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
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);

        ushort reserved = reader.ReadUInt16();
        ushort type = reader.ReadUInt16();
        ushort count = reader.ReadUInt16();

        if (reserved != 0 || type != 1 || count == 0)
            throw new InvalidDataException("The ICO file has an invalid ICONDIR header.");

        var entries = new List<IconEntry>(count);
        for (ushort i = 0; i < count; i++)
        {
            entries.Add(new IconEntry
            {
                Width = reader.ReadByte(),
                Height = reader.ReadByte(),
                ColorCount = reader.ReadByte(),
                Reserved = reader.ReadByte(),
                Planes = reader.ReadUInt16(),
                BitCount = reader.ReadUInt16(),
                BytesInRes = reader.ReadUInt32(),
                ImageOffset = reader.ReadUInt32(),
                ResourceId = checked((ushort)(i + 1))
            });
        }

        foreach (var entry in entries)
        {
            if (entry.ImageOffset + entry.BytesInRes > bytes.Length)
                throw new InvalidDataException("The ICO file contains an out-of-range image entry.");

            entry.ImageData = new byte[entry.BytesInRes];
            Buffer.BlockCopy(
                bytes,
                checked((int)entry.ImageOffset),
                entry.ImageData,
                0,
                checked((int)entry.BytesInRes));
        }

        return entries;
    }

    static byte[] BuildGroupIcon(List<IconEntry> entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // icon
        writer.Write(checked((ushort)entries.Count));

        foreach (var entry in entries)
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

    static void ThrowLastWin32(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException($"{operation} failed with Win32 error {error}.");
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$resolvedIcon = (Resolve-Path -LiteralPath $IconPath).Path

[WorkshopExeIconPatcher]::Patch($resolvedExe, $resolvedIcon)

Write-Host "Stamped Workshop icon resources into:" -ForegroundColor Green
Write-Host "  $resolvedExe"
