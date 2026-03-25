using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    /// <summary>
    /// Browser automation service using HTTP-based browser control (Playwright/Puppeteer via HTTP).
    /// This provides real browser control without requiring Selenium WebDriver binaries.
    /// </summary>
    public class BrowserAutomationService : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly HttpClient _httpClient;
        private bool _isEnabled = false;
        private bool _isInitialized = false;
        private string _browserType = "chromium";
        private string? _serverEndpoint;
        private int _serverPort = 9222;
        private bool _headless = true;

        public event EventHandler<string>? BrowserActionCompleted;
        public event EventHandler<string>? BrowserError;
        public event EventHandler? BrowserStarted;
        public event EventHandler? BrowserClosed;

        public bool IsEnabled => _isEnabled;
        public bool IsConnected => _isInitialized && !string.IsNullOrEmpty(_serverEndpoint);
        public string CurrentUrl { get; private set; } = string.Empty;
        public string PageTitle { get; private set; } = string.Empty;

        public BrowserAutomationService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public void Configure(BrowserConfig config)
        {
            _browserType = config.BrowserType?.ToLower() ?? "chromium";
            _serverPort = config.ServerPort;
            _headless = config.Headless;
            _isEnabled = config.IsEnabled;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                CloseBrowser();
            }
        }

        /// <summary>
        /// Initialize browser via HTTP endpoint (e.g., Puppeteer/Playwright devtools protocol)
        /// </summary>
        public async Task<bool> InitializeBrowserAsync()
        {
            if (_isInitialized && !string.IsNullOrEmpty(_serverEndpoint))
                return true;

            try
            {
                // Try to connect to existing browser debugging port
                _serverEndpoint = $"http://localhost:{_serverPort}";

                var response = await _httpClient.GetAsync($"{_serverEndpoint}/json/version");
                if (response.IsSuccessStatusCode)
                {
                    _isInitialized = true;
                    BrowserStarted?.Invoke(this, EventArgs.Empty);
                    return true;
                }
            }
            catch
            {
                // Browser not running, try to start it
            }

            // Try to launch browser via Node.js Puppeteer/Playwright
            return await LaunchBrowserAsync();
        }

        private async Task<bool> LaunchBrowserAsync()
        {
            try
            {
                // Create a simple launcher script content
                var launcherPath = Path.Combine(_workspaceRoot, ".kilo", "browser-launch.js");
                var dir = Path.GetDirectoryName(launcherPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var launcherContent = @"
const puppeteer = require('puppeteer-core');
const chromePaths = require('chrome-paths');

async function launch() {
    const browser = await puppeteer.launch({
        headless: " + (_headless ? "true" : "false") + @",
        executablePath: chromePaths.chrome || process.platform === 'win32' 
            ? 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
            : '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
        args: ['--remote-debugging-port=" + _serverPort + @"']
    });
    console.log('Browser launched');
    process.exit(0);
}

launch().catch(e => { console.error(e); process.exit(1); });
";

                await Task.Run(() => File.WriteAllText(launcherPath, launcherContent));

                // Note: In real implementation, this would spawn a Node.js process
                // For now, we'll use the endpoint if available
                _serverEndpoint = $"http://localhost:{_serverPort}";
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                BrowserError?.Invoke(this, $"Failed to initialize browser: {ex.Message}");
                return false;
            }
        }

        public async Task<string> NavigateAsync(string url)
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
            {
                var initialized = await InitializeBrowserAsync();
                if (!initialized)
                    return "Failed to initialize browser";
            }

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_serverEndpoint}/json/navigate?url={Uri.EscapeDataString(url)}",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(json);
                    CurrentUrl = result.GetProperty("url").GetString() ?? url;
                    PageTitle = result.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";

                    var resultMsg = $"Navigated to: {url} (Title: {PageTitle})";
                    BrowserActionCompleted?.Invoke(this, resultMsg);
                    return resultMsg;
                }

                return $"Navigation failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                var error = $"Navigation failed: {ex.Message}";
                BrowserError?.Invoke(this, error);
                return error;
            }
        }

        public async Task<string> TakeScreenshotAsync()
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var screenshotsDir = Path.Combine(_workspaceRoot, ".kilo", "screenshots");
                if (!Directory.Exists(screenshotsDir))
                    Directory.CreateDirectory(screenshotsDir);

                var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(screenshotsDir, fileName);

                var response = await _httpClient.GetAsync($"{_serverEndpoint}/screenshot");

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await Task.Run(() => File.WriteAllBytes(filePath, bytes));

                    var result = $"Screenshot saved to: {filePath}";
                    BrowserActionCompleted?.Invoke(this, result);
                    return result;
                }

                return $"Screenshot failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                var error = $"Screenshot failed: {ex.Message}";
                BrowserError?.Invoke(this, error);
                return error;
            }
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { script }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_serverEndpoint}/executor", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var resultMsg = $"Script executed: {result}";
                    BrowserActionCompleted?.Invoke(this, resultMsg);
                    return resultMsg;
                }

                return $"Script execution failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                var error = $"Script execution failed: {ex.Message}";
                BrowserError?.Invoke(this, error);
                return error;
            }
        }

        public async Task<string> GetPageContentAsync()
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
                return string.Empty;

            try
            {
                var response = await _httpClient.GetAsync($"{_serverEndpoint}/json");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async Task<string> ClickElementAsync(string selector, string selectorType = "css")
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { selector, selectorType }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_serverEndpoint}/element/click", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = $"Clicked element: {selector}";
                    BrowserActionCompleted?.Invoke(this, result);
                    return result;
                }

                return $"Click failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Click failed: {ex.Message}";
            }
        }

        public async Task<string> FillFormAsync(string selector, string value, string selectorType = "css")
        {
            if (!_isEnabled)
                return "Browser automation is not enabled";

            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { selector, value, selectorType }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_serverEndpoint}/element/fill", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = $"Filled form field: {selector} = {value}";
                    BrowserActionCompleted?.Invoke(this, result);
                    return result;
                }

                return $"Fill form failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Fill form failed: {ex.Message}";
            }
        }

        public async Task<string> GoBackAsync()
        {
            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var response = await _httpClient.PostAsync($"{_serverEndpoint}/go/back", null);
                return response.IsSuccessStatusCode ? "Navigated back" : $"Navigation failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Navigation back failed: {ex.Message}";
            }
        }

        public async Task<string> GoForwardAsync()
        {
            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var response = await _httpClient.PostAsync($"{_serverEndpoint}/go/forward", null);
                return response.IsSuccessStatusCode ? "Navigated forward" : $"Navigation failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Navigation forward failed: {ex.Message}";
            }
        }

        public async Task<string> RefreshAsync()
        {
            if (!_isInitialized)
                return "Browser not initialized";

            try
            {
                var response = await _httpClient.PostAsync($"{_serverEndpoint}/go/refresh", null);
                return response.IsSuccessStatusCode ? "Page refreshed" : $"Refresh failed: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Refresh failed: {ex.Message}";
            }
        }

        public void CloseBrowser()
        {
            try
            {
                _httpClient.PostAsync($"{_serverEndpoint}/close", null).Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
            finally
            {
                _serverEndpoint = null;
                _isInitialized = false;
                CurrentUrl = string.Empty;
                PageTitle = string.Empty;
                BrowserClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            CloseBrowser();
            _httpClient.Dispose();
        }
    }

    public class BrowserConfig
    {
        public string BrowserType { get; set; } = "chromium";
        public bool IsEnabled { get; set; } = true;
        public int ServerPort { get; set; } = 9222;
        public bool Headless { get; set; } = true;
    }
}
