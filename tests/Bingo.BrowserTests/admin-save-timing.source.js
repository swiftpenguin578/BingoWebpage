const assert=require('node:assert/strict'),fs=require('node:fs');
const {checkSaveTiming}=require('../../scripts/lib/admin-page-conformance-checks.cjs');
const pages=require('../../scripts/lib/admin-page-conformance-pages.cjs');
for(const page of pages){const source=fs.readFileSync('src/Bingo.Web/wwwroot/js/'+page.module,'utf8');checkSaveTiming(source,page);assert.throws(()=>checkSaveTiming(source,{...page,postSaveCount:page.postSaveCount+1}),/declared POST save paths/);}
const catalogue=fs.readFileSync('src/Bingo.Web/wwwroot/js/admin-catalogue.js','utf8'),registration=pages.find(p=>p.family==='catalogue');
assert.throws(()=>checkSaveTiming(catalogue.replace('=> ui.busy(() => window.AdminFetch.request(`/Admin/Catalogue?handler=${handler}`', '=> window.AdminFetch.request(`/Admin/Catalogue?handler=${handler}`'),registration),/save transport uses shared busy timing/);
assert.throws(()=>checkSaveTiming(catalogue.replace('const outcome = await post(', 'setTimeout(save,250); const outcome = await post('),registration),/no local busy timer/);
const signup=fs.readFileSync('src/Bingo.Web/wwwroot/js/admin-signup-setup.js','utf8'),signupRegistration=pages.find(p=>p.family==='signupsetup');
assert.throws(()=>checkSaveTiming(signup.replace('await ui.busy(()=>request(handler,attempt.body))','await request(handler,attempt.body)'),signupRegistration),/save transport uses shared busy timing/);
console.log('PASS all eight declared POST counts; direct/returned/conditional-helper save boundaries; removed busy, wrong counts and local timers rejected.');
