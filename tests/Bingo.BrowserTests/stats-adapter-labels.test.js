const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

// Hotfix 182: estimated / zero-recorded results render only the percentage or KC, with no suffix.
const source = fs.readFileSync(path.resolve(__dirname, "../../src/Bingo.Web/wwwroot/js/stats-adapter.js"), "utf8");
const window = {};
vm.runInNewContext(source, { window });
const { luckText, kcText } = window.StatsAdapter;

for (const flags of [{}, { estimated: true }, { zeroRecordedApproximation: true }, { estimated: true, zeroRecordedApproximation: true }]) {
  assert.equal(luckText({ percentage: 55.04, ...flags }), "55%");
  assert.equal(kcText({ kcDifference: 12.34, ...flags }), "+12.3 KC");
  assert.equal(kcText({ kcDifference: -7, ...flags }), "-7 KC");
}
assert.equal(luckText({ percentage: null, status: "WaitingForActivityData" }), "Waiting for activity");
assert.equal(kcText({ kcDifference: null, status: "Incomplete" }), "Incomplete data");

const page = fs.readFileSync(path.resolve(__dirname, "../../src/Bingo.Web/wwwroot/js/stats-page.js"), "utf8");
assert.doesNotMatch(page, /zero-recorded|luckEstimated|luckZeroRecorded/);
console.log("stats-adapter-labels: ok");
