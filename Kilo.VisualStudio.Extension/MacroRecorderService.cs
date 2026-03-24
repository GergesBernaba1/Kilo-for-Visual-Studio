using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Extension
{
    public class MacroRecorderService
    {
        private readonly DTE _dte;
        private bool _isRecording;
        private List<AutomationStep> _recordedSteps;
        private DateTime _lastActionTime;
        private readonly TimeSpan _actionTimeout = TimeSpan.FromSeconds(5);

        public event EventHandler<bool>? RecordingStateChanged;
        public event EventHandler<AutomationStep>? StepRecorded;

        public MacroRecorderService(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
            _recordedSteps = new List<AutomationStep>();
            _isRecording = false;
        }

        public bool IsRecording => _isRecording;

        public IReadOnlyList<AutomationStep> RecordedSteps => _recordedSteps.AsReadOnly();

        public void StartRecording()
        {
            if (_isRecording) return;

            _isRecording = true;
            _recordedSteps.Clear();
            _lastActionTime = DateTime.Now;

            // Hook into VS events
            HookEvents();

            RecordingStateChanged?.Invoke(this, true);
        }

        public AutomationTemplate StopRecording(string name, string description = "")
        {
            if (!_isRecording) return null;

            _isRecording = false;

            // Unhook events
            UnhookEvents();

            var template = new AutomationTemplate
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Description = description,
                Category = "Macro",
                Steps = new List<AutomationStep>(_recordedSteps)
            };

            _recordedSteps.Clear();

            RecordingStateChanged?.Invoke(this, false);

            return template;
        }

        public void CancelRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;
            _recordedSteps.Clear();

            UnhookEvents();
            RecordingStateChanged?.Invoke(this, false);
        }

        private void HookEvents()
        {
            // Hook into DTE events for common actions
            _dte.Events.CommandEvents.BeforeExecute += OnCommandExecuted;
            _dte.Events.DocumentEvents.DocumentOpened += OnDocumentOpened;
            _dte.Events.DocumentEvents.DocumentSaved += OnDocumentSaved;
            _dte.Events.SolutionEvents.ProjectAdded += OnProjectAdded;
            _dte.Events.SolutionEvents.ProjectRemoved += OnProjectRemoved;
        }

        private void UnhookEvents()
        {
            _dte.Events.CommandEvents.BeforeExecute -= OnCommandExecuted;
            _dte.Events.DocumentEvents.DocumentOpened -= OnDocumentOpened;
            _dte.Events.DocumentEvents.DocumentSaved -= OnDocumentSaved;
            _dte.Events.SolutionEvents.ProjectAdded -= OnProjectAdded;
            _dte.Events.SolutionEvents.ProjectRemoved -= OnProjectRemoved;
        }

        private void OnCommandExecuted(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            if (!_isRecording) return;

            // Skip certain commands that are too frequent or internal
            if (ShouldSkipCommand(Guid, ID)) return;

            // Check for timeout between actions
            if (DateTime.Now - _lastActionTime > _actionTimeout)
            {
                // Add a delay step
                var delayStep = new AutomationStep
                {
                    Type = AutomationStepType.WaitForUserInput,
                    Description = $"Wait {(DateTime.Now - _lastActionTime).TotalSeconds:F1} seconds",
                    Parameters = new Dictionary<string, string>
                    {
                        { "delay", ((DateTime.Now - _lastActionTime).TotalMilliseconds).ToString() }
                    }
                };
                _recordedSteps.Add(delayStep);
                StepRecorded?.Invoke(this, delayStep);
            }

            var step = new AutomationStep
            {
                Type = AutomationStepType.RunCommand,
                Description = $"Execute command: {GetCommandName(Guid, ID)}",
                Parameters = new Dictionary<string, string>
                {
                    { "command", $"{Guid}:{ID}" },
                    { "customIn", CustomIn?.ToString() ?? "" },
                    { "customOut", CustomOut?.ToString() ?? "" }
                }
            };

            _recordedSteps.Add(step);
            StepRecorded?.Invoke(this, step);
            _lastActionTime = DateTime.Now;
        }

        private void OnDocumentOpened(Document document)
        {
            if (!_isRecording) return;

            var step = new AutomationStep
            {
                Type = AutomationStepType.OpenFile,
                Description = $"Open file: {document.Name}",
                Parameters = new Dictionary<string, string>
                {
                    { "filePath", document.FullName }
                }
            };

            _recordedSteps.Add(step);
            StepRecorded?.Invoke(this, step);
            _lastActionTime = DateTime.Now;
        }

        private void OnDocumentSaved(Document document)
        {
            if (!_isRecording) return;

            var step = new AutomationStep
            {
                Type = AutomationStepType.SaveFile,
                Description = $"Save file: {document.Name}",
                Parameters = new Dictionary<string, string>
                {
                    { "filePath", document.FullName }
                }
            };

            _recordedSteps.Add(step);
            StepRecorded?.Invoke(this, step);
            _lastActionTime = DateTime.Now;
        }

        private void OnProjectAdded(Project project)
        {
            if (!_isRecording) return;

            var step = new AutomationStep
            {
                Type = AutomationStepType.RunCommand,
                Description = $"Add project: {project.Name}",
                Parameters = new Dictionary<string, string>
                {
                    { "command", "Project.Add" },
                    { "projectPath", project.FullName }
                }
            };

            _recordedSteps.Add(step);
            StepRecorded?.Invoke(this, step);
            _lastActionTime = DateTime.Now;
        }

        private void OnProjectRemoved(Project project)
        {
            if (!_isRecording) return;

            var step = new AutomationStep
            {
                Type = AutomationStepType.RunCommand,
                Description = $"Remove project: {project.Name}",
                Parameters = new Dictionary<string, string>
                {
                    { "command", "Project.Remove" },
                    { "projectPath", project.FullName }
                }
            };

            _recordedSteps.Add(step);
            StepRecorded?.Invoke(this, step);
            _lastActionTime = DateTime.Now;
        }

        private bool ShouldSkipCommand(string guid, int id)
        {
            // Skip cursor movement, typing, and other high-frequency commands
            var skipCommands = new[]
            {
                "{GUID_VSStandardCommandSet97}:cmdidCut",
                "{GUID_VSStandardCommandSet97}:cmdidCopy",
                "{GUID_VSStandardCommandSet97}:cmdidPaste",
                "{GUID_VSStandardCommandSet97}:cmdidUndo",
                "{GUID_VSStandardCommandSet97}:cmdidRedo",
                "{GUID_VSStandardCommandSet97}:cmdidSelectAll",
                "{GUID_VSStandardCommandSet97}:cmdidFind",
                "{GUID_VSStandardCommandSet97}:cmdidReplace"
            };

            var commandKey = $"{guid}:{id}";
            return skipCommands.Contains(commandKey);
        }

        private string GetCommandName(string guid, int id)
        {
            try
            {
                var command = _dte.Commands.Item(guid, id);
                return command?.Name ?? $"{guid}:{id}";
            }
            catch
            {
                return $"{guid}:{id}";
            }
        }
    }
}