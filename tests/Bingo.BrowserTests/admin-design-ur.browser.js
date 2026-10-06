// Every controlled UR profile is exercised in the runner-selected browser.
process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER || 'chromium';
require('../../scripts/check-u2-ur.cjs');
