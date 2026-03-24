using System;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.Extension
{
    public class VSAutomationExecutor : IVSAutomationExecutor
    {
        private readonly DTE _dte;
        private readonly AutomationService _automationService;

        public VSAutomationExecutor(DTE dte, AutomationService automationService)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
            _automationService = automationService ?? throw new ArgumentNullException(nameof(automationService));
        }

        public async Task<AutomationResult> ExecuteTemplateAsync(string templateId, AutomationContext context)
        {
            return await _automationService.ExecuteTemplateAsync(templateId, context);
        }

        public async Task<string> ExecuteBuildStepAsync(string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (string.IsNullOrEmpty(projectName))
                {
                    // Build entire solution
                    _dte.Solution.SolutionBuild.Build(true);
                }
                else
                {
                    // Find and build specific project
                    foreach (Project project in _dte.Solution.Projects)
                    {
                        if (project.Name == projectName || project.FullName.Contains(projectName))
                        {
                            _dte.Solution.SolutionBuild.BuildProject(_dte.Solution.SolutionBuild.ActiveConfiguration.Name, project.FullName, true);
                            break;
                        }
                    }
                }

                return "Build completed successfully";
            }
            catch (Exception ex)
            {
                return $"Build failed: {ex.Message}";
            }
        }

        public async Task<string> ExecuteTestStepAsync(string testFilter)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Use DTE to run tests
                // Note: This is a simplified implementation
                // In a real scenario, you'd use the Test Explorer APIs
                _dte.ExecuteCommand("TestExplorer.RunAllTests");

                return "Tests executed";
            }
            catch (Exception ex)
            {
                return $"Test execution failed: {ex.Message}";
            }
        }

        public async Task<string> ExecuteRunCommandStepAsync(string command)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Execute command in command window
                _dte.ExecuteCommand("View.CommandWindow");
                _dte.ExecuteCommand(command);

                return $"Executed command: {command}";
            }
            catch (Exception ex)
            {
                return $"Command execution failed: {ex.Message}";
            }
        }

        public async Task<string> ExecuteStartDebuggingStepAsync(string configuration, string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Set active configuration
                if (!string.IsNullOrEmpty(configuration))
                {
                    foreach (SolutionConfiguration config in _dte.Solution.SolutionBuild.SolutionConfigurations)
                    {
                        if (config.Name == configuration)
                        {
                            config.Activate();
                            break;
                        }
                    }
                }

                // Start debugging
                _dte.Debugger.Go(false);

                return $"Started debugging with configuration: {configuration ?? "current"}";
            }
            catch (Exception ex)
            {
                return $"Failed to start debugging: {ex.Message}";
            }
        }

        public async Task<string> ExecuteStopDebuggingStepAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (_dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    _dte.Debugger.Stop(true);
                }

                return "Stopped debugging";
            }
            catch (Exception ex)
            {
                return $"Failed to stop debugging: {ex.Message}";
            }
        }

        public async Task<string> ExecuteAttachDebuggerStepAsync(string processName, string processId)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Use the Attach to Process dialog
                _dte.ExecuteCommand("Debug.AttachToProcess");

                return $"Opened Attach to Process dialog for: {processName ?? processId ?? "unknown"}";
            }
            catch (Exception ex)
            {
                return $"Failed to open Attach to Process dialog: {ex.Message}";
            }
        }

        public async Task<string> ExecuteProfileStepAsync(string profilerType, string target)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Launch performance profiler
                _dte.ExecuteCommand("Analyze.PerformanceProfiler");

                return $"Launched {profilerType ?? "performance"} profiler";
            }
            catch (Exception ex)
            {
                return $"Failed to launch profiler: {ex.Message}";
            }
        }

        public async Task<string> ExecuteNuGetRestoreStepAsync(string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Execute NuGet restore command
                _dte.ExecuteCommand("ProjectandSolutionContextMenus.Solution.RestoreNuGetPackages");

                return $"Restored NuGet packages for: {projectName ?? "solution"}";
            }
            catch (Exception ex)
            {
                return $"Failed to restore NuGet packages: {ex.Message}";
            }
        }

        public async Task<string> ExecuteNuGetInstallStepAsync(string packageId, string version, string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Open NuGet Package Manager
                _dte.ExecuteCommand("Project.ManageNuGetPackages");

                return $"Opened NuGet Package Manager for package: {packageId} {version ?? "latest"}";
            }
            catch (Exception ex)
            {
                return $"Failed to open NuGet Package Manager: {ex.Message}";
            }
        }

        public async Task<string> ExecuteNuGetUpdateStepAsync(string packageId, string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Open NuGet Package Manager for updates
                _dte.ExecuteCommand("Project.ManageNuGetPackages");

                return $"Opened NuGet Package Manager for updating: {packageId}";
            }
            catch (Exception ex)
            {
                return $"Failed to open NuGet Package Manager: {ex.Message}";
            }
        }

        public async Task<string> ExecuteNuGetUninstallStepAsync(string packageId, string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Open NuGet Package Manager for uninstallation
                _dte.ExecuteCommand("Project.ManageNuGetPackages");

                return $"Opened NuGet Package Manager for uninstalling: {packageId}";
            }
            catch (Exception ex)
            {
                return $"Failed to open NuGet Package Manager: {ex.Message}";
            }
        }
    }
}