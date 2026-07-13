(() => {
    const script = document.currentScript;
    const eventId = script?.dataset.eventId;
    const banner = document.querySelector("[data-progress-update]");
    if (!eventId) return;

    let refreshTimer;
    const refresh = () => window.location.reload();
    const announceUpdate = () => {
        if (banner) banner.hidden = false;
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(refresh, 3500);
    };

    document.querySelector("[data-progress-refresh]")?.addEventListener("click", refresh);
    window.setInterval(refresh, 30000);

    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/progress")
        .withAutomaticReconnect()
        .build();
    connection.on("progressChanged", announceUpdate);
    connection.onreconnected(() => connection.invoke("WatchEvent", eventId));
    connection.start()
        .then(() => connection.invoke("WatchEvent", eventId))
        .catch(() => { /* The timed refresh remains available when live updates are unavailable. */ });
})();
