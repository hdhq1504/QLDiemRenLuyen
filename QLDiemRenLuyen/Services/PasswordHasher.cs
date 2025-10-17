using System.Security.Cryptography;
using System.Text;

namespace QLDiemRenLuyen.Services
{
    public static class PasswordHasher
    {
        public static (string HashBase64, string SaltBase64) HashPassword(string password)
        {
            // Salt 16 bytes, PBKDF2 100k iterations, 32 bytes key
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string saltBase64, string hashBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);
            var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);
            return CryptographicOperations.FixedTimeEquals(hashToCompare, Convert.FromBase64String(hashBase64));
        }
    }
}
