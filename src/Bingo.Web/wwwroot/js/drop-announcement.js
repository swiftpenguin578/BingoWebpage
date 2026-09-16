(() => {
    const root = document.querySelector('[data-drop-announcement]');
    if (!root) return;

    const main = root.querySelector('[data-drop-announcement-main]');
    const nav = root.querySelector('[data-drop-announcement-nav]');
    const previous = root.querySelector('[data-drop-announcement-previous]');
    const next = root.querySelector('[data-drop-announcement-next]');
    const position = root.querySelector('[data-drop-announcement-position]');
    const link = root.querySelector('[data-drop-announcement-link]');
    const count = root.querySelector('[data-drop-announcement-count]');
    const expand = root.querySelector('[data-drop-announcement-expand]');
    const dismiss = root.querySelector('[data-drop-announcement-dismiss]');
    const clearAll = document.querySelector('[data-drop-clear-all]');
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const accountId = root.dataset.accountId;
    const storagePrefix = `bingo:drop-announcement:${accountId}:`;
    const MOTION_MS = 320;
    const HEIGHT_MS = MOTION_MS * 2;
    const DISPLAY_MS = 10000;
    const label = (name, fallback) => root.dataset[name] || fallback;
    let snapshot = null;
    let queue = [];
    let selected = 0;
    let expanded = true;
    let visible = false;
    let timer = 0;
    let interaction = false;
    let suppressStorage = false;
    let transitionTimer = 0;
    let transitionToken = 0;
    let stateTransitionTimer = 0;
    let stateTransitionToken = 0;
    let stateHeightFrame = 0;
    let revealToken = 0;
    let hideToken = 0;
    let claimPending = false;
    let requestRevision = 0;
    let stateRevision = 0;
    let mutationsPending = 0;
    let refreshQueued = false;

    const reducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    const submissionActive = () => Boolean(document.querySelector?.('[data-submission-drawer], [data-submission-result]'));
    const current = () => queue[selected] || queue[0];
    const key = () => snapshot ? storagePrefix + snapshot.eventId : null;
    const readState = () => {
        try { return key() ? JSON.parse(sessionStorage.getItem(key() || '') || 'null') : null; } catch { return null; }
    };
    const saveState = () => {
        if (suppressStorage || !key()) return;
        try { sessionStorage.setItem(key(), JSON.stringify({ generation: snapshot.generation, snapshotSequence: snapshot.snapshotSequence || 0, selectedId: current()?.submissionId || null, expanded, claimPending })); }
        catch { /* Session storage is only a navigation convenience. */ }
    };
    const setVisible = value => {
        visible = value;
        root.hidden = !value;
        root.dataset.visible = value ? 'true' : 'false';
        root.setAttribute('aria-hidden', value ? 'false' : 'true');
    };
    const updateNavigation = () => {
        const links = [...(document.querySelectorAll?.('[data-drop-navigation]') || [])];
        links.forEach(dropLink => {
            const matches = snapshot && dropLink.dataset.dropNavigationSlug === snapshot.eventSlug;
            const badge = dropLink.querySelector('[data-drop-navigation-new]');
            if (badge) badge.hidden = !matches || Number(snapshot.newCount || 0) === 0;
        });
    };
    const startCountdown = () => {
        window.clearTimeout(timer);
        root.dataset.countdown = interaction ? 'held' : 'running';
        if (!visible || !expanded || interaction) return;
        timer = window.setTimeout(() => setCompact(), DISPLAY_MS);
    };
    const beginCountdown = startCountdown;
    const ensureCountdown = () => {
        if (!timer && visible && expanded && !interaction) startCountdown();
        else root.dataset.countdown = interaction ? 'held' : 'running';
    };
    const holdCountdown = () => {
        if (!visible || !expanded) return;
        interaction = true;
        window.clearTimeout(timer);
        timer = 0;
        root.dataset.countdown = 'held';
    };
    const releaseCountdown = () => {
        if (!interaction) return;
        interaction = false;
        beginCountdown();
    };

    const buildSlide = item => {
        const slide = document.createElement('div');
        slide.className = 'drop-announcement__slide';
        slide.setAttribute('aria-hidden', 'false');
        const thumb = document.createElement('div');
        thumb.className = 'drop-announcement__thumb';
        thumb.setAttribute('aria-hidden', 'true');
        const imageCandidates = [item.itemArtworkReference, item.tileArtworkReference]
            .filter(reference => typeof reference === 'string')
            .map(reference => reference.trim())
            .filter((reference, index, references) => reference && references.indexOf(reference) === index);
        if (imageCandidates.length) {
            const img = document.createElement('img');
            img.alt = '';
            img.decoding = 'async';
            let candidateIndex = 0;
            const handleImageError = () => {
                candidateIndex += 1;
                if (candidateIndex < imageCandidates.length) {
                    img.src = imageCandidates[candidateIndex];
                    return;
                }
                img.removeEventListener('error', handleImageError);
                thumb.remove();
                slide.classList.add('drop-announcement__slide--no-artwork');
            };
            img.addEventListener('error', handleImageError);
            thumb.append(img);
            slide.append(thumb);
            img.src = imageCandidates[candidateIndex];
        } else {
            slide.classList.add('drop-announcement__slide--no-artwork');
        }
        const copy = document.createElement('div');
        copy.className = 'drop-announcement__copy';
        const kicker = document.createElement('div');
        kicker.className = `drop-announcement__kicker${item.completedTileAtApproval ? ' is-complete' : ''}`;
        const progression = label('progressionTemplate', `${label('progressionLabel', 'New tile progression')}: {0} / {1}`)
            .replace('{0}', String(item.progressAfter))
            .replace('{1}', String(item.target));
        kicker.append(document.createTextNode(item.completedTileAtApproval ? label('completedLabel', 'Tile completed') : progression));
        const title = document.createElement('h2');
        title.textContent = item.dropName || item.tileName;
        const byline = document.createElement('p');
        byline.className = 'drop-announcement__byline';
        const player = document.createElement('strong');
        player.textContent = item.playerName || label('participantLabel', 'Participant');
        byline.append(player, document.createTextNode(` · ${item.teamName}`));
        copy.append(kicker, title, byline);
        slide.append(copy);
        return slide;
    };
    const settleSlide = () => { const item = current(); if (item) main.replaceChildren(buildSlide(item)); };
    const cancelSlide = () => { window.clearTimeout(transitionTimer); transitionTimer = 0; transitionToken += 1; settleSlide(); };
    const clearStateTransition = () => {
        window.clearTimeout(stateTransitionTimer);
        window.cancelAnimationFrame?.(stateHeightFrame);
        stateTransitionTimer = 0;
        stateHeightFrame = 0;
        stateTransitionToken += 1;
        delete root.dataset.transition;
        root.style?.removeProperty('height');
    };
    const measureStateHeight = state => {
        const previousState = root.dataset.state;
        const previousTransition = root.dataset.transition;
        const previousHeight = root.style?.height;
        delete root.dataset.transition;
        root.style?.removeProperty('height');
        root.dataset.state = state;
        const height = root.getBoundingClientRect?.().height || 0;
        root.dataset.state = previousState;
        if (previousTransition) root.dataset.transition = previousTransition;
        if (previousHeight) root.style.height = previousHeight;
        else root.style?.removeProperty('height');
        return height;
    };
    const animateHeight = (fromHeight, toHeight, transitionTokenValue) => {
        root.style.height = `${fromHeight}px`;
        root.getBoundingClientRect?.();
        stateHeightFrame = window.requestAnimationFrame?.(() => {
            if (transitionTokenValue !== stateTransitionToken) return;
            root.style.height = `${toHeight}px`;
            stateHeightFrame = 0;
        }) || 0;
    };
    const beginStateTransition = (nextState, phase) => {
        clearStateTransition();
        if (reducedMotion() || root.dataset.state === nextState) { root.dataset.state = nextState; return; }
        const transitionTokenValue = stateTransitionToken;
        const currentHeight = root.getBoundingClientRect?.().height || 0;
        const targetHeight = measureStateHeight(nextState);
        if (nextState === 'expanded') {
            root.dataset.transition = `${phase}-out`;
            stateTransitionTimer = window.setTimeout(() => {
                if (transitionTokenValue !== stateTransitionToken) return;
                root.dataset.state = nextState;
                root.dataset.transition = `${phase}-in`;
                animateHeight(currentHeight, targetHeight, transitionTokenValue);
                stateTransitionTimer = window.setTimeout(() => {
                    if (transitionTokenValue !== stateTransitionToken) return;
                    delete root.dataset.transition;
                    root.style?.removeProperty('height');
                    stateTransitionTimer = 0;
                }, MOTION_MS);
            }, MOTION_MS);
            return;
        }
        root.dataset.transition = `${phase}-out`;
        animateHeight(currentHeight, targetHeight, transitionTokenValue);
        stateTransitionTimer = window.setTimeout(() => {
            if (transitionTokenValue !== stateTransitionToken) return;
            root.dataset.state = nextState;
            root.dataset.transition = `${phase}-in`;
            stateTransitionTimer = window.setTimeout(() => {
                if (transitionTokenValue !== stateTransitionToken) return;
                delete root.dataset.transition;
                root.style?.removeProperty('height');
                stateTransitionTimer = 0;
            }, HEIGHT_MS - MOTION_MS);
        }, MOTION_MS);
    };
    const updateSelectionUI = () => {
        const item = current();
        if (!item || !snapshot) return;
        const multiple = queue.length > 1;
        nav.hidden = !multiple;
        position.textContent = multiple
            ? label('positionTemplate', '{0} of {1}').replace('{0}', String(selected + 1)).replace('{1}', String(queue.length))
            : '';
        previous.disabled = !multiple || selected === 0;
        next.disabled = !multiple || selected === queue.length - 1;
        const total = Number(snapshot.queueTotalCount || queue.length);
        count.textContent = (total === 1 ? label('newSingular', '{0} NEW UPDATE') : label('newPlural', '{0} NEW UPDATES')).replace('{0}', String(total));
        link.href = `/Events/${encodeURIComponent(snapshot.eventSlug)}/Board?view=drops&submissionId=${encodeURIComponent(item.submissionId)}`;
    };
    const render = () => { settleSlide(); updateSelectionUI(); };
    const setCompact = () => {
        if (!visible || !expanded || interaction) return;
        window.clearTimeout(timer);
        timer = 0;
        cancelSlide();
        expanded = false;
        beginStateTransition('compact', 'to-compact');
        root.dataset.countdown = 'held';
        updateSelectionUI();
        saveState();
    };
    const setExpanded = (focus = false) => {
        if (!visible) return;
        clearStateTransition();
        cancelSlide();
        expanded = true;
        if (root.dataset.state === 'compact') beginStateTransition('expanded', 'to-expanded');
        else root.dataset.state = 'expanded';
        root.dataset.countdown = 'held';
        render();
        startCountdown();
        saveState();
        if (focus) (queue.length > 1 && selected === queue.length - 1 ? previous : link)?.focus({ preventScroll: true });
    };
    const showExpanded = () => {
        clearStateTransition();
        cancelSlide();
        expanded = true;
        visible = true;
        root.hidden = false;
        root.dataset.state = 'expanded';
        root.setAttribute('aria-hidden', 'false');
        root.dataset.countdown = 'held';
        render();
        saveState();
        const tokenValue = ++revealToken;
        const reveal = () => { if (tokenValue === revealToken && visible) { root.dataset.visible = 'true'; startCountdown(); } };
        if (root.dataset.visible === 'true') reveal(); else window.requestAnimationFrame?.(reveal);
    };
    const hideAnnouncement = clearCollection => {
        const visualState = root.dataset.state;
        clearStateTransition();
        window.clearTimeout(timer);
        timer = 0;
        revealToken += 1;
        cancelSlide();
        visible = false;
        expanded = false;
        interaction = false;
        root.dataset.visible = 'false';
        root.setAttribute('aria-hidden', 'true');
        if (visualState === 'compact') root.dataset.transition = 'dismiss';
        else root.dataset.state = 'expanded';
        if (clearCollection) { queue = []; selected = 0; }
        const tokenValue = ++hideToken;
        window.setTimeout(() => {
            if (visible || tokenValue !== hideToken) return;
            root.hidden = true;
            root.dataset.state = 'expanded';
            delete root.dataset.transition;
        }, reducedMotion() ? 0 : MOTION_MS + 20);
    };
    const switchSelection = direction => {
        const target = selected + direction;
        if (target < 0 || target >= queue.length) return;
        cancelSlide();
        const outgoing = main.firstElementChild;
        selected = target;
        updateSelectionUI();
        const incoming = buildSlide(current());
        if (!outgoing || reducedMotion()) { settleSlide(); if (expanded) startCountdown(); return; }
        const transitionTokenValue = ++transitionToken;
        outgoing.setAttribute('aria-hidden', 'true');
        incoming.setAttribute('aria-hidden', 'true');
        const incomingDirection = direction > 0 ? 'next' : 'previous';
        const outgoingDirection = direction > 0 ? 'out-next' : 'out-previous';
        incoming.dataset.direction = incomingDirection;
        main.append(incoming);
        window.requestAnimationFrame?.(() => { if (transitionTokenValue === transitionToken) outgoing.dataset.direction = outgoingDirection; });
        transitionTimer = window.setTimeout(() => {
            if (transitionTokenValue !== transitionToken) return;
            main.replaceChildren(incoming);
            incoming.setAttribute('aria-hidden', 'false');
            window.requestAnimationFrame?.(() => { if (transitionTokenValue === transitionToken) delete incoming.dataset.direction; });
            transitionTimer = window.setTimeout(() => {
                if (transitionTokenValue === transitionToken) { delete incoming.dataset.direction; transitionTimer = 0; }
            }, MOTION_MS);
        }, MOTION_MS);
        if (expanded) startCountdown();
        saveState();
    };

    const reconcile = incoming => {
        const revision = ++stateRevision;
        const oldId = current()?.submissionId;
        const oldIds = new Set(queue.map(item => item.submissionId));
        snapshot = incoming;
        updateNavigation();
        const oldState = readState();
        const storedId = oldState?.generation === snapshot.generation ? oldState.selectedId : null;
        claimPending = oldState?.generation === snapshot.generation && Boolean(oldState.claimPending);
        queue = Array.isArray(snapshot.queue) ? snapshot.queue : [];
        const selectedId = oldId && queue.some(item => item.submissionId === oldId) ? oldId : storedId;
        selected = Math.max(0, queue.findIndex(item => item.submissionId === selectedId));
        if (selected < 0) selected = 0;
        if (!queue.length) { claimPending = false; expanded = false; setVisible(false); window.clearTimeout(timer); timer = 0; saveState(); return; }
        const firstArrival = !oldState || oldState.generation !== snapshot.generation;
        const claimAndMaybeExpand = () => {
            if (submissionActive()) { claimPending = true; expanded = false; setVisible(true); root.dataset.state = 'compact'; render(); saveState(); return; }
            claimPending = true;
            const claimSnapshot = snapshot;
            const claimIsCurrent = () => revision === stateRevision && snapshot === claimSnapshot && queue.length > 0 && mutationsPending === 0;
            fetch('/api/drop-announcements/claim', { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, body: JSON.stringify({ eventId: claimSnapshot.eventId, snapshotSequence: claimSnapshot.snapshotSequence }) })
                .then(response => response.ok ? response.json() : null)
                .then(result => {
                    if (!claimIsCurrent()) return;
                    if (result?.claimed) {
                        claimPending = false;
                        if (!submissionActive()) {
                            selected = 0;
                            if (!visible || root.hidden || root.dataset.visible !== 'true') showExpanded();
                            else setExpanded();
                        } else { expanded = false; setVisible(true); root.dataset.state = 'compact'; render(); saveState(); }
                    } else { claimPending = true; expanded = false; setVisible(true); root.dataset.state = 'compact'; render(); saveState(); }
                })
                .catch(() => { if (!claimIsCurrent()) return; claimPending = true; expanded = false; setVisible(true); root.dataset.state = 'compact'; render(); saveState(); });
        };
        if (firstArrival) {
            suppressStorage = true;
            expanded = false;
            visible = false;
            root.hidden = true;
            root.dataset.visible = 'false';
            root.setAttribute('aria-hidden', 'true');
            root.dataset.state = 'compact';
            render();
            suppressStorage = false;
            claimAndMaybeExpand();
        } else {
            expanded = Boolean(oldState.expanded);
            setVisible(true);
            root.dataset.state = expanded ? 'expanded' : 'compact';
            render();
            if (expanded) ensureCountdown();
            else if (claimPending || queue.some(item => !oldIds.has(item.submissionId))) claimAndMaybeExpand();
        }
        saveState();
    };
    const refresh = async () => {
        const revision = ++requestRevision;
        if (mutationsPending) { refreshQueued = true; return; }
        refreshQueued = false;
        try {
            const response = await fetch('/api/drop-announcements/current?limit=100', { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
            const incoming = response.ok && response.status !== 204 ? await response.json() : null;
            if (revision !== requestRevision || mutationsPending) return;
            if (response.status === 204) {
                stateRevision += 1;
                try { if (key()) sessionStorage.removeItem(key()); } catch { }
                clearStateTransition();
                window.clearTimeout(timer); timer = 0;
                revealToken += 1;
                snapshot = null; queue = []; selected = 0; claimPending = false;
                updateNavigation(); setVisible(false);
                return;
            }
            if (response.ok) reconcile(incoming);
        } catch { /* Reconnect and the next navigation reconcile authoritative state. */ }
    };
    const mutate = async (path, body) => {
        mutationsPending += 1;
        requestRevision += 1;
        stateRevision += 1;
        const response = await fetch(`/api/drop-announcements/${path}`, { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, body: JSON.stringify(body), credentials: 'same-origin' });
        if (!response.ok) throw new Error('announcement mutation failed');
    };
    const settleMutation = async () => {
        // Keep reads fenced until each handler has also applied its local UI outcome.
        mutationsPending -= 1;
        requestRevision += 1;
        stateRevision += 1;
        if (!mutationsPending && refreshQueued) await refresh();
    };

    previous.addEventListener('click', () => switchSelection(-1));
    next.addEventListener('click', () => switchSelection(1));
    expand.addEventListener('click', () => setExpanded(true));
    dismiss.addEventListener('click', async () => {
        if (!snapshot) return;
        try { await mutate('dismiss', { eventId: snapshot.eventId, generation: snapshot.generation, snapshotSequence: snapshot.snapshotSequence || 0, submissionIds: queue.map(item => item.submissionId) }); hideAnnouncement(true); saveState(); } catch { await refresh(); } finally { await settleMutation(); }
    });
    clearAll?.addEventListener('click', async () => {
        const eventId = clearAll.dataset.dropEventId || snapshot?.eventId;
        if (!eventId) return;
        clearAll.disabled = true;
        try { await mutate('clear-all-new', { eventId }); window.showBingoToast?.(label('clearSuccess', 'All new marks cleared.')); window.dispatchEvent(new CustomEvent('drop-announcement-acknowledged', { detail: { eventId, all: true } })); await refresh(); } catch { window.showBingoToast?.(document.documentElement?.dataset.postError || 'The change could not be sent. Check your connection and try again.', 'error'); } finally { clearAll.disabled = false; await settleMutation(); }
    });
    window.addEventListener('public-evidence-opened', async event => {
        const submissionId = event.detail?.submissionId;
        const eventId = event.detail?.eventId || snapshot?.eventId;
        if (!submissionId || !eventId) return;
        try {
            await mutate('acknowledge-both', { eventId, submissionId });
            window.dispatchEvent(new CustomEvent('drop-announcement-acknowledged', { detail: { eventId, submissionIds: [submissionId] } }));
            const index = queue.findIndex(item => item.submissionId === submissionId);
            if (index >= 0) { queue.splice(index, 1); selected = Math.min(selected, Math.max(0, queue.length - 1)); if (!queue.length) hideAnnouncement(false); else { expanded = false; root.dataset.state = 'compact'; render(); saveState(); } }
            await refresh();
        } catch { } finally { await settleMutation(); }
    });
    root.addEventListener('pointerenter', holdCountdown);
    root.addEventListener('pointerleave', releaseCountdown);
    root.addEventListener('pointerdown', holdCountdown);
    root.addEventListener('focusin', holdCountdown);
    root.addEventListener('focusout', event => { if (!root.contains(event.relatedTarget)) releaseCountdown(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && visible) { event.preventDefault(); setCompact(); } });
    window.addEventListener('bingo-progress-changed', refresh);
    window.addEventListener('pageshow', refresh);

    if (window.bingoProgressConnection) window.bingoProgressConnection.onreconnected(() => refresh());
    else if (window.signalR) {
        const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/progress').withAutomaticReconnect().build();
        connection.on('progressChanged', refresh);
        connection.start().catch(() => {});
    }
    refresh();
})();
