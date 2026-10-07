const assert = require('node:assert/strict');
const {registeredBlockShifts} = require('../../scripts/lib/admin-page-conformance-checks.cjs');
const pages = require('../../scripts/lib/admin-page-conformance-pages.cjs');
let missingCases = 0;
for (const page of pages) {
  const state = () => ({blocks: Object.fromEntries(Object.keys(page.blocks).map(key => [key, {x: 12, y: 30, width: 200}]))});
  assert.equal(Object.keys(registeredBlockShifts(page, state(), state(), 0)).length, Object.keys(page.blocks).length);
  for (const key of Object.keys(page.blocks)) for (const missing of ['loading', 'loaded']) {
    const loading = state(), loaded = state();
    (missing === 'loading' ? loading : loaded).blocks[key] = null;
    assert.throws(() => registeredBlockShifts(page, loading, loaded, 0), new RegExp('missing ' + missing + ' body block ' + key));
    missingCases++;
  }
}
console.log('PASS: every registered body block required in both states; ' + missingCases + ' missing-block cases rejected.');
