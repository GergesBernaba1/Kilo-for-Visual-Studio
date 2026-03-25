# Kilo for Visual Studio 2022

A Visual Studio 2022 extension implementation of Kilo-style AI coding assistance.

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

## Implemented features

### Core functionality
- Dockable tool window with assistant UI
- Tools menu command to open Kilo Assistant
- Editor context-menu commands for selection-based queries
- Language ID detection based on file extension
- Prompt + context packaging with file path, selection, language

### Backend integration
- `KiloBackendClient` with mock mode support
- Bearer token authentication with API key
- Configurable backend URL
- Mock responses for: explain, refactor, generate code

### UI features
- Active file path display
- Selected text display
- Prompt input field
- Response display area
- Suggested code expander
- Patch diff expander with apply button placeholder

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
2. `AssistantService.AskAssistantAsync` validates prompt and delegates to backend.
3. `KiloBackendClient` adds `Authorization: Bearer <API_KEY>` and posts to configured endpoint.
4. Response returned as `AssistantResponse`, including `SuggestedCode` / `PatchDiff`.

## How to release

1. Confirm tests: `dotnet test`.
2. Build `Kilo.VisualStudio.Extension` VSIX package.
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

## Notes

- This repo is fully aligned with the `Kilo Visual Studio 2022 initiation` plan and spec.
