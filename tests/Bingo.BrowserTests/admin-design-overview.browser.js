// U4 / OS-1: Overview lifecycle dialogs against the real parity host (PostgreSQL).
// Uncertain readback, stale and "gone" re-evaluation, session loss (C-CMP-2), the
// reason rule before the scheduled end, evidence codes (RC01 R2) and link copy.
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER||'chromium',output=path.join(process.cwd(),'artifacts/overview-'+engine),fixture=await startFixture(process.cwd(),output);let browser;const results=[];
 try{
  browser=await(engine==='webkit'?webkit:chromium).launch({headless:true});
  const context=await browser.newContext({viewport:{width:1280,height:900}}),page=await login(context,fixture),errors=[];page.on('pageerror',e=>errors.push(e.message));
  const open='/Admin/Events/Manage/'+fixture.events['autumn-bingo-2027'],live='/Admin/Events/Manage/'+fixture.events['midsummer-skilling-sprint'];
  const current=p=>p.locator('[data-overview]').getAttribute('data-current').then(JSON.parse);
  const ready=p=>p.waitForFunction(()=>document.querySelector('[data-overview]')&&window.AdminUI);
  const banner=()=>page.locator('.modal .ov-dlg-banner').textContent();

  // Uncertain outcome: the close really applies, the response is lost; Check again reads it back.
  await page.goto(fixture.origin+open);await ready(page);
  assert.equal((await current(page)).phase,'SignupOpen');
  await page.locator('#act-close').click();await page.locator('.modal [data-confirm-title]').waitFor();
  assert.match(await page.locator('.modal [data-confirm-title]').textContent(),/^Close signups for Autumn Bingo 2027\?$/);
  assert.match(await page.locator('.modal .ov-effects').textContent(),/keep their places/);
  await page.route('**'+open+'?handler=CloseSignup',async r=>{await r.fetch({maxRedirects:0});await r.fulfill({status:502,contentType:'text/plain',body:'lost'});});
  await page.locator('.modal [data-confirm-accept]').click();
  await page.waitForFunction(()=>document.querySelector('.modal [data-confirm-accept]')?.textContent.includes('Check again'));
  assert.match(await banner(),/couldn’t confirm whether this happened/);
  await page.unroute('**'+open+'?handler=CloseSignup');
  await page.locator('.modal [data-confirm-accept]').click();
  await page.waitForFunction(()=>!document.querySelector('.modal')&&JSON.parse(document.querySelector('[data-overview]').dataset.current).phase==='SignupClosed');
  assert.match(await page.locator('[data-toast-host]').textContent(),/Signups are closed for Autumn Bingo 2027\./);
  assert.equal(await page.locator('.page-head .badge').textContent(),'Signups closed');
  await page.waitForFunction(()=>document.activeElement?.id==='now-title');
  results.push('uncertain close: Check again reads back the applied change, toast, in-place update, focus to the stage heading');

  // Reopen is gated by the same signup checklist (A-Overview-3): Discord sign-in isn't configured in the fixture.
  assert.equal(await page.locator('#act-reopen').getAttribute('aria-disabled'),'true');
  await page.locator('#act-reopen').evaluate(e=>e.click());await page.waitForFunction(()=>document.querySelector('[data-toast-host]')?.textContent.includes('Finish the requirements listed first.'));
  assert.equal(await page.locator('.modal').count(),0);assert.match(await page.locator('#reopen-checklist').textContent(),/Discord sign-in/);
  results.push('Reopen inert with its own checklist rows; no dialog');

  // End before the scheduled end needs a reason; the field error stays in the dialog.
  await page.goto(fixture.origin+live);await ready(page);
  await page.locator('#act-end').click();await page.locator('#dlg-reason').waitFor();
  await page.locator('.modal [data-confirm-accept]').click();
  assert.match(await page.locator('#dlg-reason-err').textContent(),/Enter a reason\./);
  // Stale: the server reports a changed version; the dialog refreshes and asks again.
  await page.locator('#dlg-reason').fill('Called early by the clan');
  await page.route('**'+live+'?handler=EndEvent',r=>r.fulfill({status:200,contentType:'application/json',body:JSON.stringify({succeeded:false,outcome:'stale',error:'changed'})}));
  await page.locator('.modal [data-confirm-accept]').click();
  await page.waitForFunction(()=>document.querySelector('.modal .ov-dlg-banner')?.textContent.includes('The details below are up to date'));
  assert.equal(await page.locator('#dlg-reason').inputValue(),'Called early by the clan');
  results.push('reason required before the scheduled end; stale refresh keeps the entry');
  // C-CMP-2: a lost session shows what wasn't saved and keeps the dialog.
  await page.unroute('**'+live+'?handler=EndEvent');
  await page.route('**'+live+'?handler=EndEvent',r=>r.fulfill({status:200,headers:{'Content-Type':'text/html','X-Bingo-Post-Navigation':'/Account/Login'},body:'<html>Sign in</html>'}));
  await page.locator('.modal [data-confirm-accept]').click();
  await page.getByRole('heading',{name:'Your changes were not saved'}).waitFor();
  assert.match(await page.locator('.kv').textContent(),/Called early by the clan/);
  await page.getByRole('button',{name:'Keep editing',exact:true}).click();
  assert.equal(await page.locator('#dlg-reason').inputValue(),'Called early by the clan');
  await page.unroute('**'+live+'?handler=EndEvent');
  results.push('session loss lists the unsent reason and keeps the dialog');

  // "Gone": another admin ends the event first; this dialog says it no longer applies.
  const other=await context.newPage();await other.goto(fixture.origin+live);await ready(other);
  await other.locator('#act-end').click();await other.locator('#dlg-reason').fill('Second admin');await other.locator('.modal [data-confirm-accept]').click();
  await other.waitForFunction(()=>JSON.parse(document.querySelector('[data-overview]').dataset.current).phase==='AwaitingFinalReview');await other.close();
  await page.locator('.modal [data-confirm-accept]').click();
  await page.waitForFunction(()=>document.querySelector('.modal .ov-dlg-banner')?.textContent.includes('no longer applies'));
  assert.equal(await page.locator('.modal [data-confirm-accept]').isVisible(),false);assert.equal(await page.locator('.modal [data-confirm-cancel]').textContent(),'Close');
  await page.locator('.modal [data-confirm-cancel]').click();
  await page.waitForFunction(()=>!document.querySelector('.modal')&&document.querySelector('.page-head .badge')?.textContent==='Final review');
  results.push('gone: only Close, then the page re-reads into Final review');

  // Resume always asks for a replacement end (AU20) and a reason.
  await page.locator('#act-resume').click();await page.locator('#dlg-until-date').waitFor();await page.locator('#dlg-reason').waitFor();
  assert.notEqual(await page.locator('input[name="ReplacementEventEndsAtLocal"]').inputValue(),'');
  await page.keyboard.press('Escape');await page.locator('.modal').waitFor({state:'detached'});
  results.push('Resume asks for a new end and a reason; Escape closes an untouched dialog');

  // Evidence codes dialog on a Live event (uploads still open after the early end window).
  await page.goto(fixture.origin+'/Admin/Events/Manage/'+fixture.events['clan-cup-pvm-week']);await ready(page);
  await page.locator('[data-overview-codes]').click();await page.locator('#codes-enabled').waitFor();
  await page.locator('#codes-enabled').check();await page.waitForFunction(()=>document.querySelector('#code-value')&&!document.querySelector('#code-value').disabled);
  await page.locator('#codes-save').click();assert.match(await page.locator('#code-err').textContent(),/Enter or generate a code\./);
  await page.locator('#code-value').fill('ab12cd');await page.locator('#codes-save').click();
  await page.waitForFunction(()=>document.querySelector('.modal .ro-list')?.textContent.includes('AB12CD'));
  await page.locator('#codes-close').click();await page.locator('.modal').waitFor({state:'detached'});
  await page.waitForFunction(()=>document.querySelector('.ov-aside')?.textContent.includes('Evidence codes'));
  results.push('evidence codes: enable, validate, add, list refreshes');

  // Copy shows a toast either way (clipboard may be unavailable headless).
  await page.goto(fixture.origin+open);await ready(page);
  await page.locator('[data-overview-copy]').first().click();
  await page.waitForFunction(()=>/Link copied\.|Couldn’t copy automatically/.test(document.querySelector('[data-toast-host]')?.textContent||''));
  results.push('copy permanent link');
  assert.deepEqual(errors,[]);
  console.log('PASS overview '+engine+': '+results.join('; '));
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
