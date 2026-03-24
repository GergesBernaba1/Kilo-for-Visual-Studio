using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public enum AgentMode
    {
        Default,
        Architect,
        Coder,
        Debugger,
        Reviewer,
        Optimizer,
        Tester,
        Documenter,
        Custom
    }

    public class AgentModeDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string[] AllowedTools { get; set; } = Array.Empty<string>();
        public bool AutoApproveTools { get; set; } = false;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = "#007ACC";
        public Dictionary<string, object> ContextRules { get; set; } = new Dictionary<string, object>();
        public bool IsBuiltIn { get; set; } = true;
    }

    public class AgentModeService
    {
        private readonly string _workspaceRoot;
        private readonly string _customModesFilePath;
        private Dictionary<AgentMode, AgentModeDefinition> _modeDefinitions;
        private Dictionary<string, AgentModeDefinition> _customModeDefinitions;
        private AgentMode _currentMode = AgentMode.Default;
        private Action<string>? _saveModeCallback;

        public event EventHandler<AgentMode>? ModeChanged;

        public AgentModeService(string workspaceRoot, Action<string>? saveModeCallback = null)
        {
            _workspaceRoot = workspaceRoot;
            _customModesFilePath = Path.Combine(workspaceRoot, ".kilo", "agent_modes.json");
            _saveModeCallback = saveModeCallback;
            _modeDefinitions = new Dictionary<AgentMode, AgentModeDefinition>();
            _customModeDefinitions = new Dictionary<string, AgentModeDefinition>();
            InitializeBuiltInModes();
            LoadCustomModes();
        }

        private void InitializeBuiltInModes()
        {
            _modeDefinitions = new Dictionary<AgentMode, AgentModeDefinition>
            {
                {
                    AgentMode.Default, new AgentModeDefinition
                    {
                        Id = "default",
                        Name = "Default",
                        Description = "General-purpose coding assistant",
                        SystemPrompt = "You are Kilo, a helpful AI coding assistant.",
                        AllowedTools = new[] { "read", "edit", "write", "bash", "glob", "grep" },
                        AutoApproveTools = false,
                        Icon = "🤖",
                        Color = "#007ACC",
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Architect, new AgentModeDefinition
                    {
                        Id = "architect",
                        Name = "Architect",
                        Description = "Focuses on code design, architecture patterns, and high-level recommendations",
                        SystemPrompt = "You are Kilo-Architect, specialized in software architecture and design patterns. Provide high-level architectural guidance and design recommendations.",
                        AllowedTools = new[] { "read", "glob", "grep", "architect" },
                        AutoApproveTools = true,
                        Icon = "🏗️",
                        Color = "#8A2BE2",
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Coder, new AgentModeDefinition
                    {
                        Id = "coder",
                        Name = "Coder",
                        Description = "Focuses on code implementation, refactoring, and bug fixes",
                        SystemPrompt = "You are Kilo-Coder, specialized in code implementation and refactoring. Write clean, efficient code and suggest improvements.",
                        AllowedTools = new[] { "read", "edit", "write", "bash" },
                        AutoApproveTools = false,
                        Icon = "💻",
                        Color = "#28A745",
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Debugger, new AgentModeDefinition
                    {
                        Id = "debugger",
                        Name = "Debugger",
                        Description = "Focuses on debugging, error analysis, and troubleshooting",
                        SystemPrompt = "You are Kilo-Debugger, specialized in debugging and troubleshooting. Analyze errors and provide step-by-step debugging guidance.",
                        AllowedTools = new[] { "read", "bash", "debug", "inspect" },
                        AutoApproveTools = true,
                        Icon = "🐛",
                        Color = "#DC3545",
                        ContextRules = new Dictionary<string, object>
                        {
                            { "autoActivateOnBreakpoint", true },
                            { "autoActivateOnException", true }
                        },
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Reviewer, new AgentModeDefinition
                    {
                        Id = "reviewer",
                        Name = "Reviewer",
                        Description = "Focuses on code reviews, quality analysis, and best practices",
                        SystemPrompt = "You are Kilo-Reviewer, specialized in code review and quality assurance. Analyze code for bugs, security issues, and adherence to best practices.",
                        AllowedTools = new[] { "read", "grep", "analyze", "review" },
                        AutoApproveTools = false,
                        Icon = "🔍",
                        Color = "#FFC107",
                        ContextRules = new Dictionary<string, object>
                        {
                            { "autoActivateOnCommit", false },
                            { "autoActivateOnPullRequest", true }
                        },
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Optimizer, new AgentModeDefinition
                    {
                        Id = "optimizer",
                        Name = "Optimizer",
                        Description = "Focuses on performance optimization and efficiency improvements",
                        SystemPrompt = "You are Kilo-Optimizer, specialized in performance analysis and optimization. Identify bottlenecks and suggest performance improvements.",
                        AllowedTools = new[] { "read", "profile", "analyze", "optimize" },
                        AutoApproveTools = false,
                        Icon = "⚡",
                        Color = "#17A2B8",
                        ContextRules = new Dictionary<string, object>
                        {
                            { "autoActivateOnPerformanceIssue", true }
                        },
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Tester, new AgentModeDefinition
                    {
                        Id = "tester",
                        Name = "Tester",
                        Description = "Focuses on test creation, test coverage, and quality assurance",
                        SystemPrompt = "You are Kilo-Tester, specialized in testing and quality assurance. Create comprehensive tests and ensure code reliability.",
                        AllowedTools = new[] { "read", "write", "test", "analyze" },
                        AutoApproveTools = false,
                        Icon = "🧪",
                        Color = "#6F42C1",
                        ContextRules = new Dictionary<string, object>
                        {
                            { "autoActivateOnTestFailure", true }
                        },
                        IsBuiltIn = true
                    }
                },
                {
                    AgentMode.Documenter, new AgentModeDefinition
                    {
                        Id = "documenter",
                        Name = "Documenter",
                        Description = "Focuses on documentation, comments, and knowledge sharing",
                        SystemPrompt = "You are Kilo-Documenter, specialized in creating clear documentation and code comments. Help make code more maintainable through better documentation.",
                        AllowedTools = new[] { "read", "write", "document" },
                        AutoApproveTools = true,
                        Icon = "📚",
                        Color = "#20C997",
                        IsBuiltIn = true
                    }
                }
            };
        }

        public AgentMode CurrentMode => _currentMode;
        public AgentModeDefinition CurrentModeDefinition => GetModeDefinition(_currentMode);

        public void SetMode(AgentMode mode)
        {
            if (_modeDefinitions.ContainsKey(mode) || mode == AgentMode.Custom)
            {
                _currentMode = mode;
                SaveCurrentModeToSettings();
                ModeChanged?.Invoke(this, mode);
            }
        }

        public void SetMode(string modeName)
        {
            // Check built-in modes
            foreach (var kvp in _modeDefinitions)
            {
                if (string.Equals(kvp.Value.Name, modeName, StringComparison.OrdinalIgnoreCase))
                {
                    _currentMode = kvp.Key;
                    SaveCurrentModeToSettings();
                    ModeChanged?.Invoke(this, kvp.Key);
                    return;
                }
            }

            // Check custom modes
            if (_customModeDefinitions.TryGetValue(modeName, out var customMode))
            {
                _currentMode = AgentMode.Custom;
                SaveCurrentModeToSettings();
                ModeChanged?.Invoke(this, AgentMode.Custom);
                return;
            }

            _currentMode = AgentMode.Default;
            SaveCurrentModeToSettings();
            ModeChanged?.Invoke(this, AgentMode.Default);
        }

        public void SetCustomMode(string modeId)
        {
            if (_customModeDefinitions.ContainsKey(modeId))
            {
                _currentMode = AgentMode.Custom;
                SaveCurrentModeToSettings();
                ModeChanged?.Invoke(this, AgentMode.Custom);
            }
        }

        public void CycleMode()
        {
            var allModes = new List<string>();
            allModes.AddRange(_modeDefinitions.Keys.Select(m => m.ToString()));
            allModes.AddRange(_customModeDefinitions.Keys);

            var currentModeName = _currentMode == AgentMode.Custom ?
                _customModeDefinitions.Keys.FirstOrDefault() ?? "Default" :
                _currentMode.ToString();

            var currentIndex = allModes.IndexOf(currentModeName);
            if (currentIndex == -1) currentIndex = 0;

            var nextIndex = (currentIndex + 1) % allModes.Count;
            var nextModeName = allModes[nextIndex];

            // Check if it's a built-in mode
            if (Enum.TryParse<AgentMode>(nextModeName, out var builtInMode))
            {
                _currentMode = builtInMode;
            }
            else
            {
                // It's a custom mode
                _currentMode = AgentMode.Custom;
            }

            SaveCurrentModeToSettings();
            ModeChanged?.Invoke(this, _currentMode);
        }

        public IReadOnlyList<AgentMode> GetAvailableModes()
        {
            return new List<AgentMode>(_modeDefinitions.Keys);
        }

        public IReadOnlyList<AgentModeDefinition> GetAllModeDefinitions()
        {
            var allModes = new List<AgentModeDefinition>(_modeDefinitions.Values);
            allModes.AddRange(_customModeDefinitions.Values);
            return allModes;
        }

        public AgentModeDefinition GetModeDefinition(AgentMode mode)
        {
            if (mode == AgentMode.Custom)
            {
                // Return the first custom mode or default
                return _customModeDefinitions.Values.FirstOrDefault() ?? _modeDefinitions[AgentMode.Default];
            }

            return _modeDefinitions.TryGetValue(mode, out var definition) ? definition : _modeDefinitions[AgentMode.Default];
        }

        public AgentModeDefinition? GetCustomModeDefinition(string modeId)
        {
            return _customModeDefinitions.TryGetValue(modeId, out var definition) ? definition : null;
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

        public void AddCustomMode(AgentModeDefinition modeDefinition)
        {
            modeDefinition.Id = Guid.NewGuid().ToString("N");
            modeDefinition.IsBuiltIn = false;
            _customModeDefinitions[modeDefinition.Id] = modeDefinition;
            SaveCustomModes();
        }

        public void UpdateCustomMode(AgentModeDefinition modeDefinition)
        {
            if (_customModeDefinitions.ContainsKey(modeDefinition.Id))
            {
                modeDefinition.IsBuiltIn = false;
                _customModeDefinitions[modeDefinition.Id] = modeDefinition;
                SaveCustomModes();
            }
        }

        public void RemoveCustomMode(string modeId)
        {
            if (_customModeDefinitions.Remove(modeId))
            {
                SaveCustomModes();
            }
        }

        public void AutoSwitchModeBasedOnContext(string contextType, object contextData = null)
        {
            switch (contextType.ToLowerInvariant())
            {
                case "breakpoint":
                case "exception":
                case "debugging":
                    if (GetModeDefinition(AgentMode.Debugger).ContextRules.TryGetValue("autoActivateOnBreakpoint", out var activateOnBreakpoint) &&
                        activateOnBreakpoint is bool boolValue && boolValue)
                    {
                        SetMode(AgentMode.Debugger);
                    }
                    break;

                case "pullrequest":
                case "review":
                    if (GetModeDefinition(AgentMode.Reviewer).ContextRules.TryGetValue("autoActivateOnPullRequest", out var activateOnPR) &&
                        activateOnPR is bool prValue && prValue)
                    {
                        SetMode(AgentMode.Reviewer);
                    }
                    break;

                case "performance":
                case "profiling":
                    if (GetModeDefinition(AgentMode.Optimizer).ContextRules.TryGetValue("autoActivateOnPerformanceIssue", out var activateOnPerf) &&
                        activateOnPerf is bool perfValue && perfValue)
                    {
                        SetMode(AgentMode.Optimizer);
                    }
                    break;

                case "testfailure":
                    if (GetModeDefinition(AgentMode.Tester).ContextRules.TryGetValue("autoActivateOnTestFailure", out var activateOnTest) &&
                        activateOnTest is bool testValue && testValue)
                    {
                        SetMode(AgentMode.Tester);
                    }
                    break;
            }
        }

        public void SetCurrentModeFromSettings(string? modeName)
        {
            if (!string.IsNullOrEmpty(modeName))
            {
                SetMode(modeName);
            }
        }

        private void SaveCurrentModeToSettings()
        {
            _saveModeCallback?.Invoke(CurrentModeDefinition.Name);
        }

        private void LoadCustomModes()
        {
            try
            {
                if (File.Exists(_customModesFilePath))
                {
                    var json = File.ReadAllText(_customModesFilePath);
                    var customModes = JsonSerializer.Deserialize<Dictionary<string, AgentModeDefinition>>(json);
                    if (customModes != null)
                    {
                        foreach (var kvp in customModes)
                        {
                            kvp.Value.IsBuiltIn = false;
                            _customModeDefinitions[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when loading custom modes
            }
        }

        private void SaveCustomModes()
        {
            try
            {
                var dir = Path.GetDirectoryName(_customModesFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_customModeDefinitions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_customModesFilePath, json);
            }
            catch
            {
                // Ignore errors when saving custom modes
            }
        }
    }
}