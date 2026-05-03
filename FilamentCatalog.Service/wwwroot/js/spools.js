// spools.js — spool list rendering, filter logic, add/edit/delete dialog

import { getSpools, createSpool, updateSpool, deleteSpool } from './api.js';
import { initializeCatalogSelects, resetCatalogSelects, restoreCatalogSelectsFromSpool } from './catalog.js';

// Module-level state
let allSpools = [];
let allOwners = [];
let pendingHighlightId = null; // spool id to glow after next renderSpools

// ---- Filter state ----
const spoolStatusFilter   = new Set();  // empty = show all
const paymentStatusFilter = new Set(); // empty = show all

// ---- DOM refs ----
const listEl       = document.getElementById('spool-list');
const filterOwner  = document.getElementById('filter-owner');
const filterMat    = document.getElementById('filter-material');
const filterSearch = document.getElementById('filter-search');
const addBtn              = document.getElementById('add-spool-btn');
const expandCollapseBtn   = document.getElementById('expand-collapse-btn');

// Spool dialog refs
const dialog        = document.getElementById('spool-dialog');
const dialogTitle   = document.getElementById('spool-dialog-title');
const form          = document.getElementById('spool-form');
const errorEl       = document.getElementById('spool-error');
const nameInput     = document.getElementById('spool-name');
const materialSelect = document.getElementById('spool-catalog-material');
const colorPicker   = document.getElementById('spool-color-picker');
const colorHexInput = document.getElementById('spool-color-hex');
const colorSwatch   = document.getElementById('spool-color-swatch');
const ownerSelect   = document.getElementById('spool-owner');
const weightInput   = document.getElementById('spool-weight');
const priceInput    = document.getElementById('spool-price');
const statusSelect  = document.getElementById('spool-status');
const paymentSelect = document.getElementById('spool-payment');
const notesInput    = document.getElementById('spool-notes');
const saveBtn       = document.getElementById('spool-save-btn');
const cancelBtn     = document.getElementById('spool-cancel-btn');
const deleteBtn     = document.getElementById('spool-delete-btn');
const deleteConfirm = document.getElementById('spool-delete-confirm');
const confirmDelBtn = document.getElementById('spool-confirm-delete-btn');

const HEX_RE = /^#[0-9A-Fa-f]{6}$/;

// ---- Color hex sync ----
colorPicker.addEventListener('input', () => {
    colorHexInput.value = colorPicker.value;
    colorSwatch.style.background = colorPicker.value;
});
colorHexInput.addEventListener('input', () => {
    if (HEX_RE.test(colorHexInput.value)) {
        colorPicker.value = colorHexInput.value;
        colorSwatch.style.background = colorHexInput.value;
    }
});
function getColorHex() {
    return HEX_RE.test(colorHexInput.value) ? colorHexInput.value : '#888888';
}

// ---- Filter logic ----
function applyFilters() {
    const ownerFilter    = filterOwner.value;
    const materialFilter = filterMat.value;
    const searchText     = filterSearch.value.trim().toLowerCase();

    const visible = allSpools.filter(spool => {
        if (ownerFilter && spool.ownerId !== parseInt(ownerFilter)) return false;
        if (materialFilter && spool.material !== materialFilter) return false;
        if (spoolStatusFilter.size > 0 && !spoolStatusFilter.has(spool.spoolStatus)) return false;
        if (paymentStatusFilter.size > 0 && !paymentStatusFilter.has(spool.paymentStatus)) return false;
        if (searchText) {
            const hay = `${spool.name} ${spool.material} ${spool.notes ?? ''}`.toLowerCase();
            if (!hay.includes(searchText)) return false;
        }
        return true;
    });

    // Show/hide individual spool rows
    const rows = listEl.querySelectorAll('.spool-row[data-id]');
    rows.forEach(row => {
        const id = parseInt(row.dataset.id);
        row.hidden = !visible.some(s => s.id === id);
    });

    // Show/hide owner groups (D-07, D-08): hide groups with 0 visible rows
    const groups = listEl.querySelectorAll('.owner-group');
    groups.forEach(group => {
        const ownerId = parseInt(group.dataset.ownerId);
        // D-07: if owner filter active, hide non-matching groups entirely
        if (ownerFilter && ownerId !== parseInt(ownerFilter)) {
            group.hidden = true;
            return;
        }
        // D-08: hide groups where no spool rows are visible
        const visibleRows = group.querySelectorAll('.spool-row:not([hidden])');
        group.hidden = visibleRows.length === 0;
    });

    // Show empty state if needed
    const emptyEl = listEl.querySelector('.empty-state');
    if (emptyEl) {
        emptyEl.hidden = visible.length > 0 || allSpools.length === 0;
        const h2 = emptyEl.querySelector('h2');
        const p  = emptyEl.querySelector('p');
        if (allSpools.length === 0) {
            h2.textContent = 'No spools yet';
            p.textContent  = 'Add your first spool to get started.';
        } else {
            h2.textContent = 'No spools match your filters.';
            p.textContent  = '';
        }
    }
}

