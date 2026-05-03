---
status: partial
phase: 06-ui-layout-redesign
source: [06-VERIFICATION.md]
started: 2026-05-03T00:00:00Z
updated: 2026-05-03T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Sticky sidebar scroll behaviour
expected: Open http://localhost:5000 with enough spools to overflow the viewport, scroll down — the #balance-sidebar (240px left column) stays anchored at 48px from the viewport top; balance content never goes off-screen
result: [pending]

### 2. Owner group collapse/expand visual behaviour
expected: Click any owner-group header row — child spool rows hide and chevron changes from ▼ to ▶; click again and rows reappear with chevron returning to ▼
result: [pending]

### 3. localStorage collapse persistence across page reload
expected: Collapse one owner group, reload the page — the previously-collapsed group remains collapsed (fc:collapse:{id} key with value "1" is read from localStorage on buildOwnerGroup)
result: [pending]

### 4. Expand/Collapse All button label cycling
expected: Click "Collapse all" — all groups collapse and button label changes to "Expand all"; click again — all groups expand and label reverts to "Collapse all"
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
