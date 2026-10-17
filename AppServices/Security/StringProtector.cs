using System.Security.Cryptography;
using System.Text;

namespace AppServices.Security;

/// <summary>
/// Encrypts local strings using an application-wide fixed key.
/// The embedded key provides concealment, not protection against executable analysis.
/// </summary>
public static class StringProtector
{
    private const string Prefix = "aesgcm:v1:";
    private const string LegacyPrefix = "dpapi:v1:";
    private const string ApplicationKey = "gS6WZo4iZRsYaycHg/BAIQjpDuXnC/f8WFItuHaZ3R8=";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static bool IsEncrypted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.StartsWith(LegacyPrefix, StringComparison.Ordinal);
    }

    public static string Encrypt(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] plainBytes = Encoding.UTF8.GetBytes(value);
        byte[] key = Convert.FromBase64String(ApplicationKey);
        try
        {
            // A new nonce is mandatory even though the application key is fixed.
            byte[] payload = new byte[NonceSize + TagSize + plainBytes.Length];
            RandomNumberGenerator.Fill(payload.AsSpan(0, NonceSize));
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(payload.AsSpan(0, NonceSize), plainBytes,
                payload.AsSpan(NonceSize + TagSize), payload.AsSpan(NonceSize, TagSize));
            return Prefix + Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string Decrypt(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            return DecryptLegacy(value);
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            throw new FormatException("Il valore non ha un formato cifrato supportato.");

        byte[] payload = Convert.FromBase64String(value[Prefix.Length..]);
        if (payload.Length < NonceSize + TagSize)
            throw new FormatException("Il valore cifrato è incompleto.");

        byte[] plainBytes = new byte[payload.Length - NonceSize - TagSize];
        byte[] key = Convert.FromBase64String(ApplicationKey);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize), plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static bool TryDecrypt(string? value, out string decryptedValue)
    {
        decryptedValue = string.Empty;
        if (value is null) return false;
        try
        {
            decryptedValue = Decrypt(value);
            return true;
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
        catch (PlatformNotSupportedException) { return false; }
    }

    // Read-only compatibility for values produced by the previous implementation.
    private static string DecryptLegacy(string value)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("La lettura dei vecchi valori DPAPI richiede Windows.");
        byte[] payload = Convert.FromBase64String(value[LegacyPrefix.Length..]);
        byte[] plainBytes = ProtectedData.Unprotect(payload, null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(plainBytes); }
        finally { CryptographicOperations.ZeroMemory(plainBytes); }
    }
}