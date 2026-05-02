// api.js — fetch wrappers for all Filament Catalog API endpoints
// All functions are async and throw Error on non-2xx responses.

// ---- Owners ----

export async function getOwners() {
    const res = await fetch('/api/owners');
    if (!res.ok) throw new Error(`GET /api/owners failed: ${res.status}`);
    return res.json();
}

export async function createOwner(name) {
    const res = await fetch('/api/owners', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name })
    });
    if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.error ?? `POST /api/owners failed: ${res.status}`);
    }
    return res.json();
}

export async function deleteOwner(id) {
    const res = await fetch(`/api/owners/${id}`, { method: 'DELETE' });
    if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.error ?? `DELETE /api/owners/${id} failed: ${res.status}`);
    }
    // 204 No Content — no body to parse
}

// ---- Spools ----

export async function getSpools() {
    const res = await fetch('/api/spools');
    if (!res.ok) throw new Error(`GET /api/spools failed: ${res.status}`);
    return res.json();
}

export async function createSpool(data) {
    const res = await fetch('/api/spools', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `POST /api/spools failed: ${res.status}`);
    }
    return res.json();
}

export async function updateSpool(id, data) {
    const res = await fetch(`/api/spools/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `PUT /api/spools/${id} failed: ${res.status}`);
    }
    return res.json();
}

export async function deleteSpool(id) {
    const res = await fetch(`/api/spools/${id}`, { method: 'DELETE' });
    if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `DELETE /api/spools/${id} failed: ${res.status}`);
    }
    // 204 No Content — no body to parse
}

// ---- Summary ----

export async function getSummary() {
    const res = await fetch('/api/summary');
    if (!res.ok) throw new Error(`GET /api/summary failed: ${res.status}`);
    return res.json();
}

// ---- Balance ----

export async function getBalance() {
    const res = await fetch('/api/balance');
    if (!res.ok) throw new Error(`GET /api/balance failed: ${res.status}`);
    return res.json();
}

// ---- Sync ----

export async function startSync() {
    const res = await fetch('/api/sync/start', { method: 'POST' });
    if (!res.ok) throw new Error(`POST /api/sync/start failed: ${res.status}`);
    // 202 Accepted — no body to parse
}

export async function getSyncStatus() {
    const res = await fetch('/api/sync/status');
    if (!res.ok) throw new Error(`GET /api/sync/status failed: ${res.status}`);
    return res.json();
}

// ---- Catalog ----

export async function getCatalogCount() {
    const res = await fetch('/api/catalog/count');
    if (!res.ok) throw new Error(`GET /api/catalog/count failed: ${res.status}`);
    return res.json(); // Returns { count: number }
}

export async function getCatalogMaterials() {
    const res = await fetch('/api/catalog/materials');
    if (!res.ok) throw new Error(`GET /api/catalog/materials failed: ${res.status}`);
    return res.json(); // Returns string[]
}

export async function getCatalogColors(material) {
    const res = await fetch(`/api/catalog/colors?material=${encodeURIComponent(material)}`);
    if (!res.ok) throw new Error(`GET /api/catalog/colors failed: ${res.status}`);
    return res.json(); // Returns [{ id, colorName, colorHex, productTitle }]
}