// ---- Collapse state helpers (D-11) ----
function isCollapsed(ownerId) {
    return localStorage.getItem('fc:collapse:' + ownerId) === '1';
}

function setCollapsed(ownerId, collapsed) {
    if (collapsed) {
        localStorage.setItem('fc:collapse:' + ownerId, '1');
    } else {
        localStorage.removeItem('fc:collapse:' + ownerId);
    }
}

function applyCollapseState(groupEl, collapsed) {
    const header = groupEl.querySelector('.owner-group-header');
    const rows   = groupEl.querySelector('.owner-group-rows');
    const chevron = groupEl.querySelector('.owner-chevron');
    if (collapsed) {
        rows.hidden = true;
        header.setAttribute('aria-expanded', 'false');
        chevron.textContent = '▶';
    } else {
        rows.hidden = false;
        header.setAttribute('aria-expanded', 'true');
        chevron.textContent = '▼';
    }
}

function updateExpandCollapseBtn() {
    if (!expandCollapseBtn) return;
    const groups = listEl.querySelectorAll('.owner-group');
    const anyCollapsed = Array.from(groups).some(g => {
        const ownerId = g.dataset.ownerId;
        return isCollapsed(ownerId);
    });
    expandCollapseBtn.textContent = anyCollapsed ? 'Expand all' : 'Collapse all';
}

// ---- Material select population ----
function repopulateMaterialSelect() {
    const current = filterMat.value;
    const materials = [...new Set(allSpools.map(s => s.material))].sort();
    filterMat.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: 'All materials' }),
        ...materials.map(m => Object.assign(document.createElement('option'), { value: m, textContent: m }))
    );
    filterMat.value = materials.includes(current) ? current : '';
}

// ---- Owner select population (filter bar) ----
export function repopulateOwnerFilter(owners) {
    const current = filterOwner.value;
    filterOwner.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: 'All owners' }),
        ...owners.map(o => Object.assign(document.createElement('option'), { value: String(o.id), textContent: o.name }))
    );
    filterOwner.value = owners.some(o => String(o.id) === current) ? current : '';
}

// ---- Owner select population (spool form) ----
export function repopulateOwnerSelect(owners) {
    ownerSelect.replaceChildren(
        ...owners.map(o => Object.assign(document.createElement('option'), { value: String(o.id), textContent: o.name }))
    );
}

// ---- Spool list rendering ----
function badgeEl(text, cssClass) {
    const span = document.createElement('span');
    span.className = `badge ${cssClass}`;
    span.textContent = text;
    return span;
}

function spoolStatusBadge(status) {
    const map = { Sealed: 'badge-sealed', Active: 'badge-active', Empty: 'badge-empty' };
    return badgeEl(status, map[status] ?? 'badge-empty');
}

function paymentStatusBadge(status) {
    const map = { Paid: 'badge-paid', Unpaid: 'badge-unpaid', Partial: 'badge-partial' };
    return badgeEl(status, map[status] ?? 'badge-unpaid');
}

function formatPrice(price) {
    return price != null
        ? new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(price)
        : '—';
}

