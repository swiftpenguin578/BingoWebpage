(() => {
    const script = document.currentScript;
    const eventId = script?.dataset.eventId;
    const teamId = script?.dataset.teamId;
    const inspectEnabled = script?.dataset.inspect === "true";
    if (!eventId || !teamId || !window.signalR) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/team-focus")
        .withAutomaticReconnect()
        .build();
    connection.on("focusChanged", () => window.location.reload());
    connection.onreconnected(() => connection.invoke("WatchTeam", eventId, teamId, inspectEnabled));
    connection.start()
        .then(() => connection.invoke("WatchTeam", eventId, teamId, inspectEnabled))
        .catch(() => { /* The route-backed controls remain usable without live invalidation. */ });
})();
