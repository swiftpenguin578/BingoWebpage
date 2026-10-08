// Add a page here once. The conformance gate uses the real Razor fixture,
// loading template, family stylesheet, and these page-specific interaction probes.
// U11: a page whose table scrolls inside its own wrapper must not widen the page. The empty
// fixture event has no table rows, so `rowProbe` adds (or swaps in for `replace`) a table built with the
// page's real table classes before the horizontal-scroll check (see checkNoSidewaysScroll).
const probeRows = (n, cell) => Array.from({ length: n }, (_, i) => `<div class="tr row" role="row">${cell(i)}</div>`).join('');
module.exports = [
  // U8: Review queue (Review.dc.html). One module serves queue and workspace; its one POST path is the in-place decision.
  { family: 'review', postSaveCount: 1, countSummary: { words: ['pending'], wordsDa: ['afventer'], numberItems: [0] },
    url: f => '/Admin/Review?eventId=' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027', source: 'Pages/Admin/Review/Index.cshtml', module: 'admin-review.js',
    textRows: { 'rv-sk-main':['control',1.45], 'rv-sk-sub':['small',1.45] }, reference: 'Review.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar' },
    style: ['.rv-toolbar','justify-content','flex-start'], titleDa: 'Review',
    update: { control: '[data-review-status][value="Pending"]', action: 'click', request: true, selected: '[data-review-status][value="Pending"]:checked' } },
  // U6: Teams / Draft. Geometry on the setup workspace; the in-place update probe is the running
  // draft's pool sort (update.url), seeded only for this page by BINGO_PARITY_DRAFT (planner ruling, 8 Oct).
  { family: 'draft', postSaveCount: 1, fixtureEnv: { BINGO_PARITY_DRAFT: '1' },
    url: f => '/Admin/Events/Draft/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Draft.cshtml', module: 'admin-draft.js',
    textRows: { 'td-sk-line': ['control',1.45] }, reference: 'TeamsDraft.dc.html', referenceEvent: 'community-mini-bingo', first: '.td-ready', blocks: { first: '.td-ready' },
    style: ['.td-ready-main','display','flex'], titleDa: 'Hold / draft',
    update: { url: f => '/Admin/Events/Draft/' + f.events['clan-cup-pvm-week'], control: '#pool-sort-name', action: 'click', selected: '#pool-sort-name-opt.is-on' } },
  // U7: Board (Board.dc.html). One shared POST path (ctx.command) serves every Board command.
  // U7-E1 (c): plus one background POST, the edit-lease renewal (no busy state).
  { family: 'board', postSaveCount: 1, backgroundPostCount: 1, url: f => '/Admin/Events/Board/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Board.cshtml', module: 'admin-board.js',
    textRows: { 'bd-sk-line': ['control',1.45] }, reference: 'Board.dc.html', first: '.card', blocks: { first: '.card' },
    style: ['.bd-work','display','grid'], titleDa: 'Board',
    // U7-E2 (a), page-specific exemption: at 390 px the loaded header actions (editing
    // chip, Preview, Approve, More) wrap to a second row, so the header grows 42 px
    // beyond the summary change. The reference hides its actions while loading too.
    // Only this width; every other width keeps the generic "only summary growth" check.
    headerGrowth: { 390: 42 },
    update: { control: '#plan-more', action: 'click', selected: '#plan-more[aria-expanded="true"]' } },
  // U5: Participants. The first summary item carries two numbers ("{0} of {1} confirmed").
  { family: 'participants', postSaveCount: 1, countSummary: { words: ['of  confirmed', 'waiting', 'unpaid'], wordsDa: ['af  bekræftet', 'på venteliste', 'ubetalt'], numberCounts: { 0: 2 } },
    url: f => '/Admin/Events/Participants/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Participants.cshtml', module: 'admin-participants.js',
    textRows: { 'pa-sk-line': ['control',1.45] }, reference: 'Participants.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.pa-tbl' },
    style: ['.pa-tbl','--table-min',{ narrow: '570px', wide: '1020px', breakpoint: 860 }], titleDa: 'Deltagere',
    update: { control: '[data-participants-pay][value="paid"]', action: 'click', request: true, selected: '[data-participants-pay][value="paid"]:checked' } },
  { family: 'wom', postSaveCount: 1, summaryReserve: 24, url: f => '/Admin/Events/WiseOldMan/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/WiseOldMan.cshtml', module: 'admin-wom.js', textRows: { 'wm-sk-line': ['control',1.45] },
    reference: 'Wom.dc.html', first: '.card', blocks: { first: '.card' }, style: ['.wm-content','display','grid'], titleDa: 'Wise Old Man',
    rowProbe: { host: '.wm-content', html: '<section class="card wm-cov"><div class="tbl-wrap is-scroll"><div class="tbl wm-tbl sticky-first" role="table"><div class="tr th-row" role="row"><div class="th c-name">Team</div><div class="th">Accounts found</div><div class="th wm-num">Participants</div><div class="th">Not found on Wise Old Man</div></div><div class="rows" role="rowgroup">' + probeRows(4, i => `<div class="td c-name"><span class="cell-main wm-team">Team ${i}</span></div><div class="td"><span class="meter"><span class="meter-track"><span class="meter-fill" style="width:50%"></span></span><span class="meter-label">4 of 8</span></span></div><div class="td wm-num">8</div><div class="td"><span class="wm-missing">Player One, Player Two, Player Three</span></div>`) + '</div></div></div></section>' },
    update: { control: '#tech-btn', action: 'click', selected: '#tech-btn', attribute: ['aria-expanded','true'] } },
  { family: 'final-review', postSaveCount: 1, summaryReserve: 24, url: f => '/Admin/Events/Finalize/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Finalize.cshtml', module: 'admin-final-review.js',
    textRows: { 'fr-sk-line': ['control',1.45] }, reference: 'FinalReview.dc.html', first: '.card', blocks: { first: '.card' },
    style: ['.fr-content','display','grid'], titleDa: 'Afsluttende gennemgang',
    rowProbe: { host: '.fr-standings', replace: '.empty', html: '<div class="tbl-wrap is-scroll"><div class="tbl fr-tbl sticky-first" role="table"><div class="tr th-row" role="row"><div class="th c-name">Place and team</div><div class="th">Full board</div><div class="th fr-num">Lines</div><div class="th fr-num">Tiles</div><div class="th fr-num">Credited EHB</div><div class="th">Score reached</div></div><div class="rows" role="rowgroup">' + probeRows(4, i => `<div class="td c-name"><div class="fr-team"><span class="place">${i + 1}</span><div><span class="cell-main">Team ${i}</span><div class="cell-sub fr-why">Completed the board first</div></div></div></div><div class="td"><span class="tval">1 Jan 12:00</span></div><div class="td fr-num"><span class="tval">12</span></div><div class="td fr-num"><span class="tval">25</span></div><div class="td fr-num"><span class="tval">1,412.6</span></div><div class="td"><span class="tval">At completion</span></div>`) + '</div></div></div>' },
    update: { control: '#how-btn', action: 'click', selected: '#how-btn', attribute: ['aria-expanded','true'] } },
  // U4: Overview (Manage route). The h1 is the event's name, so the Danish title is the name too.
  { family: 'overview', postSaveCount: 1, url: f => '/Admin/Events/Manage/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Manage.cshtml', module: 'admin-overview.js',
    textRows: { 'ov-sk-name': ['control-sm',1.45], 'ov-sk-title': ['control',1.45] }, reference: 'Overview.dc.html', first: '.card', blocks: { first: '.card' },
    style: ['.ov-grid','display','grid'], titleDa: 'Autumn Bingo 2027',
    update: { control: '[aria-describedby="lnkv-signups"]', action: 'click', selected: '[aria-describedby="lnkv-signups"]' } },
  { family: 'catalogue', postSaveCount: 1, countSummary: { words: ['active activities', 'drops', 'Shared by every event’s board estimates'], wordsDa: ['aktive aktiviteter', 'drops', 'Bruges af alle events’ pladeestimater'], numberItems: [0,1] },
    url: () => '/Admin/Catalogue', fixture: 'community-catalogue', source: 'Pages/Admin/Catalogue/Index.cshtml', module: 'admin-catalogue.js',
    textRows: { 'ct-sk-text':['body',1.45], 'ct-sk-line':['control-sm',1.45] }, reference: 'Catalogue.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.ct-tbl' },
    style: ['.ct-tbl','--table-min','860px'], titleDa: 'Katalog',
    update: { control: '[data-catalogue-category][value="Boss"]', action: 'click', selected: '[data-catalogue-category][value="Boss"]:checked' } },
  { family: 'accounts', postSaveCount: 3, countSummary: { words: ['accounts', 'disabled'], wordsDa: ['konti', 'deaktiveret'] },
    url: () => '/Admin/Accounts', fixture: 'community-accounts', source: 'Pages/Admin/Accounts/Index.cshtml', module: 'admin-accounts.js',
    textRows: { 'ac-sk-text':['body',1.45], 'ac-sk-line':['control-sm',1.45] }, reference: 'Accounts.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.ac-tbl' },
    style: ['.ac-tbl','--table-min','900px'], titleDa: 'Konti',
    update: { control: '[data-accounts-role][value="admin"]', action: 'click', request: true, selected: '[data-accounts-role][value="admin"]:checked' } },
  { family: 'audit', postSaveCount: 0, fixedSummary: { words: [/^Times in Copenhagen time \(UTC[+-]\d{2}:\d{2}\)$/], wordsDa: [/^Tider i københavnsk tid \(UTC[+-]\d{2}:\d{2}\)$/] },
    url: () => '/Admin/Audit', fixture: 'community-audit', source: 'Pages/Admin/Audit/Index.cshtml', module: 'admin-audit.js',
    textRows: { 'au-sk-main':['control',1.45], 'au-sk-sub':['small',1.45] }, reference: 'Audit.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.au-tbl' },
    style: ['.au-tbl','--table-min','880px'], titleDa: 'Audit',
    update: { control: '#actor-input', action: 'input', value: 'ReviewOwner' } },
  { family: 'signupsetup', postSaveCount: 1, url: f => '/Admin/Events/SignupSetup/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/SignupSetup.cshtml', module: 'admin-signup-setup.js',
    textRows: { 'ss-sk-line': ['control',1.45] }, reference: 'SignupSetup.dc.html', first: '.card.form-card', blocks: { first: '.card.form-card', tabs: '.ss-tabs' },
    style: ['.ss-cap','display','grid'], titleDa: 'Tilmeldingsopsætning',
    update: { control: '#cap-input', action: 'input', value: '125', selected: '[data-card-dirty="cap"]:not([hidden])' } },
  { family: 'schedule', postSaveCount: 1, url: f => '/Admin/Events/Schedule/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Schedule.cshtml', module: 'admin-schedule.js',
    textRows: { 'schedule-sk-title': ['control',1.45] }, reference: 'Schedule.dc.html', first: '.card.form-card', blocks: { first: '.card.form-card' },
    style: ['.sc-pair','display','grid'], additionalStyles: [['.form-banners','display','none']], titleDa: 'Tidsplan',
    update: { control: '#schedule-draftAt-time', action: 'input', value: '15:35', selected: '[data-identity-dirty]:not([hidden])' } },
  { family: 'identity', postSaveCount: 1, url: f => '/Admin/Events/Identity/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Identity.cshtml', module: 'event-identity.js',
    textRows: { 'identity-sk-title': ['control',1.45] }, reference: 'Identity.dc.html', first: '.card.form-card', blocks: { first: '.card.form-card' },
    readOnlyStyle: ['.identity-editor-form .ro-value', 'white-space', 'pre-wrap'],
    style: ['.id-desc', 'minHeight', '132px'], additionalStyles: [['.form-banners','display','none']], titleDa: 'Identitet',
    update: { control: '#Input_Name', action: 'input', value: 'Conformance draft', selected: '[data-identity-dirty]:not([hidden])' } },
  { family: 'dashboard', postSaveCount: 0, url: () => '/Admin', fixture: 'community-history',
    source: 'Pages/Admin/Index.cshtml', module: 'admin-dashboard.js',
    textRows: { 'dash-sk-stat-label':['small',1.45], 'dash-sk-stat-value':['stat',1.1], 'dash-sk-stat-note':['meta',1.45,17], 'dash-sk-panel-title':['control',1.45], 'dash-sk-fact-first':['control',1.45], 'dash-sk-fact':['control',1.45] }, reference: 'Dashboard.dc.html', first: '.card', blocks: { first: '.card', stats: '.stat-strip', section: '.dash-grid>.card:first-child' },
    style: ['.dash-grid', 'display', 'grid'], titleDa: 'Dashboard',
    update: { control: '[data-dashboard-sort="Winner"]', action: 'click', selected: '[data-dashboard-sort="Winner"]', attribute: ['aria-sort', 'ascending'], attributeParent: '.th' } },
  { family: 'events', postSaveCount: 0, countSummary: { words: ['live', 'upcoming or in setup'], wordsDa: ['live', 'kommende eller under opsætning'] }, url: () => '/Admin/Events/Index', fixture: 'community-events',
    source: 'Pages/Admin/Events/Index.cshtml', module: 'admin-events.js',
    textRows: { 'events-sk-name':['body',1.45], 'events-sk-main':['control-sm',1.45], 'events-sk-start':['control-sm',1.45], 'events-sk-end':['small',1.45] }, reference: 'Events.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.ev-tbl' },
    style: ['.tbl.ev-tbl', '--table-min', { narrow: '900px', wide: '990px', breakpoint: 640 }], titleDa: 'Events',
    update: { control: '#directory-sort-identity', action: 'click', request: true, selected: '#directory-sort-identity', attribute: ['aria-sort', 'ascending'], attributeParent: '.th' } }
];
