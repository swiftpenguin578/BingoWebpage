// AU10's shipped GET transport against controlled HTTP, with a simulated lost
// mutation response. Real Razor save/persistence coverage is in integration tests.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const vm = require('node:vm');
const instantFields = ['signupOpensAt', 'signupClosesAt', 'draftAt', 'eventStartsAt', 'eventEndsAt'];
const copy = value => JSON.parse(JSON.stringify(value));
const initial = {
  eventId: '11111111-1111-1111-1111-111111111111', version: '9007199254740993', timezone: 'Europe/Copenhagen', phase: 'Draft', draftState: 'Setup',
  values: {signupOpensAt:'2027-10-10T12:00:11.1234560+00:00',signupClosesAt:'2027-10-11T12:00:12.2345670+00:00',draftAt:'2027-10-31T01:30:17.1234560+00:00',eventStartsAt:'2027-11-01T12:00:13.3456780+00:00',eventEndsAt:'2027-11-02T12:00:14.4567890+00:00',participantCap:20,scheduledSignupOpeningEnabled:false},
  editable: Object.fromEntries(instantFields.map(field => [field, true]))
};
let current = copy(initial), failure = null, posts = 0, reads = 0;
const server = http.createServer(async (req, res) => {
  if (req.method === 'POST') {
    posts++; let body = ''; for await (const chunk of req) body += chunk;
    current.values = JSON.parse(body); current.version = '9007199254740994';
    req.socket.destroy(); // Applied, response lost; no result consumed by the session.
    return;
  }
  reads++; assert.equal(req.method, 'GET'); assert.equal(new URL(req.url, 'http://localhost').searchParams.get('handler'), 'Current');
  if (failure === 'network') return req.socket.destroy();
  if (failure === 'redirect') { res.writeHead(302, {location:'/Account/Login'}); return res.end(); }
  if (failure === '503' || failure === '403') { res.writeHead(Number(failure)); return res.end(); }
  res.setHeader('Content-Type', 'application/json');
  res.end(failure === 'json' ? '{' : JSON.stringify(current));
});
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    const url = `http://127.0.0.1:${server.address().port}/Admin/Events/Schedule/${initial.eventId}`;
    const context = { window:{location:new URL(url)}, URL, fetch };
    vm.runInNewContext(fs.readFileSync('src/Bingo.Web/wwwroot/js/event-schedule.js','utf8'), context);
    const draft = {...initial.values, eventEndsAt:'2027-11-03T12:00:00Z'};
    const observed = copy(initial);
    const session = context.window.createScheduleReadbackSession(observed, draft, url);
    const expected = JSON.stringify(session.expected), baseline = JSON.stringify(session.baseline);
    assert(Object.isFrozen(session) && Object.isFrozen(session.expected) && Object.isFrozen(session.baseline.values) && Object.isFrozen(session.baseline.editable));
    observed.values.draftAt = null; draft.draftAt = null; // Later UI edits cannot alter submitted intent/history.
    let result = await session.checkAgain(); assert.equal(result.state, 'unchanged'); assert.equal(result.versionChanged, false);
    await assert.rejects(fetch(url, {method:'POST',body:expected}));
    result = await session.checkAgain(); assert.equal(result.state, 'upToDate'); assert.equal(result.versionChanged, true); assert.equal(result.contextChanged, false);
    assert(!('saved' in result)); assert(!('receipt' in result));
    // A different Admin can produce exactly the same values. Version changes
    // provide context but cannot attribute the write to the lost request.
    current.version = '9007199254740995'; assert.equal((await session.checkAgain()).state, 'upToDate');
    current.values.eventStartsAt = '2027-11-01T12:00:13.345679Z'; assert.equal((await session.checkAgain()).state, 'different');
    current.values.eventStartsAt = session.expected.eventStartsAt;
    current.values.scheduledSignupOpeningEnabled = true; assert.equal((await session.checkAgain()).state, 'different');
    current.values.scheduledSignupOpeningEnabled = false;
    current.phase = 'Live'; current.draftState = 'Finalized'; current.editable.eventStartsAt = false;
    result = await session.checkAgain(); assert.equal(result.state, 'upToDate'); assert.equal(result.contextChanged, true); assert.equal(result.current.phase, 'Live');
    current.timezone = 'UTC'; assert.equal((await session.checkAgain()).contextChanged, true);
    for (failure of ['503', '403', 'network', 'redirect', 'json']) assert.equal((await session.checkAgain()).state, 'unknown');
    failure = null;
    for (const mutate of [value => {delete value.editable;}, value => {value.eventId='wrong';}, value => {delete value.values.eventEndsAt;}, value => {value.version='9007199254740992';}, value => {value.version=123;}]) {
      current=copy(initial); mutate(current); assert.equal((await session.checkAgain()).state, 'unknown');
    }
    assert.equal(JSON.stringify(session.expected), expected); assert.equal(JSON.stringify(session.baseline), baseline);
    assert.equal(draft.draftAt, null); assert.equal(posts, 1); assert(reads >= 15);
    console.log('PASS: shipped real GET transport; immutable precise full submission/baseline; applied lost response, unchanged, matching competing values, distinct microseconds/auto-opening, large versions, phase/timezone/editability, unauthorized/failed/malformed reads; retained draft and GET-only recovery, no request attribution.');
  } finally { await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode=1; });
