# Kilo for Visual Studio 2022 — Initiation Document

## 1. Purpose

This project will create a new **Visual Studio 2022 extension** that delivers a **Kilo Code–style experience** inside Visual Studio for developers who prefer or require Visual Studio instead of VS Code.

The target product direction is to align with the value proposition implied by **AI Coding Agent, Copilot, and Autocomplete**, but to achieve that alignment in **phases**.

This project is **not** a direct port of the existing VS Code extension UI. Instead, it is a **Visual Studio-native extension** that will reuse shared backend or integration capabilities where practical, while implementing Visual Studio-specific commands, UI, and editor integration.

## 2. Product Vision

Enable Visual Studio 2022 developers to use a modern AI coding assistant inside the IDE for:

* asking questions about selected code or files
* explaining methods, classes, and projects
* generating code from natural-language prompts
* refactoring existing code
* proposing edits and patches
* understanding solution and project context
* supporting developer workflows in a Kilo-like assistant experience

The long-term vision is to evolve from a **developer assistant** into a **Visual Studio-native AI coding agent and copilot**, and then later into an experience that includes **autocomplete and inline coding assistance**.

## 3. Product Positioning

The extension should be positioned as:

**Kilo for Visual Studio 2022**
**Phase-based target:** AI Coding Agent and Copilot-style assistant first, Autocomplete later.

This positioning is important because a Visual Studio 2022 extension can realistically achieve strong parity with the **assistant / coding agent** aspects first, while the **inline autocomplete** portion requires a deeper and more specialized editor integration track.

## 4. Why a Separate Extension Is Needed

Kilo’s current editor implementation is built for the **VS Code extension model**, while Visual Studio 2022 uses its own extensibility models and APIs. Therefore:

* VS Code-specific editor integration cannot be reused directly
* tool windows, commands, menus, and editor context integration must be implemented specifically for Visual Studio
* a compatibility layer should be used for any reusable Kilo backend or CLI functionality

Accordingly, this effort should be treated as a **new host implementation** for Visual Studio rather than a direct extension port.

## 5. Recommended Technical Direction

### 5.1 Host Model

Build a **Visual Studio 2022 extension** packaged as **VSIX**.

### 5.2 Extensibility Strategy

Start with a **Visual Studio-native extension architecture** and prefer the modern Visual Studio extensibility approach for command and tool-window scenarios, while allowing hybrid usage of deeper Visual Studio SDK APIs if later editor scenarios require them.

### 5.3 Integration Strategy

Use a layered architecture:

1. **Visual Studio Host Layer**

   * commands
   * tool window / assistant panel
   * active document and selected text access
   * solution and project context collection
   * file-change and patch application workflow
   * future editor integration hooks

2. **Application Layer**

   * request orchestration
   * context packaging
   * session state
   * command routing
   * feature-level policies

3. **Kilo Integration Layer**

   * connect to reusable Kilo backend / CLI / server functionality where available
   * otherwise provide a compatibility abstraction that can be replaced later without affecting the Visual Studio host layer

## 6. Product Scope and Parity Strategy

The objective is **functional alignment**, not immediate feature parity.

The project should explicitly distinguish between:

* **v1 capabilities** that strongly match the assistant/coding-agent experience
* **v2 capabilities** that expand toward copilot-style multi-step productivity workflows
* **v3 capabilities** that approach inline autocomplete and richer in-editor assistance

## 7. Feature Alignment Matrix

### 7.1 Full-match candidates for v1

These features are realistic and should be targeted in the first release:

* AI assistant tool window
* Ask about selected code
* Ask about active file
* Explain code
* Generate code from prompts
* Refactor selected code
* Suggest patches/edits
* Commands from menu and editor context menu

### 7.2 Partial-match candidates for v1/v2

These are feasible but may start in a simplified form:

* multi-file change suggestions
* project/solution context awareness
* agent-like task flows
* change preview and selective apply
* session persistence
* configurable providers and settings

### 7.3 Deferred parity targets for later phases

These should not be promised in the first version:

* inline autocomplete / ghost-text suggestions while typing
* advanced autonomous orchestration
* full VS Code parity for all workflows
* mature terminal/task automation parity
* complete editor-inline AI assistance experience

## 8. MVP Scope

The first release should be intentionally small and prove the architecture while matching the most important Kilo-like assistant behaviors.

### MVP features

* dockable tool window named **Kilo Assistant**
* command in **Tools** menu to open the assistant
* editor context-menu command for **Ask Kilo About Selection**
* capture:

  * active file path
  * selected text
  * document language/type
* send prompt + context to a mock or real backend
* display textual response in the tool window
* support at least these tasks:

  * explain selected code
  * answer question about current file
  * generate/refactor snippet suggestions

### Explicitly out of scope for MVP

* inline autocomplete
* autonomous multi-step agent execution
* terminal orchestration parity
* large-scale project-wide edits
* full feature parity with the VS Code extension
* final marketplace packaging and public distribution

## 9. Product Roadmap

### Phase 1 — Assistant Foundation

Goal: prove the Visual Studio-native host.

Deliverables:

* Kilo Assistant tool window
* Tools menu command
* selection-based command
* prompt + context packaging
* mock backend, then real backend abstraction
* simple conversational assistant behavior

### Phase 2 — Copilot-style Productivity Expansion

Goal: move beyond Q&A into actionable IDE assistance.

Deliverables:

