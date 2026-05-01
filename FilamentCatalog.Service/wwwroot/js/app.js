// app.js — page-load init and cross-module coordination

import { getSpools, getOwners, getSummary, getBalance } from './api.js';
import { renderSpools, initSpoolDialog, initChipFilters, onSpoolDialogClose, repopulateOwnerFilter, repopulateOwnerSelect } from './spools.js';
import { initOwnerModal } from './owners.js';
import { renderSummary, renderBalance, refreshSummaryAndBalance } from './summary.js';

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
