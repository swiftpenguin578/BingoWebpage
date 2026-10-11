// Shared page conformance (scripts/check-admin-page-conformance.cjs), entry 3 of 5. The families are split
// over five entries so each run fits the runner's per-file limit and shards on its own; every registered
// family belongs to exactly one entry (checked by admin-conformance-blocks.source.js).
process.env.BINGO_CONFORMANCE_PAGES = 'catalogue,accounts,audit,signupsetup,events';
process.env.BINGO_CONFORMANCE_GROUP = '3';
require('../../scripts/check-admin-page-conformance.cjs');
