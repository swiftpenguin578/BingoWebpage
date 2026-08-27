const assert = require("node:assert/strict");
const { calculateCountdown } = require("../../src/Bingo.Web/wwwroot/js/public-countdown.js");

const start = "2026-08-16T12:00:00Z";
const end = "2026-08-16T14:00:00Z";
const labels = { live: "Ends in", upcoming: "Starts in", ended: "Event ended", days: "days", hours: "hours", minutes: "minutes", seconds: "seconds" };

assert.equal(calculateCountdown(start, end, Date.parse("2026-08-16T11:59:00Z"), labels).state, "upcoming");
const live = calculateCountdown(start, end, Date.parse("2026-08-16T13:00:00Z"), labels);
assert.equal(live.state, "live");
assert.equal(live.progress, 50);
assert.deepEqual(calculateCountdown(start, end, Date.parse("2026-08-16T14:00:00Z"), labels), {
  state: "ended",
  label: "Event ended",
  days: "00",
  hours: "00",
  minutes: "00",
  seconds: "00",
  progress: 100,
  ariaLabel: "Event ended"
});
