// Runs before stylesheets, so OS/manual theme is present on first paint.
(() => {
  const key = 'bingo.admin.theme';
  const media = matchMedia('(prefers-color-scheme: dark)');
  let choice;
  try { choice = localStorage.getItem(key); } catch { /* Browser storage can be disabled. */ }
  const apply = () => {
    const dark = choice === 'dark' || (choice !== 'light' && media.matches);
    document.documentElement.classList.toggle('theme-dark', dark);
    document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
    document.querySelectorAll('[data-theme]').forEach(button => {
      const active = button.dataset.theme === (dark ? 'dark' : 'light');
      button.classList.toggle('is-on', active);
      button.setAttribute('aria-pressed', String(active));
    });
  };
  apply();
  media.addEventListener('change', apply);
  document.addEventListener('DOMContentLoaded', apply, { once: true });
  document.addEventListener('click', event => {
    const button = event.target.closest('[data-theme]');
    if (!button) return;
    choice = button.dataset.theme;
    try { localStorage.setItem(key, choice); } catch { /* Retain the in-memory choice. */ }
    apply();
  });
  window.adminDesignTheme = { apply };
})();
