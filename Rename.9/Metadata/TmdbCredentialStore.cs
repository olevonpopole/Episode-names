using System;
using System.Security.Cryptography;
using System.Text;

namespace Episode_Names.Metadata
{
    /// <summary>Protects the optional user-supplied TMDB credential with Windows DPAPI, current user scope.</summary>
    internal static class TmdbCredentialStore
    {
        private const string Prefix = "dpapi:v1:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Episode-Names/TMDB/v1");

        public static string Protect(string credential)
        {
            if (string.IsNullOrWhiteSpace(credential))
                throw new ArgumentException("Ein TMDB API-Key oder Read Access Token fehlt.", "credential");

            byte[] plain = Encoding.UTF8.GetBytes(credential.Trim());
            try
            {
                byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(encrypted);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return string.Empty;
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
                throw new MetadataProviderException("Die TMDB-Anmeldedaten sind nicht im unterstützten verschlüsselten Format gespeichert.");

            try
            {
                byte[] encrypted = Convert.FromBase64String(stored.Substring(Prefix.Length));
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                try
                {
                    return Encoding.UTF8.GetString(plain);
                }
                finally
                {
                    Array.Clear(plain, 0, plain.Length);
                }
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                throw new MetadataProviderException("Der TMDB-Key kann unter diesem Windows-Benutzer nicht entschlüsselt werden. Bitte erneut eingeben.");
            }
        }
    }
}
