using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.Integration
{
    public class TerminalIntegrationService
    {
        private string? _workspaceRoot;

        public void SetWorkspaceRoot(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public async Task<string> ExecuteCommandAsync(string command, string workingDirectory = "")
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {command}",
                        WorkingDirectory = string.IsNullOrEmpty(workingDirectory) 
                            ? (_workspaceRoot ?? Directory.GetCurrentDirectory()) 
                            : workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null)
                        return string.Empty;

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    return string.IsNullOrEmpty(error) ? output : $"{output}\n[STDERR]: {error}";
                }
                catch (Exception ex)
                {
                    return $"Error executing command: {ex.Message}";
                }
            });
        }

        public async Task<string> GetSelectedTerminalOutputAsync()
        {
            return string.Empty;
        }

        public async Task<string> GetIntegratedTerminalBufferAsync()
        {
            return await ExecuteCommandAsync("echo Integrate with VS terminal via VS automation API");
        }

        public void OpenIntegratedTerminal(string workingDirectory = "")
        {
        }
    }
}