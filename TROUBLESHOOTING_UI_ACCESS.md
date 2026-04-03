# DETAILED TROUBLESHOOTING: Finding Kilo Extension UI

## Problem
You've installed the Kilo extension and it's enabled, but you can't find the UI.

## Solution Steps (Try in Order)

### Step 1: Reset Visual Studio Experimental Instance (If Debugging)
If you're running from Visual Studio (F5):
```powershell
# Close all Visual Studio instances first, then run:
"C:\Program Files\Microsoft Visual Studio\2022\Community\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=17.0 /RootSuffix=Exp
```

### Step 2: Look in the EXACT Right Place

**The commands should appear in the Tools menu:**

```
Visual Studio Top Menu Bar
├─ File
├─ Edit  
├─ View
├─ Git
├─ Project
├─ Build
├─ Debug
├─ Test
├─ Analyze
├─ Tools  ◄────── LOOK HERE!
│   ├─ Get Tools and Features...
│   ├─ Connect to Database...
│   ├─ ...
│   ├─ ──────────────────────
│   ├─ Open Kilo Assistant       ◄────── Should be here!
│   ├─ Ask Kilo About Current File
│   ├─ Session History
│   ├─ Kilo Settings
│   ├─ Cycle Agent Mode
│   ├─ New Session
│   ├─ Open Automation
│   ├─ Agent Manager
│   └─ Sub-Agent Viewer
├─ Extensions
└─ Window
```

### Step 3: Try the Keyboard Shortcut
**Press: Ctrl + Shift + K**

This should open the Kilo Assistant window directly.

### Step 4: Check View → Other Windows
1. Go to **View** menu
2. Click **Other Windows**
3. Scroll down - you might see "Kilo Assistant" listed there
4. Click it if you see it

### Step 5: Verify in Extensions Manager
1. Go to **Extensions** → **Manage Extensions**
2. Click **Installed** tab
3. Search for "Kilo"
4. You should see: **"Kilo Visual Studio Assistant"**
5. Make sure it says **"Enabled"** (not "Disabled" or "Scheduled for install")
6. If it says "Scheduled for install", **restart Visual Studio**

### Step 6: Check the Activity Log
1. Close all Visual Studio instances
2. Find the activity log file:
   ```
   %APPDATA%\Microsoft\VisualStudio\17.0_<random>\ActivityLog.xml
   ```
   For example:
   ```
   C:\Users\YourName\AppData\Roaming\Microsoft\VisualStudio\17.0_12345678\ActivityLog.xml
   ```
3. Open it in a text editor
4. Search for "Kilo" or "3f60a1dd-6f2e-4aaf-8ef6-497f36cf3b27"
5. Look for any errors

### Step 7: Check Output Window
1. In Visual Studio, go to **View** → **Output**
2. In the "Show output from:" dropdown, look for **"Kilo Assistant"**
3. If you see it, check for any error messages
4. Also check dropdown options like:
   - "Extensions"
   - "Debug"
   - "Build"

### Step 8: Completely Uninstall and Reinstall

**Uninstall:**
1. **Extensions** → **Manage Extensions**
2. Find "Kilo Visual Studio Assistant"
3. Click **Uninstall**
4. **Close Visual Studio** completely
5. **Restart Visual Studio** (this actually removes it)

**Reinstall:**
1. Build the extension:
   ```powershell
   cd "E:\Gerges Files\Kilo-for-Visual-Studio"
   dotnet build Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj -c Release
   ```
2. Find the VSIX file (should be in the bin/Release folder)
3. Double-click the `.vsix` file
4. Follow installation prompts
5. **Restart Visual Studio**

### Step 9: Check if VSIX Built Correctly

Run this to build and check:
```powershell
cd "E:\Gerges Files\Kilo-for-Visual-Studio"
dotnet clean Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj
dotnet build Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj -c Release

# Check if the VSIXMANIFEST is correct
Get-Content "Kilo.VisualStudio.Extension\source.extension.vsixmanifest"
```

### Step 10: Try Command Window
1. In Visual Studio, press: **Ctrl + Alt + A** (or go to View → Other Windows → Command Window)
2. Type and press Enter:
   ```
   Tools.OpenKiloAssistant
   ```
   or try:
   ```
   View.KiloAssistantToolWindow
   ```

### Step 11: Check for Conflicting Extensions
Some extensions might conflict with custom tool windows:
1. **Extensions** → **Manage Extensions**
2. Try temporarily disabling other AI/assistant extensions
3. Restart Visual Studio
4. Try to find Kilo again

### Step 12: Run Visual Studio with Logging
1. Close Visual Studio
2. Open Command Prompt as Administrator
3. Run:
   ```
   "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" /log "%TEMP%\vslog.txt"
   ```
4. Try to use Kilo
5. Close Visual Studio
6. Check `%TEMP%\vslog.txt` for Kilo-related messages

### Step 13: Check Visual Studio Version
The extension requires **Visual Studio 2022** (version 17.0 or later):
1. Go to **Help** → **About Microsoft Visual Studio**
2. Check the version number
3. Must be 17.0 or higher
4. Supported editions:
   - Community
   - Professional  
   - Enterprise

### Step 14: Manual Tool Window Access (Last Resort)
Try this code in the Immediate Window (Debug → Windows → Immediate):
1. Start debugging any project (orpress F5)
2. Break execution (pause)
3. Open Immediate Window (Ctrl + Alt + I)
4. Type:
   ```csharp
   DTE.Windows.Item("Kilo Assistant").Visible = true
   ```
   Press Enter

## Still Not Working?

If none of the above works, there might be an issue with the extension installation. Please provide:

1. **Visual Studio version**: Help → About (copy the version info)
2. **Activity Log**: The ActivityLog.xml file mentioned above
3. **Extension status**: Screenshot of Extensions → Manage Extensions showing Kilo
4. **Build output**: Output from building the extension
5. **Installation log**: Windows Event Viewer → Windows Logs → Application (filter for "Visual Studio")

## Working Alternative: Debug Mode
If you can't get the installed version working, try running in debug mode:

1. Open the solution in Visual Studio 2022
2. Set `Kilo.VisualStudio.Extension` as the startup project
3. Press **F5**
4. This opens a new "Experimental Instance" of VS
5. In the experimental instance, go to **Tools** → **Open Kilo Assistant**

## Expected Behavior When Working
When the extension is working correctly:
- ✅ "Open Kilo Assistant" appears in Tools menu
- ✅ Ctrl+Shift+K opens the assistant window
- ✅ A dockable panel appears with purple/dark theme
- ✅ You see "Kilo Assistant" in the title bar of the panel
- ✅ The panel has a prompt box at the bottom

## Quick Test
To verify the extension loaded, check if ANY of these work:
- Ctrl + Shift + K (Open Assistant)
- Ctrl + Shift + A (Ask Selection)  
- Ctrl + Shift + F (Ask File)
- Ctrl + Shift + H (Session History)

If NONE of those keyboard shortcuts work, the extension likely failed to load.
