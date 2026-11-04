
using System.Text.Json;
using Arkive.Core.Archives;
using Arkive.Core.Packages;
using Arkive.UPKDiagnostic;

namespace Arkive.UPKDiagnostic;

public sealed class ChunkDiagnostic
{
    public static void Run(string upkPath, IChunkProtectionProvider? provider = null)
    {
        var providerLabel = provider is null ? "none" : provider.Name;
        var report = new DiagnosticReport
        {
            FileName = Path.GetFileName(upkPath),
            FilePath = Path.GetFullPath(upkPath),
            StartedAt = DateTimeOffset.Now
        };

        try
        {
            // 1. UPK 헤더 읽기
            var summary = new UpkPackageReader().Read(upkPath);

            report.FileVersion = summary.FileVersion;
            report.LicenseeVersion = summary.LicenseeVersion;
            report.CompressionFlags = summary.CompressionFlags;
            report.ChunkCount = summary.CompressedChunks.Count;

            // 2. 첫 번째 청크 메타데이터 확인
            if (summary.CompressedChunks.Count == 0)
            {
                report.Status = "NO_COMPRESSED_CHUNKS";
                report.Message = "압축 청크가 없습니다.";
            }
            else
            {
                var chunk = summary.CompressedChunks[0];

                report.FirstChunk = new ChunkInfo
                {
                    Index = 0,
                    UncompressedOffset = chunk.UncompressedOffset,
                    UncompressedSize = chunk.UncompressedSize,
                    CompressedOffset = chunk.CompressedOffset,
                    CompressedSize = chunk.CompressedSize,
                    Flags = chunk.LostArkFlags
                };

                // 3. 논리 스트림으로 첫 청크 전체를 읽는다.
                // 제공자가 없으면 보호된 청크는 PackageAccessException(정상 거부)이다.
                // 제공자가 있으면 복호화 -> LZ4 해제 -> 크기 검증까지 수행된다.
                using var logicalStream =
                    new LogicalPackageStream(
                        summary,
                        provider ?? NoChunkProtectionProvider.Instance);

                logicalStream.Position = chunk.UncompressedOffset;

                var buffer = new byte[chunk.UncompressedSize];
                var total = 0;
                while (total < buffer.Length)
                {
                    var read = logicalStream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                }

                if (total == buffer.Length)
                {
                    report.Status = "READ_SUCCESS";
                    report.Message =
                        $"첫 청크 {total:N0} bytes 해제 성공 (제공자: {providerLabel}).";
                    report.DecodedHeadHex = Convert.ToHexString(buffer.AsSpan(0, Math.Min(32, total)));
                }
                else
                {
                    report.Status = "SHORT_READ";
                    report.Message =
                        $"첫 청크에서 {total:N0}/{buffer.Length:N0} bytes만 읽었습니다.";
                }
            }
        }
        catch (PackageAccessException ex)
        {
            report.Status = "PROTECTED_CHUNK";
            report.Message = ex.Message;
        }
        catch (PackageFormatException ex)
        {
            // 복호화 키/알고리즘이 맞지 않으면 보통 LZ4 단계에서 여기로 떨어진다.
            report.Status = provider is null ? "FORMAT_ERROR" : "DECODE_FAILED";
            report.Message = ex.Message +
                (ex.InnerException is null ? "" : " | inner: " + ex.InnerException.Message);
            report.ExceptionType = ex.GetType().FullName;
        }
        catch (Exception ex)
        {
            report.Status = "ERROR";
            report.Message = ex.Message;
            report.ExceptionType = ex.GetType().FullName;
        }

        report.Provider = providerLabel;
        report.CompletedAt = DateTimeOffset.Now;

        // 4. 원본 UPK와 별도 위치에 JSON 저장
        var outputPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(upkPath))!,
            Path.GetFileNameWithoutExtension(upkPath)
                + (provider is null ? ".chunk-diagnostic.json" : ".chunk-diagnostic.provider.json"));

        File.WriteAllText(
            outputPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        Console.WriteLine();
        Console.WriteLine($"진단 결과: {report.Status}");
        Console.WriteLine(report.Message);
        Console.WriteLine($"보고서: {outputPath}");
    }
}

