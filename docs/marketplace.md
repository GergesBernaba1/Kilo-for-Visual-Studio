# Marketplace Listing

This marketplace page describes Kilo for Visual Studio features and how to install and use the extension.

## Features Matrix

| Feature | Status | Notes |
|---|---|---|
| Docked assistant tool window | ✅ | MVP complete |
| Prompt/response workflow | ✅ | Core functionality |
| Backend connector settings | ✅ | Configurable `backendUrl`, `apiKey` |
| Mock backend support | ✅ | For local testing |
| Patch diff preview | ✅ | Partial (manual apply placeholder) |
| Session history | ⚠️ | Planned in roadmap |
| Mode system (Architect/Coder/Debugger) | ⚠️ | Basic implementation with future customization |
| Inline autocomplete | ⚠️ | Roadmap Phase 1 |
| Task automation templates | ⚠️ | Advanced workflow in progress |
| Telemetry / privacy opt-in | ✅ | TelemetryService opt-in available |

## Screenshots

![Kilo Assistant Main](../docs/assets/kilo-assistant-main.png)
*Main assistant tool window with active file context and prompt.*

![Kilo Marketplace](../docs/assets/kilo-marketplace.png)
*Marketplace-style feature matrix and settings.*

## Install

- Via Visual Studio: build the extension and install VSIX.
- In development: `dotnet restore`, `dotnet build`, then run experimental instance.

## Usage

1. Open `Tools -> Kilo Assistant`.
2. Configure API key and backend URL.
3. Enter prompt and click `Ask`.
4. Use the suggested code and patch preview sections to copy/apply.

## Contributing Marketplace Content

If you want to add your own MCP package listing design, create a PR with `docs/marketplace.md` updates and screenshot assets in `docs/assets/`.
