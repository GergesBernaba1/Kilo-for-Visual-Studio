# Feature Specification: Kilo for Visual Studio 2022 MVP

**Feature Branch**: `kilo-visualstudio-2022`  
**Created**: 2026-03-24  
**Status**: Draft  
**Input**: User description: "create a new Visual Studio 2022 extension that delivers a Kilo Code–style experience inside Visual Studio for developers who prefer or require Visual Studio instead of VS Code"

## Clarifications

### Session 2026-03-24
- Q: What backend authentication model should be used in MVP? → A: API key (configurable, header-based)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assistant Popup for Selected Code (Priority: P1)

A developer selects code in the Visual Studio editor, invokes a command (context menu or keyboard shortcut), and receives a natural-language explanation or improvement suggestion in a docked assistant tool window.

**Why this priority**: High user value for the core AI assistant scenario; proves host + context extraction end-to-end.

**Independent Test**: Select code, run "Ask Kilo About Selection", verify tool window appears, request is sent to mock backend, and a textual response is shown.

**Acceptance Scenarios**:
1. **Given** Visual Studio is open with an active C#/VB file, **When** the user selects one or more lines and executes "Ask Kilo About Selection", **Then** the tool window opens and displays a response message.
2. **Given** the backend is unavailable, **When** the user invokes the command, **Then** an actionable error message is shown with retry guidance.

---

### User Story 2 - Ask about Current File (Priority: P2)

A developer opens the assistant (Tools menu / command) and asks a question about the active file (e.g., "Explain this class").

**Why this priority**: Extends value beyond selected snippet to whole-file context and Q&A flow.

**Independent Test**: Open assistant, click "Ask about current file", verify active file path and language are captured, request sent, and response received.

**Acceptance Scenarios**:
1. **Given** a .cs file is active, **When** user selects "Ask about current file" and types a question, **Then** the assistant replies with relevant context-aware information.

---

### User Story 3 - Generate/Refactor Snippet (Priority: P3)

A developer asks the assistant to generate or refactor code, receiving a response containing a suggested snippet and optional preview/patch text.

**Why this priority**: Core Kilo-style generation/refactor capability that drives productivity value.

**Independent Test**: In assistant pane, request "Refactor this method" with selected code; verify assistant returns updated snippet and patch option.

**Acceptance Scenarios**:
1. **Given** selected method code and prompt "Refactor for safety", **When** user submits, **Then** assistant returns a refactored proposal.

---

### Edge Cases

- Selected text is empty (no selection). Should still allow macro prompt flows or show instruction to select code.
- File is unsaved/untitled. Tool should either use buffer content or warn about best effort.
- Backend times out: show the error, do not freeze UI.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The extension MUST provide a dockable tool window named "Kilo Assistant".
- **FR-002**: The extension MUST expose a Tools menu command to open the assistant.
- **FR-003**: The extension MUST expose an editor context command "Ask Kilo About Selection" for selected text.
- **FR-004**: The extension MUST capture active file path, selected text, and document language/type before forming a request.
- **FR-005**: The extension MUST support a backend abstraction with mock and real backend implementations.
- **FR-006**: The extension MUST display user-friendly error states for network/backend faults.
- **FR-007**: The extension MUST include basic accessibility support for the tool window (keyboard and screen reader labels).
- **FR-008**: The extension MUST not block the VS UI thread for backend requests.
- **FR-009**: The extension MUST allow a user to request explanation, code generation, and code refactor recommendations.
- **FR-010**: The extension MUST log request events per diagnostic level (no sensitive data in logs).
- **FR-011**: The extension MUST support secure backend integration via a configurable API key (header-based), with mock backend option for local/dev flows.

### Key Entities *(include if feature involves data)*

- **AssistantRequest**: active file path, selection text, language type, prompt text, session metadata.
- **AssistantResponse**: status, message text, optional snippet, optional patch metadata, error info.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of users can complete the primary flow (Select code → Ask Kilo → see response) within 3 steps in first usability test.
- **SC-002**: Tool window opens and initializes in <1 second in 90% of runs (local performance benchmark).
- **SC-003**: Extension supports 5 concurrent request sessions without UI freeze.
- **SC-004**: 100% of new user-facing flows include safety error message and retry guidance.
- **SC-005**: 80% of regression tests for core flows pass on CI on every PR.

## Assumptions

- Users have stable local Visual Studio environment and the extension runs under Visual Studio 2022.
- MVP excludes inline autocomplete, autonomous multi-step agent workflows, and full VS Code parity.
- Existing Kilo backend APIs are available for integration after mock backend phase.
- Performance goals are verified via local automated test harness + basic profiling.

