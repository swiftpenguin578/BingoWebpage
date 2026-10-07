// Add a page here once. The conformance gate uses the real Razor fixture,
// loading template, family stylesheet, and these page-specific interaction probes.
module.exports = [
  { family: 'identity', url: f => '/Admin/Events/Identity/' + f.events['autumn-bingo-2027'], fixture: 'autumn-bingo-2027',
    source: 'Pages/Admin/Events/Identity.cshtml', module: 'event-identity.js',
    textRows: { 'identity-sk-title': ['control',1.45] }, reference: 'Identity.dc.html', first: '.card.form-card', blocks: { first: '.card.form-card' },
    style: ['.id-desc', 'minHeight', '132px'], additionalStyles: [['.form-banners','display','none']], titleDa: 'Identitet',
    update: { control: '#Input_Name', action: 'input', value: 'Conformance draft', selected: '[data-identity-dirty]:not([hidden])' } },
  { family: 'dashboard', url: () => '/Admin', fixture: 'community-history',
    source: 'Pages/Admin/Index.cshtml', module: 'admin-dashboard.js',
    textRows: { 'dash-sk-stat-label':['small',1.45], 'dash-sk-stat-value':['stat',1.1], 'dash-sk-stat-note':['meta',1.45,17], 'dash-sk-panel-title':['control',1.45], 'dash-sk-fact-first':['control',1.45], 'dash-sk-fact':['control',1.45] }, reference: 'Dashboard.dc.html', first: '.card', blocks: { first: '.card', stats: '.stat-strip', section: '.dash-grid>.card:first-child' },
    style: ['.dash-grid', 'display', 'grid'], titleDa: 'Dashboard',
    update: { control: '[data-dashboard-sort="Winner"]', action: 'click', selected: '[data-dashboard-sort="Winner"]', attribute: ['aria-sort', 'ascending'], attributeParent: '.th' } },
  { family: 'events', url: () => '/Admin/Events/Index', fixture: 'community-events',
    source: 'Pages/Admin/Events/Index.cshtml', module: 'admin-events.js',
    textRows: { 'events-sk-name':['body',1.45], 'events-sk-main':['control-sm',1.45], 'events-sk-start':['control-sm',1.45], 'events-sk-end':['small',1.45] }, reference: 'Events.dc.html', first: '.card', blocks: { first: '.card', toolbar: '.toolbar', table: '.ev-tbl' },
    style: ['.tbl.ev-tbl', '--table-min', { narrow: '900px', wide: '990px', breakpoint: 640 }], titleDa: 'Events',
    update: { control: '#directory-sort-identity', action: 'click', request: true, selected: '#directory-sort-identity', attribute: ['aria-sort', 'ascending'], attributeParent: '.th' } }
];
