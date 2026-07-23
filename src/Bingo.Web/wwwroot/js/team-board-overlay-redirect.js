(() => {
  const restoreKey = "team-board-overlay-restore";
  const overviewPath = document.currentScript?.dataset.overviewUrl;
  if (!window.matchMedia("(min-width: 901px)").matches || !overviewPath) return;

  try {
    const overviewUrl = new URL(overviewPath, window.location.href);
    const teamUrl = new URL(window.location.href);
    teamUrl.searchParams.delete("overlay");
    window.sessionStorage.setItem(restoreKey, JSON.stringify({ overviewUrl: overviewUrl.href, teamUrl: teamUrl.href }));
    window.location.replace(overviewUrl);
  } catch { /* A direct team URL remains the fallback. */ }
})();
