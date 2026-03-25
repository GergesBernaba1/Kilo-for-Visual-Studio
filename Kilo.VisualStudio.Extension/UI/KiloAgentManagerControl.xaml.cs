using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloAgentManagerControl : UserControl
    {
        public KiloAgentManagerControl()
        {
            InitializeComponent();
            LoadAgents();
        }

        private void LoadAgents()
        {
            var agentModeService = KiloPackage.AgentModeServiceInstance;
            if (agentModeService != null)
            {
                var modes = new List<AgentDisplayItem>
                {
                    new AgentDisplayItem 
                    { 
                        Id = "default", 
                        Name = "Default", 
                        Description = "General-purpose coding assistant",
                        Icon = "🤖",
                        Color = "#007ACC",
                        Status = agentModeService.CurrentMode == AgentMode.Default ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "architect", 
                        Name = "Architect", 
                        Description = "Focuses on system design and architecture",
                        Icon = "🏗️",
                        Color = "#28A745",
                        Status = agentModeService.CurrentMode == AgentMode.Architect ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "coder", 
                        Name = "Coder", 
                        Description = "Focuses on code generation and editing",
                        Icon = "💻",
                        Color = "#007ACC",
                        Status = agentModeService.CurrentMode == AgentMode.Coder ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "debugger", 
                        Name = "Debugger", 
                        Description = "Focuses on debugging and error fixing",
                        Icon = "🐛",
                        Color = "#DC3545",
                        Status = agentModeService.CurrentMode == AgentMode.Debugger ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "reviewer", 
                        Name = "Reviewer", 
                        Description = "Focuses on code review and quality",
                        Icon = "🔍",
                        Color = "#FFC107",
                        Status = agentModeService.CurrentMode == AgentMode.Reviewer ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "optimizer", 
                        Name = "Optimizer", 
                        Description = "Focuses on performance optimization",
                        Icon = "⚡",
                        Color = "#17A2B8",
                        Status = agentModeService.CurrentMode == AgentMode.Optimizer ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "tester", 
                        Name = "Tester", 
                        Description = "Focuses on testing and QA",
                        Icon = "🧪",
                        Color = "#6F42C1",
                        Status = agentModeService.CurrentMode == AgentMode.Tester ? "Active" : "Available"
                    },
                    new AgentDisplayItem 
                    { 
                        Id = "documenter", 
                        Name = "Documenter", 
                        Description = "Focuses on documentation",
                        Icon = "📚",
                        Color = "#20C997",
                        Status = agentModeService.CurrentMode == AgentMode.Documenter ? "Active" : "Available"
                    }
                };
                AgentListBox.ItemsSource = modes;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAgents();
        }

        private void NewAgentButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Custom agent creation coming soon!", "Kilo Agent Manager", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window?.Close();
        }

        public class AgentDisplayItem
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string Color { get; set; } = "#007ACC";
            public string Status { get; set; } = "Available";
        }
    }
}
