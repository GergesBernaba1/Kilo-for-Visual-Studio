<!--
Sync Impact Report
- Version change: none -> 1.0.0
- Modified principles: placeholders -> concrete principle definitions
- Added sections: Core Principles, Additional Constraints, Development Workflow, Governance
- Removed sections: None removed, placeholders replaced
- Templates reviewed: .specify/templates/plan-template.md (✅ updated alignment check), .specify/templates/spec-template.md (✅ reviewed), .specify/templates/tasks-template.md (✅ reviewed)
- Templates not found: .specify/templates/commands/ (⚠ no folder exists in this workspace)
- Follow-up TODOs: none
-->

# Kilo for Visual Studio Constitution

## Core Principles

### I. Code Quality and Maintainability
Code MUST be readable, modular, and strictly follow established style guides.
- Enforce linting and formatting checks in CI on every PR.
- Maintain a maximum complexity threshold (cyclomatic complexity and function length limits).
- Prefer clear intent and minimal hidden side effects.
- Ensure code is documented where behavior is non-obvious.

### II. Testing Standards
Testing MUST be part of every change, with regression coverage and defined quality gates.
- Unit tests MUST cover new logic with at least 80% module-level coverage (or higher for critical components).
- Integration tests MUST validate data contract boundaries and end-to-end flows for features affecting multiple components.
- Automated tests MUST run in CI and fail builds on regressions.
- Any bug fix MUST include a regression test case before merge.

### III. User Experience Consistency
Functional behavior, UI/UX patterns, and error handling MUST be consistent across the product.
- Follow the established design system and interaction patterns for appearance and flows.
- Errors MUST be user-friendly, actionable, and logged with context for support/troubleshooting.
- Accessibility considerations (keyboard nav, screen reader semantics, contrast) MUST be included for UI features.
- UX behavior and labels MUST be validated via review and, where applicable, usability testing.

### IV. Performance and Resource Efficiency
Performance goals MUST be defined, measured, and met for all production-facing features.
- Identify baseline metrics and target p95 or p99 latency budgets for APIs/features before implementation.
- Monitor memory, CPU, and I/O impact; avoid regressions against defined thresholds.
- Use profiling results to guide optimizations; prefer algorithmic improvement over micro-optimizations.
- Document performance trade-offs and include fallback behavior for constrained environments.

## Additional Constraints
- Security and privacy MUST be enforced for any data handled by the system (e.g., encryption in transit and at rest where required).
- External dependencies MUST be approved and periodically reviewed for licensing and security risk.

## Development Workflow
- Every change MUST be delivered through pull requests with at least one approving code review from domain experts.
- Definition of Done for a change: code review approval, CI green, tests added/updated, and documentation updates if behavior changed.
- Non-blocking upgrades and refactorings MUST include an explicit plan in PR description and risk assessment.

## Governance
- This constitution is the authoritative standard for development, testing, UX, and performance rules.
- Amendments require a documented proposal, review by maintainers, and a recorded change-log in the repository.
- Version increments:
  - MAJOR for incompatible governance changes.
  - MINOR for new principle categories or material expansion of existing governance.
  - PATCH for clarifications, typo fixes, and small refinements.
- Compliance reviews MUST be conducted quarterly; findings and remediation plans MUST be documented.
- Any conflict between this constitution and local guidelines MUST be escalated to the project governance board for resolution.

**Version**: 1.0.0 | **Ratified**: 2026-03-24 | **Last Amended**: 2026-03-24
