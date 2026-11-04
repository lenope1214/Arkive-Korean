namespace Arkive.Core.Archives;

/// <summary>
/// Optional, process-wide replacement for the data-archive (.lpk) keys. By default Arkive uses the Western/global
/// client's keys. A tool that works on another client region can supply that region's keys at startup, from local
/// configuration, without the region's constants being compiled into Arkive.
/// </summary>
public static class LostArkArchiveKeys
{
    private static volatile byte[]? _indexKey;
    private static volatile byte[]? _databaseBase;

    /// <summary>The Blowfish key of the .lpk index and compressed entries, or null for the default.</summary>
    internal static byte[]? IndexKeyOverride => _indexKey;

    /// <summary>The 16-byte base of the per-database AES key derivation, or null for the default.</summary>
    internal static byte[]? DatabaseBaseOverride => _databaseBase;

    /// <summary>Replaces the keys used by every archive operation until <see cref="Reset"/> is called.</summary>
    /// <param name="indexKey">The Blowfish key bytes (for the Western client, the 32 ASCII characters of its key text).</param>
    /// <param name="databaseBase">The 16-byte database key base.</param>
    public static void Use(ReadOnlySpan<byte> indexKey, ReadOnlySpan<byte> databaseBase)
    {
        if (indexKey.Length is < 4 or > 56)
            throw new ArgumentException("A Blowfish key is 4 to 56 bytes.", nameof(indexKey));
        if (databaseBase.Length != 16)
            throw new ArgumentException("The database key base is exactly 16 bytes.", nameof(databaseBase));
        _indexKey = indexKey.ToArray();
        _databaseBase = databaseBase.ToArray();
    }

    /// <summary>Goes back to the built-in Western/global keys.</summary>
    public static void Reset()
    {
        _indexKey = null;
        _databaseBase = null;
    }
}
