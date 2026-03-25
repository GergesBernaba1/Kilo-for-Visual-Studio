using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace Kilo.VisualStudio.App.Services
{
    public class SkillsSystemService
    {
        private readonly string _workspaceRoot;
        private readonly string _skillsPath;
        private List<SkillDefinition> _skills = new List<SkillDefinition>();

        public event EventHandler? SkillsChanged;

        public IReadOnlyList<SkillDefinition> Skills => _skills;

        public SkillsSystemService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _skillsPath = Path.Combine(workspaceRoot, ".kilo", "skills.json");
            LoadSkills();
        }

        public void LoadSkills()
        {
            try
            {
                if (File.Exists(_skillsPath))
                {
                    var json = File.ReadAllText(_skillsPath);
                    _skills = JsonSerializer.Deserialize<List<SkillDefinition>>(json) ?? new List<SkillDefinition>();
                }
                else
                {
                    _skills = GetDefaultSkills();
                    SaveSkills();
                }
            }
            catch
            {
                _skills = GetDefaultSkills();
            }
        }

        public void AddSkill(SkillDefinition skill)
        {
            skill.Id = Guid.NewGuid().ToString("N");
            _skills.Add(skill);
            SaveSkills();
            SkillsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateSkill(SkillDefinition skill)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Id == skill.Id)
                {
                    _skills[i] = skill;
                    break;
                }
            }
            SaveSkills();
            SkillsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveSkill(string skillId)
        {
            _skills.RemoveAll(s => s.Id == skillId);
            SaveSkills();
            SkillsChanged?.Invoke(this, EventArgs.Empty);
        }

        public SkillDefinition? GetSkill(string skillId)
        {
            foreach (var skill in _skills)
            {
                if (skill.Id == skillId)
                    return skill;
            }
            return null;
        }

        public async Task<string> ExecuteSkillAsync(string skillId, string input)
        {
            var skill = GetSkill(skillId);
            if (skill == null)
                return "Skill not found";

            return $"Executed skill: {skill.Name} with input: {input.Substring(0, Math.Min(50, input.Length))}...";
        }

        private void SaveSkills()
        {
            try
            {
                var dir = Path.GetDirectoryName(_skillsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_skills, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_skillsPath, json);
            }
            catch { }
        }

        private List<SkillDefinition> GetDefaultSkills()
        {
            return new List<SkillDefinition>
            {
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Code Review",
                    Description = "Perform comprehensive code review",
                    Triggers = new List<string> { "review", "code review", "analyze code" },
                    PromptTemplate = "Perform a thorough code review of the following code:\n\n{input}\n\nFocus on: bugs, performance, security, readability, and best practices.",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Refactor",
                    Description = "Refactor code for better quality",
                    Triggers = new List<string> { "refactor", "improve", "clean up" },
                    PromptTemplate = "Refactor this code to improve quality, readability, and performance:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Write Tests",
                    Description = "Generate unit tests",
                    Triggers = new List<string> { "test", "unittest", "write test" },
                    PromptTemplate = "Write comprehensive unit tests for this code:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Explain",
                    Description = "Explain code functionality",
                    Triggers = new List<string> { "explain", "what does", "how does" },
                    PromptTemplate = "Explain what this code does in detail:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Debug",
                    Description = "Help debug issues",
                    Triggers = new List<string> { "debug", "fix", "error", "bug" },
                    PromptTemplate = "Analyze this code and help identify and fix the bug:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Debug .NET app",
                    Description = "Diagnose .NET crashes and exceptions",
                    Triggers = new List<string> { "dotnet", "debug .net", "exception" },
                    PromptTemplate = "Analyze the following .NET code and suggest debugging steps for crashes and exceptions:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Optimize SQL query",
                    Description = "Suggest SQL query improvements for performance",
                    Triggers = new List<string> { "sql", "query", "performance" },
                    PromptTemplate = "Analyze this SQL query and provide optimized alternative with explanation:\n\n{input}",
                    IsEnabled = true
                },
                new SkillDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Profile performance",
                    Description = "Suggest improvements based on performance metrics",
                    Triggers = new List<string> { "profile", "performance", "slow" },
                    PromptTemplate = "Given these performance metrics, suggest improvements and identify bottlenecks:\n\n{input}",
                    IsEnabled = true
                }
            };
        }
    }

    public class SkillDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Triggers { get; set; } = new List<string>();
        public string PromptTemplate { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
}