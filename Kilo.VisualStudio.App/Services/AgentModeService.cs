using System;
using System.Collections.Generic;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public enum AgentMode
    {
        Default,
        Architect,
        Coder,
        Debugger,
        Custom
    }

    public class AgentModeDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string[] AllowedTools { get; set; } = Array.Empty<string>();
        public bool AutoApproveTools { get; set; } = false;
    }

    public class AgentModeService
    {
        private static readonly Dictionary<AgentMode, AgentModeDefinition> ModeDefinitions = new Dictionary<AgentMode, AgentModeDefinition>
        {
            {
                AgentMode.Default, new AgentModeDefinition
                {
                    Name = "Default",
                    Description = "General-purpose coding assistant",
                    SystemPrompt = "You are Kilo, a helpful AI coding assistant.",
                    AllowedTools = new[] { "read", "edit", "write", "bash", "glob", "grep" },
                    AutoApproveTools = false
                }
            },
            {
                AgentMode.Architect, new AgentModeDefinition
                {
                    Name = "Architect",
                    Description = "Focuses on code design, architecture patterns, and high-level recommendations",
                    SystemPrompt = "You are Kilo-Architect, specialized in software architecture and design patterns. Provide high-level architectural guidance and design recommendations.",
                    AllowedTools = new[] { "read", "glob", "grep", "architect" },
                    AutoApproveTools = true
                }
            },
            {
                AgentMode.Coder, new AgentModeDefinition
                {
                    Name = "Coder",
                    Description = "Focuses on code implementation, refactoring, and bug fixes",
                    SystemPrompt = "You are Kilo-Coder, specialized in code implementation and refactoring. Write clean, efficient code and suggest improvements.",
                    AllowedTools = new[] { "read", "edit", "write", "bash" },
                    AutoApproveTools = false
                }
            },
            {
                AgentMode.Debugger, new AgentModeDefinition
                {
                    Name = "Debugger",
                    Description = "Focuses on debugging, error analysis, and troubleshooting",
                    SystemPrompt = "You are Kilo-Debugger, specialized in debugging and troubleshooting. Analyze errors and provide step-by-step debugging guidance.",
                    AllowedTools = new[] { "read", "bash", "debug", "inspect" },
                    AutoApproveTools = true
                }
            }
        };

        private AgentMode _currentMode = AgentMode.Default;

        public AgentMode CurrentMode => _currentMode;
        public AgentModeDefinition CurrentModeDefinition => ModeDefinitions[_currentMode];

        public void SetMode(AgentMode mode)
        {
            if (ModeDefinitions.ContainsKey(mode))
            {
                _currentMode = mode;
            }
        }

        public void SetMode(string modeName)
        {
            foreach (var kvp in ModeDefinitions)
            {
                if (string.Equals(kvp.Value.Name, modeName, StringComparison.OrdinalIgnoreCase))
                {
                    _currentMode = kvp.Key;
                    return;
                }
            }
            _currentMode = AgentMode.Default;
        }

        public void CycleMode()
        {
            var modes = new[] { AgentMode.Default, AgentMode.Architect, AgentMode.Coder, AgentMode.Debugger };
            var currentIndex = Array.IndexOf(modes, _currentMode);
            var nextIndex = (currentIndex + 1) % modes.Length;
            _currentMode = modes[nextIndex];
        }

        public IReadOnlyList<AgentMode> GetAvailableModes()
        {
            return new List<AgentMode>(ModeDefinitions.Keys);
        }

        public AgentModeDefinition GetModeDefinition(AgentMode mode)
        {
            return ModeDefinitions.TryGetValue(mode, out var definition) ? definition : ModeDefinitions[AgentMode.Default];
        }

        public string GetModeDisplayName()
        {
            return $"{CurrentModeDefinition.Name} Mode";
        }

        public string GetModeDescription()
        {
            return CurrentModeDefinition.Description;
        }

        public bool ShouldAutoApproveTool(string toolId)
        {
            return CurrentModeDefinition.AutoApproveTools || 
                   Array.Exists(CurrentModeDefinition.AllowedTools, t => string.Equals(t, toolId, StringComparison.OrdinalIgnoreCase));
        }
    }
}