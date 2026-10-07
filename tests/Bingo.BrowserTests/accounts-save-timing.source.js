const assert=require('node:assert/strict'),fs=require('node:fs');
const {checkSaveTiming}=require('../../scripts/lib/admin-page-conformance-checks.cjs');
const checkAccountSaveTiming=source=>checkSaveTiming(source,{family:'accounts',postSaveCount:3});
const source=fs.readFileSync('src/Bingo.Web/wwwroot/js/admin-accounts.js','utf8');
assert.match(source,/setTimeout\(searchNow, 250\)/);checkAccountSaveTiming(source);
assert.throws(()=>checkAccountSaveTiming(source.replace("await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST'", "await window.AdminFetch.request(form.action, { method: 'POST'")),/save transport uses shared busy timing/);
assert.throws(()=>checkAccountSaveTiming(source.replace("const outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST'", "setTimeout(save,250); const outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST'")),/no local busy timer/);
console.log('PASS Accounts three save paths require shared busy;250ms search debounce allowed; unwrapped/timed-save mutations rejected');
