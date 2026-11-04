
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const uint UnrealPackageTag = 0x9E2A83C1;
    private const int MaxTableEntries = 10_000_000;
    private const int MaxChunks = 1_000_000;
    private const int MaxStringLength = 1_048_576;

    private static int Main(string[] args)
    {
        Console.WriteLine("==================================");
        Console.WriteLine("       UPK Diagnostic Lab v0.1");
        Console.WriteLine("==================================");

        // LPK 프로브 모드: UPKDiagnostic --lpk <lpk 경로>... [--out <보고서 폴더>]
        // 키가 필요 없는 범위(인덱스 읽기 + 압축 엔트리 해제)만 시험한다.
        if (args.Contains("--lpk", StringComparer.OrdinalIgnoreCase))
        {
            var outIndex = Array.FindIndex(args, a => a.Equals("--out", StringComparison.OrdinalIgnoreCase));
            var outDir = outIndex >= 0 && outIndex + 1 < args.Length
                ? args[outIndex + 1]
                : Path.Combine(Environment.CurrentDirectory, "work", "unpack", "lpk");
            var lpkPaths = args.Where((a, i) =>
                !a.StartsWith("--", StringComparison.Ordinal) &&
                (outIndex < 0 || i != outIndex + 1)).ToList();
            var lpkKeyIndex = Array.FindIndex(args, a => a.Equals("--key-file", StringComparison.OrdinalIgnoreCase));
            if (lpkKeyIndex >= 0 && lpkKeyIndex + 1 < args.Length)
            {
                Console.WriteLine("키 프로파일: " + Arkive.Core.Archives.ArchiveKeyProfile.ActivateFromFile(args[lpkKeyIndex + 1]));
                lpkPaths.Remove(args[lpkKeyIndex + 1]);
            }

            var worst = 0;
            foreach (var lpk in lpkPaths)
            {
                if (!File.Exists(lpk))
                {
                    Console.WriteLine($"파일을 찾을 수 없습니다: {lpk}");
                    worst = Math.Max(worst, 2);
                    continue;
                }

                worst = Math.Max(worst, Arkive.UPKDiagnostic.LpkProbe.Run(
                    lpk, outDir, args.Contains("--all-db", StringComparer.OrdinalIgnoreCase)));
            }

            return worst;
        }

        // 전 청크 검증 모드: UPKDiagnostic --all --key-file <json> <upk 경로>...
        if (args.Contains("--all", StringComparer.OrdinalIgnoreCase))
        {
            var kfIndex = Array.FindIndex(args, a => a.Equals("--key-file", StringComparison.OrdinalIgnoreCase));
            if (kfIndex < 0 || kfIndex + 1 >= args.Length)
            {
                Console.WriteLine("--all 에는 --key-file <json> 이 필요합니다.");
                return 2;
            }

            Arkive.Core.Archives.ArchiveKeyProfile.ActivateFromFile(args[kfIndex + 1]);
            var allProvider = Arkive.Core.Archives.ChunkProtectionProviderFactory.CreateDefault();
            var worstAll = 0;
            foreach (var upk in args.Where((a, i) => !a.StartsWith("--", StringComparison.Ordinal) && i != kfIndex + 1))
                worstAll = Math.Max(worstAll, Arkive.UPKDiagnostic.AllChunksVerifier.Run(upk, allProvider));
            return worstAll;
        }

        // 사용법: UPKDiagnostic [--western] <upk 경로>
        // --western : 서구판 보호 프로파일로 첫 청크 해제를 시도한다 (한국판 호환성 검증용).
        // --key-file <json> : 사용자가 로컬에 둔 키 파일(예: work/unpack/keys.local.json)로 보호 제공자를 만든다.
        //   { "upkChunkKeyAscii": "<32자>" }  또는  { "upkChunkKeyHex": "<64 hex>" }
        bool useWestern = args.Contains("--western", StringComparer.OrdinalIgnoreCase);
        int keyFileIndex = Array.FindIndex(args, a => a.Equals("--key-file", StringComparison.OrdinalIgnoreCase));
        string? keyFile = keyFileIndex >= 0 && keyFileIndex + 1 < args.Length ? args[keyFileIndex + 1] : null;
        string path = args.Where((a, i) =>
                !a.StartsWith("--", StringComparison.Ordinal) &&
                (keyFileIndex < 0 || i != keyFileIndex + 1))
            .FirstOrDefault() ?? "";

        while (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Console.Write("UPK 파일 경로를 입력하세요: ");
            path = Console.ReadLine()?.Trim().Trim('"') ?? "";

            if (!File.Exists(path))
                Console.WriteLine("파일을 찾을 수 없습니다.\n");
        }

        try
        {
            var report = Analyze(path);

            Console.WriteLine("\n========== 분석 결과 ==========");
            Console.WriteLine($"파일: {report.FileName}");
            Console.WriteLine($"크기: {report.FileSize:N0} bytes");
            Console.WriteLine($"태그: 0x{report.Tag:X8}");
            Console.WriteLine($"버전: {report.FileVersion}/{report.LicenseeVersion}");
            Console.WriteLine($"압축 플래그: 0x{report.CompressionFlags:X8}");
            Console.WriteLine($"헤더 크기: {report.HeaderSize}");
            Console.WriteLine($"Name: {report.NameCount} / 0x{report.NameOffset:X}");
            Console.WriteLine($"Export: {report.ExportCount} / 0x{report.ExportOffset:X}");
            Console.WriteLine($"Import: {report.ImportCount} / 0x{report.ImportOffset:X}");
            Console.WriteLine($"압축 청크: {report.Chunks.Count}");

            foreach (var chunk in report.Chunks)
            {
                Console.WriteLine(
                    $"  Chunk: 압축 {chunk.CompressedSize:N0} bytes, " +
                    $"해제 {chunk.UncompressedSize:N0} bytes, " +
                    $"플래그 0x{chunk.Flags:X8}, " +
                    $"검증 {(chunk.Valid ? "정상" : "오류")}");
            }

            Console.WriteLine("\n========== 진단 ==========");
            foreach (var issue in report.Issues)
                Console.WriteLine($"[{issue.Level}] {issue.Message}");

            string output = Path.ChangeExtension(path, ".diagnostic.json");

            File.WriteAllText(
                output,
                JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                }));

            Console.WriteLine($"\n보고서 저장: {output}");

            bool headerOk = !report.Issues.Any(x => x.Level == "ERROR");

            // 2단계: 헤더가 정상이고 압축 청크가 있으면 첫 청크를 실제로 읽어 본다.
            // (보호 제공자 없이 읽으므로, 보호된 청크는 PROTECTED_CHUNK로 보고된다.)
            if (headerOk && report.Chunks.Count > 0)
            {
                Console.WriteLine("\n========== 청크 읽기 진단 ==========");
                Arkive.UPKDiagnostic.ChunkDiagnostic.Run(
                    path,
                    useWestern
                        ? Arkive.Core.Archives.ChunkProtectionProviderFactory.CreateWesternGlobal()
                        : keyFile is not null ? ActivateAndGetProvider(keyFile) : null);
            }

            Console.WriteLine("원본 UPK 파일은 수정하지 않았습니다.");

            return headerOk ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n분석 실패: {ex.Message}");
            return 2;
        }
    }

    private static Arkive.Core.Archives.IChunkProtectionProvider ActivateAndGetProvider(string keyFile)
    {
        Arkive.Core.Archives.ArchiveKeyProfile.ActivateFromFile(keyFile);
        return Arkive.Core.Archives.ChunkProtectionProviderFactory.CreateDefault();
    }

    private static PackageReport Analyze(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);

        using var reader = new BinaryReader(
            stream, Encoding.UTF8, leaveOpen: true);

        var report = new PackageReport
        {
            FileName = Path.GetFileName(path),
            FilePath = Path.GetFullPath(path),
            FileSize = stream.Length
        };

        uint tag = reader.ReadUInt32();
        report.Tag = tag;

        if (tag != UnrealPackageTag)
            throw new InvalidDataException(
                $"UPK 태그 불일치: 0x{tag:X8}");

        report.FileVersion = reader.ReadUInt16();
        report.LicenseeVersion = reader.ReadUInt16();
        report.HeaderSize = reader.ReadInt32();
        report.FolderName = ReadUnrealString(reader);
        report.PackageFlags = reader.ReadUInt32();

        report.NameCount = ReadCount(reader, "Name");
        report.NameOffset = reader.ReadInt32();

        report.ExportCount = ReadCount(reader, "Export");
        report.ExportOffset = reader.ReadInt32();

        report.ImportCount = ReadCount(reader, "Import");
        report.ImportOffset = reader.ReadInt32();

        report.DependsOffset = reader.ReadInt32();
        report.ImportExportGuidsOffset = reader.ReadInt32();

        report.ImportGuidsCount = ReadCount(reader, "Import GUID");
        report.ExportGuidsCount = ReadCount(reader, "Export GUID");

        report.ThumbnailTableOffset = reader.ReadInt32();

        byte[] guid = reader.ReadBytes(16);
        if (guid.Length != 16)
            throw new EndOfStreamException("GUID 데이터가 잘렸습니다.");

        report.PackageGuid = new Guid(guid).ToString();

        int generationCount = ReadBoundedCount(
            reader, "Generation", 100_000);

        for (int i = 0; i < generationCount; i++)
        {
            report.Generations.Add(new GenerationInfo
            {
                ExportCount = reader.ReadInt32(),
                NameCount = reader.ReadInt32(),
                NetObjectCount = reader.ReadInt32()
            });
        }

        report.EngineVersion = reader.ReadInt32();
        report.CookerVersion = reader.ReadInt32();

        report.CompressionFlagsOffset = stream.Position;
        report.CompressionFlags = reader.ReadUInt32();

        report.ChunkCountOffset = stream.Position;

        int chunkCount = ReadBoundedCount(
            reader, "Compressed Chunk", MaxChunks);

        report.ChunkTableOffset = stream.Position;

        for (int i = 0; i < chunkCount; i++)
        {
            var chunk = new ChunkInfo
            {
                UncompressedOffset = reader.ReadUInt32(),
                UncompressedSize = reader.ReadUInt32(),
                CompressedOffset = reader.ReadUInt32(),
                CompressedSize = reader.ReadUInt32(),
                Flags = reader.ReadUInt32()
            };

            ValidateChunk(chunk, stream.Length, report);
            report.Chunks.Add(chunk);
        }

        report.PackageSourceOffset = stream.Position;
        report.PackageSource = reader.ReadUInt32();
        report.ParsedSummarySize = stream.Position;

        ValidateOffset("HeaderSize", report.HeaderSize,
            stream.Length, report);

        ValidateTable("Name", report.NameCount,
            report.NameOffset, stream.Length, report);

        ValidateTable("Export", report.ExportCount,
            report.ExportOffset, stream.Length, report);

        ValidateTable("Import", report.ImportCount,
            report.ImportOffset, stream.Length, report);

        report.Profile =
            report.FileVersion == 868 &&
            report.LicenseeVersion == 16 &&
            report.CompressionFlags == 0x44
                ? "Western-compatible 868/16 (candidate)"
                : "Unknown";

        report.HeaderParsed = true;

        report.Issues.Add(new IssueInfo
        {
            Level = "INFO",
            Message = "헤더 및 청크 메타데이터 분석 완료. 실제 압축 해제는 수행하지 않았습니다."
        });

        return report;
    }

    private static string ReadUnrealString(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        if (count == 0)
            return "";

        if (count == int.MinValue ||
            Math.Abs(count) > MaxStringLength)
            throw new InvalidDataException(
                $"비정상 문자열 길이: {count}");

        if (count > 0)
        {
            byte[] bytes = reader.ReadBytes(count);

            if (bytes.Length != count)
                throw new EndOfStreamException();

            int length = bytes[^1] == 0
                ? bytes.Length - 1 : bytes.Length;

            return Encoding.Latin1.GetString(bytes, 0, length);
        }

        int utf16Count = -count;
        int byteCount = checked(utf16Count * 2);

        byte[] utf16 = reader.ReadBytes(byteCount);

        if (utf16.Length != byteCount)
            throw new EndOfStreamException();

        int contentLength =
            utf16.Length >= 2 &&
            utf16[^1] == 0 && utf16[^2] == 0
                ? utf16.Length - 2 : utf16.Length;

        return Encoding.Unicode.GetString(
            utf16, 0, contentLength);
    }

    private static int ReadCount(BinaryReader reader, string name)
    {
        return ReadBoundedCount(reader, name, MaxTableEntries);
    }

    private static int ReadBoundedCount(
        BinaryReader reader, string name, int max)
    {
        int value = reader.ReadInt32();

        if (value < 0 || value > max)
            throw new InvalidDataException(
                $"{name} 개수가 비정상적입니다: {value}");

        return value;
    }

    private static void ValidateOffset(
        string name, long offset, long fileSize,
        PackageReport report)
    {
        if (offset < 0 || offset > fileSize)
        {
            report.Issues.Add(new IssueInfo
            {
                Level = "ERROR",
                Message = $"{name} 범위 오류: {offset}"
            });
        }
    }

    private static void ValidateTable(
        string name, int count, int offset,
        long fileSize, PackageReport report)
    {
        if (count == 0 && offset == 0)
            return;

        ValidateOffset(name + " Offset", offset,
            fileSize, report);
    }

    private static void ValidateChunk(
        ChunkInfo chunk, long fileSize,
        PackageReport report)
    {
        ulong compressedEnd =
            (ulong)chunk.CompressedOffset + chunk.CompressedSize;

        ulong uncompressedEnd =
            (ulong)chunk.UncompressedOffset + chunk.UncompressedSize;

        bool valid =
            chunk.CompressedSize > 0 &&
            chunk.UncompressedSize > 0 &&
            compressedEnd <= (ulong)fileSize;

        chunk.Valid = valid;

        if (!valid)
        {
            report.Issues.Add(new IssueInfo
            {
                Level = "ERROR",
                Message =
                    $"압축 청크 범위 오류: " +
                    $"CompressedOffset=0x{chunk.CompressedOffset:X}, " +
                    $"CompressedSize={chunk.CompressedSize}, " +
                    $"UncompressedEnd={uncompressedEnd}"
            });
        }
    }
}

