// app.js — page-load init and cross-module coordination

import { getSpools, getOwners, getSummary, getBalance, startSync, getSyncStatus, getCatalogCount } from './api.js';
import { renderSpools, initSpoolDialog, initChipFilters, onSpoolDialogClose, repopulateOwnerFilter, repopulateOwnerSelect } from './spools.js';
import { initOwnerModal } from './owners.js';
import { renderSummary, renderBalance, refreshSummaryAndBalance } from './summary.js';

// ---- DOM refs ----
const syncCatalogBtn   = document.getElementById('sync-catalog-btn');
const statLastSynced   = document.getElementById('stat-last-synced');
const catalogNotice    = document.getElementById('catalog-empty-notice');
const addSpoolBtn      = document.getElementById('add-spool-btn');

// ---- Page-load init ----
try {
    const [spools, owners, summary, balance] = await Promise.all([
        getSpools(), getOwners(), getSummary(), getBalance()
    ]);

    renderSummary(summary);
    renderBalance(balance);
    renderSpools(spools, owners);
    initSpoolDialog();
    initChipFilters();
    initOwnerModal();
    // ---- Catalog gate (per CONTEXT.md D-05) ----
    await initCatalogGate();
} catch (err) {
    // Show load error — API not reachable
    document.getElementById('spool-list').innerHTML = '';
    const errEl = document.createElement('p');
    errEl.style.cssText = 'color:#6b7280;font-style:italic;padding:16px';
    errEl.textContent = 'Could not load data. Is the service running?';
    document.getElementById('spool-list').appendChild(errEl);
}

// ---- After spool dialog closes (add/edit/delete) — refresh data ----
onSpoolDialogClose(async () => {
    try {
        const [spools, owners, summary, balance] = await Promise.all([
            getSpools(), getOwners(), getSummary(), getBalance()
        ]);
        renderSpools(spools, owners);
        renderSummary(summary);
        renderBalance(balance);
    } catch (err) {
        console.error('Failed to refresh after spool mutation:', err);
    }
});

// ---- After owner modal closes — refresh owner selects ----
document.addEventListener('owners-updated', async () => {
    try {
        const owners = await getOwners();
        repopulateOwnerFilter(owners);
        repopulateOwnerSelect(owners);
        // Also refresh balance (owner count may have changed)
        await refreshSummaryAndBalance();
    } catch (err) {
        console.error('Failed to refresh after owner mutation:', err);
    }
});

// ---- Sync button and status display ----

// Format ISO UTC date string for display per UI-SPEC copywriting contract
function formatLastSynced(isoString) {
    if (!isoString) return 'Never';
    try {
        return new Date(isoString).toLocaleString('en-GB', {
            day: 'numeric', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    } catch {
        return isoString; // fallback if parsing fails
    }
}

// Check catalog count and set Add Spool button gate state
async function initCatalogGate() {
    try {
        const { count } = await getCatalogCount();
        setCatalogGate(count);

        // Also update last-synced display from current sync status
        const status = await getSyncStatus();
        if (status.lastSyncedAt) {
            statLastSynced.textContent = formatLastSynced(status.lastSyncedAt);
            statLastSynced.style.color = '';
        }
    } catch (err) {
        console.error('Failed to initialize catalog gate', err);
    }
}

// Enable/disable Add Spool button based on catalog count
function setCatalogGate(count) {
    if (count === 0) {
        addSpoolBtn.disabled = true;
        addSpoolBtn.style.opacity = '0.5';
        addSpoolBtn.style.cursor = 'not-allowed';
        catalogNotice.hidden = false;
    } else {
        addSpoolBtn.disabled = false;
        addSpoolBtn.style.opacity = '';
        addSpoolBtn.style.cursor = '';
        catalogNotice.hidden = true;
    }
}

// Poll /api/sync/status until status is 'completed' or 'error'
async function pollSyncStatus() {
    const POLL_INTERVAL_MS = 500; // per RESEARCH.md recommendation
    const MAX_POLLS = 600;        // 5-minute hard cap (600 × 500 ms)
    let polls = 0;

    while (polls++ < MAX_POLLS) {
        try {
            const status = await getSyncStatus();

            if (status.processedCount > 0 && status.totalEstimate > 0) {
                const pct = status.percentComplete ?? Math.round((status.processedCount / status.totalEstimate) * 100);
                syncCatalogBtn.textContent = `Syncing… (${pct}%)`;
            } else {
                syncCatalogBtn.textContent = 'Syncing…';
            }

            if (status.status === 'completed') {
                // Sync done — update UI
                syncCatalogBtn.textContent = 'Sync Bambu catalog';
                syncCatalogBtn.disabled = false;
                statLastSynced.textContent = formatLastSynced(status.lastSyncedAt);
                statLastSynced.style.color = '';
                // Re-enable Add Spool button now that catalog has data
                const { count } = await getCatalogCount();
                setCatalogGate(count);
                break;
            }

            if (status.status === 'error') {
                syncCatalogBtn.textContent = 'Sync Bambu catalog';
                syncCatalogBtn.disabled = false;
                statLastSynced.textContent = 'Sync failed';
                statLastSynced.style.color = 'var(--color-destructive)';
                console.error('Sync failed:', status.errorMessage);
                break;
            }
        } catch (err) {
            console.error('Polling error:', err);
            syncCatalogBtn.textContent = 'Sync Bambu catalog';
            syncCatalogBtn.disabled = false;
            statLastSynced.textContent = 'Sync failed';
            statLastSynced.style.color = 'var(--color-destructive)';
            break;
        }

        await new Promise(r => setTimeout(r, POLL_INTERVAL_MS));
    }

    // Timed out — treat as error so the button re-enables
    syncCatalogBtn.textContent = 'Sync Bambu catalog';
    syncCatalogBtn.disabled = false;
    statLastSynced.textContent = 'Sync timed out';
    statLastSynced.style.color = 'var(--color-destructive)';
}

// Sync button click handler
syncCatalogBtn.addEventListener('click', async () => {
    if (syncCatalogBtn.disabled) return;

    try {
        syncCatalogBtn.textContent = 'Syncing…';
        syncCatalogBtn.disabled = true;
        statLastSynced.textContent = 'Syncing…';
        statLastSynced.style.color = 'var(--color-muted)';

        await startSync();
        await pollSyncStatus();
    } catch (err) {
        syncCatalogBtn.textContent = 'Sync Bambu catalog';
        syncCatalogBtn.disabled = false;
        statLastSynced.textContent = 'Sync failed';
        statLastSynced.style.color = 'var(--color-destructive)';
        console.error('Failed to start sync:', err);
    }
});
