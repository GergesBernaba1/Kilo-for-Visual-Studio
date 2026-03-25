# User Guide

## Overview
Kilo for Visual Studio is an AI assistant plugin for Visual Studio 2022 providing prompt-based code generation, explanation, and refactor help.

## Setup
1. Clone the repository.
2. Open `Kilo.VisualStudio.slnx` in Visual Studio 2022.
3. Restore packages: `dotnet restore`.
4. Build solution.
5. Run tests: `dotnet test Kilo.VisualStudio.Tests/Kilo.VisualStudio.Tests.csproj`.

## Configuration
Edit `ExtensionSettings` via the in-extension panel:
- `ApiKey`
- `BackendUrl`
- `UseMockBackend`

## Using Kilo Assistant
- Open `Tools` > `Kilo Assistant`.
- Type a prompt and click "Ask".
- Receive results in the response panel.
- Use "Apply Patch" in patch section (placeholder behavior in MVP).

## Advanced usage
- Set your actual backend endpoint.
- For offline development, enable `UseMockBackend`.
- Debug logs are in `%LOCALAPPDATA%\Kilo.VisualStudio\Logs`.
