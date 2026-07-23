(() => {
    const script = document.currentScript;
    const eventId = script?.dataset.eventId;
    const banner = document.querySelector("[data-progress-update]");
    if (!eventId) return;

    const refresh = () => window.location.reload();
    const announceUpdate = () => {
        if (banner) banner.hidden = false;
        window.dispatchEvent(new CustomEvent("public-progress-available"));
    };

    document.querySelector("[data-progress-refresh]")?.addEventListener("click", refresh);

    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/progress")
        .withAutomaticReconnect()
        .build();
    connection.on("progressChanged", announceUpdate);
    connection.onreconnected(() => connection.invoke("WatchEvent", eventId));
    connection.start()
        .then(() => connection.invoke("WatchEvent", eventId))
        .catch(() => { /* The page remains usable without live update notifications. */ });
})();
