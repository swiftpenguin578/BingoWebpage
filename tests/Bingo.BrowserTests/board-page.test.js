// U7 (brief 88, A10): replaces board-dialog.test.js and board-objective-kind.test.js,
// which exercised the retired inline Board script. Pure Board helpers (RC05 B5/B6) and
// the label contract: every t(...) literal in the Board modules is served by the page.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "../..");
const js = name => path.join(root, "src/Bingo.Web/wwwroot/js", name);

(async () => {
  const model = await import("data:text/javascript," + encodeURIComponent(fs.readFileSync(js("admin-board-model.js"), "utf8")));
  // B6: one complete decimal parser; trailing junk and comma decimals are rejected.
  assert.equal(model.parseDecimal("1.5"), 1.5);
  assert.equal(model.parseDecimal(" 12 "), 12);
  for (const bad of ["1,5", "1abc", "", ".5", "1.", "-1", "1e3"]) assert.equal(model.parseDecimal(bad), null, bad);
  assert.equal(model.parseWhole("10000", 1, 10000), 10000);
  assert.equal(model.parseWhole("10001", 1, 10000), null);
  assert.equal(model.parseWhole("0", 1, 10000), null);
  assert.equal(model.parseWhole("2.0", 1, 10), null);
  // B5: ?tile=B3 is row letter plus 1-based column on the current board size.
  assert.equal(model.parseTileRef("B3", 5, 5), 7);
  assert.equal(model.parseTileRef("b3", 5, 5), 7);
  assert.equal(model.parseTileRef("A1", 1, 1), 0);
  assert.equal(model.parseTileRef("F1", 5, 5), null);
  assert.equal(model.parseTileRef("A6", 5, 5), null);
  assert.equal(model.parseTileRef("Z9", 8, 8), null);
  assert.equal(model.posName(7, 5), "B3");
  // U7-E1 (c): activity renews the lease at most once a minute, one request at a time.
  let clock = 0, sends = 0, settle;
  const renew = model.leaseRenewer(() => { sends++; return new Promise(resolve => { settle = resolve; }); }, { interval: 60000, now: () => clock });
  assert.equal(renew(), true);
  await Promise.resolve(); assert.equal(sends, 1);
  clock = 70000; assert.equal(renew(), false, "no second request while one is in flight");
  settle(); await new Promise(resolve => setTimeout(resolve, 0));
  clock = 71000; assert.equal(renew(), true, "a minute after the last renewal, activity renews again");
  await Promise.resolve(); assert.equal(sends, 2); settle(); await new Promise(resolve => setTimeout(resolve, 0));
  clock = 130000; assert.equal(renew(), false, "within the minute, activity does not renew");
  clock = 131000; assert.equal(renew(), true);
  await Promise.resolve(); assert.equal(sends, 3);
  const failing = model.leaseRenewer(() => Promise.reject(new Error("offline")), { interval: 60000, now: () => clock });
  assert.equal(failing(), true); await new Promise(resolve => setTimeout(resolve, 0));
  clock = 191000; assert.equal(failing(), true, "a failed renewal does not block the next window");
  const board = fs.readFileSync(js("admin-board.js"), "utf8");
  assert.match(board, /ctx\.url\('RenewEditing'\)/);
  assert.match(board, /if \(ctx\.canEdit\(\) && !life\.signal\.aborted\) renew\(\)/);

  const labels = fs.readFileSync(path.join(root, "src/Bingo.Web/Pages/Admin/Events/Board.Labels.cs"), "utf8");
  const served = new Set([...labels.matchAll(/^\s+"((?:[^"\\]|\\.)*)",$/gm)].map(m => m[1].replace(/\\"/g, '"').replace(/\\\\/g, "\\")));
  const modules = fs.readdirSync(path.dirname(js("admin-board.js"))).filter(name => /^admin-board.*\.js$/.test(name));
  for (const name of modules) {
    const source = fs.readFileSync(js(name), "utf8");
    for (const match of source.matchAll(/(?<![\w.$])t\('((?:[^'\\]|\\.)*)'/g)) assert.ok(served.has(match[1]), `${name}: label not served: ${match[1]}`);
  }
  console.log("board-page tests passed");
})().catch(error => { console.error(error); process.exitCode = 1; });
