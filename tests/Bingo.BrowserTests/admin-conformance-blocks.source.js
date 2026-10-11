const assert = require('node:assert/strict');
const {registeredBlockShifts} = require('../../scripts/lib/admin-page-conformance-checks.cjs');
const pages = require('../../scripts/lib/admin-page-conformance-pages.cjs');
const fs = require('node:fs'), path = require('node:path');
// Every registered family runs in exactly one page-conformance entry.
const entries = fs.readdirSync(__dirname).filter(file => /^admin-design-page-conformance-\d+\.browser\.js$/.test(file));
const entryFamilies = entries.flatMap(file => /BINGO_CONFORMANCE_PAGES = '([^']+)'/.exec(fs.readFileSync(path.join(__dirname, file), 'utf8'))[1].split(','));
assert.deepEqual([...entryFamilies].sort(), pages.map(page => page.family).sort(), 'each registered family in exactly one page-conformance entry');
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
console.log('PASS: every registered body block required in both states; ' + missingCases + ' missing-block cases rejected; ' + pages.length + ' families in ' + entries.length + ' page-conformance entries.');
