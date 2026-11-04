using Microsoft.Data.Sqlite;

namespace Arkive.Core.Databases;

/// <summary>
/// Picks the table of the game text database (EFTable_GameMsg.db) that holds a language's lines. Western clients
/// have a table per language (GameMsg_English, GameMsg_Spanish...); single-language clients, such as the Korean one,
/// have one table named just GameMsg with the same KEY and MSG columns.
/// </summary>
internal static class GameMessageTable
{
    internal const string Prefix = "GameMsg_";
    internal const string SingleLanguage = "GameMsg";

    /// <summary>
    /// GameMsg_<paramref name="language"/> when the database has it, else GameMsg when that is the only text
    /// table, else GameMsg_<paramref name="language"/> so the caller's missing-table error names what was asked for.
    /// </summary>
    internal static string Resolve(SqliteConnection connection, string language)
    {
        var languageTable = Prefix + language;
        if (Exists(connection, languageTable)) return languageTable;
        return Exists(connection, SingleLanguage) ? SingleLanguage : languageTable;
    }

    private static bool Exists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_schema WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }
}
