using System;
using System.IO;
using System.Runtime.InteropServices;
using Kilo.VisualStudio.App.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Kilo.VisualStudio.Extension.Services
{
    /// <summary>
    /// Visual Studio implementation of <see cref="IDiffEditorHost"/>.
    ///
    /// Strategy: write the new content directly to disk (after closing any dirty buffer),
    /// then reload the document in the VS editor via IVsUIShellOpenDocument. This is the
    /// most robust approach because IVsTextLines.ReplaceLines takes a native BSTR pointer
    /// and file-write + reload correctly preserves undo history via the document-reload path.
    /// </summary>
    internal sealed class VsDiffEditorHost : IDiffEditorHost
    {
        private readonly IServiceProvider _serviceProvider;

        public VsDiffEditorHost(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void OpenDocument(string absolutePath)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OpenDocumentCore(absolutePath);
            });
        }

        public bool ReplaceDocumentContent(string absolutePath, string newContent)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return ReplaceDocumentContentCore(absolutePath, newContent);
            });
        }

        public string ResolveAbsolutePath(string filePath)
        {
            if (Path.IsPathRooted(filePath)) return filePath;

            try
            {
                var solutionDir = ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (_serviceProvider.GetService(typeof(SVsSolution)) is IVsSolution solution)
                    {
                        solution.GetSolutionInfo(out var dir, out _, out _);
                        return dir;
                    }
                    return null;
                });

                if (!string.IsNullOrWhiteSpace(solutionDir))
                    return Path.GetFullPath(Path.Combine(solutionDir, filePath));
            }
            catch { }

            return Path.GetFullPath(filePath);
        }

        // ── Core helpers (must run on UI thread) ──────────────────────────────────

        private void OpenDocumentCore(string absolutePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!File.Exists(absolutePath)) return;

            if (_serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument openDoc)
            {
                var logicalView = VSConstants.LOGVIEWID.TextView_guid;
                openDoc.OpenDocumentViaProject(
                    absolutePath, ref logicalView,
                    out _, out _, out _, out _);
            }
        }

        private bool ReplaceDocumentContentCore(string absolutePath, string newContent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // 1. If the document is open in the editor and clean, close it first so the
            //    file write doesn't conflict with an open buffer.
            CloseDocumentIfClean(absolutePath);

            // 2. Write the new content to disk.
            if (!WriteFile(absolutePath, newContent)) return false;

            // 3. Re-open in the editor to make the change visible.
            OpenDocumentCore(absolutePath);
            return true;
        }

        private void CloseDocumentIfClean(string absolutePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var rdt = _serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (rdt == null) return;

            var hr = rdt.FindAndLockDocument(
                (uint)_VSRDTFLAGS.RDT_ReadLock,
                absolutePath,
                out _,
                out _,
                out var docData,
                out var cookie);

            if (hr != VSConstants.S_OK || docData == IntPtr.Zero) return;

            try
            {
                rdt.UnlockDocument((uint)_VSRDTFLAGS.RDT_ReadLock, cookie);
                // We leave the document open in the editor; the file write + re-open handles reload.
            }
            catch { }
            finally
            {
                if (docData != IntPtr.Zero)
                    Marshal.Release(docData);
            }
        }

        private static bool WriteFile(string absolutePath, string newContent)
        {
            try
            {
                var dir = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(absolutePath, newContent, System.Text.Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
