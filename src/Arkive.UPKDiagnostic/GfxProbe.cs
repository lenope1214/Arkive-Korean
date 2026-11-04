using System.Buffers.Binary;
using System.Text.Json;
using Arkive.Core.Archives;
using Arkive.Core.Assets;
using Arkive.Core.Discovery;
using Arkive.Core.Interface;
using Arkive.Core.Packages;

namespace Arkive.UPKDiagnostic;

/// <summary>
/// P2-5a: checks how retail Scaleform movies write PlaceObject3. The SWF spec reads a class-name string when
/// HasClassName (0x08 of the second flag byte) is set, or when HasImage (0x10) and HasCharacter are both set; the
/// fork's notes say the retail exporter writes the string only for 0x08. For every placement where the two rules
/// disagree, this reads the character id both ways and counts which one names a character the movie defines.
/// </summary>
public static class GfxProbe
{
    private const int PlaceObject3 = 70;
    private const int DefineSprite = 39;
    // Tags whose body starts with the id of the character they define.
    private static readonly HashSet<int> Defines =
        [2, 22, 32, 83, 39, 37, 6, 21, 35, 36, 20, 46, 84, 11, 33, 10, 48, 75, 7, 34, 60, 90, 1009];

    public static int Run(string packagesDirectory, int maxPackages, string outputDirectory)
    {
        var provider = ChunkProtectionProviderFactory.CreateDefault();
        var packages = new DirectoryInfo(packagesDirectory).EnumerateFiles("*.upk")
            .Select(file => LostArkPackageName.TryDecodeFileName(file.Name, out var name) ? (Name: name, file.FullName) : default)
            .Where(item => item.Name is not null && item.Name.StartsWith("EFUI_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxPackages)
            .ToList();
        Console.WriteLine($"EFUI 패키지 {packages.Count}개 검사");

        var report = new GfxProbeReport { Packages = packages.Count };
        foreach (var (name, path) in packages)
        {
            try
            {
                var catalog = new AssetCatalogReader(null, provider).Read(path);
                foreach (var export in catalog.Assets.Where(asset =>
                             asset.ClassName.Equals("EFSwfMovie", StringComparison.OrdinalIgnoreCase)))
                {
                    using var logical = new LogicalPackageStream(catalog.Summary, provider);
                    var bytes = new byte[export.SerialSize];
                    logical.Position = export.SerialOffset;
                    logical.ReadExactly(bytes);
                    if (GfxMovieFile.Locate(bytes) is null) continue;
                    var movie = GfxMovieFile.Inflate(GfxMovieFile.Extract(bytes));
                    var counts = Analyse(movie);
                    report.Movies++;
                    report.Disputed += counts.Disputed;
                    report.SpecValid += counts.SpecValid;
                    report.RetailValid += counts.RetailValid;
                    report.BothValid += counts.BothValid;
                    report.HasClassNamePlacements += counts.HasClassName;
                    if (counts.Disputed > 0 && report.Examples.Count < 20)
                        report.Examples.Add($"{name}.{export.ObjectName}: 쟁점 {counts.Disputed}, 명세 {counts.SpecValid}, 리테일 {counts.RetailValid}");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or PackageFormatException
                                                  or ArgumentException or EndOfStreamException)
            {
                report.Failures++;
                if (report.FailureExamples.Count < 10) report.FailureExamples.Add($"{name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, "gfx-placeobject3.json");
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"무비 {report.Movies:N0}개, HasClassName 배치 {report.HasClassNamePlacements:N0}, " +
                          $"규칙이 갈리는 배치 {report.Disputed:N0}: 명세식만 유효 {report.SpecValid:N0}, " +
                          $"리테일식만 유효 {report.RetailValid:N0}, 둘 다 {report.BothValid:N0}, 실패 패키지 {report.Failures}");
        foreach (var example in report.Examples.Take(8)) Console.WriteLine("  " + example);
        foreach (var failure in report.FailureExamples.Take(5)) Console.WriteLine("  실패 " + failure);
        Console.WriteLine("  보고서: " + output);
        return 0;
    }

    private sealed record Counts(int Disputed, int SpecValid, int RetailValid, int BothValid, int HasClassName);

    private static Counts Analyse(byte[] movie)
    {
        // Header: signature, version, file length, then the stage RECT (5-bit field size + 4 fields), rate, count.
        var bits = movie[8] >> 3;
        var start = 8 + (5 + 4 * bits + 7) / 8 + 4;
        var defined = new HashSet<int>();
        var disputed = new List<(int Spec, int Retail)>();
        var hasClassName = 0;
        Walk(movie, start, movie.Length, defined, disputed, ref hasClassName);
        int spec = 0, retail = 0, both = 0;
        foreach (var (specId, retailId) in disputed)
        {
            var s = defined.Contains(specId);
            var r = defined.Contains(retailId);
            if (s && r) both++;
            else if (s) spec++;
            else if (r) retail++;
        }
        return new(disputed.Count, spec, retail, both, hasClassName);
    }

    private static void Walk(byte[] data, int position, int end, HashSet<int> defined,
        List<(int, int)> disputed, ref int hasClassName)
    {
        while (position + 2 <= end)
        {
            var header = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
            position += 2;
            var code = header >> 6;
            var length = header & 0x3F;
            if (length == 0x3F)
            {
                if (position + 4 > end) return;
                length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
                position += 4;
            }
            if (code == 0 || length < 0 || position + length > end) return;
            if (Defines.Contains(code) && length >= 2) defined.Add(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position)));
            if (code == DefineSprite && length >= 4) Walk(data, position + 4, position + length, defined, disputed, ref hasClassName);
            if (code == PlaceObject3 && length >= 4)
            {
                var flags = data[position];
                var more = data[position + 1];
                var body = position + 4; // after both flag bytes and the depth
                if ((more & 0x08) != 0) hasClassName++;
                // The rules disagree only when HasImage and HasCharacter are set without HasClassName.
                if ((flags & 0x02) != 0 && (more & 0x10) != 0 && (more & 0x08) == 0 && body + 2 <= position + length)
                {
                    var retailId = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body));
                    var terminator = Array.IndexOf(data, (byte)0, body, position + length - body);
                    var specId = terminator >= 0 && terminator + 3 <= position + length
                        ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(terminator + 1)) : -1;
                    disputed.Add((specId, retailId));
                }
            }
            position += length;
        }
    }
}

public sealed class GfxProbeReport
{
    public int Packages { get; set; }
    public int Movies { get; set; }
    public int Failures { get; set; }
    public int HasClassNamePlacements { get; set; }
    public int Disputed { get; set; }
    public int SpecValid { get; set; }
    public int RetailValid { get; set; }
    public int BothValid { get; set; }
    public List<string> Examples { get; set; } = [];
    public List<string> FailureExamples { get; set; } = [];
}
