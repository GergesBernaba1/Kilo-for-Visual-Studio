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
    }
}
