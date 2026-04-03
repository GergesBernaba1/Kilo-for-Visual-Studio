# Kilo for Visual Studio 2022

A Visual Studio 2022 extension with GitHub Copilot-inspired UI and AI-powered coding assistance.

## ✨ New: Copilot-Inspired Interface

Kilo now features a modern, polished UI inspired by GitHub Copilot:
- **Inline Ghost Text**: AI suggestions appear as translucent gray text at your cursor
- **Tab to Accept**: Press Tab to accept suggestions, Esc to dismiss
- **Smooth Animations**: Fade-in/out transitions and entrance animations
- **Copilot Theme**: Purple accent colors, rounded corners, modern styling
- **Message Bubbles**: Chat-style interface with user/assistant messages
- **Loading Indicators**: Pulsing dots animation while AI is thinking

See [COPILOT_REFACTORING.md](COPILOT_REFACTORING.md) for detailed documentation.

## Repository structure

- `.specify/`: workflow specs, plan, tasks, constitution
- `Kilo.VisualStudio.Contracts/`: models and service interface
- `Kilo.VisualStudio.App/`: assistant orchestration service
- `Kilo.VisualStudio.Integration/`: backend client adapter
- `Kilo.VisualStudio.Extension/`: VS extension shell + tool window and commands
- `Kilo.VisualStudio.Tests/`: unit tests
- `Kilo-visualstudio-2022-initiation.Md`: project initiation doc

## Getting started

1. Open solution: `Kilo.VisualStudio.slnx` in Visual Studio 2022.
2. Restore packages: `dotnet restore`.
3. Build and run tests: `dotnet test Kilo.VisualStudio.Tests/Kilo.VisualStudio.Tests.csproj`.

## Install extension (VSIX)

1. Build package:
   - `msbuild Kilo.VisualStudio.Vsix\Kilo.VisualStudio.Vsix.csproj /t:CreateVsixContainer /p:Configuration=Release /p:DeployExtension=false`
2. Install:
   - `Kilo.VisualStudio.Vsix\Kilo.VisualStudio.vsix`
3. If installer reports blocking processes, close all Visual Studio instances and install again.

## Open the UI in Visual Studio

Use one of these:
- `Tools` -> `Kilo Assistant` -> `Open Kilo Assistant`
- Shortcut: `Ctrl+Shift+K`

If you do not see the menu:
- Confirm extension is enabled in `Extensions -> Manage Extensions`.
- Restart Visual Studio once after install/update.
- Reinstall the latest VSIX from `Kilo.VisualStudio.Vsix\Kilo.VisualStudio.vsix`.

## Implemented features

### Core functionality
- Dockable tool window with Copilot-inspired assistant UI
- Tools menu command to open Kilo Assistant
- Editor context-menu commands for selection-based queries
- Language ID detection based on file extension
- Prompt + context packaging with file path, selection, language

### 🎨 Copilot-Style UI (NEW)
- **Inline ghost text suggestions** with Tab acceptance
- **Animated message bubbles** for chat interface
- **Copilot purple theme** with smooth transitions
- **Loading animations** with pulsing dots
- **Context-aware completions** with 500ms debouncing
- **Auto-dismissal** on cursor movement or typing

### Backend integration
- `KiloBackendClient` with mock mode support
- Bearer token authentication with API key
- Configurable backend URL
- Mock responses for: explain, refactor, generate code
- Streaming SSE support for real-time responses

### UI features
- Active file path display
- Selected text display
- Prompt input field with Copilot styling
- Response display area with message bubbles
- Suggested code expander
- Patch diff expander with apply button
- Session management with multiple conversations
- Tool execution visualization
- MCP server marketplace integration

### Settings
- `ExtensionSettings` with JSON persistence
- API key configuration
- Backend URL configuration
- Mock backend toggle

### Developer experience
- File-based logging to `%LOCALAPPDATA%\Kilo.VisualStudio\Logs`
- Unit tests for AssistantService and mock backend
- EditorConfig for code style consistency

## MVP command flow

1. UI command creates `AssistantRequest` with:
   - active file path
   - language id (csharp, python, javascript, etc.)
   - selected text (optional)
   - prompt text
2. **Copilot Refactoring Guide**](COPILOT_REFACTORING.md) - New Copilot-inspired features
- [Roadmap](docs/roadmap.md)
- [User Guide](docs/user-guide.md)
- [Contributing](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Key Features

### 🚀 Inline Completions
- AI-powered code suggestions appear as you type
- Translucent ghost text at cursor position
- Tab to accept, Esc to dismiss
- Context-aware with 20 lines before, 5 lines after

### 💬 Chat Interface
- Copilot-style message bubbles
- Real-time streaming responses
- Session management
- Tool execution visualization

### 🎨 Modern UI
- GitHub Copilot-inspired design
- Smooth fade-in/out animations
- Purple accent colors (#6E40C9)
- Responsive and accessible
## How to release

1. Confirm tests: `dotnet test`.
2. Build `Kilo.VisualStudio.Vsix` package.
3. Install and verify `Kilo Assistant` command functions in VS2022.

## Documentation

- [Roadmap](docs/roadmap.md)
- [User Guide](docs/user-guide.md)
- [Contributing](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Developer

**Gerges Bernaba**
- Email: gergesbernaba2@gmail.com
- LinkedIn: https://www.linkedin.com/in/gergesbernaba/


