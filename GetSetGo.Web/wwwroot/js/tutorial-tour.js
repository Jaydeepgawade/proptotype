(() => {
  const query = new URLSearchParams(location.search);
  if (query.get('tour') !== '1') return;
  const path = location.pathname.toLowerCase();
  const steps = path.includes('/risk') ? [
    ['.simple-total', 'Step 1 of 5', 'Enter your total trading money here.'],
    ['[data-style-picker]', 'Step 1 of 5', 'Choose the trading type you want to set up.'],
    ['[data-style-capital]', 'Step 1 of 5', 'Allocate money for this trading type, then save your setup.']
  ] : path.includes('/signals') ? [
    ['.record-list', 'Step 2 of 5', 'These are signals matching your saved risk rules. Check entry, stop-loss and target before SET.']
  ] : path.includes('/orders/set') ? [
    ['.record-list', 'Step 3 of 5', 'Your SET orders appear here. Open an order when you are ready to GO.']
  ] : path.includes('/orders/tracker') ? [
    ['.record-list', 'Step 4 of 5', 'Go/Tracker shows your executed simulated trades.']
  ] : path.includes('/orders/history') ? [
    ['.record-list', 'Step 5 of 5', 'History keeps completed, cancelled and expired orders. Tutorial complete.']
  ] : [];
  if (!steps.length) return;
  let index = 0; const overlay = document.createElement('div'); overlay.className = 'tour-overlay'; document.body.append(overlay);
  const panel = document.createElement('section'); panel.className = 'tour-panel'; document.body.append(panel);
  function render(){ const [selector,label,text] = steps[index]; document.querySelectorAll('.tour-focus').forEach(x=>x.classList.remove('tour-focus')); const target=document.querySelector(selector); target?.classList.add('tour-focus'); target?.scrollIntoView({block:'center',behavior:'smooth'}); panel.innerHTML=`<span>${label}</span><b>${text}</b><div><button data-skip>Skip</button><button data-next>${index===steps.length-1?'Next':'Next'}</button></div>`; panel.querySelector('[data-skip]').onclick=done; panel.querySelector('[data-next]').onclick=next; }
  function next(){ if(++index<steps.length)return render(); const nextPath=path.includes('/risk')?'/Signals?tour=1':path.includes('/signals')?'/Orders/Set?tour=1':path.includes('/orders/set')?'/Orders/Tracker?tour=1':path.includes('/tracker')?'/Orders/History?tour=1':null; if(nextPath)location.assign(nextPath);else done(); }
  function done(){ localStorage.setItem('getsetgo-tour-complete','1'); overlay.remove(); panel.remove(); history.replaceState({},'',location.pathname); }
  render();
})();
