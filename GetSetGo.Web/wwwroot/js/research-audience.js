(() => {
    const panel = document.querySelector('[data-audience-url]');
    if (!panel) return;
    const fields = { style: 'TradingStyle', side: 'Side', entry: 'EntryPrice', stop: 'StopLoss', target: 'TargetPrice' };
    const status = panel.querySelector('[data-audience-status]');
    const rows = panel.querySelector('[data-audience-rows]');
    let timer, request, version = 0;
    async function refresh() {
        const current = ++version;
        request?.abort();
        request = new AbortController();
        rows.replaceChildren();
        const params = new URLSearchParams();
        for (const [key, id] of Object.entries(fields)) params.set(key, document.getElementById(id)?.value || '0');
        status.textContent = 'Checking eligible clients...';
        try {
            const response = await fetch(panel.dataset.audienceUrl + '?' + params, { signal: request.signal, cache: 'no-store' });
            const data = await response.json();
            if (current !== version) return;
            if (!response.ok) throw new Error(data.message || 'Unable to check eligibility.');
            status.textContent = 'Eligible clients: ' + data.eligible;
            for (const client of data.clients) {
                const row = document.createElement('tr');
                for (const value of [client.name, client.email, data.style === 'ShortTermDelivery' ? 'Short-term delivery' : data.style, Number(client.capital).toLocaleString('en-IN', { style: 'currency', currency: 'INR' }), client.minimumRatio + ' : 1']) {
                    const cell = document.createElement('td');
                    cell.textContent = value;
                    row.append(cell);
                }
                rows.append(row);
            }
        } catch (error) {
            if (error.name !== 'AbortError' && current === version) status.textContent = error.message;
        }
    }
    function schedule() { clearTimeout(timer); timer = setTimeout(refresh, 250); }
    for (const id of Object.values(fields)) {
        const field = document.getElementById(id);
        field?.addEventListener('input', schedule);
        field?.addEventListener('change', schedule);
    }
    document.querySelector('[data-demo-signal-grid]')?.addEventListener('click', schedule);
    refresh();
})();
