---
status: resolved
phase: 06-ui-layout-redesign
source: [06-VERIFICATION.md]
started: 2026-05-03T00:00:00Z
updated: 2026-05-03T00:00:00Z
---

## Current Test

All tests passed.

## Tests

### 1. Sticky sidebar scroll behaviour
expected: Open http://localhost:5000 with enough spools to overflow the viewport, scroll down — the #balance-sidebar (240px left column) stays anchored at 48px from the viewport top; balance content never goes off-screen
result: [passed]

### 2. Owner group collapse/expand visual behaviour
expected: Click any owner-group header row — child spool rows hide and chevron changes from ▼ to ▶; click again and rows reappear with chevron returning to ▼
result: [passed]

### 3. localStorage collapse persistence across page reload
expected: Collapse one owner group, reload the page — the previously-collapsed group remains collapsed (fc:collapse:{id} key with value "1" is read from localStorage on buildOwnerGroup)
result: [passed]

### 4. Expand/Collapse All button label cycling
expected: Click "Collapse all" — all groups collapse and button label changes to "Expand all"; click again — all groups expand and label reverts to "Collapse all"
result: [passed]

## Summary

total: 4
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
