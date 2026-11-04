using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Arkive.Core.Archives;
using Arkive.Core.Assets;

namespace Arkive.Core.Interface;

/// <summary>One mouse cursor file as the game ships it, ready for Windows.</summary>
/// <param name="Cursor">The cursor's export name ("normal", "attack_blue").</param>
/// <param name="Preset">"default", or "preset1".."preset6" (the accessibility tab's cursor choices).</param>
/// <param name="Size">"normal" (48 px), "large", "full", "xlarge" or "xxlarge".</param>
/// <param name="Format">"ani" (animated) or "cur".</param>
/// <param name="HotspotX">The click point, when the file or the export records one.</param>
public sealed record GameCursorFile(string Cursor, string Preset, string Size, string Format, int Width, int Height,
    int? HotspotX, int? HotspotY, byte[] Data)
{
    public string FileName => $"{Cursor}.{Format}";
}

/// <summary>
/// Reads the game's mouse cursors from the interface cursor package (EFUI_CURSOR). Each cursor export keeps finished
/// Windows cursor files in byte-array properties named cursordata[_size][preset N]: an animated .ani (RIFF ACON), a
/// .cur, or an .ico whose click point the export stores separately ([size]hotspotx / [size]hotspoty), which is
/// written out as a .cur with that click point.
/// </summary>
public static partial class GameCursorReader
{
    public const string PackageName = "EFUI_CURSOR";
    private const int MaximumProperties = 512;

    /// <summary>Every cursor file in a cursor package.</summary>
    public static IReadOnlyList<GameCursorFile> ReadPackage(string packagePath, IChunkProtectionProvider? protectionProvider = null)
    {
        var provider = protectionProvider ?? ChunkProtectionProviderFactory.CreateDefault();
        var catalog = new AssetCatalogReader(null, provider).Read(packagePath);
        using var logical = new LogicalPackageStream(catalog.Summary, provider);
        var files = new List<GameCursorFile>();
        foreach (var export in catalog.Assets.Where(asset => asset.SerialSize > 0))
        {
            var bytes = new byte[export.SerialSize];
            logical.Position = export.SerialOffset;
            logical.ReadExactly(bytes);
            files.AddRange(ReadExport(export.ObjectName, bytes, catalog.Names));
        }
        return files;
    }

    /// <summary>The cursor files in one export's serialized data (none when it holds no cursor arrays).</summary>
    public static IReadOnlyList<GameCursorFile> ReadExport(string cursor, ReadOnlySpan<byte> export,
        IReadOnlyList<UnrealNameEntry> names)
    {
        var arrays = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var bytes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // The export starts with its net index, then UE3 tagged properties ending at "None".
        var cursorOffset = 4;
        for (var count = 0; count < MaximumProperties; count++)
        {
            if (!TryName(export, ref cursorOffset, names, out var name) ||
                name.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                !TryName(export, ref cursorOffset, names, out var type) ||
                cursorOffset + 8 > export.Length)
                break;
            var size = BinaryPrimitives.ReadInt32LittleEndian(export[cursorOffset..]);
            cursorOffset += 8; // size, array index
            if (type.Equals("StructProperty", StringComparison.OrdinalIgnoreCase) && !TryName(export, ref cursorOffset, names, out _)) break;
            if (type.Equals("BoolProperty", StringComparison.OrdinalIgnoreCase)) cursorOffset++;
            if (type.Equals("ByteProperty", StringComparison.OrdinalIgnoreCase) && !TryName(export, ref cursorOffset, names, out _)) break;
            if (size < 0 || cursorOffset > export.Length - size) break;
            var value = export.Slice(cursorOffset, size);
            if (type.Equals("ArrayProperty", StringComparison.OrdinalIgnoreCase) && size >= 4)
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(value);
                if (length > 0 && length <= size - 4) arrays[name] = value.Slice(4, length).ToArray();
            }
            else if (type.Equals("ByteProperty", StringComparison.OrdinalIgnoreCase) && size == 1) bytes[name] = value[0];
            else if (type.Equals("IntProperty", StringComparison.OrdinalIgnoreCase) && size == 4)
                bytes[name] = BinaryPrimitives.ReadInt32LittleEndian(value);
            cursorOffset += size;
        }

        var files = new List<GameCursorFile>();
        foreach (var (property, data) in arrays.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var slot = Slot().Match(property);
            if (!slot.Success) continue;
            var size = slot.Groups["size"].Success ? slot.Groups["size"].Value.ToLowerInvariant() : "normal";
            var preset = slot.Groups["preset"].Success ? "preset" + slot.Groups["preset"].Value : "default";
            var prefix = size == "normal" ? "" : size;
            int? authoredX = bytes.TryGetValue(prefix + "hotspotx", out var x) ? x : null;
            int? authoredY = bytes.TryGetValue(prefix + "hotspoty", out var y) ? y : null;
            if (data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("ACON"u8))
            {
                files.Add(new(cursor, preset, size, "ani", 0, 0, authoredX, authoredY, data));
                continue;
            }
            if (data.Length < 22 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0 ||
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4)) == 0)
                continue;
            var kind = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
            if (kind is not (1 or 2)) continue;
            var width = data[6] == 0 ? 256 : data[6];
            var height = data[7] == 0 ? 256 : data[7];
            if (kind == 1)
            {
                // An icon: the same image directory, re-tagged as a cursor that carries the export's click point.
                var fixedData = (byte[])data.Clone();
                BinaryPrimitives.WriteUInt16LittleEndian(fixedData.AsSpan(2), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(fixedData.AsSpan(10), (ushort)(authoredX ?? 0));
                BinaryPrimitives.WriteUInt16LittleEndian(fixedData.AsSpan(12), (ushort)(authoredY ?? 0));
                files.Add(new(cursor, preset, size, "cur", width, height, authoredX ?? 0, authoredY ?? 0, fixedData));
            }
            else
            {
                files.Add(new(cursor, preset, size, "cur", width, height,
                    BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10)), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(12)), data));
            }
        }
        return files;
    }

    private static bool TryName(ReadOnlySpan<byte> bytes, ref int offset, IReadOnlyList<UnrealNameEntry> names, out string value)
    {
        value = "";
        if (offset < 0 || offset > bytes.Length - 8) return false;
        var index = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
        var number = BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 4)..]);
        if (index < 0 || index >= names.Count || number < 0) return false;
        value = new UnrealNameReference(index, number).Resolve(names);
        offset += 8;
        return true;
    }

    // cursordata, cursordata_full, cursordata_largepreset3, cursordatapreset2 ...
    [GeneratedRegex("^cursordata(?:_(?<size>full|large|xlarge|xxlarge))?(?:_?preset(?<preset>[0-9]+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Slot();
}
