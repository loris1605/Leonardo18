using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AppServices.Security;

/// <summary>
/// Stores passwords as versioned PBKDF2-SHA256 hashes and verifies legacy
/// plaintext values during the gradual migration of existing operator rows.
/// </summary>
public static class PasswordHasher
{
    private const string FormatVersion = "v1";
    private const string FormatPrefix = FormatVersion + "$";
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinimumAcceptedIterations = 100_000;
    private const int MaximumAcceptedIterations = 1_000_000;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            FormatVersion,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool Verify(string? password, string? storedValue)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedValue))
            return false;

        if (!storedValue.StartsWith(FormatPrefix, StringComparison.Ordinal))
            return FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(storedValue));

        string[] parts = storedValue.Split('$');
        if (parts.Length != 4 || parts[0] != FormatVersion ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) ||
            iterations < MinimumAcceptedIterations || iterations > MaximumAcceptedIterations)
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expectedHash.Length != HashSize)
                return false;

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static bool NeedsRehash(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue) || !storedValue.StartsWith(FormatPrefix, StringComparison.Ordinal))
            return true;

        string[] parts = storedValue.Split('$');
        return parts.Length != 4 ||
               !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) ||
               iterations != Iterations;
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

