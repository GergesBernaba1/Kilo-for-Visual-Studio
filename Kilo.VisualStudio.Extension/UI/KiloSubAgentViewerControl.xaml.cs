using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloSubAgentViewerControl : UserControl
    {
        private readonly List<SubAgentDisplayItem> _agents = new List<SubAgentDisplayItem>();

        public KiloSubAgentViewerControl()
        {
            InitializeComponent();
            SubAgentListBox.ItemsSource = _agents;
            LoadSubAgents();
        }

        private void LoadSubAgents()
        {
            // TODO: Wire up to SubAgentVisualizationService when available
            // For now, show placeholder data
            _agents.Clear();
            _agents.Add(new SubAgentDisplayItem
            {
                AgentId = 1,
                Name = "Code Review Agent",
                Task = "Reviewing authentication logic...",
                Status = "Running",
                Progress = 65
            });
            _agents.Add(new SubAgentDisplayItem
            {
                AgentId = 2,
                Name = "Test Generator",
                Task = "Generating unit tests for UserService.cs",
                Status = "Pending",
                Progress = 0
            });
            SubAgentListBox.Items.Refresh();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSubAgents();
        }

        public void AddAgent(SubAgentInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                _agents.Add(new SubAgentDisplayItem
                {
                    AgentId = info.AgentId,
                    Name = info.Name,
                    Task = info.Task,
                    Status = info.Status.ToString(),
                    Progress = info.Progress
                });
                SubAgentListBox.Items.Refresh();
            });
        }

        public void UpdateAgent(int agentId, AgentStatus status, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var agent in _agents)
                {
                    if (agent.AgentId == agentId)
                    {
                        agent.Status = status.ToString();
                        agent.Progress = progress;
                        break;
                    }
                }
                SubAgentListBox.Items.Refresh();
            });
        }

        public void AppendOutput(string output)
        {
            Dispatcher.Invoke(() =>
            {
                OutputLogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {output}");
                if (OutputLogBox.Items.Count > 100)
                    OutputLogBox.Items.RemoveAt(0);
            });
        }

        public class SubAgentDisplayItem
        {
            public int AgentId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Task { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending";
            public int Progress { get; set; }
        }
    }
}
