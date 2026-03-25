using System;
using System.Diagnostics;
using System.Linq;
using Xunit;


namespace Kilo.VisualStudio.Tests
{
    public class UIAutomationTests
    {
        [Fact(Skip = "Requires Visual Studio + external UI Automation ability (WinAppDriver/FlaUI).")]
        public void KiloAssistantToolWindow_AskKiloFlow_WorksInRunningVS()
        {
            // This test is intentionally skipped by default in CI.
            // It demonstrates how to hook into the VS process and verify control flow:
            // - prompt input
            // - ask button click
            // - conversation history updated
            // - usage history updated
            throw new NotSupportedException("Interactive UI automation test is not executed in this environment.");
        }
    }
}
