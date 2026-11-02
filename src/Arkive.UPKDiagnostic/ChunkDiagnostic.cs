
using System.Text.Json;
using Arkive.Core.Archives;
using Arkive.Core.Packages;
using Arkive.UPKDiagnostic;

namespace Arkive.UPKDiagnostic;

public sealed class ChunkDiagnostic
{
    public static void Run(string upkPath)
    {
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

                // 3. 보호 제공자 없이 논리 스트림 읽기
                // 보호된 청크라면 Arkive가 접근을 거부해야 정상이다.
                using var logicalStream =
                    new LogicalPackageStream(
                        summary,
                        NoChunkProtectionProvider.Instance);

                logicalStream.Position = chunk.UncompressedOffset;

                var buffer = new byte[1];
                var bytesRead = logicalStream.Read(buffer, 0, 1);

                if (bytesRead == 1)
                {
                    report.Status = "READ_SUCCESS";
                    report.Message =
                        "첫 번째 청크에서 1바이트를 읽었습니다.";
                }
                else
                {
                    report.Status = "EMPTY_READ";
                    report.Message =
                        "청크에서 데이터를 읽지 못했습니다.";
                }
            }
        }
        catch (PackageAccessException ex)
        {
            report.Status = "PROTECTED_CHUNK";
            report.Message = ex.Message;
        }
        catch (Exception ex)
        {
            report.Status = "ERROR";
            report.Message = ex.Message;
            report.ExceptionType = ex.GetType().FullName;
        }

        report.CompletedAt = DateTimeOffset.Now;

        // 4. 원본 UPK와 별도 위치에 JSON 저장
        var outputPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(upkPath))!,
            Path.GetFileNameWithoutExtension(upkPath)
                + ".chunk-diagnostic.json");

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
