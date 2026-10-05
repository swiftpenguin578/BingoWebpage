// Every repository JS test is a standalone Node program. Keep each in its own
// process so fake DOMs/globals and browser fixtures cannot leak between files.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const directory = path.join(root, 'tests/Bingo.BrowserTests');
const output = path.join(root, 'artifacts/js-tests');
const files = fs.readdirSync(directory).filter(file => file.endsWith('.js')).sort();
if (!files.length) throw new Error('No JavaScript test files found.');
const fixtures = process.env.BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY;
for (const action of ['Disable', 'Restore', 'GrantAdmin', 'RevokeAdmin']) {
  for (const phase of ['opened', 'stale']) {
    if (!fixtures || !fs.existsSync(path.join(fixtures, `account-${action}-${phase}.html`))) {
      throw new Error('Generate AdminStaleChangeIntegrationTests.AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction fixtures with BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY before this runner. See README.');
    }
  }
}
fs.mkdirSync(output, { recursive: true });
const results = [];
for (const file of files) {
  console.log(`Running ${file}`);
  const started = Date.now();
  const result = spawnSync(process.execPath, [path.join(directory, file)], {
    cwd: root,
    env: { ...process.env, PLAYWRIGHT_CHANNEL: process.env.PLAYWRIGHT_CHANNEL || 'chromium' },
    encoding: 'utf8',
    timeout: 120000,
    maxBuffer: 8 * 1024 * 1024
  });
  const passed = !result.error && result.status === 0;
  const log = `${result.stdout || ''}${result.stderr || ''}${result.error ? `${result.error.stack}\n` : ''}`;
  fs.writeFileSync(path.join(output, `${file}.log`), log);
  results.push({ file, passed, exitCode: result.status, signal: result.signal, milliseconds: Date.now() - started });
  console.log(`${passed ? 'PASS' : 'FAIL'} ${file}`);
  if (!passed) process.stdout.write(log);
}
const summary = { total: results.length, passed: results.filter(result => result.passed).length, failed: results.filter(result => !result.passed).length, results };
fs.writeFileSync(path.join(output, 'results.json'), `${JSON.stringify(summary, null, 2)}\n`);
console.log(`JavaScript files: ${summary.passed} passed, ${summary.failed} failed, ${summary.total} total.`);
process.exitCode = summary.failed ? 1 : 0;
