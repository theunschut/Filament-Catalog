// catalog.js — two-step catalog picker for the spool Add/Edit/Duplicate dialog
// Manages #spool-catalog-material and #spool-catalog-color selects

import { getCatalogMaterials, getCatalogColors } from './api.js';

// ---- DOM refs ----
const materialSelect = document.getElementById('spool-catalog-material');
const colorSelect    = document.getElementById('spool-catalog-color');
const nameInput      = document.getElementById('spool-name');
const colorHexInput  = document.getElementById('spool-color-hex');
const colorPicker    = document.getElementById('spool-color-picker');
const colorSwatch    = document.getElementById('spool-color-swatch');

// ---- Material change: rebuild color select ----
materialSelect.addEventListener('change', async () => {
    // Reset color select to loading state
    colorSelect.disabled = true;
    colorSelect.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: 'Loading…' })
    );

    if (!materialSelect.value) {
        colorSelect.replaceChildren(
            Object.assign(document.createElement('option'), { value: '', textContent: '— Select material first —' })
        );
        return;
    }

    try {
        const colors = await getCatalogColors(materialSelect.value);
        colorSelect.replaceChildren(
            Object.assign(document.createElement('option'), { value: '', textContent: '— Select color —' })
        );
        colors.forEach(color => {
            const opt = document.createElement('option');
            opt.value = String(color.id);
            opt.dataset.colorName    = color.colorName;
            opt.dataset.colorHex     = color.colorHex;
            opt.dataset.productTitle = color.productTitle;
            opt.textContent = color.colorName;  // textContent — XSS safe
            colorSelect.appendChild(opt);
        });
        colorSelect.disabled = false;
    } catch (err) {
        console.error('Failed to load colors for material', materialSelect.value, err);
        colorSelect.replaceChildren(
            Object.assign(document.createElement('option'), { value: '', textContent: 'Failed to load colors' })
        );
    }
});

// ---- Color change: auto-fill name and color fields ----
colorSelect.addEventListener('change', () => {
    if (!colorSelect.value) return;

    const opt = colorSelect.selectedOptions[0];
    const productTitle = opt.dataset.productTitle ?? '';
    const colorName    = opt.dataset.colorName    ?? '';
    const colorHex     = opt.dataset.colorHex     ?? '#888888';

    // Auto-fill name: "Product Title — Color Name" per CONTEXT.md D-02
    nameInput.value = `${productTitle} — ${colorName}`;

    // Auto-fill color fields per CONTEXT.md D-04 (stays editable)
    colorHexInput.value = colorHex;
    colorPicker.value   = colorHex;
    colorSwatch.style.background = colorHex;
});

// ---- Export: initialize (call when dialog opens in add/duplicate mode) ----
export async function initializeCatalogSelects() {
    // Reset selects to empty/placeholder state first
    resetCatalogSelects();

    try {
        const materials = await getCatalogMaterials();
        materialSelect.replaceChildren(
            Object.assign(document.createElement('option'), { value: '', textContent: '— Select material —' })
        );
        materials.forEach(mat => {
            const opt = document.createElement('option');
            opt.value = mat;
            opt.textContent = mat;  // textContent — XSS safe
            materialSelect.appendChild(opt);
        });
    } catch (err) {
        console.error('Failed to load catalog materials', err);
    }
}

// ---- Export: reset selects to blank state (called by resetFormForAdd in spools.js) ----
export function resetCatalogSelects() {
    materialSelect.value = '';
    materialSelect.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: '— Select material —' })
    );
    colorSelect.disabled = true;
    colorSelect.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: '— Select material first —' })
    );
}

// ---- Export: restore selects from saved spool data (edit/duplicate dialogs) ----
// Per CONTEXT.md D-03: restore uses spool.material to pre-select material,
// then loads colors for that material, then matches colorName from spool.name.
export async function restoreCatalogSelectsFromSpool(spool) {
    if (!spool.material) return;

    // Step 1: populate material select and pre-select the spool's material
    await initializeCatalogSelects();
    materialSelect.value = spool.material;

    if (!materialSelect.value) return; // Material not found in catalog (product deleted)

    // Step 2: load colors for the selected material
    colorSelect.disabled = true;
    colorSelect.replaceChildren(
        Object.assign(document.createElement('option'), { value: '', textContent: 'Loading…' })
    );

    try {
        const colors = await getCatalogColors(spool.material);
        colorSelect.replaceChildren(
            Object.assign(document.createElement('option'), { value: '', textContent: '— Select color —' })
        );
        colors.forEach(color => {
            const opt = document.createElement('option');
            opt.value = String(color.id);
            opt.dataset.colorName    = color.colorName;
            opt.dataset.colorHex     = color.colorHex;
            opt.dataset.productTitle = color.productTitle;
            opt.textContent = color.colorName;  // textContent — XSS safe
            colorSelect.appendChild(opt);
        });
        colorSelect.disabled = false;

        // Step 3: pre-select color by matching the expected name pattern
        // spool.name format: "Product Title — Color Name" (per CONTEXT.md D-02)
        // Extract color name by finding which option's productTitle + colorName matches
        const matchingOpt = [...colorSelect.options].find(o => {
            if (!o.value) return false;
            const expectedName = `${o.dataset.productTitle} — ${o.dataset.colorName}`;
            return expectedName === spool.name || o.dataset.colorName === spool.name;
        });

        if (matchingOpt) {
            colorSelect.value = matchingOpt.value;
            // Do NOT auto-fill name/hex here — preserve values already set by populateFormForEdit
        }
    } catch (err) {
        console.error('Failed to restore catalog selects for spool', spool.id, err);
    }
}
