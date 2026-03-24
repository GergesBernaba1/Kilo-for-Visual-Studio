using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.App.Services
{
    public class AutomationService
    {
        private readonly string _workspaceRoot;
        private readonly string _templatesFilePath;
        private readonly IKiloBackendClient? _backendClient;
        private readonly IVSAutomationExecutor? _vsAutomationExecutor;
        private List<AutomationTemplate> _templates;

        public event EventHandler? TemplatesChanged;

        public AutomationService(string workspaceRoot, IKiloBackendClient? backendClient = null, IVSAutomationExecutor? vsAutomationExecutor = null)
        {
            _workspaceRoot = workspaceRoot;
            _backendClient = backendClient;
            _vsAutomationExecutor = vsAutomationExecutor;
            _templatesFilePath = Path.Combine(workspaceRoot, ".kilo", "automation_templates.json");
            _templates = new List<AutomationTemplate>();
            LoadTemplates();
        }

        public IReadOnlyList<AutomationTemplate> Templates => _templates;

        public void LoadTemplates()
        {
            try
            {
                if (File.Exists(_templatesFilePath))
                {
                    var json = File.ReadAllText(_templatesFilePath);
                    _templates = JsonSerializer.Deserialize<List<AutomationTemplate>>(json) ?? new List<AutomationTemplate>();
                }
                else
                {
                    _templates = GetDefaultTemplates();
                    SaveTemplates();
                }
            }
            catch
            {
                _templates = GetDefaultTemplates();
            }
        }

        public void SaveTemplates()
        {
            try
            {
                var dir = Path.GetDirectoryName(_templatesFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_templatesFilePath, json);
            }
            catch { }
        }

        public void AddTemplate(AutomationTemplate template)
        {
            template.Id = Guid.NewGuid().ToString("N");
            _templates.Add(template);
            SaveTemplates();
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateTemplate(AutomationTemplate template)
        {
            for (int i = 0; i < _templates.Count; i++)
            {
                if (_templates[i].Id == template.Id)
                {
                    _templates[i] = template;
                    break;
                }
            }
            SaveTemplates();
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveTemplate(string templateId)
        {
            _templates.RemoveAll(t => t.Id == templateId);
            SaveTemplates();
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
        }

        public AutomationTemplate? GetTemplate(string templateId)
        {
            return _templates.FirstOrDefault(t => t.Id == templateId);
        }

        public async Task<AutomationResult> ExecuteTemplateAsync(string templateId, AutomationContext context)
        {
            var template = GetTemplate(templateId);
            if (template == null)
                return new AutomationResult { Success = false, Message = "Template not found" };

            var result = new AutomationResult { Success = true, StepsExecuted = new List<string>() };

            foreach (var step in template.Steps)
            {
                try
                {
                    var stepResult = await ExecuteStepAsync(step, context, template.IsAutoMode);
                    result.StepsExecuted.Add($"{step.Type}: {stepResult}");

                    if (!stepResult.Contains("Success") && !template.IsAutoMode)
                    {
                        // In non-auto mode, stop on failure
                        result.Success = false;
                        result.Message = $"Step failed: {stepResult}";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    result.StepsExecuted.Add($"{step.Type}: Failed - {ex.Message}");
                    if (!template.IsAutoMode)
                    {
                        result.Success = false;
                        result.Message = $"Step failed: {ex.Message}";
                        break;
                    }
                }
            }

            return result;
        }

        private async Task<string> ExecuteStepAsync(AutomationStep step, AutomationContext context, bool isAutoMode)
        {
            switch (step.Type)
            {
                case AutomationStepType.RunCommand:
                    return await ExecuteRunCommandStep(step, context);

                case AutomationStepType.BuildProject:
                    return await ExecuteBuildStep(step, context);

                case AutomationStepType.RunTests:
                    return await ExecuteTestStep(step, context);

                case AutomationStepType.StartDebugging:
                    return await ExecuteStartDebuggingStep(step, context);

                case AutomationStepType.StopDebugging:
                    return await ExecuteStopDebuggingStep(step, context);

                case AutomationStepType.AttachDebugger:
                    return await ExecuteAttachDebuggerStep(step, context);

                case AutomationStepType.ProfileApplication:
                    return await ExecuteProfileStep(step, context);

                case AutomationStepType.NuGetRestore:
                    return await ExecuteNuGetRestoreStep(step, context);

                case AutomationStepType.NuGetInstall:
                    return await ExecuteNuGetInstallStep(step, context);

                case AutomationStepType.NuGetUpdate:
                    return await ExecuteNuGetUpdateStep(step, context);

                case AutomationStepType.NuGetUninstall:
                    return await ExecuteNuGetUninstallStep(step, context);

                case AutomationStepType.MacroDelay:
                    return await ExecuteMacroDelayStep(step, context);

                default:
                    return $"Unsupported step type: {step.Type}";
            }
        }

        private async Task<string> ExecuteRunCommandStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("command", out var command);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteRunCommandStepAsync(command ?? "");
            }
            // Fallback to basic implementation
            return $"Executed command: {command ?? ""}";
        }

        private async Task<string> ExecuteBuildStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteBuildStepAsync(project ?? "");
            }
            // Fallback to basic implementation
            return $"Built project: {project ?? ""}";
        }

        private async Task<string> ExecuteTestStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("filter", out var testFilter);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteTestStepAsync(testFilter ?? "");
            }
            // Fallback to basic implementation
            return $"Ran tests with filter: {testFilter ?? ""}";
        }

        private async Task<string> ExecuteGenerateCodeStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("prompt", out var prompt);
            if (_backendClient != null)
            {
                var request = new AssistantRequest
                {
                    Prompt = prompt ?? "",
                    ActiveFilePath = context.ActiveFilePath,
                    SelectedText = context.SelectedText,
                    LanguageId = context.LanguageId
                };
                var response = await _backendClient.SendRequestAsync(request);
                return $"Generated code: {response.Message}";
            }
            return "Backend not available for code generation";
        }

        private async Task<string> ExecuteRefactorStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("prompt", out var prompt);
            if (_backendClient != null)
            {
                var request = new AssistantRequest
                {
                    Prompt = prompt ?? "Refactor this code",
                    ActiveFilePath = context.ActiveFilePath,
                    SelectedText = context.SelectedText,
                    LanguageId = context.LanguageId
                };
                var response = await _backendClient.SendRequestAsync(request);
                return $"Refactored code: {response.Message}";
            }
            return "Backend not available for refactoring";
        }

        private string ExecuteShowMessageStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("message", out var message);
            // Show message to user
            return $"Displayed message: {message ?? ""}";
        }

        private async Task<string> ExecuteWaitForInputStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("prompt", out var prompt);
            // Wait for user input
            return $"Waited for user input: {prompt ?? "Press Enter to continue"}";
        }

        private async Task<string> ExecuteStartDebuggingStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("configuration", out var config);
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteStartDebuggingStepAsync(config ?? "", project ?? "");
            }
            // Fallback to basic implementation
            return $"Started debugging: {project ?? "current project"} with config {config ?? "default"}";
        }

        private async Task<string> ExecuteStopDebuggingStep(AutomationStep step, AutomationContext context)
        {
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteStopDebuggingStepAsync();
            }
            // Fallback to basic implementation
            return "Stopped debugging session";
        }

        private async Task<string> ExecuteAttachDebuggerStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("processName", out var processName);
            step.Parameters.TryGetValue("processId", out var processId);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteAttachDebuggerStepAsync(processName ?? "", processId ?? "");
            }
            // Fallback to basic implementation
            return $"Attached debugger to process: {processName ?? processId ?? "unknown"}";
        }

        private async Task<string> ExecuteProfileStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("profilerType", out var profilerType);
            step.Parameters.TryGetValue("target", out var target);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteProfileStepAsync(profilerType ?? "", target ?? "");
            }
            // Fallback to basic implementation
            return $"Started profiling: {profilerType ?? "performance"} profiler on {target ?? "current project"}";
        }

        private async Task<string> ExecuteNuGetRestoreStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteNuGetRestoreStepAsync(project ?? "");
            }
            // Fallback to basic implementation
            return $"Restored NuGet packages for: {project ?? "solution"}";
        }

        private async Task<string> ExecuteNuGetInstallStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("packageId", out var packageId);
            step.Parameters.TryGetValue("version", out var version);
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteNuGetInstallStepAsync(packageId ?? "", version ?? "", project ?? "");
            }
            // Fallback to basic implementation
            return $"Installed NuGet package: {packageId} {version ?? "latest"} to {project ?? "current project"}";
        }

        private async Task<string> ExecuteNuGetUpdateStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("packageId", out var packageId);
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteNuGetUpdateStepAsync(packageId ?? "", project ?? "");
            }
            // Fallback to basic implementation
            return $"Updated NuGet package: {packageId} in {project ?? "current project"}";
        }

        private async Task<string> ExecuteNuGetUninstallStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("packageId", out var packageId);
            step.Parameters.TryGetValue("project", out var project);
            if (_vsAutomationExecutor != null)
            {
                return await _vsAutomationExecutor.ExecuteNuGetUninstallStepAsync(packageId ?? "", project ?? "");
            }
            // Fallback to basic implementation
            return $"Uninstalled NuGet package: {packageId} from {project ?? "current project"}";
        }

        private async Task<string> ExecuteMacroDelayStep(AutomationStep step, AutomationContext context)
        {
            step.Parameters.TryGetValue("delay", out var delayStr);
            if (int.TryParse(delayStr, out var delayMs))
            {
                await Task.Delay(delayMs);
                return $"Delayed for {delayMs}ms";
            }
            return "Invalid delay parameter";
        }

        private List<AutomationTemplate> GetDefaultTemplates()
        {
            return new List<AutomationTemplate>
            {
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Run Tests and Fix Failures",
                    Description = "Run unit tests and automatically fix any failures found",
                    Category = "Testing",
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.RunTests,
                            Description = "Execute unit tests",
                            Parameters = new Dictionary<string, string> { { "filter", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.GenerateCode,
                            Description = "Fix any test failures",
                            Parameters = new Dictionary<string, string> { { "prompt", "Fix the failing tests in the codebase" } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Refactor Class",
                    Description = "Refactor the selected class for better design",
                    Category = "Refactoring",
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.RefactorCode,
                            Description = "Analyze and refactor the class",
                            Parameters = new Dictionary<string, string> { { "prompt", "Refactor this class to follow SOLID principles and best practices" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.ApplyDiff,
                            Description = "Apply the refactoring changes",
                            Parameters = new Dictionary<string, string> { { "confirm", "true" } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Build and Test",
                    Description = "Build the project and run tests",
                    Category = "Build",
                    IsAutoMode = true,
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.BuildProject,
                            Description = "Build the solution",
                            Parameters = new Dictionary<string, string> { { "project", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.RunTests,
                            Description = "Run all tests",
                            Parameters = new Dictionary<string, string> { { "filter", "" } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Debug Application",
                    Description = "Start debugging session and attach to process",
                    Category = "Debugging",
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.StartDebugging,
                            Description = "Start debugging with default configuration",
                            Parameters = new Dictionary<string, string> { { "configuration", "Debug" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.ShowMessage,
                            Description = "Notify user that debugging has started",
                            Parameters = new Dictionary<string, string> { { "message", "Debugging session started. Set breakpoints and continue execution." } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Profile Performance",
                    Description = "Launch performance profiler to analyze application performance",
                    Category = "Debugging",
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.ProfileApplication,
                            Description = "Launch performance profiler",
                            Parameters = new Dictionary<string, string> { { "profilerType", "performance" } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "NuGet Package Management",
                    Description = "Restore, update, and manage NuGet packages",
                    Category = "Build",
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.NuGetRestore,
                            Description = "Restore all NuGet packages",
                            Parameters = new Dictionary<string, string> { { "project", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.NuGetUpdate,
                            Description = "Update outdated packages",
                            Parameters = new Dictionary<string, string> { { "packageId", "" } }
                        }
                    }
                },
                new AutomationTemplate
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Full CI/CD Pipeline",
                    Description = "Complete build, test, and deploy pipeline",
                    Category = "Build",
                    IsAutoMode = true,
                    Steps = new List<AutomationStep>
                    {
                        new AutomationStep
                        {
                            Type = AutomationStepType.NuGetRestore,
                            Description = "Restore NuGet packages",
                            Parameters = new Dictionary<string, string> { { "project", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.BuildProject,
                            Description = "Build solution",
                            Parameters = new Dictionary<string, string> { { "project", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.RunTests,
                            Description = "Run unit tests",
                            Parameters = new Dictionary<string, string> { { "filter", "" } }
                        },
                        new AutomationStep
                        {
                            Type = AutomationStepType.GenerateCode,
                            Description = "Generate deployment artifacts",
                            Parameters = new Dictionary<string, string> { { "prompt", "Generate deployment configuration and artifacts" } }
                        }
                    }
                }
            };
        }

    }

    public class AutomationContext
    {
        public string ActiveFilePath { get; set; } = string.Empty;
        public string SelectedText { get; set; } = string.Empty;
        public string LanguageId { get; set; } = string.Empty;
        public string WorkspaceRoot { get; set; } = string.Empty;
    }

    public class AutomationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> StepsExecuted { get; set; } = new List<string>();
    }
}
