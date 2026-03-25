using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kilo.VisualStudio.App.Services;
using Xunit;

namespace Kilo.VisualStudio.Tests
{
    public class AdvancedExperienceTests
    {
        [Fact]
        public void SkillsSystemService_DefaultSkillsIncludeNewSkills()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var skills = new SkillsSystemService(root);

            Assert.Contains(skills.Skills, s => s.Name == "Debug .NET app");
            Assert.Contains(skills.Skills, s => s.Name == "Optimize SQL query");
            Assert.Contains(skills.Skills, s => s.Name == "Profile performance");
        }

        [Fact]
        public async Task TelemetryService_OptInAndLogEvents_Workflow()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var telemetry = new TelemetryService(root);
            Assert.False(telemetry.HasConsent);
            Assert.False(telemetry.IsEnabled);

            telemetry.SetUserConsent(true);
            telemetry.SetTelemetryEnabled(true);

            await telemetry.LogFeatureUsageAsync("test_feature");
            await telemetry.LogErrorAsync("test_error", "message");

            Assert.True(telemetry.HasConsent);
            Assert.True(telemetry.IsEnabled);
        }

        [Fact]
        public void CollaborationService_SharingGeneratesLink()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var collaboration = new CollaborationService(root);
            var link = collaboration.ShareSession("session-123", new[] { "hello" });

            Assert.StartsWith("kilo-share://", link);
            Assert.NotNull(collaboration.LoadSharedSession(link.Replace("kilo-share://", "")));
        }

        [Fact]
        public void AgentModeService_CanSwitchModesAndLoadDefault()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var agentMode = new AgentModeService(root, _ => { });
            agentMode.SetMode(AgentMode.Debugger);

            Assert.Equal(AgentMode.Debugger, agentMode.CurrentMode);
            Assert.Equal("Debugger", agentMode.CurrentModeDefinition.Name);

            agentMode.SetMode("Coder");
            Assert.Equal(AgentMode.Coder, agentMode.CurrentMode);
        }

        [Fact]
        public async Task PerformanceService_RecordsMetricsAndProducesReport()
        {
            var performance = new PerformanceService();
            using (performance.StartMeasure("unit_test"))
            {
                await Task.Delay(15);
            }

            performance.RecordMemory(52428800);
            performance.RecordThreadPoolUsage(3, 100);

            var report = performance.GetPerformanceReport();

            Assert.Contains("unit_test", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Current Memory", report, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RoslynAnalyzerService_GeneratesBasicAnalysisForCsFile()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "Test.cs");
            File.WriteAllText(file, "public class A { public void M() { } } // TODO add tests");

            var analyzer = new RoslynAnalyzerService(root);
            var report = await analyzer.AnalyzeFileAsync(file);

            Assert.Contains("Roslyn-style analysis report", report);
            Assert.Contains("TODO/FIXME markers", report);
        }
    }
}
