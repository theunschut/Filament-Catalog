# Features Research

**Domain:** Filament inventory / catalog app for a single-owner, multi-friend 3D printing setup
**Researched:** 2026-04-30
**Comparable apps surveyed:** Spoolman, Spoolstock, BambuTracker, SimplyPrint Filament Manager, Filament.uno, FilaMan

---

## Table Stakes (must have or app feels broken)

These are features users of any filament tracker expect. Their absence creates friction or distrust.

| Feature | Why Expected | Status in Spec | Notes |
|---------|--------------|----------------|-------|
| Color swatch visible per spool in the list | Filament IS its color — a list of names alone is useless for inventory scanning | Implied (catalog sync extracts color) | Must be visible in the spool list row, not just in add/edit dialogs |
| Material type visible per spool | PLA vs PETG vs ABS changes print settings entirely — material is load-bearing context | Implied via catalog | Should be a prominent field, not buried |
| Spool status (sealed / active / empty) | Users need to know what's usable at a glance | In spec | Good |
| Owner visible per spool | Core to the friend-tracking use case | In spec | Good |
| Free-text search | Any list of 30+ items becomes painful without it | In spec | Good |
| Filter by material | "Show me all my PETG" is a daily query | In spec (filter by status/payment) | Verify material filter is included — spec lists "owner, status, payment, free text" but does not explicitly name material as a filter facet |
| Filter by owner | "What does Alice still owe me?" | In spec | Good |
| Total value summary | Core for the debt-tracking use case | In spec | Good |
| Edit and delete spools | Data entry mistakes happen | In spec | Good |
| Graceful catalog sync failure | Scraper will break when Bambu redesigns their store | Not in spec | The app must not crash or lose data when the sync fails; show a clear error and keep using the last good catalog snapshot |

**Gap found:** The spec lists filters for "owner, status, payment, and free text" but does not explicitly include material type as a filter. For a filament catalog where PLA/PETG/ABS/ASA are fundamentally different things, this is a near-table-stakes omission worth adding.

---

## Differentiators (make this app notably good)

These go beyond what comparable apps offer. The catalog scraper is already the primary differentiator; these build on it.

### 1. Catalog-backed add flow (already in spec — this IS the killer feature)

Every comparable app (Spoolman, Spoolstock, FilaMan) requires manual entry of brand, material, color name, and color hex. BambuTracker comes closest with real-time Bambu store integration but does not extract a canonical hex color per product for offline use. The spec's approach — scrape once, store locally, pick from a dropdown with a live color preview — is meaningfully faster for the primary workflow (add a spool you just received). Do not dilute this by also supporting manual entry as a co-equal path in v1. Catalog-backed should be the only path.

### 2. Per-friend balance summary with spool breakdown

Competitors with any debt tracking (essentially none at this level of specificity) show only a total. The spec's balance overview per friend (spool count + total value + amount owed) is the right level of detail. Add one more thing: make each row in the balance view clickable to jump to a filtered spool list showing only that friend's unpaid spools. This closes the loop: "Alice owes €47 → [click] → here are her 3 unpaid spools."

### 3. Color swatch as a column in the spool list (not just at add time)

BambuTracker shows color swatches in its catalog view. Most Spoolman-style apps show a color dot in the list but it is tiny (8–12px). For a filament-specific app, the color IS the primary identifier alongside material. A 24–32px colored circle in the list row (with a thin border for light colors like white/ivory) makes the list scannable in a way that no competitor currently does well. This is cheap to build given the color extraction is already in the stack.

### 4. Spool count badge per material in the summary bar

The spec has "total spools, my spools, total value, total owed." A small addition: show a breakdown by material (e.g., "PLA: 12 · PETG: 4 · ABS: 3") either in the summary bar or as a secondary stats section. This answers the question "do I have enough PETG for this weekend?" without opening a filter. Low build cost, genuinely useful.

### 5. Notes field per spool (small but expected)

Every mature filament tracker (Spoolstock, BambuTracker, SimplyPrint) supports a freetext notes field per spool. Common uses: "dried at 65°C for 6h", "slight moisture — use soon", "special order for project X". The spec does not include this. It is a one-column addition to the spools table and a single textarea in the add/edit form. Omitting it means users have no way to record per-spool context, which is a felt gap once the catalog is populated.

### 6. Purchase date per spool (optional, defer to v1 polish)

