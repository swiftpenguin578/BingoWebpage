const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
async function startFixture(root, output, environment = {}) {
  fs.mkdirSync(output, { recursive: true });
  const app = spawn('dotnet', ['run', '--project', 'tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj', '--configuration', 'Release', '--no-build'], { cwd: root, env: { ...process.env, ...environment, BINGO_PARITY_ROOT: root }, stdio: ['pipe', 'pipe', 'pipe'] });
  const log = fs.createWriteStream(path.join(output, 'fixture.log'));
  const manifest = await new Promise((resolve, reject) => {
    let buffer = ''; const timer = setTimeout(() => reject(new Error('Controlled Kestrel fixture did not start within 120 seconds.')), 120000);
    app.stdout.on('data', chunk => { log.write(chunk); buffer += chunk; const match = /PARITY_READY (.+)\n/.exec(buffer); if (match) { clearTimeout(timer); resolve(JSON.parse(match[1])); } });
    app.stderr.on('data', chunk => log.write(chunk));
    app.on('exit', code => { clearTimeout(timer); reject(new Error(`Controlled fixture exited before ready (${code}); see fixture.log.`)); });
  }).catch(error => { app.stdin.end(); throw error; });
  const reference = spawn('python3', ['-u', '-m', 'http.server', '0', '--bind', '127.0.0.1'], { cwd: path.join(root, 'docs/references/admin-ui'), stdio: ['ignore', 'pipe', 'pipe'] });
  const refLog = fs.createWriteStream(path.join(output, 'reference-server.log'));
  const port = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Reference server did not start within 10 seconds.')), 10000);
    reference.stdout.on('data', chunk => { refLog.write(chunk); const match = /port (\d+)/.exec(String(chunk)); if (match) { clearTimeout(timer); resolve(Number(match[1])); } });
    reference.stderr.on('data', chunk => refLog.write(chunk));
    reference.on('exit', code => { clearTimeout(timer); reject(new Error(`Reference server exited before ready (${code}).`)); });
  });
  const onExit = () => { reference.kill('SIGTERM'); app.stdin.end(); };
  process.once('exit', onExit);
  return { ...manifest, reference: `http://127.0.0.1:${port}/Identity.dc.html`, async close() {
    process.removeListener('exit', onExit); reference.kill('SIGTERM'); app.stdin.end('\n');
    await new Promise(resolve => app.exitCode !== null ? resolve() : app.once('exit', resolve)); log.end(); refLog.end();
  } };
}
async function referencePage(context, fixture, root, filename = 'Identity.dc.html', transform = source => source) {
  const page = await context.newPage();
  const url = new URL(filename, fixture.reference).href;
  // Expose the original reference component for deterministic state selection;
  // this adds no rendering or behavior and never changes the frozen source file.
  await page.route(url, route => route.fulfill({ contentType: 'text/html', body: transform(fs.readFileSync(path.join(root, 'docs/references/admin-ui', filename), 'utf8')).replace('componentDidMount() {', 'componentDidMount() { window.__parityReference = this;') }));
  // Render both surfaces with the exact self-hosted font bytes, without external calls.
  await page.route('https://fonts.googleapis.com/**', route => route.fulfill({ contentType: 'text/css', body: "@font-face{font-family:Geist;src:url('https://fonts.gstatic.com/parity-geist.woff2');font-weight:100 900}@font-face{font-family:'Geist Mono';src:url('https://fonts.gstatic.com/parity-geist-mono.woff2');font-weight:100 900}" }));
  await page.route('https://fonts.gstatic.com/**', route => route.fulfill({ contentType: 'font/woff2', headers: { 'Access-Control-Allow-Origin': '*' }, body: fs.readFileSync(path.join(path.resolve(__dirname, '../..'), 'src/Bingo.Web/wwwroot/fonts/prototypes', route.request().url().includes('geist-mono') ? 'geist-mono/GeistMono-Variable.woff2' : 'geist/Geist-Variable.woff2')) }));
  await page.goto(url); await page.waitForFunction(() => window.__parityReference);
  await page.evaluate(() => { const c = window.__parityReference; if (c.world) window.__parityWorld = structuredClone(c.world); return new Promise(resolve => c.setState({ showBar: false }, resolve)); });
  await page.evaluate(() => document.fonts.ready); return page;
}
async function login(context, fixture) {
  const page = await context.newPage(); await page.goto(fixture.origin + '/Account/Login');
  await page.locator('#Input_Username').fill(fixture.username); await page.locator('#Input_Password').fill(fixture.password);
  await Promise.all([page.waitForURL(url => !url.pathname.endsWith('/Account/Login')), page.locator('button[type=submit]').click()]);
  return page;
}
module.exports = { startFixture, referencePage, login };
