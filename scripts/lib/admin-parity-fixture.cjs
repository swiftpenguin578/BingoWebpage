const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
async function startFixture(root, output, environment = {}) {
  fs.mkdirSync(output, { recursive: true });
  const app = spawn('dotnet', ['run', '--project', 'tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj', '--configuration', process.env.BINGO_PARITY_CONFIGURATION || 'Release', '--no-build'], { cwd: root, env: { ...process.env, ...environment, BINGO_PARITY_ROOT: root }, stdio: ['pipe', 'pipe', 'pipe'] });
  const log = fs.createWriteStream(path.join(output, 'fixture.log'));
  const manifest = await new Promise((resolve, reject) => {
    let buffer = ''; const timer = setTimeout(() => reject(new Error('Controlled Kestrel fixture did not start within 120 seconds.')), 120000);
    app.stdout.on('data', chunk => { log.write(chunk); buffer += chunk; const match = /PARITY_READY (.+)\n/.exec(buffer); if (match) { clearTimeout(timer); resolve(JSON.parse(match[1])); } });
    app.stderr.on('data', chunk => log.write(chunk));
    app.on('exit', code => { clearTimeout(timer); reject(new Error(`Controlled fixture exited before ready (${code}); see fixture.log.`)); });
  }).catch(error => { app.stdin.end(); throw error; });
  const onExit = () => { app.stdin.end(); };
  process.once('exit', onExit);
  return { ...manifest, async close() {
    process.removeListener('exit', onExit); app.stdin.end('\n');
    await new Promise(resolve => app.exitCode !== null ? resolve() : app.once('exit', resolve)); log.end();
  } };
}
async function login(context, fixture) {
  const page = await context.newPage(); await page.goto(fixture.origin + '/Account/Login');
  await page.locator('#Input_Username').fill(fixture.username); await page.locator('#Input_Password').fill(fixture.password);
  await Promise.all([page.waitForURL(url => !url.pathname.endsWith('/Account/Login')), page.locator('button[type=submit]').click()]);
  return page;
}
module.exports = { startFixture, login };