Comparable apps track this. It feeds "how old is this spool?" questions and would enable future sorted views. Low priority for v1 but worth one nullable date column on the spools table now, even if the UI just auto-populates it with today's date silently. Retrofitting a date column later requires a migration.

---

## Anti-Features (tempting but avoid in v1)

These look like improvements but add complexity without proportionate value for this specific use case.

| Anti-Feature | Why It Seems Useful | Why to Avoid in v1 | What to Do Instead |
|--------------|---------------------|--------------------|-------------------|
| Weight remaining (grams) per spool | Spoolman's core feature; lets you know if a spool has enough material for a job | Requires a scale, a workflow change, and makes every "active" spool require ongoing maintenance. The spec explicitly calls this out-of-scope. Sealed/active/empty is sufficient for tracking who owns what. | Stick with status (sealed / active / empty). |
| Partial payment amount (e.g., "paid €8 of €22") | Splitwise-style granularity feels more accurate | Splits the debt model into a running ledger. For spools at €15–30 each, people either paid or they didn't. The spec's "partial = treated as unpaid" is pragmatic and correct. | Keep paid / unpaid / partial. Partial is a signal, not a ledger entry. |
| QR code / NFC label generation | Spoolstock and SimplyPrint offer this for shelf scanning | Adds a print dependency (label printer or QR stickers). For a personal setup with ~30 spools, a searchable list is faster than scanning. | Use the spool list + filters. |
| Multiple Bambu store regions (US, EU, UK, etc.) | Internationalization feels correct | The catalog structure will differ per region. Scraping one region well is harder than it looks. EU is the confirmed target. | Scrape EU store only. If a region flag is ever needed, add it to the Catalog table, not as a UI option. |
| Printer/AMS slot tracking | BambuTracker tracks which AMS slot a filament is loaded in | Adds a second inventory layer (printers → slots → spools) with no debt-tracking payoff. | Out of scope. |
| Print job history per spool | Spoolman's integration with OctoPrint/Klipper enables this | Requires either print farm software integration or manual logging. Neither fits the local Windows-service model. | Out of scope. |
| CSV import/export | BambuTracker supports this for bulk workflows | For a personal catalog starting from zero, import is not needed. Export is useful but not v1. | Add export in a later milestone if users want backups. SQLite file is already a backup. |
| Wish list / "want to buy" spools | BambuTracker has this | Conflates two separate concerns (owned inventory vs. shopping list) in one UI. | Out of scope. |

---

## Debt/Balance Model Analysis

**The spec's model:** Sum all spool prices where `owner != me AND payment_status IN ('unpaid', 'partial')`. Each friend has a balance = sum of their unpaid spool prices.

**Assessment: This is correct and sufficient for the stated use case.**

The model is simple, auditable, and matches the stated workflow: "Friends buy rolls via Theun; some pay upfront, some pay later." This is not a Splitwise-style ledger — it is a receivables tracker. Spool price is set at time of logging, not derived from Bambu catalog prices, which matters.

**Edge cases worth handling:**

1. **Price not set / zero price.** If a spool is added without a price (or the catalog product has no price), the balance calculation silently contributes €0. The UI should visually flag zero-price unpaid spools so the user knows the balance is incomplete. A simple warning icon or "(price missing)" label on the balance summary row is sufficient.

2. **Spool deleted after partial payment.** If a friend paid partially and the spool is later deleted, the debt record vanishes with it. For v1 this is acceptable — the app is a current-state tracker, not a historical ledger. Document this as a known limitation.

