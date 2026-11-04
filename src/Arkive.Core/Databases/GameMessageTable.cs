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
    internal static string Resolve(SqliteConnection connection, string language) => Resolve(connection, language, "main");

    /// <summary>The same, for a database attached to <paramref name="connection"/> as <paramref name="schema"/> (db1).</summary>
    internal static string Resolve(SqliteConnection connection, string language, string schema)
    {
        if (schema.Length == 0 || !schema.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("A schema name is letters and digits only.", nameof(schema));
        var languageTable = Prefix + language;
        if (Exists(connection, schema, languageTable)) return languageTable;
        return Exists(connection, schema, SingleLanguage) ? SingleLanguage : languageTable;
    }

    private static bool Exists(SqliteConnection connection, string schema, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT 1 FROM {schema}.sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }
}
