// Readback transport for the Schedule integration. Callers resolve changed local
// values before dispatch and supply the full intended UTC tuple; this module
// never reparses minute display strings or reconstructs an intent after a timeout.
(() => {
    const instantFields = ['signupOpensAt', 'signupClosesAt', 'draftAt', 'eventStartsAt', 'eventEndsAt'];
    const valueFields = [...instantFields, 'scheduledSignupOpeningEnabled'];
    const phases = ['Draft', 'SignupOpen', 'SignupClosed', 'Live', 'AwaitingFinalReview', 'Finalized', 'Archived', 'Cancelled'];
    const drafts = [null, 'Setup', 'Running', 'Paused', 'Finalized'];
    function instant(value) {
        if (value === null) return null;
        if (typeof value !== 'string') throw new Error('Missing UTC instant');
        const match = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(?:Z|\+00:00)$/.exec(value);
        if (!match || !Number.isFinite(Date.parse(value))) throw new Error('Invalid UTC instant');
        // Date comparisons would discard microseconds. Retain all seven .NET
        // fraction digits; persisted PostgreSQL values already have microsecond precision.
        return `${match[1]}.${(match[2] || '').padEnd(7, '0')}Z`;
    }
    function values(source) {
        if (!source || typeof source.scheduledSignupOpeningEnabled !== 'boolean')
            throw new Error('Incomplete schedule');
        const result = Object.fromEntries(instantFields.map(field => [field, instant(source[field])]));
        result.scheduledSignupOpeningEnabled = source.scheduledSignupOpeningEnabled;
        return Object.freeze(result);
    }
    function snapshot(source) {
        if (!source || typeof source.eventId !== 'string' || !/^[0-9]+$/.test(source.version) || typeof source.version !== 'string'
            || typeof source.timezone !== 'string' || !source.timezone || !phases.includes(source.phase) || !drafts.includes(source.draftState)
            || !source.editable || instantFields.some(field => typeof source.editable[field] !== 'boolean')) throw new Error('Incomplete current state');
        return Object.freeze({ eventId: source.eventId, version: source.version, timezone: source.timezone, displayTimezone: source.displayTimezone || source.timezone, phase: source.phase,
            draftState: source.draftState, values: values(source.values),
            editable: Object.freeze(Object.fromEntries(instantFields.map(field => [field, source.editable[field]]))) });
    }
    const equalValues = (a, b) => valueFields.every(field => a[field] === b[field]);
    window.createScheduleReadbackSession = (observed, submittedValues, routeUrl) => {
        const baseline = snapshot(observed);
        const expected = values(submittedValues);
        const url = new URL(routeUrl, window.location.href);
        if (url.origin !== window.location.origin) throw new Error('Schedule readback must be same-origin');
        url.searchParams.set('handler', 'Current');
        return Object.freeze({ baseline, expected, async checkAgain(signal, draft) {
            try {
                const outcome = await window.AdminFetch.request(url.href, { cache: 'no-store', signal, draft, readback: true });
                if (outcome.kind !== 'handler') return Object.freeze({ state: 'unknown', outcome });
                const current = snapshot(outcome.data);
                if (current.eventId !== baseline.eventId || BigInt(current.version) < BigInt(baseline.version)) throw new Error('Unexpected current state');
                const versionChanged = current.version !== baseline.version;
                const contextChanged = current.timezone !== baseline.timezone || current.phase !== baseline.phase || current.draftState !== baseline.draftState
                    || instantFields.some(field => current.editable[field] !== baseline.editable[field]);
                const state = equalValues(current.values, expected) ? 'upToDate'
                    : !versionChanged && equalValues(current.values, baseline.values) ? 'unchanged' : 'different';
                // Matching is current-state evidence only, regardless of which Admin
                // wrote it. All results retain the same immutable baseline and intent.
                return Object.freeze({ state, current, versionChanged, contextChanged });
            } catch {
                return Object.freeze({ state: 'unknown' });
            }
        } });
    };
})();
