using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class SubAgentVisualizationService
    {
        private readonly List<SubAgentInfo> _activeSubAgents = new List<SubAgentInfo>();
        private int _nextAgentId = 1;

        public event EventHandler<SubAgentInfo>? SubAgentStarted;
        public event EventHandler<SubAgentInfo>? SubAgentCompleted;
        public event EventHandler<string>? SubAgentOutput;
        public event EventHandler? AllSubAgentsCompleted;

        public IReadOnlyList<SubAgentInfo> ActiveSubAgents => _activeSubAgents;

        public SubAgentInfo CreateSubAgent(string name, string task, AgentCapability capability = AgentCapability.General)
        {
            var agent = new SubAgentInfo
            {
                AgentId = _nextAgentId++,
                Name = name,
                Task = task,
                Capability = capability,
                Status = AgentStatus.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _activeSubAgents.Add(agent);
            SubAgentStarted?.Invoke(this, agent);

            return agent;
        }

        public void UpdateStatus(int agentId, AgentStatus status, string? output = null)
        {
            foreach (var agent in _activeSubAgents)
            {
                if (agent.AgentId == agentId)
                {
                    agent.Status = status;
                    agent.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    if (output != null)
                    {
                        agent.Output = output;
                        SubAgentOutput?.Invoke(this, $"[{agent.Name}] {output}");
                    }
                    if (status == AgentStatus.Completed || status == AgentStatus.Failed)
                    {
                        agent.CompletedAtUtc = DateTimeOffset.UtcNow;
                        SubAgentCompleted?.Invoke(this, agent);
                        CheckAllCompleted();
                    }
                    return;
                }
            }
        }

        public void UpdateProgress(int agentId, int progressPercent, string? message = null)
        {
            foreach (var agent in _activeSubAgents)
            {
                if (agent.AgentId == agentId)
                {
                    agent.Progress = progressPercent;
                    agent.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    if (message != null)
                    {
                        agent.Output = message;
                        SubAgentOutput?.Invoke(this, $"[{agent.Name}] {message}");
                    }
                    return;
                }
            }
        }

        public void TerminateSubAgent(int agentId)
        {
            for (int i = 0; i < _activeSubAgents.Count; i++)
            {
                if (_activeSubAgents[i].AgentId == agentId)
                {
                    _activeSubAgents[i].Status = AgentStatus.Terminated;
                    _activeSubAgents[i].CompletedAtUtc = DateTimeOffset.UtcNow;
                    SubAgentCompleted?.Invoke(this, _activeSubAgents[i]);
                    _activeSubAgents.RemoveAt(i);
                    return;
                }
            }
        }

        public void TerminateAll()
        {
            foreach (var agent in _activeSubAgents)
            {
                agent.Status = AgentStatus.Terminated;
                agent.CompletedAtUtc = DateTimeOffset.UtcNow;
            }
            _activeSubAgents.Clear();
            AllSubAgentsCompleted?.Invoke(this, EventArgs.Empty);
        }

        public Task<string> GetVisualizationDataAsync()
        {
            var json = "{\"agents\": [";
            var agentData = new List<string>();

            foreach (var agent in _activeSubAgents)
            {
                agentData.Add($"{{\"id\":{agent.AgentId},\"name\":\"{agent.Name}\",\"status\":\"{agent.Status}\",\"progress\":{agent.Progress}}}");
            }

            json += string.Join(",", agentData) + "]}";
            return Task.FromResult(json);
        }

        private void CheckAllCompleted()
        {
            foreach (var agent in _activeSubAgents)
            {
                if (agent.Status == AgentStatus.Running)
                    return;
            }
            AllSubAgentsCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public class SubAgentInfo
    {
        public int AgentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public AgentCapability Capability { get; set; }
        public AgentStatus Status { get; set; }
        public int Progress { get; set; }
        public string Output { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    public enum AgentCapability
    {
        General,
        CodeReview,
        Testing,
        Refactoring,
        Debugging,
        Documentation,
        Research
    }

    public enum AgentStatus
    {
        Pending,
        Initializing,
        Running,
        WaitingApproval,
        Completed,
        Failed,
        Terminated
    }
}