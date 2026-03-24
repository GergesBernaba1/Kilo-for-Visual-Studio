using System;
using System.Security.Cryptography;
using System.Text;

namespace Kilo.VisualStudio.App.Services
{
    public class SecureStorageService
    {
        private const string SecretContainerName = "KiloVisualStudio";
        private readonly string _workspaceRoot;

        public SecureStorageService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public bool StoreApiKey(string keyName, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                var encrypted = ProtectData(Encoding.UTF8.GetBytes(apiKey));
                var filePath = GetSecretPath(keyName);
                var dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllBytes(filePath, encrypted);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string? RetrieveApiKey(string keyName)
        {
            try
            {
                var filePath = GetSecretPath(keyName);
                if (!System.IO.File.Exists(filePath))
                    return null;

                var encrypted = System.IO.File.ReadAllBytes(filePath);
                var decrypted = UnprotectData(encrypted);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        public bool DeleteApiKey(string keyName)
        {
            try
            {
                var filePath = GetSecretPath(keyName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool HasApiKey(string keyName)
        {
            var filePath = GetSecretPath(keyName);
            return System.IO.File.Exists(filePath);
        }

        private string GetSecretPath(string keyName)
        {
            return System.IO.Path.Combine(_workspaceRoot, ".kilo", "secrets", $"{keyName}.dat");
        }

        private byte[] ProtectData(byte[] data)
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }

        private byte[] UnprotectData(byte[] data)
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
    }
}