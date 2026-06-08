using System.Security.Cryptography;
using System.Text;

namespace rrs_pay.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyPassword(string password, string passwordHash)
    {
        var computedHash = HashPassword(password);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(passwordHash.ToLowerInvariant());

        return computedBytes.Length == storedBytes.Length
            && CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
