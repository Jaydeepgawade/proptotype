(() => {
  const form = document.querySelector('[data-signal-form]');
  if (!form) return;

  const input = name => form.querySelector('[data-preview="' + name + '"]');
  const money = value => '₹' + (Number(value) || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const customRatio = document.querySelector('[data-custom-ratio]');
  const roundPrice = value => Math.round(value * 100) / 100;

  function applyCustomRatio() {
    const ratio = Number(customRatio?.value);
    const entry = Number(input('entry')?.value) || 0;
    const stop = Number(input('stop')?.value) || 0;
    const risk = Math.abs(entry - stop);
    if (!ratio || ratio <= 0 || !entry || !risk) return;

    const sideText = input('side')?.selectedOptions?.[0]?.text?.toUpperCase() || 'BUY';
    const target = sideText.includes('SELL') ? entry - (risk * ratio) : entry + (risk * ratio);
    input('target').value = roundPrice(target).toFixed(2);
  }

  ['entry', 'stop', 'target'].forEach(name => {
    const field = input(name);
    if (field && Number(field.value) === 0) field.value = '';
  });

  function update() {
    const symbol = input('symbol').value.trim().toUpperCase() || 'Enter symbol';
    const side = input('side').selectedOptions[0].text.toUpperCase();
    const style = input('style').selectedOptions[0].text;
    const entry = Number(input('entry').value) || 0;
    const stop = Number(input('stop').value) || 0;
    const target = Number(input('target').value) || 0;
    const risk = Math.abs(entry - stop);
    const reward = Math.abs(target - entry);
    const ratioText = risk > 0 && reward > 0 ? (reward / risk).toFixed(2) + ' : 1' : '—';

    document.querySelector('[data-preview-symbol]').textContent = symbol;
    document.querySelector('[data-preview-side]').textContent = side;
    document.querySelector('[data-preview-side]').className = 'badge ' + side.toLowerCase();

    const styleBadge = document.querySelector('[data-preview-style]');
    styleBadge.textContent = style;
    styleBadge.className = 'style-badge style-' + style.toLowerCase().replaceAll(' ', '').replaceAll('_', '');

    document.querySelector('[data-preview-entry]').textContent = money(entry);
    document.querySelector('[data-preview-stop]').textContent = money(stop);
    document.querySelector('[data-preview-target]').textContent = money(target);
    document.querySelector('[data-preview-ratio]').textContent = ratioText;

    const formRatio = document.querySelector('[data-form-ratio]');
    const formHelp = document.querySelector('[data-form-ratio-help]');
    if (formRatio) formRatio.textContent = ratioText;
    if (formHelp) formHelp.textContent = ratioText === '—'
      ? 'Enter entry, stop-loss and target to calculate.'
      : 'Calculated from entry, stop-loss and target.';

    document.querySelector('[data-preview-brief]').textContent = input('brief').value.trim() || 'Your research context will appear here.';
  }

  form.querySelectorAll('[data-preview]').forEach(field => field.addEventListener('input', update));
  ['entry', 'stop'].forEach(name => input(name)?.addEventListener('input', () => {
    if (customRatio?.value) applyCustomRatio();
    update();
  }));
  input('side')?.addEventListener('change', () => {
    if (customRatio?.value) applyCustomRatio();
    update();
  });
  customRatio?.addEventListener('input', () => {
    applyCustomRatio();
    update();
  });
  update();

  const grid = document.querySelector('[data-demo-signal-grid]');
  if (!grid) return;

  const escape = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' })[char]);
  const styleMap = { INTRADAY: '1', SWING: '2', POSITIONAL: '3', SHORT_TERM_DELIVERY: '4' };

  fetch('/data/research-report.json')
    .then(response => response.ok ? response.json() : Promise.reject())
    .then(report => {
      const publishedSymbols = new Set(JSON.parse(grid.dataset.publishedSymbols || '[]').map(symbol => String(symbol).toUpperCase()));
      const stocks = (report.stocks || []).filter(stock => !publishedSymbols.has(String(stock.symbol).toUpperCase())).slice(0, 100);

      if (!stocks.length) {
        grid.innerHTML = '<p class="muted">All generated research cards have already been published.</p>';
        return;
      }

      grid.innerHTML = stocks.map((stock, index) => {
        const call = stock.research_call || {};
        const news = stock.news || {};
        const style = stock.research_style?.style || 'INTRADAY';
        const side = call.side || 'BUY';
        return '<article class="demo-signal-card card"><div><span class="style-badge style-' + style.toLowerCase().replaceAll('_', '') + '">' + escape(style.replaceAll('_', ' ')) + '</span><span class="badge ' + side.toLowerCase() + '">' + escape(side) + '</span></div><h3>' + escape(stock.symbol) + '</h3><p class="demo-headline">' + escape(news.headline || 'AI research setup') + '</p><div class="demo-levels"><span>Entry <b>₹' + Number(call.entry).toFixed(2) + '</b></span><span>SL <b>₹' + Number(call.stop_loss).toFixed(2) + '</b></span><span>Target <b>₹' + Number(call.target).toFixed(2) + '</b></span></div><div class="ratio"><span>Score ' + Number(stock.overall_score || 0).toFixed(1) + '</span><strong>' + Number(call.risk_reward_ratio || 0).toFixed(2) + ' : 1</strong></div><button class="btn secondary" type="button" data-demo-index="' + index + '">Use this signal</button></article>';
      }).join('');

      grid.addEventListener('click', event => {
        const button = event.target.closest('[data-demo-index]');
        if (!button) return;

        const stock = stocks[Number(button.dataset.demoIndex)];
        const call = stock.research_call || {};
        const news = stock.news || {};
        input('symbol').value = stock.symbol || '';
        input('side').value = call.side === 'SELL' ? '2' : '1';
        input('style').value = styleMap[stock.research_style?.style] || '1';
        input('entry').value = call.entry ?? '';
        input('stop').value = call.stop_loss ?? '';
        input('target').value = call.target ?? '';
        if (customRatio) customRatio.value = Number(call.risk_reward_ratio || 0).toFixed(2);
        input('brief').value = (news.headline || 'AI research setup') + '\n\n' + (news.content || '');
        update();
        form.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
    })
    .catch(() => {
      grid.innerHTML = '<p class="validation">Research demo data could not be loaded.</p>';
    });
})();