/// <summary>T2: 모든 청크를 순회하며 해제 크기와 오류를 검증한다.</summary>
public static class AllChunksVerifier
{
    public static int Run(string upkPath, IChunkProtectionProvider provider)
    {
        var summary = new UpkPackageReader().Read(upkPath);
        var result = new AllChunksReport
        {
            FileName = Path.GetFileName(upkPath),
            ChunkCount = summary.CompressedChunks.Count,
            Provider = provider.Name
        };

        using var stream = new LogicalPackageStream(summary, provider);
        var started = DateTimeOffset.Now;

        for (var index = 0; index < summary.CompressedChunks.Count; index++)
        {
            var chunk = summary.CompressedChunks[index];
            try
            {
                stream.Position = chunk.UncompressedOffset;
                var buffer = new byte[chunk.UncompressedSize];
                var total = 0;
                while (total < buffer.Length)
                {
                    var read = stream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                }

                if (total == buffer.Length)
                {
                    result.Ok++;
                    result.DecodedBytes += total;
                }
                else
                {
                    result.Failed++;
                    result.AddError(index, $"short read {total}/{buffer.Length}");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.AddError(index, ex.GetType().Name + ": " + ex.Message);
            }
        }

        result.Seconds = (DateTimeOffset.Now - started).TotalSeconds;
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "work", "unpack", "verify");
        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, result.FileName + ".all-chunks.json");
        File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"[{result.FileName}] 청크 {result.ChunkCount:N0}개: 성공 {result.Ok:N0} / 실패 {result.Failed:N0}, " +
            $"해제 {result.DecodedBytes:N0} bytes, {result.Seconds:F1}s");
        foreach (var error in result.FirstErrors)
            Console.WriteLine("  오류 " + error);
        Console.WriteLine("  보고서: " + output);

        // T4: 해제한 논리 스트림에서 Name/Import/Export 테이블을 실제로 파싱해 본다.
        if (result.Failed == 0)
        {
            try
            {
                var catalog = new Arkive.Core.Assets.AssetCatalogReader(null, provider).Read(upkPath);
                var countsMatch =
                    catalog.Names.Count == summary.NameCount &&
                    catalog.Imports.Count == summary.ImportCount &&
                    catalog.Exports.Count == summary.ExportCount;
                Console.WriteLine(
                    $"  카탈로그: Name {catalog.Names.Count:N0}/{summary.NameCount:N0}, " +
                    $"Import {catalog.Imports.Count:N0}/{summary.ImportCount:N0}, " +
                    $"Export {catalog.Exports.Count:N0}/{summary.ExportCount:N0}, " +
                    $"에셋 {catalog.Assets.Count:N0} -> {(countsMatch ? "개수 일치" : "개수 불일치")}");
                foreach (var asset in catalog.Assets.Take(5))
                    Console.WriteLine($"    {asset.Kind,-14} {asset.ClassName,-24} {asset.ObjectPath}");
                result.CatalogOk = countsMatch;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  카탈로그 실패: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        return result.Failed == 0 && result.CatalogOk ? 0 : 1;
    }
}

public sealed class AllChunksReport
{
    public string FileName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int ChunkCount { get; set; }
    public int Ok { get; set; }
    public int Failed { get; set; }
    public long DecodedBytes { get; set; }
    public double Seconds { get; set; }
    public bool CatalogOk { get; set; }
    public List<string> FirstErrors { get; set; } = new();

    public void AddError(int index, string message)
    {
        if (FirstErrors.Count < 20) FirstErrors.Add($"chunk {index}: {message}");
    }
}

public sealed class DiagnosticReport
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }

    public int FileVersion { get; set; }
    public int LicenseeVersion { get; set; }
    public uint CompressionFlags { get; set; }
    public int ChunkCount { get; set; }

    public ChunkInfo? FirstChunk { get; set; }

    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ExceptionType { get; set; }
    public string Provider { get; set; } = "none";
    public string? DecodedHeadHex { get; set; }
}

public sealed class ChunkInfo
{
    public int Index { get; set; }
    public uint UncompressedOffset { get; set; }
    public uint UncompressedSize { get; set; }
    public uint CompressedOffset { get; set; }
    public uint CompressedSize { get; set; }
    public uint Flags { get; set; }
}
