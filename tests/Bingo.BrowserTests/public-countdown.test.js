const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
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

test("Board countdown status is rendered from lifecycle state with localized pre-Live styling", () => {
  const root = path.resolve(__dirname, "../../src/Bingo.Web");
  const board = fs.readFileSync(path.join(root, "Pages/Shared/_EventMasthead.cshtml"), "utf8");
  const css = fs.readFileSync(path.join(root, "wwwroot/css/site.public-ui.css"), "utf8");
  const danish = fs.readFileSync(path.join(root, "Resources/SharedResource.da.resx"), "utf8");

  assert.match(board, /var countdownIsPreLive = Model\.Board\.EventState is EventState\.Draft or EventState\.SignupOpen or EventState\.SignupClosed;/);
  assert.match(board, /var countdownStatus = countdownIsPreLive \? T\["Starts in"\] : eventStatus;/);
  assert.match(board, /role="group" aria-label="@countdownStatus"/);
  assert.ok(board.includes('class="public-board-masthead__live-status@(countdownIsPreLive ? " public-board-masthead__live-status--pre-live" : string.Empty)" aria-hidden="true"><span class="public-board-masthead__live-dot"></span>@countdownStatus</span>'));
  assert.match(css, /\.public-board-masthead__live-status--pre-live \{ color: var\(--board-blue\); \}/);
  assert.match(css, /\.public-board-masthead__live-status--pre-live \.public-board-masthead__live-dot \{ background: var\(--board-blue\); \}/);
  assert.match(danish, /name="Starts in"[\s\S]{0,100}<value>Starter om<\/value>/);
});
