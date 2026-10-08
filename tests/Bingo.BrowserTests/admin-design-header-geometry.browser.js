// Gate Q-H1 normal/narrow loaded/loading reference geometry in both engines.
process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? 'webkit' : 'chromium';
require('../../scripts/measure-u2-header.cjs');
