using System;
using System.Collections.Generic;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    /// <summary>
    /// Callback interface supplied by the Extension layer so DiffApplyService can open
    /// documents and make edits in the VS text-buffer without taking a direct
    /// dependency on VS SDK types inside the App layer.
    /// </summary>
    public interface IDiffEditorHost
    {
        /// <summary>Opens the document at <paramref name="absolutePath"/> if not already open.</summary>
        void OpenDocument(string absolutePath);

        /// <summary>Replaces the full text of an open document buffer.</summary>
        /// <returns>true if the replacement succeeded.</returns>
        bool ReplaceDocumentContent(string absolutePath, string newContent);

        /// <summary>Resolves an extension-relative or workspace-relative path to an absolute path.</summary>
        string ResolveAbsolutePath(string relativePath);
    }

    /// <summary>
    /// Applies a list of <see cref="KiloFileDiff"/> objects to the workspace.
    /// Uses the "after" content from the diff as the new file content, which matches
    /// what the Kilo backend delivers (full new content rather than a line-level patch).
    ///
    /// Errors are collected and returned; partial successes are allowed.
    /// </summary>
    public sealed class DiffApplyService
    {
        private readonly IDiffEditorHost _host;

        public DiffApplyService(IDiffEditorHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public IReadOnlyList<DiffApplyResult> ApplyDiffs(IReadOnlyList<KiloFileDiff> diffs, string workspaceRoot)
        {
            if (diffs == null) throw new ArgumentNullException(nameof(diffs));

            var results = new List<DiffApplyResult>();

            foreach (var diff in diffs)
            {
                results.Add(ApplyOne(diff, workspaceRoot));
            }

            return results;
        }

        private DiffApplyResult ApplyOne(KiloFileDiff diff, string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(diff.FilePath))
                return DiffApplyResult.Fail(diff.FilePath, "File path is empty.");

            if (string.Equals(diff.Status, "deleted", StringComparison.OrdinalIgnoreCase))
                return DiffApplyResult.Skipped(diff.FilePath, "Deleted files are not applied automatically.");

            if (string.IsNullOrEmpty(diff.After))
                return DiffApplyResult.Skipped(diff.FilePath, "No 'after' content provided.");

            string absolutePath;
            try
            {
                absolutePath = _host.ResolveAbsolutePath(diff.FilePath);
                if (string.IsNullOrWhiteSpace(absolutePath))
                    absolutePath = System.IO.Path.IsPathRooted(diff.FilePath)
                        ? diff.FilePath
                        : System.IO.Path.Combine(workspaceRoot, diff.FilePath);
            }
            catch (Exception ex)
            {
                return DiffApplyResult.Fail(diff.FilePath, $"Path resolution error: {ex.Message}");
            }

            try
            {
                _host.OpenDocument(absolutePath);
                var ok = _host.ReplaceDocumentContent(absolutePath, diff.After);
                return ok
                    ? DiffApplyResult.Ok(absolutePath)
                    : DiffApplyResult.Fail(absolutePath, "ReplaceDocumentContent returned false.");
            }
            catch (Exception ex)
            {
                return DiffApplyResult.Fail(absolutePath, ex.Message);
            }
        }
    }

    public sealed class DiffApplyResult
    {
        public string FilePath { get; }
        public bool IsSuccess { get; }
        public bool IsSkipped { get; }
        public string Message { get; }

        private DiffApplyResult(string filePath, bool success, bool skipped, string message)
        {
            FilePath = filePath ?? string.Empty;
            IsSuccess = success;
            IsSkipped = skipped;
            Message = message ?? string.Empty;
        }

        public static DiffApplyResult Ok(string filePath) => new DiffApplyResult(filePath, true, false, "Applied.");
        public static DiffApplyResult Fail(string filePath, string reason) => new DiffApplyResult(filePath, false, false, reason);
        public static DiffApplyResult Skipped(string filePath, string reason) => new DiffApplyResult(filePath, false, true, reason);
    }
}
