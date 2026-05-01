// summary.js — renders summary bar stats and balance table

import { getBalance, getSummary } from './api.js';

function formatCurrency(value) {
    return new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(value ?? 0);
}

export function renderSummary({ totalSpools, mySpools, totalValue, totalOwed }) {
    document.getElementById('stat-total-spools').textContent = totalSpools;
    document.getElementById('stat-my-spools').textContent = mySpools;
    document.getElementById('stat-total-value').textContent = formatCurrency(totalValue);
    document.getElementById('stat-total-owed').textContent = formatCurrency(totalOwed);
}

export function renderBalance(rows) {
    const tbody = document.querySelector('#balance-table tbody');
    if (!rows || rows.length === 0) {
        const tr = document.createElement('tr');
        const td = document.createElement('td');
        td.colSpan = 4;
        td.style.cssText = 'color:#6b7280;font-style:italic;padding:8px 16px';
        td.textContent = 'No other owners yet.';
        tr.appendChild(td);
        tbody.replaceChildren(tr);
        return;
    }
    tbody.replaceChildren(
        ...rows.map(row => {
            const tr = document.createElement('tr');

            // Owner name cell — textContent for XSS safety
            const tdOwner = document.createElement('td');
            tdOwner.textContent = row.ownerName;
            if (row.hasUnpriced) {
                const warn = document.createElement('span');
                warn.className = 'warning-icon';
                warn.textContent = ' ⚠';
                warn.title = 'One or more spools have no price — totals may be incomplete.';
                tdOwner.appendChild(warn);
            }

            const tdCount = document.createElement('td');
            tdCount.textContent = row.spoolCount;

            const tdValue = document.createElement('td');
            tdValue.textContent = formatCurrency(row.value);

            const tdOwed = document.createElement('td');
            tdOwed.textContent = formatCurrency(row.owed);

            tr.append(tdOwner, tdCount, tdValue, tdOwed);
            return tr;
        })
    );
}

export async function refreshSummaryAndBalance() {
    const [summary, balance] = await Promise.all([getSummary(), getBalance()]);
    renderSummary(summary);
    renderBalance(balance);
}
