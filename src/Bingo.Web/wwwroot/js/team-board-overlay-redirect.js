(() => {
  const restoreKey = "team-board-overlay-restore";
  const overviewPath = document.currentScript?.dataset.overviewUrl;
  const teamPath = document.currentScript?.dataset.teamUrl;
  const tilePath = document.currentScript?.dataset.tileUrl;
  if (!window.matchMedia("(min-width: 901px)").matches || !overviewPath) return;

  try {
    const overviewUrl = new URL(overviewPath, window.location.href);
    const teamUrl = new URL(teamPath || window.location.href, window.location.href);
    teamUrl.searchParams.delete("overlay");
    const tileUrl = tilePath ? new URL(tilePath, window.location.href).href : null;
    window.sessionStorage.setItem(restoreKey, JSON.stringify({ overviewUrl: overviewUrl.href, teamUrl: teamUrl.href, tileUrl }));
    window.location.replace(overviewUrl);
  } catch { /* A direct team URL remains the fallback. */ }
})();
