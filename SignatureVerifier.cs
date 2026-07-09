using System;
using System.Security.Cryptography;
using System.Text;

namespace PlanningPoker
{
    public static class SignatureVerifier
    {
        public const string HeaderName = "X-PlanningPoker-Signature";

        // Verifies header == "sha256=" + hex(HMACSHA256(rawBody, secret)), constant-time.
        public static bool VerifyHmacSha256(string rawBody, string secret, string header)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header))
            {
                return false;
            }

            var expected = "sha256=" + ComputeHex(rawBody, secret);
            var a = Encoding.UTF8.GetBytes(expected);
            var b = Encoding.UTF8.GetBytes(header.Trim());
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        public static string ComputeHex(string rawBody, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
