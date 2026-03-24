using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Contracts.Services
{
    public interface IVSAutomationExecutor
    {
        Task<string> ExecuteRunCommandStepAsync(string command);
        Task<string> ExecuteBuildStepAsync(string projectName);
        Task<string> ExecuteTestStepAsync(string testFilter);
        Task<string> ExecuteStartDebuggingStepAsync(string configuration, string projectName);
        Task<string> ExecuteStopDebuggingStepAsync();
        Task<string> ExecuteAttachDebuggerStepAsync(string processName, string processId);
        Task<string> ExecuteProfileStepAsync(string profilerType, string target);
        Task<string> ExecuteNuGetRestoreStepAsync(string projectName);
        Task<string> ExecuteNuGetInstallStepAsync(string packageId, string version, string projectName);
        Task<string> ExecuteNuGetUpdateStepAsync(string packageId, string projectName);
        Task<string> ExecuteNuGetUninstallStepAsync(string packageId, string projectName);
    }
}