using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Arkive.Core.Archives;

/// <summary>
/// Lets a user point Arkive at the keys of another client region (for example a Korean client) without those keys
/// being compiled into Arkive. The keys live in a local JSON file; <see cref="EnvironmentVariable"/> names that file.
/// With no profile active every operation keeps using the Western/global keys.
/// </summary>
/// <remarks>
/// File format (each half is optional, key values are never written to logs or error messages):
/// <code>
/// {
///   "upkChunkKeyAscii": "...32 characters..." | "upkChunkKeyHex": "...64 hex...",
///   "lpkIndexKeyAscii": "..." | "lpkIndexKeyHex": "...",   // Blowfish key of the .lpk index and entries
///   "lpkDbBaseKeyHex": "...32 hex..."                       // required together with the index key
/// }
/// </code>
/// </remarks>
public static class ArchiveKeyProfile
{
    public const string EnvironmentVariable = "ARKIVE_KEY_PROFILE";

    private static volatile IChunkProtectionProvider? _chunkProvider;
    private static volatile string? _name;

    /// <summary>The active profile's display name (the file name), or null when the built-in keys are in use.</summary>
    public static string? ActiveName => _name;

    /// <summary>The package chunk protection provider of the active profile, or null if it supplies none.</summary>
    internal static IChunkProtectionProvider? ActiveChunkProvider => _chunkProvider;

    /// <summary>Activates the profile named by <see cref="EnvironmentVariable"/>. Returns its name, or null if the variable is unset.</summary>
    public static string? ActivateFromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return string.IsNullOrWhiteSpace(path) ? null : ActivateFromFile(path.Trim().Trim('"'));
    }

    /// <summary>Reads the key file and makes its keys the ones every archive operation uses. Returns the profile name.</summary>
    /// <exception cref="InvalidDataException">The file is missing, is not valid JSON, or holds no usable key.</exception>
    public static string ActivateFromFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"The key profile file does not exist: {path}");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            throw new InvalidDataException($"The key profile file is not valid JSON: {Path.GetFileName(path)}");
        }

        var name = "User-supplied profile (" + Path.GetFileName(path) + ")";
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The key profile file must hold a JSON object.");

            var chunkKey = ReadKey(root, "upkChunkKeyHex", "upkChunkKeyAscii");
            var indexKey = ReadKey(root, "lpkIndexKeyHex", "lpkIndexKeyAscii");
            var databaseBase = ReadKey(root, "lpkDbBaseKeyHex", null);
            try
            {
                if (chunkKey is null && indexKey is null)
                    throw new InvalidDataException("The key profile file holds neither upkChunkKey* nor lpkIndexKey*.");
                if (indexKey is not null && databaseBase is null)
                    throw new InvalidDataException("lpkIndexKey* needs lpkDbBaseKeyHex as well.");

                IChunkProtectionProvider? provider = null;
                if (chunkKey is not null)
                {
                    if (chunkKey.Length != AesEcbPrefixProtectionProvider.RequiredKeyBytes)
                        throw new InvalidDataException(
                            $"upkChunkKey* must be {AesEcbPrefixProtectionProvider.RequiredKeyBytes} bytes.");
                    provider = new AesEcbPrefixProtectionProvider(chunkKey, name);
                }

                if (indexKey is not null) LostArkArchiveKeys.Use(indexKey, databaseBase!);
                else LostArkArchiveKeys.Reset();

                _chunkProvider = provider;
                _name = name;
                return name;
            }
            finally
            {
                if (chunkKey is not null) CryptographicOperations.ZeroMemory(chunkKey);
                if (indexKey is not null) CryptographicOperations.ZeroMemory(indexKey);
                if (databaseBase is not null) CryptographicOperations.ZeroMemory(databaseBase);
            }
        }
    }

    /// <summary>Goes back to the built-in Western/global keys.</summary>
    public static void Deactivate()
    {
        _chunkProvider = null;
        _name = null;
        LostArkArchiveKeys.Reset();
    }

    private static byte[]? ReadKey(JsonElement root, string hexProperty, string? asciiProperty)
    {
        if (root.TryGetProperty(hexProperty, out var hex) && hex.ValueKind == JsonValueKind.String &&
            hex.GetString() is { Length: > 0 } hexText)
        {
            try
            {
                return Convert.FromHexString(hexText.Trim());
            }
            catch (FormatException)
            {
                throw new InvalidDataException($"{hexProperty} is not valid hexadecimal.");
            }
        }

        if (asciiProperty is not null && root.TryGetProperty(asciiProperty, out var ascii) &&
            ascii.ValueKind == JsonValueKind.String && ascii.GetString() is { Length: > 0 } asciiText)
            return Encoding.ASCII.GetBytes(asciiText);

        return null;
    }
}
