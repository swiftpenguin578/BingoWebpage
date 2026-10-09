// Every repository JS test is a standalone Node program. Keep each in its own
// process so fake DOMs/globals and browser fixtures cannot leak between files.
//
// CI shards: BROWSER_TEST_SHARD=k/N runs only shard k of N (1-based). Shards are
// balanced by scripts/browser-test-timings.json (seconds per "file [browser]";
// unknown runs get a default). `--shard-info k/N` prints what that shard needs
// (browser engines, .NET fixtures, the parity check) as GitHub step outputs.
// Without BROWSER_TEST_SHARD every run executes, as before.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const directory = path.join(root, 'tests/Bingo.BrowserTests');
const output = path.join(root, 'artifacts/js-tests');
const files = fs.readdirSync(directory).filter(file => file.endsWith('.js')).sort();
if (!files.length) throw new Error('No JavaScript test files found.');
const runs = files.flatMap(file => /^(admin-design-|identity-)/.test(file)
  ? [{ file, browser: 'chromium' }, { file, browser: 'webkit' }]
  : [{ file, browser: 'default' }]);
const staleFixtureScript = 'admin-stale-change.browser.js';
const parityUnit = 'test:parity';
const staleFixtureUnit = 'fixtures:stale-evidence';

function parseShard(value) {
  const match = /^([1-9][0-9]*)\/([1-9][0-9]*)$/.exec(value || '');
  if (!match || Number(match[1]) > Number(match[2])) throw new Error(`Invalid shard "${value}"; expected k/N with 1 <= k <= N.`);
  return { index: Number(match[1]), count: Number(match[2]) };
}

const runKey = run => `${run.file} [${run.browser}]`;
// Engine runs and default-engine *.browser.js scripts launch Playwright; the
// other scripts are plain Node programs with fake DOMs.
const usesBrowser = run => run.browser !== 'default' || run.file.endsWith('.browser.js');

// Longest unit first, each to the shard with the fewest seconds; the parity
// check is one more unit.
function partition(count) {
  const timings = JSON.parse(fs.readFileSync(path.join(__dirname, 'browser-test-timings.json'), 'utf8'));
  const weight = (key, fallback) => Number.isFinite(timings.seconds[key]) ? timings.seconds[key] : fallback;
  const units = runs.map(run => ({
    run,
    key: runKey(run),
    seconds: weight(runKey(run), usesBrowser(run) ? timings.defaultBrowserSeconds : timings.defaultNodeSeconds)
      + (run.file === staleFixtureScript ? weight(staleFixtureUnit, 0) : 0)
  }));
  units.push({ run: null, key: parityUnit, seconds: weight(parityUnit, timings.defaultBrowserSeconds) });
  units.sort((a, b) => b.seconds - a.seconds || (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));
  const shards = Array.from({ length: count }, () => ({ seconds: 0, units: [] }));
  for (const unit of units) {
    const target = shards.reduce((best, shard) => shard.seconds < best.seconds ? shard : best, shards[0]);
    target.seconds += unit.seconds;
    target.units.push(unit);
  }
  if (shards.some(shard => !shard.units.length)) throw new Error(`Cannot fill ${count} shards from ${units.length} units.`);
  return shards;
}

function shardInfo(shard) {
  const selected = shard.units.filter(unit => unit.run).map(unit => unit.run);
  const parity = shard.units.some(unit => unit.key === parityUnit);
  const engines = new Set();
  for (const run of selected.filter(usesBrowser)) {
    if (run.browser === 'webkit') engines.add('webkit');
    else engines.add('chromium').add('chrome'); // One unchanged legacy test explicitly selects Chrome.
  }
  if (parity) engines.add('chromium').add('chrome').add('webkit');
  return {
    runs: selected.length,
    seconds: Math.round(shard.seconds),
    engines: [...engines].sort().join(' '),
    dotnet: engines.size > 0,
    staleFixtures: selected.some(run => run.file === staleFixtureScript),
    parity
  };
}

if (process.argv[2] === '--shard-info') {
  const { index, count } = parseShard(process.argv[3]);
  const shards = partition(count);
  for (const [key, value] of Object.entries(shardInfo(shards[index - 1]))) console.log(`${key}=${value}`);
  shards.forEach((shard, i) => console.error(`Shard ${i + 1}/${count}: ~${Math.round(shard.seconds)}s: ${shard.units.map(unit => unit.key).join(', ')}`));
  return;
}

const shardSpec = process.env.BROWSER_TEST_SHARD;
let selectedRuns = runs;
if (shardSpec) {
  const { index, count } = parseShard(shardSpec);
  selectedRuns = partition(count)[index - 1].units.filter(unit => unit.run).map(unit => unit.run);
}

const fixtures = process.env.BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY;
if (selectedRuns.some(run => run.file === staleFixtureScript)) {
  for (const action of ['Disable', 'Restore', 'GrantAdmin', 'RevokeAdmin']) {
    for (const phase of ['opened', 'stale']) {
      if (!fixtures || !fs.existsSync(path.join(fixtures, `account-${action}-${phase}.html`))) {
        throw new Error('Generate AdminStaleChangeIntegrationTests.AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction fixtures with BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY before this runner. See README.');
      }
    }
  }
}
const perFileTimeout = Number(process.env.BROWSER_TEST_TIMEOUT_MS) > 0 ? Number(process.env.BROWSER_TEST_TIMEOUT_MS) : 120000;
fs.mkdirSync(output, { recursive: true });
const results = [];
for (const { file, browser } of selectedRuns) {
  console.log(`Running ${file} [${browser}]`);
  const started = Date.now();
  const result = spawnSync(process.execPath, [path.join(directory, file)], {
    cwd: root,
    env: { ...process.env, PLAYWRIGHT_BROWSER: browser, PLAYWRIGHT_CHANNEL: process.env.PLAYWRIGHT_CHANNEL || 'chromium' },
    encoding: 'utf8',
    timeout: perFileTimeout,
    maxBuffer: 8 * 1024 * 1024
  });
  const passed = !result.error && result.status === 0;
  const log = `${result.stdout || ''}${result.stderr || ''}${result.error ? `${result.error.stack}\n` : ''}`;
  fs.writeFileSync(path.join(output, `${file}.${browser}.log`), log);
  results.push({ file, browser, passed, exitCode: result.status, signal: result.signal, milliseconds: Date.now() - started });
  console.log(`${passed ? 'PASS' : 'FAIL'} ${file} [${browser}]`);
  if (!passed) process.stdout.write(log);
}
const summary = { shard: shardSpec || null, total: results.length, passed: results.filter(result => result.passed).length, failed: results.filter(result => !result.passed).length, results };
fs.writeFileSync(path.join(output, 'results.json'), `${JSON.stringify(summary, null, 2)}\n`);
console.log(`JavaScript executions: ${summary.passed} passed, ${summary.failed} failed, ${summary.total} total.`);
process.exitCode = summary.failed ? 1 : 0;
