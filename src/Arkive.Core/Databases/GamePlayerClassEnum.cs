using System.Xml;
using Arkive.Core.Skills;

namespace Arkive.Core.Databases;

/// <summary>
/// The game's player class enum (playerclass in Common/StringData/EFGameMsg_Enums.xml, data1.lpk): class id (the PC
/// table's PrimaryKey, 101) to the enum name (warrior) that names its text key, tip.name.enum_playerclass_warrior.
/// The PC table's own Name ("Gunlancer", "Kimaster") does not always match that key, so this is how the game links
/// the two.
/// </summary>
internal static class GamePlayerClassEnum
{
    private const string EnumPath = "/Common/StringData/EFGameMsg_Enums.xml";
    private const string EnumType = "playerclass";

    /// <summary>
    /// The enum from the data1.lpk beside <paramref name="databaseArchivePath"/> (data2.lpk); empty when that archive
    /// or the file is missing or unreadable, so callers fall back to matching names.
    /// </summary>
    internal static IReadOnlyDictionary<long, string> ReadBeside(string databaseArchivePath, CancellationToken cancellation)
    {
        var archive = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databaseArchivePath))!, "data1.lpk");
        if (!File.Exists(archive)) return new Dictionary<long, string>();
        try
        {
            using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entry = LostArkDataArchive.ReadEntries(stream, cancellation).FirstOrDefault(item =>
                item.Path.Replace('\\', '/').EndsWith(EnumPath, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return new Dictionary<long, string>();
            using var xml = new MemoryStream(LostArkDataArchive.Read(stream, entry));
            return Parse(xml);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or XmlException or UnauthorizedAccessException)
        {
            return new Dictionary<long, string>();
        }
    }

    /// <summary>The playerclass nodes of an enum file: &lt;NODE Type="playerclass" Name="warrior" Index="101" /&gt;.</summary>
    internal static IReadOnlyDictionary<long, string> Parse(Stream xml)
    {
        var result = new Dictionary<long, string>();
        using var reader = XmlReader.Create(xml, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreWhitespace = true
        });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "NODE" ||
                reader.GetAttribute("Type") != EnumType) continue;
            if (reader.GetAttribute("Name") is { Length: > 0 } name &&
                long.TryParse(reader.GetAttribute("Index"), out var index) && index > 0)
                result.TryAdd(index, name);
        }
        return result;
    }
}
