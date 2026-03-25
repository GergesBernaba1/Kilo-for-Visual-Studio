using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kilo.VisualStudio.Extension.Services
{
    /// <summary>
    /// Secure storage adapter that uses DPAPI (Data Protection API) to encrypt sensitive data.
    /// Stores encrypted data in the user's LocalAppData folder.
    /// This provides user-level protection equivalent to Windows Credential Manager.
    /// </summary>
    internal sealed class VsSecureStorageAdapter
    {
        private const string SecretContainerName = "KiloVisualStudio";

        public VsSecureStorageAdapter()
        {
        }

        /// <summary>
        /// Stores a secret securely using DPAPI encryption.
        /// </summary>
        public bool StoreSecret(string keyName, string secret)
        {
            if (string.IsNullOrWhiteSpace(secret)) return false;

            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(secret),
                    null,
                    DataProtectionScope.CurrentUser);

                var filePath = GetSecretPath(keyName);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(filePath, encrypted);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves a stored secret.
        /// </summary>
        public string? RetrieveSecret(string keyName)
        {
            try
            {
                var filePath = GetSecretPath(keyName);
                if (!File.Exists(filePath)) return null;

                var encrypted = File.ReadAllBytes(filePath);
                var decrypted = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes a stored secret.
        /// </summary>
        public bool DeleteSecret(string keyName)
        {
            try
            {
                var filePath = GetSecretPath(keyName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a secret exists.
        /// </summary>
        public bool HasSecret(string keyName)
        {
            var filePath = GetSecretPath(keyName);
            return File.Exists(filePath);
        }

        private string GetSecretPath(string keyName)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SecretContainerName, "Secrets");
            return Path.Combine(folder, $"{keyName}.bin");
        }
    }
}
