// U7 / Board: pure helpers shared by the Board workspace and its tile editor.
// No DOM access; Node tests import this module directly.

export const ROWS = 'ABCDEFGH';
export const posName = (pos, cols) => ROWS[Math.floor(pos / cols)] + (pos % cols + 1);
// RC05 B5: ?tile=B3 is a row letter plus a 1-based column on the current board.
export function parseTileRef(ref, rows, cols) {
  const match = /^([A-H])([1-8])$/.exec(String(ref || '').trim().toUpperCase());
  if (!match) return null;
  const row = ROWS.indexOf(match[1]), col = +match[2] - 1;
  return row < rows && col < cols ? row * cols + col : null;
}
// RC05 B6: one complete decimal parser for validation, payload and comparison.
// Invariant digits with an optional point; anything else (1,5 or 1abc) is rejected.
export function parseDecimal(value) {
  const text = String(value ?? '').trim();
  return /^\d+(\.\d+)?$/.test(text) ? Number(text) : null;
}
export function parseWhole(value, min, max) {
  const text = String(value ?? '').trim();
  if (!/^\d+$/.test(text)) return null;
  const n = Number(text);
  return n >= min && n <= max ? n : null;
}
// U7-E1 (c): editing activity renews the edit lease at most once per interval
// (the lease lasts five minutes). One request at a time; a failed send does not
// block the next window. `send` returns a promise; `now` is injectable for tests.
export function leaseRenewer(send, { interval = 60000, now = () => Date.now() } = {}) {
  let last = null, inFlight = false;
  return function activity() {
    if (inFlight || (last !== null && now() - last < interval)) return false;
    last = now(); inFlight = true;
    Promise.resolve().then(send).catch(() => {}).finally(() => { inFlight = false; });
    return true;
  };
}