function buildSpoolRow(spool) {
    const row = document.createElement('div');
    row.className = 'spool-row';
    row.dataset.id = spool.id;

    // Color swatch
    const swatch = document.createElement('div');
    swatch.className = 'spool-color';
    swatch.style.background = spool.colorHex ?? '#888888';

    // Name + material
    const info = document.createElement('div');
    info.className = 'spool-info';
    const nameEl = document.createElement('div');
    nameEl.className = 'spool-name';
    nameEl.textContent = spool.name;       // textContent — XSS safe
    const matEl = document.createElement('div');
    matEl.className = 'spool-material';
    matEl.textContent = spool.material;    // textContent — XSS safe
    info.append(nameEl, matEl);

    // Weight
    const weightEl = document.createElement('div');
    weightEl.className = 'spool-weight';
    weightEl.textContent = spool.weightGrams != null ? `${spool.weightGrams}g` : '—';

    // Price
    const priceEl = document.createElement('div');
    priceEl.className = 'spool-price';
    priceEl.textContent = formatPrice(spool.pricePaid);

    // Status badges
    const spoolBadge   = spoolStatusBadge(spool.spoolStatus);
    const paymentBadge = paymentStatusBadge(spool.paymentStatus);

    // Notes icon
    const notesIcon = document.createElement('span');
    notesIcon.className = 'spool-notes-icon';
    if (spool.notes) {
        notesIcon.textContent = '✎';
        notesIcon.title = spool.notes;   // title attr — XSS safe (browser escapes it)
    }

    // Edit button
    const editBtn = document.createElement('button');
    editBtn.type = 'button';
    editBtn.className = 'spool-edit-btn';
    editBtn.textContent = 'Edit';
    editBtn.addEventListener('click', () => openEditDialog(spool));

    // Duplicate button
    const duplicateBtn = document.createElement('button');
    duplicateBtn.type = 'button';
    duplicateBtn.className = 'spool-edit-btn spool-duplicate-btn';
    duplicateBtn.textContent = 'Duplicate';
    duplicateBtn.addEventListener('click', () => openDuplicateDialog(spool));

    row.append(swatch, info, weightEl, priceEl, spoolBadge, paymentBadge, notesIcon, editBtn, duplicateBtn);
    return row;
}

function buildOwnerGroup(owner, spools) {
    const group = document.createElement('div');
    group.className = 'owner-group';
    group.dataset.ownerId = owner.id;

    // Header: chevron + name + badge
    const header = document.createElement('div');
    header.className = 'owner-group-header';
    header.setAttribute('role', 'button');
    header.setAttribute('aria-expanded', 'true');
    header.setAttribute('tabindex', '0');

    const chevron = document.createElement('span');
    chevron.className = 'owner-chevron';
    chevron.textContent = '▼';

    const nameEl = document.createElement('span');
    nameEl.className = 'owner-group-name';
    nameEl.textContent = owner.name;   // textContent — XSS safe

    const badge = document.createElement('span');
    badge.className = 'badge owner-spool-count';
    badge.textContent = spools.length + ' spools';

    header.append(chevron, nameEl, badge);

    // Rows container
    const rowsContainer = document.createElement('div');
    rowsContainer.className = 'owner-group-rows';
    spools.forEach(s => rowsContainer.appendChild(buildSpoolRow(s)));

    group.append(header, rowsContainer);

    // Apply persisted collapse state (D-10, D-11, D-12)
    applyCollapseState(group, isCollapsed(owner.id));

    // Toggle on header click
    header.addEventListener('click', () => {
        const nowCollapsed = !isCollapsed(owner.id);
        setCollapsed(owner.id, nowCollapsed);
        applyCollapseState(group, nowCollapsed);
        updateExpandCollapseBtn();
        applyFilters(); // re-evaluate group visibility
    });

    // Keyboard: Enter and Space also toggle
    header.addEventListener('keydown', e => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            header.click();
        }
    });

    return group;
}

