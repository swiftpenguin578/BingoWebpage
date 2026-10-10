// Multi-boss tiles show one boss at a time at full size and cross-fade to the next.
// Shared by the team board and the Board preview: timing lives here; the CSS only animates opacity.
(function () {
    'use strict';
    const DISPLAY_MS = 6000; // each boss stays this long, in tile order, looping
    const FADE_MS = 1500;    // cross-fade between two bosses
    const STAGGER_STEP_MS = 700, STAGGER_SLOTS = 4; // deterministic per-tile offset so tiles never all switch together
    const GRID = '.public-ui-team-board-tile__boss-art-grid';
    const TILE = '.public-ui-team-board-tile';
    const running = new Set();
    const reducedMotion = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;

    function show(state, index) {
        state.index = index;
        state.images.forEach((image, i) => image.classList.toggle('is-active', i === index));
        state.grid.dataset.bossActive = String(index);
    }

    function schedule(state, delay) {
        window.clearTimeout(state.timer);
        state.due = Date.now() + delay;
        state.timer = window.setTimeout(() => {
            if (!state.grid.isConnected) { stop(state); return; }
            show(state, (state.index + 1) % state.images.length);
            schedule(state, DISPLAY_MS);
        }, delay);
    }

    function stop(state) {
        window.clearTimeout(state.timer);
        running.delete(state);
    }

    function start(scope) {
        const root = scope || document;
        const tiles = Array.from(root.querySelectorAll(TILE));
        const grids = root.matches && root.matches(GRID) ? [root] : Array.from(root.querySelectorAll(GRID));
        for (const grid of grids) {
            if (grid.dataset.bossFadeStarted) continue;
            const images = Array.from(grid.querySelectorAll('img'));
            if (images.length < 2) continue;
            grid.dataset.bossFadeStarted = '';
            const tile = grid.closest(TILE);
            const tileIndex = Math.max(0, tiles.indexOf(tile));
            const state = { grid, images, index: 0, timer: 0, due: 0, remaining: 0, offset: (tileIndex % STAGGER_SLOTS) * STAGGER_STEP_MS + Math.floor(tileIndex / STAGGER_SLOTS) * 250 };
            grid.dataset.bossFade = '';
            grid.style.setProperty('--boss-fade-ms', FADE_MS + 'ms');
            show(state, 0);
            running.add(state);
            if (!reducedMotion || !reducedMotion.matches) {
                if (document.visibilityState === 'hidden') { state.remaining = DISPLAY_MS + state.offset; }
                else schedule(state, DISPLAY_MS + state.offset);
            }
        }
    }

    function dropDetached() {
        for (const state of running) if (!state.grid.isConnected) stop(state);
    }

    function pause() {
        dropDetached();
        for (const state of running) {
            if (!state.timer) continue;
            window.clearTimeout(state.timer);
            state.timer = 0;
            state.remaining = Math.max(0, state.due - Date.now());
        }
    }

    function resume() {
        dropDetached();
        if (reducedMotion && reducedMotion.matches) return;
        for (const state of running) {
            if (state.timer) continue;
            schedule(state, state.remaining || DISPLAY_MS);
        }
    }

    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') pause(); else resume(); });
    if (reducedMotion && reducedMotion.addEventListener) reducedMotion.addEventListener('change', () => {
        if (reducedMotion.matches) for (const state of running) { window.clearTimeout(state.timer); state.timer = 0; show(state, 0); }
        else if (document.visibilityState !== 'hidden') resume();
    });

    window.BossArtFade = Object.freeze({ start, displayMs: DISPLAY_MS, fadeMs: FADE_MS, activeCount: () => { dropDetached(); return running.size; } });
    // Pages that render the tiles server-side (team board) start on load; the Board preview calls start(board) when it opens.
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => start(document));
    else start(document);
})();
