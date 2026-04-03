# How to Access Kilo Extension UI in Visual Studio 2022

After installing the Kilo extension, here's how to find and use it:

## Method 1: Tools Menu (Recommended)
1. Open Visual Studio 2022
2. Go to **Tools** menu at the top
3. Look for **"Open Kilo Assistant"**
4. Click it to open the main chat interface

## Method 2: Keyboard Shortcut
Press: **Ctrl + Shift + K**

This will open the Kilo Assistant tool window.

## Method 3: View Menu (If Tool Window is Lost)
1. Go to **View** → **Other Windows**
2. Look for **"Kilo Assistant"**
3. Click to show the window

## Additional Features

### Context Menu Commands
When you have code selected:
- Right-click in the editor
- Look for Kilo-related commands in the context menu
- Options like "Ask Kilo About Selection" may appear

### Other Keyboard Shortcuts
- **Ctrl + Shift + A** - Ask Kilo about selection
- **Ctrl + Shift + F** - Ask Kilo about current file

## Troubleshooting: Can't Find the Extension?

### 1. Verify Extension is Installed
1. Go to **Extensions** → **Manage Extensions**
2. Click **"Installed"** tab
3. Search for **"Kilo"**
4. You should see **"Kilo Visual Studio Assistant"**

### 2. Enable the Extension
If it's installed but disabled:
1. In **Manage Extensions**, find "Kilo Visual Studio Assistant"
2. Click **"Enable"**
3. Restart Visual Studio

### 3. Check for Installation Errors
1. Go to **Tools** → **Options**
2. Select **"Environment"** → **"Activity Log"**
3. Check for any Kilo-related errors

### 4. Reinstall the Extension
If nothing works:
1. Go to **Extensions** → **Manage Extensions**
2. Find "Kilo Visual Studio Assistant"
3. Click **"Uninstall"**
4. Restart Visual Studio
5. Reinstall the VSIX file

## What the Extension Looks Like

When opened, you should see:
- **Header** with "Kilo Assistant" title
- **Connection status** indicator (dot + text)
- **Session selector** dropdown
- **Context display** showing active file
- **Chat/prompt area** where you type questions
- **Response area** showing AI answers
- **Tool execution panel** showing what the AI is doing

## Testing the Extension

To verify it's working:
1. Open the Kilo Assistant (Ctrl + Shift + K)
2. Type a simple question like "Hello"
3. Click the "Send" or "Ask" button
4. You should see a response (or mock response if backend isn't configured)

## Inline Completions (NEW Feature)

The inline ghost text completions will appear automatically:
1. Open any code file (.cs, .js, .py, etc.)
2. Start typing code
3. Stop typing for 500ms
4. You should see gray italic suggestions appear at your cursor
5. Press **Tab** to accept, **Esc** to dismiss

**Note:** Inline completions require the backend to be configured or mock mode enabled.

## Configuration

To configure the extension:
1. Look for a settings icon/button in the Kilo Assistant window
2. Or check: **Tools** → **Options** → search for "Kilo"
3. Configure:
   - Backend URL
   - API Key
   - Mock mode (for testing without backend)

## Still Can't Find It?

If you've tried everything and still can't find the UI:

1. **Check the Output Window**:
   - **View** → **Output**
   - Select "Kilo Assistant" from the dropdown
   - Look for initialization messages or errors

2. **Check the Activity Log**:
   ```
   %APPDATA%\Microsoft\VisualStudio\17.0_<instance>\ActivityLog.xml
   ```
   Search for "Kilo" to see if there are loading errors

3. **Verify Installation Target**:
   - The extension supports: Community, Professional, Enterprise
   - Make sure you're using Visual Studio 2022 (Version 17.0+)

4. **Try Experimental Instance**:
   If you're debugging the extension:
   - It will open in the **Experimental Instance**
   - Look for "(Experimental Instance)" in the title bar
   - The extension will only appear there during debugging

## Quick Reference

| Action | Method |
|--------|--------|
| Open Assistant | Tools → Open Kilo Assistant |
| Keyboard Shortcut | Ctrl + Shift + K |
| Accept Ghost Text | Tab |
| Dismiss Ghost Text | Esc |
| Ask About Selection | Ctrl + Shift + A |
| View Extensions | Extensions → Manage Extensions |

## Support

If you continue having issues:
1. Check the [README.md](../README.md) for installation instructions
2. Review [COPILOT_REFACTORING.md](../COPILOT_REFACTORING.md) for feature documentation
3. Check the project's issue tracker for known problems