* patch/diff preview
* apply changes to documents
* better solution/project context
* reusable prompts/actions such as Explain, Refactor, Generate, Improve
* session history and settings
* partial agent-style workflows

### Phase 3 — Autocomplete and Advanced Editor Assistance

Goal: move toward stronger parity with the autocomplete positioning.

Deliverables:

* inline suggestions
* ghost-text or completion-style experience
* context-aware typing assistance
* smarter edit prediction
* richer in-editor AI experiences

## 10. Target Users

* .NET developers using Visual Studio 2022
* enterprise teams working primarily in Visual Studio solutions
* developers who want a Kilo-like workflow without moving to VS Code
* teams that need controlled adoption of AI assistance inside their primary IDE

## 11. Success Criteria for Initiation Phase

The initiation phase is successful if it produces:

* agreed product positioning
* agreed phased scope
* chosen extension model
* solution structure
* MVP feature list
* risk register
* first implementation backlog

## 12. Proposed Solution Structure

### Solution name

`Kilo.VisualStudio`

### Initial projects

1. `Kilo.VisualStudio.Extension`

   * Visual Studio extension entry point
   * commands
   * tool windows
   * IDE integration

2. `Kilo.VisualStudio.App`

   * orchestration logic
   * session handling
   * feature workflows
   * request composition

3. `Kilo.VisualStudio.Integration`

   * abstraction over Kilo backend / CLI / service integration
   * concrete backend clients

4. `Kilo.VisualStudio.Contracts`

   * DTOs
   * shared interfaces
   * request/response models

5. `Kilo.VisualStudio.Tests`

   * unit tests for orchestration and adapters

## 13. Core MVP Use Cases

### UC-1: Ask about selected code

User selects code in the editor, runs the command, and receives an explanation or suggestion.

### UC-2: Ask about current file

User opens the assistant and asks a question using the active file as context.

### UC-3: Open assistant from menu

User opens the tool window from the Tools menu without requiring a selection.

### UC-4: Generate or refactor snippet

User asks the assistant to generate or improve a method or code block and receives a proposed output.

## 14. Non-Functional Requirements

* responsive UI
* asynchronous operations
* no unnecessary blocking of the Visual Studio UI thread
* clear fallback when backend is unavailable
* backend abstraction from the first version
* replaceable mock backend
* clean path for future editor-inline features
* enterprise-friendly extensibility later

## 15. Risks and Constraints

### Technical risks

* major differences between VS Code APIs and Visual Studio APIs
* uncertain direct reusability of Kilo editor-specific code
* potential complexity in invoking or integrating backend/CLI behavior
* richer editor features may require deeper Visual Studio SDK usage
* inline autocomplete may be significantly harder than assistant-panel scenarios

### Product risks

* trying to achieve parity too early
* tightly coupling the extension to unstable backend assumptions
* overpromising autocomplete in the first milestones
* building too much host infrastructure before validating user workflow value

## 16. Key Decisions for the First Iteration

1. Build a **Visual Studio-native extension**, not a VS Code wrapper.
2. Treat this as a **Kilo-style Visual Studio host** with phased parity.
3. Start with **assistant / coding-agent features first**.
4. Defer **autocomplete** to a later dedicated phase.
5. Use a **backend abstraction** from day one.
6. Start with a **mock backend first**, then connect real Kilo-compatible integration.
7. Avoid full parity goals until the core assistant experience works reliably.

## 17. First Backlog

### Epic A — Host Foundation

* create Visual Studio extension solution
* add VSIX metadata
* add Tools menu command
* add dockable tool window

### Epic B — Editor Context

* read active document
* read selected text
* identify file language/type
* package prompt context

### Epic C — Backend Abstraction

* define `IKiloBackendClient`
* create mock implementation
* define request/response contracts

### Epic D — Assistant UX

* input box
* conversation panel
* loading state
* error state
* response rendering

### Epic E — First Kilo-like Workflows

* explain selected code
* ask about file
* generate snippet
* refactor snippet

## 18. Next Immediate Step

Create the **technical architecture and project skeleton** for the MVP, including:

* chosen extension SDK/model
* project-by-project responsibilities
* main interfaces
* initial request/response models
* first implementation sequence
* phase-1 backlog ready for development

## 19. Final Scope Statement

This project will initiate a **Visual Studio 2022 extension inspired by Kilo Code’s AI Coding Agent, Copilot, and Autocomplete positioning**, while delivering that vision in a realistic phased roadmap:

* **first** as a Visual Studio-native assistant and coding agent
* **then** as a stronger copilot-style productivity tool
* **later** as an autocomplete-capable in-editor experience

## 20. Implementation Status

- [x] Specification document created (`.specify/specs/kilo-visualstudio-2022/spec.md`)
- [x] Plan document created (`.specify/specs/kilo-visualstudio-2022/plan.md`)
- [x] Task list created (`.specify/specs/kilo-visualstudio-2022/tasks.md`)
- [x] Constitution updated (`.specify/memory/constitution.md`)
- [x] Clarification resolved (API key backend model, `FR-011` added)
- [x] Analysis completed (`/speckit.analyze` cross-check)
- [x] MVP implementation readiness confirmed

## Remaining Next Steps

1. Create Visual Studio extension solution and projects.
2. Implement tasks T001..T021 (and T022 for FR-011) in code.
3. Run unit/integration tests and validate per success criteria.

This scope protects implementation quality, reduces risk, and keeps the product direction aligned with real Visual Studio capabilities.
