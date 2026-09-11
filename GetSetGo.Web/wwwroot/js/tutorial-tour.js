(() => {
  // Remove the previous auto-submit continuation; it must never override login routing.
  localStorage.removeItem('getsetgo-tour-next-get');
  const query = new URLSearchParams(location.search);
  if (query.get('tour') !== '1') return;
  const path = location.pathname.toLowerCase();
  const steps = path.includes('/risk') ? [
    ['.simple-total', 'Step 1 of 5', 'Enter your total trading money here.'],
    ['[data-style-picker]', 'Step 1 of 5', 'Choose the trading type you want to set up.'],
    ['[data-style-capital]', 'Step 1 of 5', 'Allocate money and choose your risk rules. Close this guide and use Save and update setup to save your own values. Next continues the page tour without saving.']
  ] : path.includes('/signals') ? [
    ['.record-list', 'Step 2 of 5', 'GET shows active signals matching your saved rules. Review entry, stop-loss and target, then click SET to review the confirmation. An empty list means no matching signals are available.']
  ] : path.includes('/orders/set') ? [
    ['.record-list', 'Step 3 of 5', 'Confirmed SET orders appear here. Open an order to review it before GO. This guide does not create orders; you can continue even when the list is empty.']
  ] : path.includes('/orders/tracker') ? [
    ['.record-list', 'Step 4 of 5', 'Go/Tracker shows executed simulated trades. After GO, open the order details to review its status and simulated profit or loss.']
  ] : path.includes('/orders/history') ? [
    ['.record-list', 'Step 5 of 5', 'You have completed the tour. Review order history here and use its filters or Excel export. Finish & Close returns you to this page.']
  ] : [];
  if (!steps.length) return;
  const previousFocus = document.activeElement;
  let index = 0;
  const overlay = document.createElement('div');
  overlay.className = 'tour-overlay';
  document.body.append(overlay);
  const panel = document.createElement('section');
  panel.className = 'tour-panel';
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  panel.setAttribute('aria-labelledby', 'tour-step-title');
  panel.setAttribute('aria-describedby', 'tour-step-description');
  document.body.append(panel);
  const isLastPage = path.includes('/orders/history');
  function clearHighlight() {
    document.querySelectorAll('.tour-focus').forEach(element => element.classList.remove('tour-focus'));
  }
  function render() {
    const [selector, label, text] = steps[index];
    clearHighlight();
    const target = Array.from(document.querySelectorAll(selector)).find(element => element.getClientRects().length);
    target?.classList.add('tour-focus');
    target?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    panel.innerHTML = '<span id="tour-step-title"></span><b id="tour-step-description"></b><div><button type="button" data-skip>Close</button><button type="button" data-next></button></div>';
    panel.querySelector('span').textContent = label;
    panel.querySelector('b').textContent = text;
    const nextButton = panel.querySelector('[data-next]');
    nextButton.textContent = isLastPage && index === steps.length - 1 ? 'Finish & Close' : 'Next';
    panel.querySelector('[data-skip]').onclick = () => done(false);
    nextButton.onclick = next;
    nextButton.focus();
  }
  function next() {
    if (++index < steps.length) return render();
    const nextPath = path.includes('/risk') ? '/Signals?tour=1'
      : path.includes('/signals') ? '/Orders/Set?tour=1'
      : path.includes('/orders/set') ? '/Orders/Tracker?tour=1'
      : path.includes('/orders/tracker') ? '/Orders/History?tour=1' : null;
    if (nextPath) location.assign(nextPath);
    else done(true);
  }
  function done(completed) {
    if (completed) localStorage.setItem('getsetgo-tour-complete', '1');
    localStorage.removeItem('getsetgo-tour-next-get');
    clearHighlight();
    overlay.remove();
    panel.remove();
    document.removeEventListener('keydown', onKeyDown);
    const url = new URL(location.href);
    url.searchParams.delete('tour');
    history.replaceState(history.state, '', url.pathname + url.search + url.hash);
    if (previousFocus?.isConnected) previousFocus.focus();
  }
  function onKeyDown(event) {
    if (event.key === 'Escape') { event.preventDefault(); done(false); }
    if (event.key === 'Tab') {
      const buttons = Array.from(panel.querySelectorAll('button'));
      const current = buttons.indexOf(document.activeElement);
      event.preventDefault();
      buttons[(current + (event.shiftKey ? -1 : 1) + buttons.length) % buttons.length].focus();
    }
  }
  document.addEventListener('keydown', onKeyDown);
  render();
})();
