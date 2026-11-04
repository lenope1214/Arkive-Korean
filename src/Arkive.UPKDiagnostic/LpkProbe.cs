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

    public static int Run(string lpkPath, string outputDirectory, bool allDatabases = false,
        IReadOnlyCollection<string>? extraDatabases = null)
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

            // T6: 압축 크기 0 = 데이터베이스 엔트리(파일별 AES 키). 복호화해서 SQLite 헤더를 확인한다.
            var databases = entries
                .Where(e => e.CompressedSize == 0 && e.Path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                .ToList();
            report.DatabaseEntries = databases.Count;
            var databaseTargets = databases
                .OrderBy(e => e.Path.Contains("EFTable_GameMsg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(e => e.Size)
                .Take(2)
                .ToList();
            if (extraDatabases is { Count: > 0 })
                databaseTargets.AddRange(databases.Where(e =>
                    extraDatabases.Contains(Path.GetFileName(e.Path), StringComparer.OrdinalIgnoreCase) &&
                    !databaseTargets.Contains(e)));
            var databaseDirectory = Path.Combine(outputDirectory, "db");
            foreach (var entry in databaseTargets)
            {
                var sample = new LpkSample { Path = entry.Path, Size = entry.Size };
                try
                {
                    Directory.CreateDirectory(databaseDirectory);
                    var destination = Path.Combine(databaseDirectory, Path.GetFileName(entry.Path));
                    if (File.Exists(destination)) File.Delete(destination);
                    Arkive.Core.Databases.LostArkDatabaseArchive.Extract(
                        lpkPath, entry.Path, destination, CancellationToken.None);
                    using var head = File.OpenRead(destination);
                    var header = new byte[16];
                    head.ReadExactly(header);
                    sample.Ok = header.AsSpan().SequenceEqual("SQLite format 3\0"u8);
                    sample.HeadHex = Convert.ToHexString(header);
                    if (!sample.Ok) sample.Error = "SQLite 헤더가 아닙니다.";
                }
                catch (Exception ex)
                {
                    sample.Ok = false;
                    sample.Error = ex.GetType().Name + ": " + ex.Message;
                }

                report.DatabaseSamples.Add(sample);
            }

            // P2-2: 모든 DB 엔트리를 하나씩 임시 파일로 복호화해 SQLite 로 실제로 열어 본 뒤 지운다.
            if (allDatabases && databases.Count > 0)
            {
                var scratch = Path.Combine(outputDirectory, "db-all-scratch");
                Directory.CreateDirectory(scratch);
                var all = new LpkAllDatabases { Total = databases.Count };
                foreach (var entry in databases)
                {
                    var temp = Path.Combine(scratch, "current.db");
                    try
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                        Arkive.Core.Databases.LostArkDatabaseArchive.Extract(
                            lpkPath, entry.Path, temp, CancellationToken.None);
                        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                        {
                            DataSource = temp,
                            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                            Pooling = false
                        }.ToString();
                        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                        {
                            connection.Open();
                            using var command = connection.CreateCommand();
                            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table'";
                            var tables = Convert.ToInt32(command.ExecuteScalar());
                            all.Tables += tables;
                        }

                        all.Ok++;
                        all.Bytes += entry.Size;
                    }
                    catch (Exception ex)
                    {
                        all.Failed++;
                        if (all.Failures.Count < 30)
                            all.Failures.Add(entry.Path + " :: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
                    }
                }

                report.AllDatabases = all;
                Console.WriteLine(
                    $"  DB 전수 검증: {all.Ok:N0}/{all.Total:N0} 성공, 실패 {all.Failed:N0}, 테이블 {all.Tables:N0}개, {all.Bytes:N0} bytes");
                foreach (var failure in all.Failures.Take(10)) Console.WriteLine("    실패 " + failure);
                if (all.Failed > 0) report.DatabaseSamples.Add(new LpkSample { Path = "(전수 검증)", Ok = false, Error = $"{all.Failed}개 실패" });
            }

            if (candidates.Count == 0 && databaseTargets.Count == 0)
            {
                report.Status = "INDEX_OK_NO_COMPRESSED_SAMPLE";
                report.Message = "인덱스는 읽혔지만 해제를 시험할 압축 엔트리가 없습니다.";
            }
            else if (report.Samples.All(s => s.Ok) && report.DatabaseSamples.All(s => s.Ok))
            {
                report.Status = "OK";
                report.Message =
                    $"인덱스 {entries.Count:N0}개 읽기 성공, 압축 샘플 {report.Samples.Count}개 · DB 샘플 {report.DatabaseSamples.Count}개 해제 성공.";
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

    /// <summary>Writes every non-database entry whose path contains one of the filters to &lt;out&gt;/entries/.</summary>
    public static void ExtractEntries(string lpkPath, string outputDirectory, IReadOnlyCollection<string> filters)
    {
        using var stream = new FileStream(lpkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var root = Path.Combine(outputDirectory, "entries", Path.GetFileNameWithoutExtension(lpkPath));
        var written = 0;
        foreach (var entry in LostArkDataArchive.ReadEntries(stream).Where(entry =>
                     // Databases (stored size 0 and .db) need their own per-file key; every other entry reads as is.
                     !(entry.CompressedSize == 0 && entry.Path.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) &&
                     filters.Any(filter => entry.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))))
        {
            // Entry paths start with "/../" or "/"; keep only the safe relative part inside the output folder.
            var relative = string.Join(Path.DirectorySeparatorChar, entry.Path.Replace('\\', '/').Split('/')
                .Where(part => part.Length > 0 && part != "." && part != ".."));
            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, LostArkDataArchive.Read(stream, entry));
            written++;
        }
        Console.WriteLine($"[{Path.GetFileName(lpkPath)}] 추출 {written:N0}개 → {root}");
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
        if (report.DatabaseEntries > 0)
            Console.WriteLine($"  DB 엔트리 {report.DatabaseEntries:N0}개");
        foreach (var sample in report.DatabaseSamples)
            Console.WriteLine($"  DB {(sample.Ok ? "OK " : "FAIL")} {sample.Path} ({sample.Size:N0} B) {sample.Error}");

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
    public int DatabaseEntries { get; set; }
    public List<LpkSample> DatabaseSamples { get; set; } = new();
    public LpkAllDatabases? AllDatabases { get; set; }
}

public sealed class LpkAllDatabases
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Failed { get; set; }
    public int Tables { get; set; }
    public long Bytes { get; set; }
    public List<string> Failures { get; set; } = new();
}

public sealed class LpkSample
{
    public string Path { get; set; } = "";
    public int Size { get; set; }
    public bool Ok { get; set; }
    public string? HeadHex { get; set; }
    public string? Error { get; set; }
}