export function renderSpools(spools, owners) {
    allSpools = spools;
    allOwners = owners;

    const fragment = document.createDocumentFragment();

    if (spools.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'empty-state';
        const h2 = document.createElement('h2');
        h2.textContent = 'No spools yet';
        const p = document.createElement('p');
        p.textContent = 'Add your first spool to get started.';
        empty.append(h2, p);
        fragment.appendChild(empty);
    } else {
        // Build hidden empty state (shown by applyFilters when filters produce no results)
        const empty = document.createElement('div');
        empty.className = 'empty-state';
        empty.hidden = true;
        const h2 = document.createElement('h2');
        const p  = document.createElement('p');
        empty.append(h2, p);
        fragment.appendChild(empty);

        // Sort owners: isMe first, then alphabetically by name
        const sortedOwners = [...owners].sort((a, b) => {
            if (a.isMe) return -1;
            if (b.isMe) return 1;
            return a.name.localeCompare(b.name);
        });

        // Group spools by owner; sorted owner order; sort by name within each group
        sortedOwners.forEach(owner => {
            const ownerSpools = spools
                .filter(s => s.ownerId === owner.id)
                .sort((a, b) => a.name.localeCompare(b.name));
            if (ownerSpools.length === 0) return; // skip owners with no spools
            fragment.appendChild(buildOwnerGroup(owner, ownerSpools));
        });
    }

    listEl.replaceChildren(fragment);
    repopulateMaterialSelect();
    repopulateOwnerFilter(owners);
    repopulateOwnerSelect(owners);
    applyFilters();
    updateExpandCollapseBtn();

    if (pendingHighlightId !== null) {
        const highlightId = pendingHighlightId;
        pendingHighlightId = null;
        // Defer one frame so the browser commits the new DOM before the animation starts
        requestAnimationFrame(() => {
            const row = listEl.querySelector(`.spool-row[data-id="${highlightId}"]`);
            if (!row) return;
            // Expand the owner group if it is collapsed so the glow is visible
            const group = row.closest('.owner-group');
            if (group && isCollapsed(group.dataset.ownerId)) {
                setCollapsed(group.dataset.ownerId, false);
                applyCollapseState(group, false);
                updateExpandCollapseBtn();
            }
            row.classList.add('spool-row--highlight');
            row.addEventListener('animationend', () => row.classList.remove('spool-row--highlight'), { once: true });
        });
    }
}

// ---- Dialog error helpers ----
function showDialogError(message) {
    errorEl.textContent = message;
    errorEl.style.display = 'block';
}
function clearDialogError() {
    errorEl.textContent = '';
    errorEl.style.display = 'none';
}

// ---- Reset form to blank (add mode) ----
function resetFormForAdd() {
    dialogTitle.textContent = 'Add Spool';
    form.reset();
    resetCatalogSelects(); // reset two-step catalog selects to placeholder state
    colorPicker.value = '#888888';
    colorHexInput.value = '#888888';
    colorSwatch.style.background = '#888888';
    statusSelect.value = 'Sealed';
    paymentSelect.value = 'Unpaid';
    deleteBtn.style.display = 'none';
    deleteConfirm.style.display = 'none';
    clearDialogError();
    dialog.dataset.editId = '';
}

// ---- Populate form for edit mode ----
function populateFormForEdit(spool) {
    dialogTitle.textContent = 'Edit Spool';
    nameInput.value = spool.name;
    const hex = spool.colorHex ?? '#888888';
    colorPicker.value = HEX_RE.test(hex) ? hex : '#888888';
    colorHexInput.value = hex;
    colorSwatch.style.background = colorPicker.value;
    ownerSelect.value = String(spool.ownerId);
    weightInput.value = spool.weightGrams ?? '';
    priceInput.value  = spool.pricePaid  ?? '';
    statusSelect.value  = spool.spoolStatus;
    paymentSelect.value = spool.paymentStatus;
    notesInput.value    = spool.notes ?? '';
    deleteBtn.style.display = '';
    deleteConfirm.style.display = 'none';
    clearDialogError();
    dialog.dataset.editId = String(spool.id);
}

function openAddDialog() {
    resetFormForAdd();
    repopulateOwnerSelect(allOwners);
    initializeCatalogSelects(); // populate material select from catalog
    dialog.showModal();
}

function openEditDialog(spool) {
    resetFormForAdd();
    repopulateOwnerSelect(allOwners);
    populateFormForEdit(spool);
    restoreCatalogSelectsFromSpool(spool); // async but fire-and-forget — restores after colors load
    dialog.showModal();
}

function openDuplicateDialog(spool) {
    resetFormForAdd();
    repopulateOwnerSelect(allOwners);
    restoreCatalogSelectsFromSpool(spool); // async but fire-and-forget — restores after colors load
    // Pre-fill all fields from source spool (mirrors populateFormForEdit but stays in add mode)
    nameInput.value     = spool.name;
    const hex = HEX_RE.test(spool.colorHex ?? '') ? spool.colorHex : '#888888';
    colorPicker.value   = hex;
    colorHexInput.value = hex;
    colorSwatch.style.background = hex;
    ownerSelect.value   = String(spool.ownerId);
    weightInput.value   = spool.weightGrams ?? '';
    priceInput.value    = spool.pricePaid  ?? '';
    statusSelect.value  = spool.spoolStatus;
    paymentSelect.value = spool.paymentStatus;
    notesInput.value    = spool.notes ?? '';
    dialogTitle.textContent = 'Duplicate Spool';  // override 'Add Spool' set by resetFormForAdd
    dialog.showModal();
}

