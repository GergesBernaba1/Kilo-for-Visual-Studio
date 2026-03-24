using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class CustomCommandService
    {
        private readonly string _workspaceRoot;
        private readonly string _commandsFilePath;
        private List<CustomCommand> _commands;

        public event EventHandler? CommandsChanged;

        public CustomCommandService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _commandsFilePath = Path.Combine(workspaceRoot, ".kilo", "custom_commands.json");
            _commands = new List<CustomCommand>();
            LoadCommands();
        }

        public IReadOnlyList<CustomCommand> Commands => _commands;

        public void LoadCommands()
        {
            try
            {
                if (File.Exists(_commandsFilePath))
                {
                    var json = File.ReadAllText(_commandsFilePath);
                    _commands = JsonSerializer.Deserialize<List<CustomCommand>>(json) ?? new List<CustomCommand>();
                }
                else
                {
                    _commands = GetDefaultCommands();
                    SaveCommands();
                }
            }
            catch
            {
                _commands = GetDefaultCommands();
            }
        }

        public void SaveCommands()
        {
            try
            {
                var dir = Path.GetDirectoryName(_commandsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_commands, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_commandsFilePath, json);
            }
            catch { }
        }

        public void AddCommand(CustomCommand command)
        {
            command.Id = Guid.NewGuid().ToString("N");
            _commands.Add(command);
            SaveCommands();
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateCommand(CustomCommand command)
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].Id == command.Id)
                {
                    _commands[i] = command;
                    break;
                }
            }
            SaveCommands();
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveCommand(string commandId)
        {
            _commands.RemoveAll(c => c.Id == commandId);
            SaveCommands();
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        public CustomCommand? GetCommand(string commandId)
        {
            foreach (var command in _commands)
            {
                if (command.Id == commandId)
                    return command;
            }
            return null;
        }

        public Task<string> ExecuteCommandAsync(string commandId, string? selectedText = null, string? activeFilePath = null)
        {
            var command = GetCommand(commandId);
            if (command == null)
                return Task.FromResult("Command not found");

            var prompt = command.PromptTemplate;
            
            if (!string.IsNullOrEmpty(selectedText))
                prompt = prompt.Replace("{selection}", selectedText);
            
            if (!string.IsNullOrEmpty(activeFilePath))
                prompt = prompt.Replace("{file}", activeFilePath);

            return Task.FromResult(prompt);
        }

        private List<CustomCommand> GetDefaultCommands()
        {
            return new List<CustomCommand>
            {
                new CustomCommand
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Explain Code",
                    Description = "Explain the selected code",
                    PromptTemplate = "Explain this code in detail:\n\n{selection}",
                    Keybinding = "Ctrl+Shift+E"
                },
                new CustomCommand
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Refactor Code",
                    Description = "Refactor the selected code",
                    PromptTemplate = "Refactor this code to be more efficient and readable:\n\n{selection}",
                    Keybinding = "Ctrl+Shift+R"
                },
                new CustomCommand
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Write Tests",
                    Description = "Generate unit tests for selected code",
                    PromptTemplate = "Write unit tests for this code:\n\n{selection}",
                    Keybinding = "Ctrl+Shift+T"
                },
                new CustomCommand
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Find Bugs",
                    Description = "Find potential bugs in selected code",
                    PromptTemplate = "Analyze this code for potential bugs and issues:\n\n{selection}",
                    Keybinding = "Ctrl+Shift+B"
                },
                new CustomCommand
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Document Code",
                    Description = "Add documentation to the selected code",
                    PromptTemplate = "Add XML documentation comments to this code:\n\n{selection}",
                    Keybinding = "Ctrl+Shift+D"
                }
            };
        }
    }

    public class CustomCommand
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PromptTemplate { get; set; } = string.Empty;
        public string Keybinding { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
}