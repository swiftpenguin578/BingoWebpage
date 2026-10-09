// Refreshes scripts/browser-test-timings.json from the results.json files that
// run-browser-tests.cjs writes (for example the CI JavaScript shard artifacts):
//   node scripts/update-browser-test-timings.cjs <results.json>...
// Only measured runs change; other entries and the defaults are kept.
const fs = require('node:fs');
const path = require('node:path');

const target = path.join(__dirname, 'browser-test-timings.json');
const sources = process.argv.slice(2);
if (!sources.length) throw new Error('Pass one or more results.json files.');
const timings = JSON.parse(fs.readFileSync(target, 'utf8'));
let updated = 0;
for (const source of sources) {
  for (const result of JSON.parse(fs.readFileSync(source, 'utf8')).results) {
    timings.seconds[`${result.file} [${result.browser}]`] = Math.max(1, Math.round(result.milliseconds / 1000));
    updated++;
  }
}
timings.seconds = Object.fromEntries(Object.entries(timings.seconds).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
timings.source = `Updated from ${sources.map(source => path.basename(path.dirname(source)) + '/' + path.basename(source)).join(', ')}.`;
fs.writeFileSync(target, `${JSON.stringify(timings, null, 2)}\n`);
console.log(`Updated ${updated} run timings in ${path.relative(process.cwd(), target)}.`);
