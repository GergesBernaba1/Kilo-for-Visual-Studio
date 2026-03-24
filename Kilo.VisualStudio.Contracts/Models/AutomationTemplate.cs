using System.Collections.Generic;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class AutomationTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<AutomationStep> Steps { get; set; } = new List<AutomationStep>();
        public bool IsAutoMode { get; set; } = false; // Execute without user prompts
        public string Category { get; set; } = string.Empty; // e.g., "Testing", "Refactoring", "Build"
    }

    public class AutomationStep
    {
        public AutomationStepType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public enum AutomationStepType
    {
        RunCommand,
        BuildProject,
        RunTests,
        DebugApplication,
        StartDebugging,
        StopDebugging,
        AttachDebugger,
        ProfileApplication,
        GenerateCode,
        RefactorCode,
        ApplyDiff,
        OpenFile,
        SaveFile,
        ShowMessage,
        WaitForUserInput,
        ConditionalBranch,
        // NuGet operations
        NuGetRestore,
        NuGetInstall,
        NuGetUpdate,
        NuGetUninstall,
        // Macro operations
        MacroDelay,
        MacroMouseClick,
        MacroKeyboardInput
    }
}