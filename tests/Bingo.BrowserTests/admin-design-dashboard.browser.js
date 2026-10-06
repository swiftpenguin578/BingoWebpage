// The repository JS runner executes this proof in both Chromium and WebKit.
process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? 'webkit' : 'chromium';
require('../../scripts/check-u2-dashboard.cjs');
