const assert = require('node:assert/strict');
const path = require('node:path');
const { startFixture, login } = require('./admin-parity-fixture.cjs');
// Ruling58-2: real authenticated server behavior replaces only WebKit's
// unavailable intercepted Cookie-header observation. Both directions are proved.
async function proveAuthenticatedFetch(browser) {
  const fixture = await startFixture(process.cwd(), path.resolve('artifacts/js-tests/served-auth-webkit'));
  const context = await browser.newContext();
  try {
    const page = await login(context, fixture), eventId = fixture.events['autumn-bingo-2027'];
    await page.goto(`${fixture.origin}/Admin/Events/Identity/${eventId}`);
    await page.waitForFunction(() => window.AdminFetch);
    const read = () => page.evaluate(async () => {
      // A caller cannot accidentally suppress the shared helper's credentials.
      const r = await AdminFetch.request(location.pathname + '?handler=Current', { credentials:'omit', notice:false, readback:true });
      return { kind:r.kind, status:r.response?.status, eventId:r.data?.eventId, name:r.data?.values?.name, destination:r.destination };
    });
    const signedIn = await read();
    assert.deepEqual({kind:signedIn.kind,status:signedIn.status,eventId:signedIn.eventId,name:signedIn.name}, {kind:'handler',status:200,eventId,name:'Autumn Bingo 2027'});
    await context.clearCookies();
    const signedOut = await read();
    assert.equal(signedOut.kind,'session-lost');
    assert.equal(new URL(signedOut.destination).pathname,'/Account/Login');
    console.log('PASS WebKit real Kestrel AdminFetch credential transport: authenticated 200/readback; removed cookie redirects to Login');
  } finally { await context.close(); await fixture.close(); }
}
module.exports = { proveAuthenticatedFetch };
