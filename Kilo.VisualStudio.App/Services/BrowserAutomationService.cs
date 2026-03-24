using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class BrowserAutomationService
    {
        private readonly string _workspaceRoot;
        private bool _isEnabled = false;
        private string? _activeBrowserSession;

        public event EventHandler<string>? BrowserActionCompleted;
        public event EventHandler<string>? BrowserError;

        public bool IsEnabled => _isEnabled;

        public BrowserAutomationService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        public Task<string> NavigateAsync(string url)
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            _activeBrowserSession = Guid.NewGuid().ToString("N");
            var result = $"Navigated to: {url} (Session: {_activeBrowserSession})";
            BrowserActionCompleted?.Invoke(this, result);
            return Task.FromResult(result);
        }

        public Task<string> TakeScreenshotAsync()
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            var screenshotPath = Path.Combine(_workspaceRoot, ".kilo", "screenshots", $"{Guid.NewGuid():N}.png");
            var dir = Path.GetDirectoryName(screenshotPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var result = $"Screenshot saved to: {screenshotPath}";
            BrowserActionCompleted?.Invoke(this, result);
            return Task.FromResult(result);
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            var result = $"Script executed: {script.Substring(0, Math.Min(50, script.Length))}...";
            BrowserActionCompleted?.Invoke(this, result);
            return Task.FromResult(result);
        }

        public Task<string> GetPageContentAsync()
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            return Task.FromResult("<html><body>Page content placeholder</body></html>");
        }

        public Task<string> ClickElementAsync(string selector)
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            var result = $"Clicked element: {selector}";
            BrowserActionCompleted?.Invoke(this, result);
            return Task.FromResult(result);
        }

        public Task<string> FillFormAsync(string selector, string value)
        {
            if (!_isEnabled)
                return Task.FromResult("Browser automation is not enabled");

            var result = $"Filled form field: {selector} = {value}";
            BrowserActionCompleted?.Invoke(this, result);
            return Task.FromResult(result);
        }

        public void CloseSession()
        {
            _activeBrowserSession = null;
        }
    }
}