internal sealed class PackageReport
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public uint Tag { get; set; }
    public ushort FileVersion { get; set; }
    public ushort LicenseeVersion { get; set; }
    public int HeaderSize { get; set; }
    public string FolderName { get; set; } = "";
    public uint PackageFlags { get; set; }
    public int NameCount { get; set; }
    public int NameOffset { get; set; }
    public int ExportCount { get; set; }
    public int ExportOffset { get; set; }
    public int ImportCount { get; set; }
    public int ImportOffset { get; set; }
    public int DependsOffset { get; set; }
    public int ImportExportGuidsOffset { get; set; }
    public int ImportGuidsCount { get; set; }
    public int ExportGuidsCount { get; set; }
    public int ThumbnailTableOffset { get; set; }
    public string PackageGuid { get; set; } = "";
    public List<GenerationInfo> Generations { get; set; } = new();
    public int EngineVersion { get; set; }
    public int CookerVersion { get; set; }
    public uint CompressionFlags { get; set; }
    public long CompressionFlagsOffset { get; set; }
    public long ChunkCountOffset { get; set; }
    public long ChunkTableOffset { get; set; }
    public List<ChunkInfo> Chunks { get; set; } = new();
    public long PackageSourceOffset { get; set; }
    public uint PackageSource { get; set; }
    public long ParsedSummarySize { get; set; }
    public string Profile { get; set; } = "Unknown";
    public bool HeaderParsed { get; set; }
    public List<IssueInfo> Issues { get; set; } = new();
}

internal sealed class GenerationInfo
{
    public int ExportCount { get; set; }
    public int NameCount { get; set; }
    public int NetObjectCount { get; set; }
}

internal sealed class ChunkInfo
{
    public uint UncompressedOffset { get; set; }
    public uint UncompressedSize { get; set; }
    public uint CompressedOffset { get; set; }
    public uint CompressedSize { get; set; }
    public uint Flags { get; set; }
    public bool Valid { get; set; }
}

internal sealed class IssueInfo
{
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
}
