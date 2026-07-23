(() => {
    const inbox = document.querySelector('[data-notification-inbox]');
    if (!inbox) return;

    const count = inbox.querySelector('[data-notification-count]');
    const list = inbox.querySelector('[data-notification-list]');
    const empty = inbox.querySelector('[data-notification-empty]');
    const heading = inbox.querySelector('[data-notification-heading]');
    const overview = inbox.querySelector('[data-notification-overview]');

    const render = data => {
        const total = Number(data.count || 0);
        count.textContent = total > 99 ? '99+' : String(total);
        count.hidden = total === 0;
        heading.textContent = data.heading || '';
        empty.textContent = data.emptyText || '';
        empty.hidden = total !== 0;
        overview.textContent = data.overviewLabel || '';
        overview.href = data.overviewUrl || '#';
        overview.hidden = !data.overviewUrl;
        inbox.dataset.eventIds = (data.eventIds || []).join(',');

        list.replaceChildren();
        for (const item of data.items || []) {
            const link = document.createElement('a');
            link.className = 'notification-item';
            link.href = item.url;
            const title = document.createElement('strong');
            title.textContent = item.title;
            const detail = document.createElement('span');
            detail.textContent = item.detail;
            link.append(title, detail);
            list.append(link);
        }
    };

    const refresh = async () => {
        try {
            const response = await fetch('/notifications', { headers: { Accept: 'application/json' } });
            if (response.ok) render(await response.json());
        } catch { /* A normal page refresh remains the fallback. */ }
    };

    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/progress').withAutomaticReconnect().build();
    const watchCurrentEvents = async () => {
        const eventIds = (inbox.dataset.eventIds || '').split(',').filter(Boolean);
        await Promise.all(eventIds.map(eventId => connection.invoke('WatchEvent', eventId)));
    };
    connection.on('progressChanged', refresh);
    connection.onreconnected(watchCurrentEvents);
    connection.start().then(watchCurrentEvents).catch(() => {});
})();
