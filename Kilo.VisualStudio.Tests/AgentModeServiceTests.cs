using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Extension;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;

namespace Kilo.VisualStudio.Tests
{
    public class AgentModeServiceTests
    {
        [Fact]
        public void Constructor_InitializesBuiltInModes()
        {
            // Arrange & Act
            var service = new AgentModeService(Path.GetTempPath());

            // Assert
            Assert.Equal(AgentMode.Default, service.CurrentMode);
            var modeDef = service.CurrentModeDefinition;
            Assert.Equal("default", modeDef.Id);
            Assert.Equal("Default", modeDef.Name);
            Assert.Equal("🤖", modeDef.Icon);
        }

        [Fact]
        public void CycleMode_CyclesThroughBuiltInModes()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());
            var expectedModes = new[]
            {
                AgentMode.Default,
                AgentMode.Architect,
                AgentMode.Coder,
                AgentMode.Debugger,
                AgentMode.Reviewer,
                AgentMode.Optimizer,
                AgentMode.Tester,
                AgentMode.Documenter
            };

            // Act & Assert
            for (int i = 0; i < expectedModes.Length; i++)
            {
                Assert.Equal(expectedModes[i], service.CurrentMode);
                service.CycleMode();
            }

            // Should cycle back to Default
            Assert.Equal(AgentMode.Default, service.CurrentMode);
        }

        [Fact]
        public void SetMode_ByEnum_SetsCorrectMode()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());

            // Act
            service.SetMode(AgentMode.Architect);

            // Assert
            Assert.Equal(AgentMode.Architect, service.CurrentMode);
            Assert.Equal("Architect", service.CurrentModeDefinition.Name);
            Assert.Equal("🏗️", service.CurrentModeDefinition.Icon);
        }

        [Fact]
        public void SetMode_ByName_SetsCorrectMode()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());

            // Act
            service.SetMode("Coder");

            // Assert
            Assert.Equal(AgentMode.Coder, service.CurrentMode);
            Assert.Equal("Coder", service.CurrentModeDefinition.Name);
            Assert.Equal("💻", service.CurrentModeDefinition.Icon);
        }

        [Fact]
        public void GetAvailableModes_ReturnsAllBuiltInModes()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());

            // Act
            var modes = service.GetAvailableModes();

            // Assert
            Assert.Equal(8, modes.Count);
            Assert.Contains(AgentMode.Default, modes);
            Assert.Contains(AgentMode.Architect, modes);
            Assert.Contains(AgentMode.Coder, modes);
            Assert.Contains(AgentMode.Debugger, modes);
            Assert.Contains(AgentMode.Reviewer, modes);
            Assert.Contains(AgentMode.Optimizer, modes);
            Assert.Contains(AgentMode.Tester, modes);
            Assert.Contains(AgentMode.Documenter, modes);
        }

        [Fact]
        public void ModeChanged_Event_FiresWhenModeChanges()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());
            AgentMode? changedMode = null;
            service.ModeChanged += (sender, mode) => changedMode = mode;

            // Act
            service.SetMode(AgentMode.Debugger);

            // Assert
            Assert.Equal(AgentMode.Debugger, changedMode);
        }

        [Fact]
        public async Task McpHubService_LoadsAndRestartsServers()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var mcphub = new McpHubService(tempPath);
            var available = await mcphub.GetAvailableServersAsync();
            Assert.Contains(available, s => s.Id == "nuget");
            Assert.Contains(available, s => s.Id == "msbuild");

            var server = available.First(s => s.Id == "nuget");
            mcphub.AddServer(new McpServerConfig
            {
                Id = "nuget-test",
                Name = "NuGet Test",
                Description = "Test custom nuget server",
                Command = server.Command,
                Args = server.Args,
                Enabled = true
            });

            var addedServer = mcphub.Servers.FirstOrDefault(s => s.Name == "NuGet Test");
            Assert.NotNull(addedServer);
            Assert.Equal("Running", addedServer.Status);

            var state = await mcphub.RestartServerAsync(addedServer!.Id);
            Assert.True(state == "Running" || state == "Disabled" || state == "Healthy");

            Directory.Delete(tempPath, true);
        }

        [Fact]
        public void BuiltInModes_HaveCorrectProperties()
        {
            // Arrange
            var service = new AgentModeService(Path.GetTempPath());

            // Act
            service.SetMode(AgentMode.Reviewer);
            var reviewerMode = service.CurrentModeDefinition;

            // Assert
            Assert.Equal("reviewer", reviewerMode.Id);
            Assert.Equal("Reviewer", reviewerMode.Name);
            Assert.Equal("🔍", reviewerMode.Icon);
            Assert.Equal("#FFC107", reviewerMode.Color);
            Assert.True(reviewerMode.IsBuiltIn);
            Assert.Contains("read", reviewerMode.AllowedTools);
            Assert.Contains("grep", reviewerMode.AllowedTools);
            Assert.Contains("analyze", reviewerMode.AllowedTools);
            Assert.Contains("review", reviewerMode.AllowedTools);
        }

        [Fact]
        public void LoadCustomModes_LoadsFromJsonFile()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var customModesFile = Path.Combine(tempDir, ".kilo", "agent_modes.json");
            Directory.CreateDirectory(Path.GetDirectoryName(customModesFile)!);

            var customModesJson = @"{
                ""test-mode"": {
                    ""id"": ""test-mode"",
                    ""name"": ""Test Mode"",
                    ""description"": ""A test custom mode"",
                    ""systemPrompt"": ""You are in test mode"",
                    ""allowedTools"": [""read"", ""write""],
                    ""autoApproveTools"": false,
                    ""icon"": ""🧪"",
                    ""color"": ""#FF0000"",
                    ""isBuiltIn"": false
                }
            }";

            File.WriteAllText(customModesFile, customModesJson);

            // Act
            var service = new AgentModeService(tempDir);

            // Assert
            var customMode = service.GetCustomModeDefinition("test-mode");
            Assert.NotNull(customMode);
            Assert.Equal("Test Mode", customMode!.Name);
            Assert.Equal("🧪", customMode.Icon);
            Assert.False(customMode.IsBuiltIn);

            // Cleanup
            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task McpHubService_QueryRemoteCommunityServersAsync_FallbacksToLocalCache()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(Path.Combine(tempPath, ".kilo"));

            var cache = new[]
            {
                new McpServerConfig
                {
                    Id = "community-1",
                    Name = "Community 1",
                    Description = "cached community server",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-fake" },
                    Enabled = true,
                    Score = 4.2,
                    Tags = new List<string> { "community", "database" },
                    DocumentationUrl = "https://example.org/docs/community1"
                }
            };

            File.WriteAllText(Path.Combine(tempPath, ".kilo", "mcp_community_cache.json"),
                System.Text.Json.JsonSerializer.Serialize(cache, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            var hub = new McpHubService(tempPath);
            var result = await hub.QueryRemoteCommunityServersAsync("http://127.0.0.1:1/invalid");

            Assert.Single(result);
            Assert.Contains(hub.Servers, s => s.Id == "community-1");
            Assert.Equal(4.2, hub.Servers.First(s => s.Id == "community-1").Score);

            Directory.Delete(tempPath, true);
        }

        [Fact]
        public async Task McpHubService_HealthCheckTimer_TriggersPoorHealthEvent()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var hub = new McpHubService(tempPath);
            hub.AutoModeThreshold = McpAutoModeThreshold.Low;

            hub.AddServer(new McpServerConfig
            {
                Id = "unhealthy-1",
                Name = "Unhealthy Server",
                Description = "No args so unhealthy",
                Command = "npx",
                Args = new List<string>(),
                Enabled = true
            });

            var addedServer = hub.Servers.Single(s => s.Name == "Unhealthy Server");

            var triggered = false;
            hub.PoorHealthDetected += (_, __) => triggered = true;

            var method = typeof(McpHubService).GetMethod("HealthCheckTimer_Elapsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(hub, new object?[] { null, null });

            await Task.Delay(1200);

            Assert.True(triggered, "Poor health event should be raised when server is unhealthy.");
            Assert.Contains(hub.HealthLog, l => l.ServerId == addedServer.Id && l.Message.IndexOf("unhealthy", StringComparison.OrdinalIgnoreCase) >= 0);

            Directory.Delete(tempPath, true);
        }

        [Fact]
        public async Task McpHubService_SearchServers_FindsEntries_ByMetadataFields()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var hub = new McpHubService(tempPath);
            hub.AddServer(new McpServerConfig
            {
                Id = "search-test",
                Name = "Search Test",
                Description = "Server used for query testing",
                Command = "npx",
                Args = new List<string> { "-y", "@modelcontextprotocol/server-test" },
                Enabled = true,
                Score = 8.9,
                Tags = new List<string> { "search", "test" },
                DocumentationUrl = "https://papertrail.dev/docs/search-test"
            });

            var byName = hub.SearchServers("Search Test");
            Assert.Single(byName);

            var byDescription = hub.SearchServers("query testing");
            Assert.Single(byDescription);

            var noResult = hub.SearchServers("non-existing");
            Assert.Empty(noResult);

            Directory.Delete(tempPath, true);
        }

        [Fact]
        public void KiloSuggestedActionsSource_RaisesSuggestedActionsChanged_WhenSelectionChanged()
        {
            var mockSelection = new Mock<ITextSelection>();
            var mockTextView = new Mock<ITextView>();
            var mockTextBuffer = new Mock<ITextBuffer>();

            mockTextView.Setup(tv => tv.Selection).Returns(mockSelection.Object);

            var source = new KiloSuggestedActionsSource(mockTextView.Object, mockTextBuffer.Object);
            var raised = false;
            source.SuggestedActionsChanged += (_, __) => raised = true;

            mockSelection.Raise(x => x.SelectionChanged += null!, EventArgs.Empty);

            Assert.True(raised);

            source.Dispose();
        }
    }
}