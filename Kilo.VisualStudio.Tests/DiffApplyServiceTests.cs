using System;
using System.Collections.Generic;
using System.IO;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Xunit;

namespace Kilo.VisualStudio.Tests
{
    public class DiffApplyServiceTests
    {
        private sealed class FakeEditorHost : IDiffEditorHost
        {
            public readonly Dictionary<string, string> Written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Opened = new List<string>();
            public bool ShouldFail { get; set; }

            public void OpenDocument(string absolutePath) => Opened.Add(absolutePath);

            public bool ReplaceDocumentContent(string absolutePath, string newContent)
            {
                if (ShouldFail) return false;
                Written[absolutePath] = newContent;
                return true;
            }

            public string ResolveAbsolutePath(string filePath) =>
                Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(filePath);
        }

        [Fact]
        public void ApplyDiffs_EmptyList_ReturnsEmptyResults()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);
            var results = service.ApplyDiffs(new KiloFileDiff[0], "C:\\workspace");
            Assert.Empty(results);
        }

        [Fact]
        public void ApplyDiffs_SingleModifiedFile_AppliesAfterContent()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff
                {
                    FilePath = "C:\\workspace\\Program.cs",
                    Before = "// old code",
                    After = "// new code",
                    Status = "modified",
                    Additions = 1,
                    Deletions = 1
                }
            };

            var results = service.ApplyDiffs(diffs, "C:\\workspace");

            Assert.Single(results);
            Assert.True(results[0].IsSuccess);
            Assert.Equal("// new code", host.Written["C:\\workspace\\Program.cs"]);
        }

        [Fact]
        public void ApplyDiffs_DeletedFile_IsSkipped()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff
                {
                    FilePath = "C:\\workspace\\Old.cs",
                    Before = "// old",
                    After = string.Empty,
                    Status = "deleted"
                }
            };

            var results = service.ApplyDiffs(diffs, "C:\\workspace");

            Assert.Single(results);
            Assert.True(results[0].IsSkipped);
            Assert.Empty(host.Written);
        }

        [Fact]
        public void ApplyDiffs_EmptyFilePath_ReturnsFail()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff
                {
                    FilePath = string.Empty,
                    After = "// content",
                    Status = "added"
                }
            };

            var results = service.ApplyDiffs(diffs, "C:\\workspace");

            Assert.Single(results);
            Assert.False(results[0].IsSuccess);
            Assert.False(results[0].IsSkipped);
        }

        [Fact]
        public void ApplyDiffs_EmptyAfterContent_IsSkipped()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff
                {
                    FilePath = "C:\\workspace\\Untouched.cs",
                    Before = "// before",
                    After = string.Empty,
                    Status = "modified"
                }
            };

            var results = service.ApplyDiffs(diffs, "C:\\workspace");

            Assert.Single(results);
            Assert.True(results[0].IsSkipped);
            Assert.Empty(host.Written);
        }

        [Fact]
        public void ApplyDiffs_HostFails_ReturnsFail()
        {
            var host = new FakeEditorHost { ShouldFail = true };
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff
                {
                    FilePath = "C:\\workspace\\Service.cs",
                    After = "// updated",
                    Status = "modified"
                }
            };

            var results = service.ApplyDiffs(diffs, "C:\\workspace");

            Assert.Single(results);
            Assert.False(results[0].IsSuccess);
        }

        [Fact]
        public void ApplyDiffs_MultipleFiles_AppliesAllSuccessfully()
        {
            var host = new FakeEditorHost();
            var service = new DiffApplyService(host);

            var diffs = new[]
            {
                new KiloFileDiff { FilePath = "C:\\ws\\A.cs", After = "// A", Status = "modified" },
                new KiloFileDiff { FilePath = "C:\\ws\\B.cs", After = "// B", Status = "added" },
                new KiloFileDiff { FilePath = "C:\\ws\\C.cs", After = "// C", Status = "modified" }
            };

            var results = service.ApplyDiffs(diffs, "C:\\ws");

            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.IsSuccess));
            Assert.Equal("// A", host.Written["C:\\ws\\A.cs"]);
            Assert.Equal("// B", host.Written["C:\\ws\\B.cs"]);
            Assert.Equal("// C", host.Written["C:\\ws\\C.cs"]);
        }
    }
}
