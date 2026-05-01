// owners.js — owner management modal

import { getOwners, createOwner, deleteOwner } from './api.js';

const dialog    = document.getElementById('owner-dialog');
const listEl    = document.getElementById('owner-list');
const errorEl   = document.getElementById('owner-error');
const nameInput = document.getElementById('owner-name-input');
const addBtn    = document.getElementById('owner-add-btn');
const gearBtn   = document.getElementById('manage-owners-btn');

function showError(message) {
    errorEl.textContent = message;
    errorEl.style.display = 'block';
}

function clearError() {
    errorEl.textContent = '';
    errorEl.style.display = 'none';
}

function renderOwnerList(owners) {
    listEl.replaceChildren(
        ...owners.map(owner => {
            const row = document.createElement('div');
            row.className = 'owner-row';

            const nameEl = document.createElement('span');
            nameEl.className = 'owner-name';
            nameEl.textContent = owner.name;           // textContent — XSS safe
            if (owner.isMe) {
                const badge = document.createElement('span');
                badge.className = 'owner-me-badge';
                badge.textContent = ' (you)';
                nameEl.appendChild(badge);
            }
            row.appendChild(nameEl);

            if (!owner.isMe) {
                const delBtn = document.createElement('button');
                delBtn.type = 'button';
                delBtn.className = 'btn-destructive-outline';
                delBtn.style.cssText = 'padding:2px 12px;font-size:13px;height:30px';
                delBtn.textContent = 'Delete';
                delBtn.addEventListener('click', async () => {
                    clearError();
                    try {
                        await deleteOwner(owner.id);
                        const updated = await getOwners();
                        renderOwnerList(updated);
                    } catch (err) {
                        showError(err.message);   // API returns "Cannot delete — N spool(s) assigned…"
                    }
                });
                row.appendChild(delBtn);
            }

            return row;
        })
    );
}

async function openModal() {
    clearError();
    nameInput.value = '';
    const owners = await getOwners();
    renderOwnerList(owners);
    dialog.showModal();
}

// Backdrop click closes dialog
dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });

// Dispatch owners-updated on close so filter bar and spool form owner selects refresh
dialog.addEventListener('close', () => {
    document.dispatchEvent(new CustomEvent('owners-updated'));
});

// Add owner form
addBtn.addEventListener('click', async () => {
    clearError();
    const name = nameInput.value.trim();
    if (!name) { showError('Owner name is required.'); return; }
    try {
        await createOwner(name);
        nameInput.value = '';
        const updated = await getOwners();
        renderOwnerList(updated);
    } catch (err) {
        showError(err.message);
    }
});

// Also submit on Enter in the name input
nameInput.addEventListener('keydown', e => { if (e.key === 'Enter') addBtn.click(); });

export function initOwnerModal() {
    gearBtn.addEventListener('click', openModal);
}
