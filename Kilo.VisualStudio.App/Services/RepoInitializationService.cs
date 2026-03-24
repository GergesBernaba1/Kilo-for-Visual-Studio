using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class RepoInitializationService
    {
        private readonly string _workspaceRoot;

        public RepoInitializationService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public Task<bool> IsGitRepositoryAsync()
        {
            var gitDir = Path.Combine(_workspaceRoot, ".git");
            var gitFile = Path.Combine(_workspaceRoot, ".git");
            return Task.FromResult(Directory.Exists(gitDir) || File.Exists(gitFile));
        }

        public Task<string> InitializeRepositoryAsync(string remoteUrl = "")
        {
            var output = new StringBuilder();
            output.AppendLine("# Git Repository Initialization");
            output.AppendLine();
            output.AppendLine("Run the following commands to initialize a git repository:");
            output.AppendLine();
            output.AppendLine("```bash");
            output.AppendLine("git init");
            output.Append("git add .");

            if (!string.IsNullOrEmpty(remoteUrl))
            {
                output.AppendLine();
                output.AppendLine($"git remote add origin {remoteUrl}");
            }

            output.AppendLine();
            output.AppendLine("git commit -m \"Initial commit\"");
            output.AppendLine("```");

            var kiloDir = Path.Combine(_workspaceRoot, ".kilo");
            if (!Directory.Exists(kiloDir))
            {
                Directory.CreateDirectory(kiloDir);
            }

            var gitignorePath = Path.Combine(_workspaceRoot, ".gitignore");
            if (!File.Exists(gitignorePath))
            {
                var defaultGitignore = @"bin/
obj/
.vs/
*.user
*.suo
*.cache
*.log
.idea/
*.swp
.vscode/
.kilo/
packages/
node_modules/
";
                File.WriteAllText(gitignorePath, defaultGitignore);
                output.AppendLine();
                output.AppendLine("Created .gitignore with common .NET patterns");
            }

            var gitattributesPath = Path.Combine(_workspaceRoot, ".gitattributes");
            if (!File.Exists(gitattributesPath))
            {
                var defaultGitattributes = @"* text=auto
*.cs text eol=crlf
*.vb text eol=crlf
*.json text eol=lf
*.md text eol=lf
";
                File.WriteAllText(gitattributesPath, defaultGitattributes);
                output.AppendLine("Created .gitattributes");
            }

            return Task.FromResult(output.ToString());
        }

        public Task<string> GetSetupInstructionsAsync()
        {
            var instructions = new StringBuilder();
            instructions.AppendLine("# Kilo Setup Instructions");
            instructions.AppendLine();
            instructions.AppendLine("1. **Initialize Git Repository** (if not already done)");
            instructions.AppendLine("   - Run `git init` in your project root");
            instructions.AppendLine();
            instructions.AppendLine("2. **Create .kilo configuration**");
            instructions.AppendLine("   - A `.kilo` folder will be created automatically");
            instructions.AppendLine();
            instructions.AppendLine("3. **Configure your API key**");
            instructions.AppendLine("   - Open Kilo Settings (Ctrl+Shift+K)");
            instructions.AppendLine("   - Enter your API key");
            instructions.AppendLine();
            instructions.AppendLine("4. **Select Model/Provider**");
            instructions.AppendLine("   - Choose from: OpenAI, Anthropic, Google, Azure, Ollama, LM Studio");
            instructions.AppendLine();

            return Task.FromResult(instructions.ToString());
        }

        public Task<RepositoryStatus> GetStatusAsync()
        {
            var status = new RepositoryStatus
            {
                WorkspacePath = _workspaceRoot,
                IsGitRepository = Directory.Exists(Path.Combine(_workspaceRoot, ".git")) || File.Exists(Path.Combine(_workspaceRoot, ".git"))
            };

            if (status.IsGitRepository)
            {
                var kiloDir = Path.Combine(_workspaceRoot, ".kilo");
                status.HasKiloConfig = Directory.Exists(kiloDir);

                status.ConfigFiles = new List<string>();
                var configFiles = new[] { ".gitignore", ".gitattributes" };
                foreach (var file in configFiles)
                {
                    if (File.Exists(Path.Combine(_workspaceRoot, file)))
                    {
                        status.ConfigFiles.Add(file);
                    }
                }
            }

            return Task.FromResult(status);
        }
    }

    public class RepositoryStatus
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public bool IsGitRepository { get; set; }
        public bool HasKiloConfig { get; set; }
        public List<string> ConfigFiles { get; set; } = new List<string>();
    }
}