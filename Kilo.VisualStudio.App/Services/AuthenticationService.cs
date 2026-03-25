using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public class AuthenticatedUser
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(Token) && ExpiresAtUtc > DateTimeOffset.UtcNow;
    }

    public class AuthenticationService
    {
        private const string SecretContainerName = "KiloVisualStudio";
        private AuthenticatedUser? _currentUser;
        private readonly string _metadataPath;

        public event EventHandler<AuthenticatedUser?>? UserChanged;

        public bool IsAuthenticated => _currentUser?.IsValid ?? false;
        public AuthenticatedUser? CurrentUser => _currentUser;

        public AuthenticationService(string workspaceRoot)
        {
            _metadataPath = Path.Combine(workspaceRoot, ".kilo", "auth-meta.json");
            LoadStoredCredentials();
        }

        public bool Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            // Generate a secure token (in production, this would come from the auth server)
            var secureToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{Guid.NewGuid():N}"));
            
            _currentUser = new AuthenticatedUser
            {
                UserId = Guid.NewGuid().ToString("N"),
                Email = email,
                DisplayName = email.Split('@')[0],
                Token = secureToken,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            // Securely store the password using DPAPI
            StorePasswordSecurely(email, password);
            SaveMetadata();
            UserChanged?.Invoke(this, _currentUser);
            return true;
        }

        public void Logout()
        {
            _currentUser = null;
            ClearStoredCredentials();
            UserChanged?.Invoke(this, null);
        }

        public bool ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                return decoded.Contains(":");
            }
            catch
            {
                return false;
            }
        }

        public string GetAuthHeader()
        {
            if (_currentUser == null || !_currentUser.IsValid)
                return string.Empty;

            return $"Bearer {_currentUser.Token}";
        }

        public bool ValidateCredentials(string email, string password)
        {
            var storedPassword = RetrievePasswordSecurely(email);
            return storedPassword == password;
        }

        private void StorePasswordSecurely(string email, string password)
        {
            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(password),
                    null,
                    DataProtectionScope.CurrentUser);

                var filePath = GetSecretPath(email);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(filePath, encrypted);
            }
            catch
            {
                // Fallback: don't store if secure storage fails
            }
        }

        private string? RetrievePasswordSecurely(string email)
        {
            try
            {
                var filePath = GetSecretPath(email);
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

        private void DeleteStoredPassword(string email)
        {
            try
            {
                var filePath = GetSecretPath(email);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        private string GetSecretPath(string keyName)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SecretContainerName, "Secrets");
            return Path.Combine(folder, $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(keyName))}.bin");
        }

        private void SaveMetadata()
        {
            try
            {
                var dir = Path.GetDirectoryName(_metadataPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var metadata = new AuthMetadata
                {
                    UserId = _currentUser?.UserId ?? string.Empty,
                    Email = _currentUser?.Email ?? string.Empty,
                    ExpiresAtUtc = _currentUser?.ExpiresAtUtc ?? DateTimeOffset.MinValue
                };
                var json = JsonSerializer.Serialize(metadata);
                File.WriteAllText(_metadataPath, json);
            }
            catch { }
        }

        private void LoadStoredCredentials()
        {
            try
            {
                if (!File.Exists(_metadataPath))
                    return;

                var json = File.ReadAllText(_metadataPath);
                var metadata = JsonSerializer.Deserialize<AuthMetadata>(json);

                if (metadata != null && metadata.ExpiresAtUtc > DateTimeOffset.UtcNow)
                {
                    _currentUser = new AuthenticatedUser
                    {
                        UserId = metadata.UserId,
                        Email = metadata.Email,
                        DisplayName = metadata.Email.Split('@')[0],
                        Token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{metadata.Email}:{metadata.UserId}")),
                        ExpiresAtUtc = metadata.ExpiresAtUtc
                    };
                }
                else
                {
                    ClearStoredCredentials();
                }
            }
            catch { }
        }

        private void ClearStoredCredentials()
        {
            try
            {
                if (File.Exists(_metadataPath))
                    File.Delete(_metadataPath);

                // Also clear the secure password storage
                if (_currentUser != null)
                {
                    DeleteStoredPassword(_currentUser.Email);
                }
            }
            catch { }
        }
    }

    internal class AuthMetadata
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}