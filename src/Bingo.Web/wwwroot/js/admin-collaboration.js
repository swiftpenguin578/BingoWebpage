(() => {
    if (!window.signalR) return;

    const boardRoot = document.querySelector('[data-admin-board-event]');
    const draftRoot = document.querySelector('[data-admin-draft-event]');
    if (!boardRoot && !draftRoot) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/admin-collaboration')
        .withAutomaticReconnect()
        .build();

    let reloadTimer;
    let boardExpiryTimer;
    let localBoardChangeUntil = 0;
    let localDraftChangeUntil = 0;
    let preserveBoardEditingOnPageHide = false;
    const reloadWithoutReleasingBoard = () => {
        preserveBoardEditingOnPageHide = true;
        window.location.reload();
    };
    const scheduleDraftReload = () => {
        if (Date.now() < localDraftChangeUntil) return;
        const notice = document.querySelector('[data-draft-update]');
        if (notice) notice.hidden = false;
        window.clearTimeout(reloadTimer);
        reloadTimer = window.setTimeout(reloadWithoutReleasingBoard, 700);
    };
    const scheduleBoardReload = () => {
        if (Date.now() < localBoardChangeUntil) return;
        scheduleDraftReload();
    };

    if (boardRoot) {
        connection.on('boardPresenceChanged', viewers => {
            const currentAccount = (boardRoot.dataset.currentAccount || '').toLowerCase();
            const others = viewers.filter(viewer => String(viewer.accountId).toLowerCase() !== currentAccount);
            boardRoot.hidden = others.length === 0;
            const names = boardRoot.querySelector('[data-board-presence-names]');
            if (names) names.textContent = others.length === 0 ? '' : `Also viewing: ${others.map(viewer => viewer.username).join(', ')}.`;
        });
        connection.on('boardChanged', scheduleBoardReload);
        const scheduleBoardExpiryReload = expiresAt => {
            window.clearTimeout(boardExpiryTimer);
            const expiry = Date.parse(expiresAt || '');
            if (Number.isFinite(expiry)) boardExpiryTimer = window.setTimeout(reloadWithoutReleasingBoard, Math.max(1000, expiry - Date.now() + 1000));
        };
        scheduleBoardExpiryReload(boardRoot.dataset.editorExpiresAt);
        boardRoot.scheduleExpiryReload = scheduleBoardExpiryReload;
    }

    if (draftRoot) connection.on('draftChanged', scheduleDraftReload);

    if (draftRoot) {
        document.addEventListener('submit', event => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)
                || !form.closest('[data-admin-draft-event]')
                || !form.dataset.updateTargets) return;
            localDraftChangeUntil = Date.now() + 2500;
            window.clearTimeout(reloadTimer);
        });
    }

    if (boardRoot) {
        document.addEventListener('submit', event => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)
                || form.hasAttribute('data-release-board-editing')) return;
            localBoardChangeUntil = Date.now() + 5000;
            window.clearTimeout(reloadTimer);
        });
    }

    const subscribe = async () => {
        if (boardRoot) {
            await connection.invoke('WatchBoard', boardRoot.dataset.adminBoardEvent);
            if (boardRoot.dataset.canEdit === 'true') {
                await connection.invoke('RenewBoardEditing', boardRoot.dataset.adminBoardEvent);
                boardRoot.scheduleExpiryReload?.(new Date(Date.now() + 300000).toISOString());
            }
        }
        if (draftRoot) {
            await connection.invoke('WatchDraft', draftRoot.dataset.adminDraftEvent);
            if (draftRoot.dataset.canControl === 'true') await connection.invoke('RenewDraftControl', draftRoot.dataset.adminDraftEvent);
        }
    };

    connection.onreconnected(() => subscribe().catch(() => {}));
    connection.start().then(subscribe).catch(() => {});

    if (draftRoot?.dataset.canControl === 'true') {
        window.setInterval(() => {
            if (connection.state === signalR.HubConnectionState.Connected) connection.invoke('RenewDraftControl', draftRoot.dataset.adminDraftEvent).catch(() => {});
        }, 120000);
    }
    if (boardRoot?.dataset.canEdit === 'true') {
        let lastBoardRenewal = Date.now();
        const renewForActivity = () => {
            if (Date.now() - lastBoardRenewal < 60000 || connection.state !== signalR.HubConnectionState.Connected) return;
            lastBoardRenewal = Date.now();
            connection.invoke('RenewBoardEditing', boardRoot.dataset.adminBoardEvent)
                .then(() => boardRoot.scheduleExpiryReload?.(new Date(Date.now() + 300000).toISOString()))
                .catch(() => {});
        };
        ['pointerdown', 'keydown', 'input', 'dragstart'].forEach(eventName => document.addEventListener(eventName, renewForActivity, { passive: true }));
        document.querySelectorAll('form:not([data-release-board-editing])').forEach(form => form.addEventListener('submit', () => { preserveBoardEditingOnPageHide = true; }));
        window.addEventListener('pagehide', () => {
            if (preserveBoardEditingOnPageHide) return;
            const releaseForm = document.querySelector('[data-release-board-editing]');
            if (!releaseForm) return;
            fetch(releaseForm.action, { method: 'POST', body: new FormData(releaseForm), credentials: 'same-origin', keepalive: true }).catch(() => {});
        });
    }
})();