3. **Spool status vs. payment status mismatch.** An empty spool that is still "unpaid" is valid (friend hasn't paid yet despite using the filament). The model handles this correctly because payment status is independent of spool status. No fix needed — just ensure the UI does not hide unpaid spools from the balance view because their status is "empty".

4. **"Partial" treated as unpaid.** This is the right call. The alternative — storing a partial amount paid — creates a running balance per spool, which is a ledger pattern. Ledgers need history, reconciliation, and correction flows. For €15–30 spools, the operational cost exceeds the benefit.

5. **Owner deletion with open balance.** The spec says delete is rejected if an owner has spools. This is the correct guard. Consider whether "mark as paid" should be required before deletion is allowed, or if bulk-marking all spools paid + then deleting the owner should be a single supported flow.

6. **Currency.** The spec targets EU store. Prices are in euros. The data model should store prices as a numeric value (DECIMAL or INTEGER cents) and the UI should display a currency symbol. Do not hard-code "€" into the calculation logic — store the symbol or ISO code alongside the price if there is any chance of mixed currencies (e.g., a friend sourced a spool from elsewhere at a different price point).

**Verdict:** The debt model is sound. The main risk is silent zero-price entries inflating trust in the balance totals. Add a visual flag for that case.

---

## UX Patterns Worth Noting

### Color swatches in list rows

- Display swatches as filled circles, 24–28px diameter, with a 1px border in a neutral gray. Without a border, white and ivory filaments become invisible against a white background.
- For metallic/glitter/special-effect filaments, the extracted hex will be an approximation. A tooltip on hover showing the product name ("Bambu Matte PLA — Ivory White") prevents misidentification.
- Do not use swatches as interactive filter controls in the list. Keep them as display-only. Filtering by color is a niche use case; the material + owner filters cover 90% of real queries.

### Filter bar placement

- Filters above the list (horizontal pill row) work better than a sidebar for a small number of filter facets (4–6). Given the spec has owner, material, status, payment status, and search text, a compact top-bar filter fits the single-page layout without needing a collapsible sidebar.
- Combine the free-text search and the filter pills in one bar. "Active filters" shown as dismissible tags (e.g., "Owner: Alice ×") is a well-understood pattern from e-commerce that maps directly here.

### Balance view vs. spool list — keep them separate

Debt-tracking UX research (Splitwise, expense-sharing case studies) consistently shows users want two distinct views: (a) the per-person balance summary and (b) the itemized list. Merging them into one table where friends are expandable rows adds accordion complexity for no gain at this scale. The spec's separate balance overview is the right call. Keep it as a dedicated section/tab.

### Inline edit vs. modal edit

For a spool list that may grow to 50–100+ rows, inline row editing (click a cell, it becomes an input) creates accidental edit risk and is hard to implement cleanly in plain HTML/CSS/JS. A modal edit form (click "edit" → modal opens with all fields) is the standard pattern for data-heavy local apps and is easier to build and maintain. The spec implies modal edit; confirm this in implementation.

### Confirmations for destructive actions

Delete spool and mark-all-paid for a friend are irreversible. Show a simple confirmation dialog. For delete, show the spool's color swatch and product name in the confirmation message ("Delete Bambu PLA Matte — Jade Green?") so the user can confirm they targeted the right row.

### Empty states matter

When the catalog has never been synced, the "add spool" dropdown is empty. This is the first thing a new user sees and it should not look like a bug. Show a clear empty state: "No catalog loaded — run a sync first" with a button that goes to the sync action. Similarly, an empty spool list should say "No spools yet — add your first spool" rather than showing a blank table.

### Summary bar stickiness

If the spool list becomes long (scrollable), consider making the summary bar (total spools, total value, total owed) sticky at the top. Losing sight of the totals while scrolling through 80 spools is a minor but felt annoyance.

---

## Sources

- [Spoolman on GitHub](https://github.com/Donkie/Spoolman) — feature set, architecture reference
- [BambuTracker](https://bambutracker.com/) — closest competitor with Bambu-specific catalog integration
- [Spoolstock on App Store](https://apps.apple.com/us/app/spoolstock-3d-printing-hub/id6480470069) — mobile filament tracker feature set
- [SimplyPrint Filament Manager](https://help.simplyprint.io/en/article/the-filament-manager-feature-track-organize-and-manage-your-filament-inventory-bpy529/) — professional-grade filament tracking
- [Filament.uno](https://www.filament.uno/) — lightweight community tracker
- [Baymard: Mobile Color Swatches](https://baymard.com/blog/mobile-interactive-color-swatches) — color swatch UX research (MEDIUM confidence — paywalled study, headline visible)
- [Splitwise UX case study](https://uxdesign.cc/splitwise-a-ux-case-study-dc2581971226) — debt/balance display patterns
- [Filter UI and UX 101 — UXPin](https://www.uxpin.com/studio/blog/filter-ui-and-ux/) — filter bar placement patterns
- [Prusa Forum: Tracking filament inventory](https://forum.prusa3d.com/forum/english-forum-general-discussion-announcements-and-releases/tracking-filament-inventory/) — community wishlist and pain points
