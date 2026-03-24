using System;
using System.IO;

namespace Kilo.VisualStudio.Integration
{
    public class EditorContextService
    {
        private string? _workspaceRoot;

        public void SetWorkspaceRoot(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public EditorContext GetCurrentEditorContext()
        {
            var context = new EditorContext();
            return context;
        }

        public string GetSelectedText()
        {
            return string.Empty;
        }

        public string GetActiveFilePath()
        {
            return string.Empty;
        }

        public string GetFileContent(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return string.Empty;

                return File.ReadAllText(filePath);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void InsertTextAtCursor(string text)
        {
        }

        public void ReplaceSelection(string text)
        {
        }
    }

    public class EditorContext
    {
        public string ActiveFilePath { get; set; } = string.Empty;
        public string LanguageId { get; set; } = "plaintext";
        public string SelectedText { get; set; } = string.Empty;
        public int SelectionStartLine { get; set; }
        public int SelectionStartColumn { get; set; }
        public int SelectionEndLine { get; set; }
        public int SelectionEndColumn { get; set; }
    }
}