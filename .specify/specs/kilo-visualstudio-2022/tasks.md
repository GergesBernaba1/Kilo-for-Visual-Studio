---
description: "Task list for Kilo for Visual Studio 2022 MVP"
---

# Tasks: Kilo for Visual Studio 2022 MVP

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Create Visual Studio 2022 extension solution with projects: Extension, App, Integration, Contracts, Tests.
- [x] T002 Add VSIX metadata and Basic command definition.
- [x] T003 Configure linting and formatting rules per constitution.

## Phase 2: Foundational (Blocking Prerequisites)

- [x] T004 Implement extension shell with tool window registration.
- [x] T005 Implement command in Tools menu and context menu entry.
- [x] T006 Add backend abstraction interface `IKiloBackendClient` and mock implementation.
- [x] T007 Add logging and error handling middleware.
- [x] T008 Create test harness for unit and integration tests.

## Phase 3: User Story 1 - Selected-Code Assistant

- [x] T009 Implement code selection capture in editor context.
- [x] T010 Implement request packaging with active file path, selection text, language.
- [x] T011 Implement assistant tool window UI and response rendering.
- [x] T012 Implement mock backend call flow and retry UI.
- [x] T013 Add unit/integration tests for the selected-code flow.

## Phase 4: User Story 2 - File Q&A

- [x] T014 Implement active file context capture and assistant input flow.
- [ ] T015 Add test coverage for file-based prompt flow.

## Phase 5: User Story 3 - Generate/Refactor Snippet

- [x] T016 Add generate/refactor prompt handlers in assistant flow.
- [x] T017 Add patch preview UI and apply patch placeholder.
- [x] T018 Add tests for code generation/refactor scenarios.

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T019 Verify accessibility landmarks and keyboard navigation in tool window.
- [x] T020 Enforce performance check and no UI thread blocking.
- [x] T021 Update docs and usage guide.

## Dependencies & Execution Order

- Setup -> Foundational -> User Stories -> Polish
