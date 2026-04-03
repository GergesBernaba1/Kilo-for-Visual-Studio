namespace Kilo.VisualStudio.Extension
{
    internal static class PackageCommandSet
    {
        // Main commands (must match IDSymbol values in KiloCommands.vsct)
        public const int OpenAssistantToolWindow = 0x0100;
        public const int AskSelection = 0x0101;
        public const int AskFile = 0x0102;
        public const int OpenDiffViewer = 0x0103;
        public const int OpenSessionHistory = 0x0104;
        public const int OpenSettings = 0x0105;
        public const int OpenChatDocument = 0x0106;
        public const int CycleAgentMode = 0x0107;
        public const int ClearChat = 0x0108;
        public const int NewSession = 0x0109;
        public const int OpenAutomationToolWindow = 0x010A;
        public const int OpenAgentManager = 0x010B;
        public const int OpenSubAgentViewer = 0x010C;
        public const int OpenExtensionLog = 0x010D;

        // Context menu commands (editor right-click, Solution Explorer right-click)
        public const int AskSelectionContext = 0x0200;
        public const int AskFileContext = 0x0201;
        public const int AskFileSolutionExplorer = 0x0202;
        public const int OpenAssistantTools = 0x0203;
    }
}
