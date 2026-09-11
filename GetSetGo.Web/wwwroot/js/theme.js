(() => {
  const root = document.documentElement;
  let saved;
  try { saved = localStorage.getItem('getsetgo-theme'); } catch {}
  root.dataset.theme = saved === 'dark' ? 'dark' : 'light';
  function sync() {
    const button = document.getElementById('themeToggle');
    if (!button) return;
    const dark = root.dataset.theme === 'dark';
    const text = button.querySelector('.theme-toggle-text');
    if (text) text.textContent = dark ? 'Light mode' : 'Dark mode';
    button.setAttribute('aria-pressed', String(dark));
  }
  document.addEventListener('DOMContentLoaded', () => {
    sync();
    document.getElementById('themeToggle')?.addEventListener('click', () => {
      root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem('getsetgo-theme', root.dataset.theme); } catch {}
      sync();
    });
  });
})();
