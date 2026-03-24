using System;
using System.IO;
using System.Linq;
using Kilo.VisualStudio.App.Services;
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
    }
}