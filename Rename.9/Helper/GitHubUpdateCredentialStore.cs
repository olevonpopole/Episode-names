using System;
using System.Security.Cryptography;
using System.Text;

namespace Episode_Names.Helper
{
    internal static class GitHubUpdateCredentialStore
    {
        private const string Prefix = "dpapi:github:v1:";
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("Episode-Names/GitHub-Releases/v1");

        internal static string Protect(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("GitHub-Zugriffstoken fehlt.");
            byte[] plain = Encoding.UTF8.GetBytes(token.Trim());
            try
            {
                return Prefix + Convert.ToBase64String(ProtectedData.Protect(
                    plain, Entropy, DataProtectionScope.CurrentUser));
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        internal static string Unprotect(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "";
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "GitHub-Token ist nicht geschützt gespeichert. Bitte erneut eingeben.");
            byte[] encrypted = Convert.FromBase64String(stored.Substring(Prefix.Length));
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy,
                DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(plain); }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
    }
}