// ---- Build request payload from form ----
function buildSpoolPayload() {
    return {
        name:          nameInput.value.trim(),
        material:      materialSelect.value.trim(),
        colorHex:      getColorHex(),
        ownerId:       parseInt(ownerSelect.value),
        weightGrams:   weightInput.value ? parseInt(weightInput.value) : null,
        pricePaid:     priceInput.value  ? parseFloat(priceInput.value) : null,
        spoolStatus:   statusSelect.value,
        paymentStatus: paymentSelect.value,
        notes:         notesInput.value.trim() || null
    };
}

// ---- Save (add or edit) ----
saveBtn.addEventListener('click', async () => {
    clearDialogError();
    const editId = dialog.dataset.editId;
    const payload = buildSpoolPayload();

    // Client-side required field check (mirrors server validation)
    if (!payload.name)     { showDialogError('Name is required.'); return; }
    if (!payload.material) { showDialogError('Select a material to continue.'); return; }
    if (!payload.ownerId)  { showDialogError('Owner is required.'); return; }

    try {
        if (editId) {
            pendingHighlightId = parseInt(editId);
            await updateSpool(parseInt(editId), payload);
        } else {
            const created = await createSpool(payload);
            pendingHighlightId = created.id;
        }
        dialog.close();
        // Refresh is triggered by the 'close' event listener in app.js
    } catch (err) {
        pendingHighlightId = null;
        showDialogError(err.message);
    }
});

// ---- Delete ----
deleteBtn.addEventListener('click', () => {
    deleteConfirm.style.display = deleteConfirm.style.display === 'none' ? '' : 'none';
});

confirmDelBtn.addEventListener('click', async () => {
    const editId = dialog.dataset.editId;
    if (!editId) return;
    clearDialogError();
    try {
        await deleteSpool(parseInt(editId));
        dialog.close();
    } catch (err) {
        showDialogError(err.message);
    }
});

// ---- Cancel ----
cancelBtn.addEventListener('click', () => dialog.close());

// ---- Backdrop click closes dialog ----
dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });

// ---- Add Spool button ----
export function initSpoolDialog() {
    addBtn.addEventListener('click', openAddDialog);
}

// ---- Chip filter wiring ----
export function initChipFilters() {
    document.querySelectorAll('.chip[data-status-filter="spool"]').forEach(btn => {
        btn.addEventListener('click', () => {
            const val = btn.dataset.value;
            if (spoolStatusFilter.has(val)) {
                spoolStatusFilter.delete(val);
                btn.classList.remove('active');
            } else {
                spoolStatusFilter.add(val);
                btn.classList.add('active');
            }
            applyFilters();
        });
    });
    document.querySelectorAll('.chip[data-status-filter="payment"]').forEach(btn => {
        btn.addEventListener('click', () => {
            const val = btn.dataset.value;
            if (paymentStatusFilter.has(val)) {
                paymentStatusFilter.delete(val);
                btn.classList.remove('active');
            } else {
                paymentStatusFilter.add(val);
                btn.classList.add('active');
            }
            applyFilters();
        });
    });
    filterOwner.addEventListener('change', applyFilters);
    filterMat.addEventListener('change', applyFilters);
    filterSearch.addEventListener('input', applyFilters);
}

// ---- Expose dialog close signal for app.js ----
export function onSpoolDialogClose(callback) {
    dialog.addEventListener('close', callback);
}

// ---- Expand/Collapse All button wiring ----
export function initExpandCollapseBtn() {
    if (!expandCollapseBtn) return;
    expandCollapseBtn.addEventListener('click', () => {
        const groups = listEl.querySelectorAll('.owner-group');
        const anyCollapsed = Array.from(groups).some(g => isCollapsed(g.dataset.ownerId));
        // If any collapsed — expand all; otherwise collapse all
        const targetCollapsed = !anyCollapsed;
        groups.forEach(g => {
            const ownerId = g.dataset.ownerId;
            setCollapsed(ownerId, targetCollapsed);
            applyCollapseState(g, targetCollapsed);
        });
        expandCollapseBtn.textContent = targetCollapsed ? 'Expand all' : 'Collapse all';
        applyFilters();
    });
}
