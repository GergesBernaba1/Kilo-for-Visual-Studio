# Kilo for Visual Studio 2022 - Current Gaps

This file tracks what is still missing after the current MVP-level implementation.

## 1) Installer and Packaging

- [ ] Sign VSIX package for trusted enterprise installation.
- [ ] Add Marketplace-ready metadata and legal links.
- [ ] Add preview image and richer branding assets.

## 2) Core Product Gaps

- [ ] Persistent multi-session chat UX (tabbed sessions and restore behavior).
- [ ] Full patch workflow parity (preview, selective apply, conflict handling).
- [ ] Strong project-wide context ingestion (beyond current file/selection flow).
- [ ] Robust reconnect and resilience behavior for backend outages.

## 3) Editor Integration Gaps

- [ ] True context-menu coverage across all relevant editor surfaces.
- [ ] Rich code actions/light-bulb workflows for explain/refactor/fix.
- [ ] Improved inline completion quality and trigger behavior.

## 4) Automation and Agent Gaps

- [ ] Deeper autonomous workflow orchestration and safe-approval controls.
- [ ] Expand built-in automation templates and execution visibility.
- [ ] Add stronger sub-agent/session inspection and control surface.

## 5) Security and Settings Gaps

- [ ] Move sensitive secrets to secure storage by default.
- [ ] Add stricter validation for backend/provider configuration.
- [ ] Add policy-friendly enterprise settings profile support.

## 6) Quality and Testing Gaps

- [ ] Add Visual Studio integration/UI automation tests.
- [ ] Add regression tests for command/menu registration and tool window activation.
- [ ] Add performance and startup impact baseline checks.

## 7) Short-Term Priority Order

1. Complete packaging/signing and marketplace readiness.
2. Finish command/tool-window discoverability and usability hardening.
3. Expand session/diff workflows and reliability.
4. Increase integration test coverage before broad distribution.
