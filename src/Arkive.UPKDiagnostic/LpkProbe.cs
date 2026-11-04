using System.Text.Json;
using Arkive.Core.Skills;

namespace Arkive.UPKDiagnostic;

/// <summary>
/// 키가 필요 없는 범위의 LPK 프로브(T5): 인덱스를 읽고, 압축 엔트리 몇 개를 실제로 해제해 본다.
/// 원본 LPK는 읽기 전용으로만 열고, 보고서는 항상 별도 출력 폴더에 쓴다.
/// </summary>
public static class LpkProbe
{
    private const int SampleReads = 3;
    private const int MaxSampleBytes = 8 * 1024 * 1024;

    public static int Run(string lpkPath, string outputDirectory)
    {
        var report = new LpkReport
        {
            FileName = Path.GetFileName(lpkPath),
            FileSize = new FileInfo(lpkPath).Length,
            StartedAt = DateTimeOffset.Now
        };

        try
        {
            using var stream = new FileStream(
                lpkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            IReadOnlyList<LostArkDataEntry> entries;
            try
            {
                entries = LostArkDataArchive.ReadEntries(stream);
            }
            catch (InvalidDataException ex)
            {
                // 인덱스 복호화가 틀리면 보통 엔트리 이름 길이 검증에서 여기로 온다.
                report.Status = "INDEX_INVALID";
                report.Message = ex.Message;
                return Finish(report, outputDirectory);
            }

            report.EntryCount = entries.Count;
            report.CompressedEntries = entries.Count(e => e.CompressedSize > 0);
            report.UncompressedEntries = entries.Count - report.CompressedEntries;
            report.TotalSize = entries.Sum(e => (long)e.Size);
            report.TopExtensions = entries
                .GroupBy(e => Path.GetExtension(e.Path).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => string.IsNullOrEmpty(g.Key) ? "(none)" : g.Key, g => g.Count());
            report.FirstNames = entries.Take(10).Select(e => e.Path).ToList();

            // 압축 엔트리 중 작은 것 몇 개를 실제로 해제해 본다.
            var candidates = entries
                .Where(e => e.CompressedSize > 0 && e.Size > 0 && e.Size <= MaxSampleBytes)
                .OrderBy(e => e.Size)
                .Take(SampleReads)
                .ToList();

            foreach (var entry in candidates)
            {
                var sample = new LpkSample { Path = entry.Path, Size = entry.Size };
                try
                {
                    var bytes = LostArkDataArchive.Read(stream, entry);
                    sample.Ok = bytes.Length == entry.Size;
                    sample.HeadHex = Convert.ToHexString(bytes.AsSpan(0, Math.Min(16, bytes.Length)));
                }
                catch (Exception ex)
                {
                    sample.Ok = false;
                    sample.Error = ex.GetType().Name + ": " + ex.Message;
                }

                report.Samples.Add(sample);
            }

            if (candidates.Count == 0)
            {
                report.Status = "INDEX_OK_NO_COMPRESSED_SAMPLE";
                report.Message = "인덱스는 읽혔지만 해제를 시험할 압축 엔트리가 없습니다.";
            }
            else if (report.Samples.All(s => s.Ok))
            {
                report.Status = "OK";
                report.Message = $"인덱스 {entries.Count:N0}개 읽기 성공, 샘플 {report.Samples.Count}개 해제 성공.";
            }
            else
            {
                report.Status = "INDEX_OK_DECODE_FAILED";
                report.Message = "인덱스는 읽혔지만 압축 엔트리 해제가 실패했습니다.";
            }
        }
        catch (Exception ex)
        {
            report.Status = "ERROR";
            report.Message = ex.Message;
            report.ExceptionType = ex.GetType().FullName;
        }

        return Finish(report, outputDirectory);
    }

    private static int Finish(LpkReport report, string outputDirectory)
    {
        report.CompletedAt = DateTimeOffset.Now;
        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, report.FileName + ".probe.json");
        File.WriteAllText(
            output,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"[{report.FileName}] {report.Status}: {report.Message}");
        if (report.EntryCount > 0)
        {
            Console.WriteLine(
                $"  엔트리 {report.EntryCount:N0} (압축 {report.CompressedEntries:N0} / 비압축 {report.UncompressedEntries:N0}), " +
                $"확장자 상위: {string.Join(", ", report.TopExtensions.Select(p => $"{p.Key}={p.Value}"))}");
        }

        foreach (var sample in report.Samples)
            Console.WriteLine($"  샘플 {(sample.Ok ? "OK " : "FAIL")} {sample.Path} ({sample.Size:N0} B) {sample.HeadHex}{sample.Error}");

        Console.WriteLine($"  보고서: {output}");
        return report.Status is "OK" or "INDEX_OK_NO_COMPRESSED_SAMPLE" ? 0 : 1;
    }
}

public sealed class LpkReport
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ExceptionType { get; set; }
    public int EntryCount { get; set; }
    public int CompressedEntries { get; set; }
    public int UncompressedEntries { get; set; }
    public long TotalSize { get; set; }
    public Dictionary<string, int> TopExtensions { get; set; } = new();
    public List<string> FirstNames { get; set; } = new();
    public List<LpkSample> Samples { get; set; } = new();
}

public sealed class LpkSample
{
    public string Path { get; set; } = "";
    public int Size { get; set; }
    public bool Ok { get; set; }
    public string? HeadHex { get; set; }
    public string? Error { get; set; }
}
