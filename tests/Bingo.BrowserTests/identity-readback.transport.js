// Focused AU09 client transport proof against shipped scripts; no application database.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const web = 'src/Bingo.Web/';
const partial = fs.readFileSync(`${web}Pages/Shared/_AdminConfirmation.cshtml`, 'utf8').replace(/@T\["([^"]+)"\]/g, '$1');
const eventId = '11111111-1111-1111-1111-111111111111';
const routeUrl = `https://bingo.test/Admin/Events/Identity/${eventId}`;
(async () => {
  const browser = await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL || 'chrome'});
  try {
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    let posts = 0, reads = 0, readFailure = false;
    let appliedValues;
    let values;
    await page.route('https://bingo.test/**', async route => {
      const request = route.request(), url = new URL(request.url());
      if (url.pathname.startsWith('/js/')) return route.fulfill({contentType:'text/javascript', body:fs.readFileSync(`${web}wwwroot${url.pathname}`, 'utf8')});
      if (request.method() === 'POST') {
        posts++;
        const wire = request.postDataBuffer().toString('utf8');
        const submitted = field => wire.match(new RegExp(`name="Input.${field}"\\r\\n\\r\\n([\\s\\S]*?)\\r\\n--`))[1];
        appliedValues = {name:submitted('Name'),description:submitted('Description'),buyInDescription:submitted('BuyInDescription'),timezone:submitted('Timezone')};
        assert.equal(appliedValues.description, 'First description line\r\nSecond description line');
        assert.equal(appliedValues.buyInDescription, 'First buy-in line\r\nSecond buy-in line');
        values = {...appliedValues};
        return route.abort('failed'); // Response lost after applying the actual serialized tuple.
      }
      if (url.searchParams.get('handler') === 'Current') {
        reads++;
        if (readFailure) return route.fulfill({status:503, body:'Read unavailable'});
        return route.fulfill({contentType:'application/json', body:JSON.stringify({eventId,values})});
      }
      return route.fulfill({contentType:'text/html',body:`<!doctype html><html><body><main id="main-content"><section data-identity-editor data-identity-event-id="${eventId}" data-identity-check-again="Check again" data-identity-up-to-date="Up to date. Current values match; request outcome is not attributed." data-identity-different="Different. The outcome is still uncertain; draft retained." data-identity-unknown="Unknown. Check again; draft retained."><form method="post" action="${routeUrl}">
      <input name="Input.Name" value="Intended"><textarea name="Input.Description">First description line
Second description line</textarea><textarea name="Input.BuyInDescription">First buy-in line
Second buy-in line</textarea><input name="Input.Timezone" value="Europe/Copenhagen"><input name="Input.HasBaseline" value="true"><input name="Input.OriginalName" value="Original"><input name="Input.OriginalDescription" value=""><input name="Input.OriginalBuyInDescription" value=""><input name="Input.OriginalTimezone" value="UTC"><input name="Input.HasReviewedValues" value="false">
      <button type="submit" data-identity-save>Save identity</button><section data-identity-timezone-preview data-title="Confirm timezone change" data-action-label="Confirm timezone change"><p data-identity-timezone-consequence>Stored instants stay fixed.</p><button type="submit" data-identity-timezone-confirm>Confirm</button></section></form></section></main>${partial}<script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/event-identity.js"></script></body></html>`});
    });
    await page.goto(routeUrl);
    const modal=page.locator('[data-admin-confirmation]'), action=modal.locator('[data-admin-confirmation-action]'), feedback=modal.locator('[data-admin-confirmation-feedback]');
    await action.click(); await feedback.waitFor({state:'visible'});
    assert.equal(posts,1); assert.equal(await action.textContent(),'Check again'); assert.match(await feedback.textContent(),/Unknown/);
    await action.click(); await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-feedback]').textContent.startsWith('Up to date'));
    assert.equal(posts,1); assert.equal(reads,1); assert.equal(page.url(),routeUrl);
    values={...values,buyInDescription:'First buy-in line\r\nGenuinely different second line'};
    await action.click(); await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-feedback]').textContent.startsWith('Different'));
    readFailure=true; await action.click(); await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-feedback]').textContent.startsWith('Unknown'));
    await page.keyboard.press('Escape');
    await page.locator('[name="Input.Name"]').fill('Further unsaved edit');
    await page.locator('[data-identity-save]').click(); await modal.waitFor({state:'visible'});
    readFailure=false; values={...appliedValues};
    await action.click(); await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-feedback]').textContent.startsWith('Up to date'));
    assert.equal(posts,1,'Check again and form resubmission never retry the mutation');
    assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Further unsaved edit','Readback preserves later draft too');
    const result=await page.evaluate(async ({eventId,routeUrl}) => {
      const data=new FormData(document.querySelector('[data-identity-editor] form'));
      data.set('Input.HasReviewedValues','true');
      data.set('Input.Name','My original intent'); data.set('Input.ReviewedName','Reviewed name'); data.set('Input.NameResolution','UseCurrent');
      data.set('Input.Description','Retained first\nRetained second'); data.set('Input.OriginalDescription','Retained first\r\nRetained second');
      data.set('Input.ReviewedDescription','Observed first\nObserved second');
      data.set('Input.BuyInDescription','My changed first\nMy changed second'); data.set('Input.BuyInDescriptionResolution','UseCurrent');
      data.set('Input.ReviewedBuyInDescription','Reviewed first\nReviewed second'); data.set('Input.ReviewedTimezone','UTC');
      const session=createIdentityReadbackSession(data,routeUrl,eventId);
      data.set('Input.ReviewedName','Later edit');
      const expected=session.expected;
      const fetch=window.fetch; const requests=[];
      window.fetch=async (url, options) => { requests.push(options.method); return {ok:true,redirected:false,json:async()=>({eventId,values:{...expected}})}; };
      const matching=await session.checkAgain();
      window.fetch=async()=>({ok:true,redirected:false,json:async()=>({eventId,values:{...expected,description:'Unseen merge after dispatch'}})});
      const different=await session.checkAgain();
      window.fetch=async()=>{throw new Error('network');}; const unknown=await session.checkAgain();
      window.fetch=async()=>({ok:true,redirected:true,json:async()=>({eventId,values:expected})}); const redirected=await session.checkAgain();
      window.fetch=async()=>({ok:true,redirected:false,json:async()=>({eventId:'another-event',values:expected})}); const wrongEvent=await session.checkAgain();
      window.fetch=async()=>({ok:true,redirected:false,json:async()=>({eventId,values:{name:expected.name}})}); const malformed=await session.checkAgain();
      window.fetch=fetch;
      return {expected, frozen:Object.isFrozen(expected), matching:matching.state,different:different.state,unknown:unknown.state,redirected:redirected.state,wrongEvent:wrongEvent.state,malformed:malformed.state,requests};
    },{eventId,routeUrl});
    assert.equal(result.expected.name,'Reviewed name'); assert.equal(result.expected.description,'Observed first\nObserved second','Untouched detection uses submitted newlines, observed value stays exact'); assert.equal(result.expected.buyInDescription,'Reviewed first\nReviewed second','Use current preserves exact observed newlines'); assert.equal(result.frozen,true);
    assert.equal(result.matching,'upToDate'); assert.equal(result.different,'different');
    for (const state of ['unknown','redirected','wrongEvent','malformed']) assert.equal(result[state],'unknown');
    assert.deepEqual(result.requests,['GET']); assert.deepEqual(errors,[]);
    console.log('PASS: native multipart multiline Description/BuyIn matching and genuine differences, exact observed/Use current multiline values, immutable full tuple, Use current, observed disjoint values, matching/different/failed reads, retained drafts, no mutation retry, no request attribution.');
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
