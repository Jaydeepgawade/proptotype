$(function () {
  'use strict';
  document.querySelectorAll('[data-record-list]').forEach((root, listIndex) => {
    const table = root.querySelector('table'), body = table.tBodies[0];
    const headers = Array.from(table.tHead.rows[0].cells);
    const rows = Array.from(body.rows).map((row, index) => ({ row, index,
      search: row.textContent.toLowerCase(),
      values: Array.from(row.cells).map(cell => cell.dataset.sort ?? cell.textContent.trim()) }));
    let page = 1, size = 10, sortIndex = -1, direction = 1;
    const toolbar = document.createElement('div'); toolbar.className = 'record-toolbar';
    const searchLabel = document.createElement('label'); searchLabel.textContent = 'Search';
    const search = document.createElement('input'); search.type = 'search'; search.placeholder = 'Search stock, order or keyword';
    searchLabel.append(search); toolbar.append(searchLabel);
    const filters = [], dateFilters = [];
    headers.forEach((header, index) => {
      if (header.dataset.filter) {
        const label = document.createElement('label'); label.textContent = header.dataset.filter;
        const select = document.createElement('select');
        select.add(new Option('All ' + header.dataset.filter.toLowerCase(), ''));
        [...new Set(rows.map(item => item.values[index]))].sort().forEach(value => select.add(new Option(value, value)));
        label.append(select); toolbar.append(label); filters.push({ select, index });
        select.addEventListener('change', () => { page = 1; render(); });
      }
      if (header.dataset.dateFilter) {
        const label = document.createElement('label'); label.textContent = header.dataset.dateFilter;
        const from = document.createElement('input'); from.type = 'date'; from.setAttribute('aria-label', header.dataset.dateFilter + ' from');
        const to = document.createElement('input'); to.type = 'date'; to.setAttribute('aria-label', header.dataset.dateFilter + ' to');
        const range = document.createElement('div'); range.className = 'record-date-range'; range.append(from, to);
        label.append(range); toolbar.append(label); dateFilters.push({ from, to, index });
        [from, to].forEach(input => input.addEventListener('change', () => { page = 1; render(); }));
      }
      if (!header.hasAttribute('data-no-sort')) {
        const title = header.textContent.trim();
        const button = document.createElement('button'); button.type = 'button'; button.className = 'record-sort';
        button.textContent = title;
        const arrow = document.createElement('span'); arrow.className = 'sort-arrow'; arrow.textContent = '↕'; arrow.setAttribute('aria-hidden', 'true');
        button.append(arrow); button.setAttribute('aria-label', 'Sort by ' + title);
        header.replaceChildren(button); header.setAttribute('aria-sort', 'none');
        button.addEventListener('click', () => {
          direction = sortIndex === index ? -direction : 1; sortIndex = index; page = 1; render();
        });
      }
    });
    const reset = document.createElement('button'); reset.type = 'button'; reset.className = 'btn secondary record-reset'; reset.textContent = 'Reset'; toolbar.append(reset);
    let exportButton;
    if (root.dataset.exportName) {
      exportButton = document.createElement('button'); exportButton.type = 'button'; exportButton.className = 'btn secondary';
      exportButton.textContent = 'Export Excel'; toolbar.append(exportButton);
    }
    root.prepend(toolbar);
    const empty = document.createElement('p'); empty.className = 'record-empty'; empty.hidden = true;
    root.append(empty);
    const footer = document.createElement('div'); footer.className = 'record-footer';
    const count = document.createElement('span'); count.setAttribute('role', 'status'); count.setAttribute('aria-live', 'polite'); footer.append(count);
    const controls = document.createElement('div'); controls.className = 'record-pagination';
    const sizeLabel = document.createElement('label'); sizeLabel.textContent = 'Rows';
    const sizes = document.createElement('select'); [10,25,50].forEach(value => sizes.add(new Option(String(value), String(value)))); sizeLabel.append(sizes); controls.append(sizeLabel);
    const prev = document.createElement('button'), next = document.createElement('button'), pageLabel = document.createElement('span');
    prev.type = next.type = 'button'; prev.className = next.className = 'btn secondary'; prev.textContent = 'Previous'; next.textContent = 'Next';
    controls.append(prev, pageLabel, next); footer.append(controls); root.append(footer);
    const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
    function render() {
      const query = search.value.trim().toLowerCase();
      const filtered = rows.filter(item => (!query || item.search.includes(query))
        && filters.every(f => !f.select.value || item.values[f.index] === f.select.value)
        && dateFilters.every(f => {
          const date = item.values[f.index].slice(0, 10);
          return (!f.from.value || date >= f.from.value) && (!f.to.value || date <= f.to.value);
        }));
      if (sortIndex >= 0) filtered.sort((a, b) => {
        const x = a.values[sortIndex], y = b.values[sortIndex];
        if (x === '' || y === '') return x === y ? a.index - b.index : x === '' ? 1 : -1;
        const type = headers[sortIndex].dataset.type;
        const comparison = type === 'number' ? Number(x) - Number(y) : type === 'date' ? x.localeCompare(y) : collator.compare(x, y);
        return comparison * direction || a.index - b.index;
      });
      const pages = Math.max(1, Math.ceil(filtered.length / size)); page = Math.min(page, pages);
      const start = (page - 1) * size;
      // Keep the original elements so chart and order form event handlers survive paging.
      const fragment = document.createDocumentFragment();
      filtered.slice(start, start + size).forEach(item => fragment.append(item.row)); body.replaceChildren(fragment);
      empty.hidden = filtered.length > 0;
      empty.textContent = rows.length ? 'No matching records. Change your search or reset the filters.' : 'No records yet.';
      count.textContent = filtered.length ? `${start + 1}–${Math.min(start + size, filtered.length)} of ${filtered.length} records${filtered.length !== rows.length ? ` (${rows.length} total)` : ''}` : '0 records';
      pageLabel.textContent = `Page ${page} of ${pages}`;
      prev.disabled = page <= 1; next.disabled = page >= pages;
      headers.forEach((header, index) => {
        if (header.hasAttribute('data-no-sort')) return;
        header.setAttribute('aria-sort', index === sortIndex ? direction === 1 ? 'ascending' : 'descending' : 'none');
        header.querySelector('.sort-arrow').textContent = index === sortIndex ? direction === 1 ? '↑' : '↓' : '↕';
      });
    }
    search.addEventListener('input', () => { page = 1; render(); });
    sizes.addEventListener('change', () => { size = Number(sizes.value); page = 1; render(); });
    prev.addEventListener('click', () => { page--; render(); }); next.addEventListener('click', () => { page++; render(); });
    reset.addEventListener('click', () => { search.value = ''; filters.forEach(f => f.select.value = ''); dateFilters.forEach(f => { f.from.value = ''; f.to.value = ''; }); sortIndex = -1; direction = 1; page = 1; size = 10; sizes.value = '10'; render(); });
    exportButton?.addEventListener('click', () => {
      const query = search.value.trim().toLowerCase();
      const exportRows = rows.filter(item => (!query || item.search.includes(query))
        && filters.every(f => !f.select.value || item.values[f.index] === f.select.value)
        && dateFilters.every(f => {
          const date = item.values[f.index].slice(0, 10);
          return (!f.from.value || date >= f.from.value) && (!f.to.value || date <= f.to.value);
        }));
      const included = headers.map((header, index) => ({ header, index })).filter(column => !column.header.hasAttribute('data-no-sort'));
      const escape = value => '"' + String(value).replaceAll('"', '""').replaceAll(/\s+/g, ' ').trim() + '"';
      const csv = [included.map(column => escape(column.header.textContent.replace(/[â†•â†‘â†“]/g, ''))).join(','),
        ...exportRows.map(item => included.map(column => escape(item.values[column.index])).join(','))].join('\r\n');
      const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' });
      const link = document.createElement('a'); link.href = URL.createObjectURL(blob);
      link.download = root.dataset.exportName + '-' + new Date().toISOString().slice(0, 10) + '.csv';
      link.click(); URL.revokeObjectURL(link.href);
    });
    render();
  });
});
