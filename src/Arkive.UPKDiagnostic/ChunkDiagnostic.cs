
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
