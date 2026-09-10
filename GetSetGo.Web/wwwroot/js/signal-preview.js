(() => {
  const form = document.querySelector('[data-signal-form]'); if (!form) return;
  const input = name => form.querySelector('[data-preview="' + name + '"]');
  const money = value => '₹' + (Number(value) || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  ['entry', 'stop', 'target'].forEach(name => {
    const field = input(name);
    if (field && Number(field.value) === 0) field.value = '';
  });
  function update() {
    const symbol = input('symbol').value.trim().toUpperCase() || 'RELIANCE';
    const side = input('side').selectedOptions[0].text.toUpperCase();
    const style = input('style').selectedOptions[0].text;
    const entry = Number(input('entry').value) || 0, stop = Number(input('stop').value) || 0, target = Number(input('target').value) || 0;
    const risk = Math.abs(entry - stop), reward = Math.abs(target - entry);
    document.querySelector('[data-preview-symbol]').textContent = symbol;
    document.querySelector('[data-preview-side]').textContent = side;
    document.querySelector('[data-preview-side]').className = 'badge ' + side.toLowerCase();
    const styleBadge = document.querySelector('[data-preview-style]'); styleBadge.textContent = style; styleBadge.className = 'style-badge style-' + style.toLowerCase().replaceAll(' ', '');
    document.querySelector('[data-preview-entry]').textContent = money(entry); document.querySelector('[data-preview-stop]').textContent = money(stop); document.querySelector('[data-preview-target]').textContent = money(target);
    document.querySelector('[data-preview-ratio]').textContent = risk > 0 ? (reward / risk).toFixed(2) + ' : 1' : '—';
    document.querySelector('[data-preview-brief]').textContent = input('brief').value.trim() || 'Your research context will appear here.';
  }
  form.querySelectorAll('[data-preview]').forEach(field => field.addEventListener('input', update)); update();
  const grid = document.querySelector('[data-demo-signal-grid]');
  const escape = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' })[char]);
  const styleMap = { INTRADAY: '1', SWING: '2', POSITIONAL: '3', SHORT_TERM_DELIVERY: '4' };
  fetch('/data/research-report.json').then(response => response.ok ? response.json() : Promise.reject()).then(report => {
    const publishedSymbols = new Set(JSON.parse(grid.dataset.publishedSymbols || '[]').map(symbol => String(symbol).toUpperCase()));
    const stocks = report.stocks.filter(stock => !publishedSymbols.has(String(stock.symbol).toUpperCase())).slice(0, 100);
    if (!stocks.length) { grid.innerHTML = '<p class="muted">All generated research cards have already been published.</p>'; return; }
    grid.innerHTML = stocks.map((stock, index) => {
      const call = stock.research_call, news = stock.news || {}, style = stock.research_style?.style || 'INTRADAY';
      return '<article class="demo-signal-card card"><div><span class="style-badge style-' + style.toLowerCase().replace('_', '') + '">' + escape(style.replace('_', ' ')) + '</span><span class="badge ' + call.side.toLowerCase() + '">' + escape(call.side) + '</span></div><h3>' + escape(stock.symbol) + '</h3><p class="demo-headline">' + escape(news.headline || 'AI research setup') + '</p><div class="demo-levels"><span>Entry <b>₹' + Number(call.entry).toFixed(2) + '</b></span><span>SL <b>₹' + Number(call.stop_loss).toFixed(2) + '</b></span><span>Target <b>₹' + Number(call.target).toFixed(2) + '</b></span></div><div class="ratio"><span>Score ' + Number(stock.overall_score).toFixed(1) + '</span><strong>' + Number(call.risk_reward_ratio).toFixed(2) + ' : 1</strong></div><button class="btn secondary" type="button" data-demo-index="' + index + '">Use this signal</button></article>';
    }).join('');
    grid.addEventListener('click', event => {
      const button = event.target.closest('[data-demo-index]'); if (!button) return;
      const stock = stocks[Number(button.dataset.demoIndex)], call = stock.research_call, news = stock.news || {};
      input('symbol').value = stock.symbol; input('side').value = call.side === 'BUY' ? '1' : '2';
      input('style').value = styleMap[stock.research_style?.style] || '4';
      input('entry').value = call.entry; input('stop').value = call.stop_loss; input('target').value = call.target;
      input('brief').value = (news.headline || 'AI research setup') + '\n\n' + (news.content || '');
      form.querySelectorAll('[data-preview]').forEach(field => field.dispatchEvent(new Event('input', { bubbles: true })));
      form.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
    grid.querySelector('[data-demo-index="0"]')?.click();
  }).catch(() => { grid.innerHTML = '<p class="validation">Research demo data could not be loaded.</p>'; });
})();
