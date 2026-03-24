using System;
using System.Collections.Generic;
using System.IO;
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
        private AuthenticatedUser? _currentUser;
        private string _secureStoragePath;

        public event EventHandler<AuthenticatedUser?>? UserChanged;

        public bool IsAuthenticated => _currentUser?.IsValid ?? false;
        public AuthenticatedUser? CurrentUser => _currentUser;

        public AuthenticationService(string workspaceRoot)
        {
            _secureStoragePath = Path.Combine(workspaceRoot, ".kilo", "auth.dat");
            LoadStoredCredentials();
        }

        public bool Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            _currentUser = new AuthenticatedUser
            {
                UserId = Guid.NewGuid().ToString("N"),
                Email = email,
                DisplayName = email.Split('@')[0],
                Token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{password}")),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            SaveCredentials();
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

        private void SaveCredentials()
        {
            try
            {
                var dir = Path.GetDirectoryName(_secureStoragePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_currentUser);
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                File.WriteAllText(_secureStoragePath, encoded);
            }
            catch { }
        }

        private void LoadStoredCredentials()
        {
            try
            {
                if (!File.Exists(_secureStoragePath))
                    return;

                var encoded = File.ReadAllText(_secureStoragePath);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                _currentUser = JsonSerializer.Deserialize<AuthenticatedUser>(json);

                if (_currentUser != null && !_currentUser.IsValid)
                {
                    _currentUser = null;
                    ClearStoredCredentials();
                }
            }
            catch { }
        }

        private void ClearStoredCredentials()
        {
            try
            {
                if (File.Exists(_secureStoragePath))
                    File.Delete(_secureStoragePath);
            }
            catch { }
        }
    }
}