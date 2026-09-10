(() => {
  const form = document.querySelector('[data-risk-setup]'); if (!form) return;
  const account = form.querySelector('[data-account-capital]'), allocated = form.querySelector('[data-allocated]'), remaining = form.querySelector('[data-remaining]'), state = form.querySelector('[data-allocation-state]'), submit = form.querySelector('[data-save-risk]'), formError = form.querySelector('[data-form-error]');
  const money = value => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Math.max(0, value));
  const number = input => Number(input.value) || 0;
  const pickers = form.querySelectorAll('[data-style-picker]');
  function showStyle(index) {
    form.querySelectorAll('[data-style-editor]').forEach(panel => panel.hidden = panel.dataset.styleEditor !== String(index));
    pickers.forEach(picker => picker.value = index);
  }
  pickers.forEach(picker => picker.addEventListener('change', () => showStyle(picker.value)));
  document.querySelectorAll('[data-update-style]').forEach(button => button.addEventListener('click', () => {
    showStyle(button.dataset.updateStyle);
    form.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }));
  function update() {
    const total = number(account); let sum = 0, valid = total >= 1000;
    form.querySelectorAll('[data-style-card]').forEach(card => {
      const capital = number(card.querySelector('[data-style-capital]')), perTrade = number(card.querySelector('[data-risk-per-trade]')), maxRisk = number(card.querySelector('[data-max-risk]'));
      const error = card.querySelector('[data-style-error]'), status = card.querySelector('[data-style-state]'); sum += capital;
      let message = '';
      if (capital > 0 && capital < 1000) message = 'Minimum allocation is ₹1,000.';
      else if (capital > 0 && maxRisk < perTrade) message = 'Maximum style risk must be at least the per-trade risk.';
      else if (capital > 0 && (perTrade < .1 || perTrade > 5 || maxRisk < .1 || maxRisk > 5)) message = 'Risk percentages must be from 0.1% to 5%.';
      error.textContent = message; card.classList.toggle('has-error', !!message); status.textContent = capital > 0 ? 'Active' : 'Disabled'; status.classList.toggle('active', capital > 0);
      card.querySelector('[data-style-risk]').textContent = money(capital * perTrade / 100); valid = valid && !message;
    });
    const left = total - sum; allocated.textContent = money(sum); remaining.textContent = money(left); state.classList.toggle('over', left < 0);
    if (total < 1000) state.textContent = 'Account capital must be at least ₹1,000.';
    else if (left < 0) state.textContent = 'Allocations exceed account capital by ' + money(-left) + '.';
    else if (sum === 0) state.textContent = 'Enter an allocation for at least one trading style.';
    else state.textContent = money(left) + ' is still available to allocate.';
    valid = valid && sum > 0 && left >= 0; formError.textContent = valid ? '' : 'Correct the highlighted values before saving.'; submit.disabled = !valid;
  }
  form.querySelectorAll('input, select').forEach(input => input.addEventListener('input', update));
  form.addEventListener('submit', event => { update(); if (submit.disabled) event.preventDefault(); }); update();
})();
