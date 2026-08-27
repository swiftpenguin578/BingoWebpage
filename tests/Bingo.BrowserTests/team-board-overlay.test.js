const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "../..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const board = read("src/Bingo.Web/Pages/Events/Board.cshtml");
const teamBoard = read("src/Bingo.Web/Pages/Events/TeamBoard.cshtml");
const tile = read("src/Bingo.Web/Pages/Events/Tile.cshtml");
const tileSidebar = read("src/Bingo.Web/Pages/Events/_TileSidebar.cshtml");
const program = read("src/Bingo.Web/Program.cs");
const layout = read("src/Bingo.Web/Pages/Shared/_Layout.cshtml");
const drawer = read("src/Bingo.Web/wwwroot/js/team-board-drawer.js");
const publicUiCss = read("src/Bingo.Web/wwwroot/css/site.public-ui.css");
const foundationCss = read("src/Bingo.Web/wwwroot/css/site.transitional.foundation.css");

assert.match(board, /<a class="mission-team-card[\s\S]*asp-page="\/Events\/TeamBoard"/, "team cards retain ordinary TeamBoard route-backed links");
assert.doesNotMatch(board, /data-team-board-overlay|team-board-overlay\.js|team-board-overlay-redirect\.js|needsTeamWorkspaceOverlay/, "Board has no popup host, interception script, or popup-only state");
assert.doesNotMatch(teamBoard, /overlayMode|OverlayMode|team-board-overlay|team-board-overlay-redirect/, "TeamBoard renders as an ordinary page without popup redirect behavior");
assert.match(teamBoard, /@page "\/Events\/\{slug\}\/Board\/\{teamSlug\}\/\{\*\*tileRoute\}"/, "base and nested URLs use one deterministic TeamBoard route");
assert.match(layout, /var isEventBoardPage = currentPage is "\/Events\/Board" or "\/Events\/TeamBoard";/, "shared event view classification includes TeamBoard and nested tile routes");
assert.match(layout, /<nav class="public-ui-header-context-nav"[\s\S]*public-ui-header-context-link @\(isViewBingo \? "is-current" : null\)[\s\S]*aria-current="@\(isViewBingo \? "page" : null\)"[\s\S]*@T\["Boards"\]/, "TeamBoard receives the shared Boards view navigation with the Board item current");
assert.doesNotMatch(layout, /@T\["View bingo"\]/, "shared event navigation no longer renders the old View bingo label");
assert.doesNotMatch(program, /AddPageRoute\("\/Events\/TeamBoard"/, "base TeamBoard links do not rely on ambiguous extra route conventions");
assert.match(teamBoard, /Model\.SelectedTile is \{ \} selectedTile[\s\S]*_TileSidebar/, "nested tile views substitute only the tile sidebar");
assert.match(teamBoard, /aria-label="@T\["Team statistics"\]"/, "base TeamBoard retains the normal statistics sidebar");
assert.match(teamBoard, /asp-page="\/Events\/TeamBoard"[\s\S]*asp-route-tileRoute="@\(\$\"Tiles\/{tile\.TileId\}\"\)"/, "tile links retain the nested real TeamBoard URL");
assert.match(tileSidebar, /data-open-submission-drawer/, "tile sidebar retains the shared Captain drawer entry");
assert.match(tileSidebar, /asp-page="\/Events\/TeamBoard"[\s\S]*asp-route-slug="@eventSlug"[\s\S]*asp-route-teamSlug="@teamSlug"[\s\S]*asp-route-tileRoute=""[\s\S]*data-team-sidebar-return/, "tile sidebar restores the route-backed Team overview action and clears the tile route");
assert.match(tileSidebar, /public-ui-overline">TILE @Model\.SequenceNumber\.ToString\("00"\)<\/span>/, "tile sidebar renders the numbered overline");
assert.match(tileSidebar, /tile-context-sidebar__progress-summary" aria-label="@T\["\{0\} of \{1\} drops", Model\.Tile\.Approved, Model\.Tile\.Target\][\s\S]*public-ui-data-label">@T\["Drops"\]/, "tile progress summary renders an accessible drops label");
assert.doesNotMatch(tileSidebar, /public-ui-progress-meter/, "tile sidebar no longer renders the progress meter");
assert.doesNotMatch(tileSidebar, /tile-context-sidebar__rail/, "tile sidebar does not render the removed rail");
assert.match(tileSidebar, /data-tile-completion-breakdown[\s\S]*tile-context-sidebar__breakdown-row/, "completion breakdown uses flat rows");
assert.match(tileSidebar, /bossGroups\.Count > 1[\s\S]*<details class=\"tile-context-sidebar__drop-disclosure\"/, "multiple bosses use native collapsed disclosures");
assert.match(tileSidebar, /eligibleDrops\.Take\(eligibleDrops\.Count <= 4 \? eligibleDrops\.Count : 3\)/, "single-boss lists keep only the first three rows visible when long");
assert.match(tileSidebar, /eligibleDrops\.Count > 4[\s\S]*tile-context-sidebar__drop-disclosure--overflow[\s\S]*eligibleDrops\.Skip\(3\)/, "long single-boss lists put only the remainder in one native disclosure");
assert.match(tileSidebar, /View all \{0\}[\s\S]*Show less/, "single-boss overflow disclosure has closed and open labels");
assert.doesNotMatch(tileSidebar, /groupByItem|showBossHeadings|collapseBossGroups|public-ui-eligible-drop-table/, "tile drops do not retain the old grouping/card heuristic");
assert.match(tileSidebar, /data-evidence-image=[\s\S]*data-evidence-alt=[\s\S]*data-evidence-drop=[\s\S]*data-evidence-player=[\s\S]*data-evidence-team=[\s\S]*data-evidence-source=/, "asset evidence rows retain every viewer data attribute");
assert.match(tileSidebar, /SubmittedAt[\s\S]*minutesAgo[\s\S]*minutesAgo == 1[\s\S]*T\["1 minute ago"\][\s\S]*hoursAgo == 1[\s\S]*T\["1 hour ago"\][\s\S]*daysAgo == 1[\s\S]*T\["1 day ago"\][\s\S]*<time datetime=/, "approved submission rows expose semantic singular/plural relative times");
assert.match(teamBoard, /team-board-drawer\.js/, "ordinary TeamBoard loads the drawer transport");
assert.match(teamBoard, /<dialog class="public-lightbox public-evidence-viewer" data-public-lightbox data-evidence-dialog[\s\S]*data-evidence-dialog-image[\s\S]*<\/dialog>\s*@section Scripts/, "every TeamBoard render includes the shared evidence viewer host");
assert.match(teamBoard, /<script src="~\/js\/team-board-drawer\.js" asp-append-version="true"><\/script>\s*<script src="~\/js\/public-evidence\.js" asp-append-version="true"><\/script>/, "every TeamBoard render loads the shared evidence viewer behavior");
assert.doesNotMatch(teamBoard, /@if \(Model\.SelectedTile is not null\)\s*\{\s*<dialog class="public-lightbox public-evidence-viewer"/, "evidence viewer host is not gated by tile selection");
assert.doesNotMatch(teamBoard, /@if \(Model\.SelectedTile is not null\)\s*\{\s*<script src="~\/js\/public-evidence\.js"/, "evidence viewer script is not gated by tile selection");
assert.doesNotMatch(tile, /@page|tile-detail-header|tile-detail-layout/, "standalone Tile presentation is retired");
assert.doesNotMatch(layout, /OverlayMode|overlay-page-frame|overlay-document/, "shared public layout has no retired TeamBoard popup frame");
assert.doesNotMatch(drawer, /team-board-overlay|mission-team-card|showModal|history\.go|sessionStorage/, "drawer script has no popup interception or restoration machinery");
assert.match(drawer, /function openDrawer[\s\S]*data-open-submission-drawer/, "drawer script handles ordinary TeamBoard submission links");
assert.match(drawer, /handlerUrl\(link\.href, "Drawer"\)/, "drawer transport uses the authoritative Drawer handler");
assert.match(drawer, /public-ui-submission-result-open[\s\S]*dataset\.submissionResult/, "submission result remains in the drawer until acknowledgement");
assert.match(drawer, /public-ui-team-board-tile[\s\S]*replaceSidebar[\s\S]*history\.pushState/, "tile clicks replace only the sidebar and push the real nested URL");
assert.match(drawer, /data-team-sidebar-return[\s\S]*replaceSidebar[\s\S]*history\.pushState/, "Team overview restores the base sidebar in place");
assert.match(drawer, /addEventListener\("popstate", restoreSidebarFromHistory\)/, "Back and Forward restore the server-rendered sidebar");
assert.match(drawer, /window\.location\.assign\(href\)|window\.location\.reload\(\)/, "sidebar enhancement falls back to ordinary navigation");
assert.match(drawer, /function syncWorkspaceGeometry[\s\S]*--public-team-board-row-height[\s\S]*--public-team-board-row-top/, "desktop board geometry synchronizes row height and top offset");
assert.match(drawer, /ResizeObserver[\s\S]*public-full-board[\s\S]*refreshWorkspaceGeometry/, "board resize and layout changes refresh synchronized geometry");
assert.match(drawer, /if \(!desktopLayout\.matches\) drawer\.scrollIntoView\(\{ behavior: reducedMotion\(\) \? "auto" : "smooth", block: "start" \}\);/, "stacked drawer opening scrolls the inserted drawer into view without changing desktop behavior");
assert.match(drawer, /drawer\.animate\(\[\{ transform:[\s\S]*drawer\.animate\(\[\{ transform:/, "drawer open and close use translate-only animations");
assert.doesNotMatch(drawer, /scrim|submission-drawer-scrim|opacity:/i, "drawer script has no scrim or opacity animation");
assert.match(drawer, /drawerDirty[\s\S]*window\.confirm/, "dirty drawer close confirmation remains protected");
assert.match(tileSidebar, /data-team-sidebar-return[\s\S]*@T\["Team overview"\]/, "tile sidebar exposes the in-place Team overview target");
assert.match(publicUiCss, /\.public-team-board\.public-ui-submission-drawer-open/, "drawer styling is owned by the ordinary TeamBoard root");
assert.match(publicUiCss, /@media \(min-width: 901px\)[\s\S]*public-team-sidebar[\s\S]*height: var\(--public-team-board-row-height[\s\S]*public-ui-submission-drawer[\s\S]*top: var\(--public-team-board-row-top[\s\S]*overflow-y: auto/, "desktop sidebar and drawer share synchronized board geometry");
assert.match(publicUiCss, /@media \(min-width: 901px\)[\s\S]*public-team-board\.public-ui-submission-drawer-open \.public-team-workspace \{ overflow-x: clip; \}/, "desktop drawer closing animation is clipped to the TeamBoard workspace");
assert.match(publicUiCss, /@media \(min-width: 901px\)[\s\S]*public-team-board\.public-ui-submission-drawer-open \.public-team-workspace > \.public-team-sidebar:not\(\.tile-context-sidebar\) \{ position: relative; z-index: 6; background: var\(--board-canvas\); \}/, "desktop normal sidebar covers the open drawer without changing tile-sidebar layering");
assert.match(publicUiCss, /\.public-ui-sectioned-surface\.tile-context-sidebar \{[^}]*z-index: 6;/, "tile sidebar keeps its existing higher stacking rule");
assert.doesNotMatch(publicUiCss, /\.public-team-board\.public-ui-submission-drawer-open \.public-team-workspace > \.public-team-sidebar:not\(\.tile-context-sidebar\) \{ position: relative; z-index: 5; \}/, "obsolete normal-sidebar stacking rule is removed");
assert.doesNotMatch(publicUiCss, /public-ui-submission-drawer-scrim/, "drawer CSS has no scrim/backdrop");
assert.match(publicUiCss, /@media \(max-width: 900px\)[\s\S]*public-team-workspace \{ grid-template-columns: 1fr; row-gap: 0; \}[\s\S]*public-board-scroll \{ grid-column: 1; grid-row: 3; \}/, "mobile TeamBoard stacking remains protected");
assert.match(publicUiCss, /@media \(max-width: 900px\)[\s\S]*?body\.public-event-shell\.public-ui-pass1 \.public-team-board \.public-team-workspace > \.public-ui-submission-drawer \{[^}]*left: 0;[^}]*width: 100%;[^}]*max-width: 100%;/, "stacked drawer placement overrides the desktop offset through the full 900px breakpoint");
assert.ok([
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-submission-form \{[^}]*border: 0;[^}]*box-shadow: none;/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer > header \{[^}]*border-bottom: 0;[^}]*\}/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer > header::after \{[^}]*right: 0\.9rem;[^}]*left: 0\.9rem;[^}]*background: color-mix\(in srgb, var\(--board-rule\) 55%, transparent\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-submission-form \.public-ui-role-label \{[^}]*color: var\(--board-ink\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \[data-evidence-paste\] \{[^}]*text-transform: none;/,
  /\.public-ui-role-label small\.public-ui-supporting-text \{[^}]*font: 400 0\.48rem/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-evidence-drop__actions[\s\S]*?::file-selector-button,[^}]*height: 1\.65rem/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer > header \[data-submission-drawer-close\] \{[^}]*width: 2\.5rem[^}]*color: var\(--board-ink\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer > header \[data-submission-drawer-close\]:hover[\s\S]*?\{[^}]*color: var\(--board-ink\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-submission-form \.public-ui-submission-confirm \{[^}]*color: var\(--board-ink\)[^}]*border-color: var\(--board-coral\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-submission-form \.public-ui-submission-confirm:hover[\s\S]*?\{[^}]*color: var\(--board-ink\)[^}]*border-color: var\(--board-coral\);/,
  /body\.public-event-shell\.public-ui-pass1 \.public-ui-submission-drawer \.public-ui-evidence-drop__preview,[\s\S]*?\.public-ui-evidence-drop__preview-button \{[^}]*background: var\(--board-field\);/
].every(pattern => pattern.test(publicUiCss)), "submission drawer surface, labels, preview, and controls retain the scoped treatment");
assert.match(publicUiCss, /\.public-team-sidebar\.tile-context-sidebar\s*\{[\s\S]*grid-template-columns: minmax\(0, 1fr\)/, "tile sidebar uses a single full-width content column");
assert.match(publicUiCss, /body\.public-ui-page-canvas \.landing-shell-links > \.landing-shell-link:not\(\.is-current\):is\(:hover, :focus-visible\) \{[^}]*color: rgba\(255, 255, 255, 0\.78\) !important;/, "shared primary navigation only softens inactive labels while hovered");
assert.match(publicUiCss, /body\.public-ui-page-canvas \.landing-shell-links > \.landing-shell-link\.is-current \{[^}]*color: #fff !important;/, "shared primary navigation keeps the selected label fully visible at rest");
assert.match(publicUiCss, /html\[data-public-theme="dark"\] body\.public-ui-page-canvas \.landing-shell-links > \.landing-shell-link \{[^}]*color: #F4EEDF !important;/, "shared primary navigation keeps dark-theme labels fully visible at rest");
assert.match(publicUiCss, /body\.public-event-shell\.public-ui-pass1 \.public-team-board \{[^}]*padding: 1rem 0 3\.5rem;/, "TeamBoard uses the shared reduced desktop view-navigation gap");
assert.match(publicUiCss, /body\.public-event-shell\.public-ui-pass1 \.tile-context-sidebar__progress-summary \.public-ui-data-label \{[^}]*margin-bottom: 0;[^}]*font: 600 clamp\(0\.9rem, 1\.26vw, 1\.38rem\)\/0\.75 "Barlow Condensed SemiBold", sans-serif;/, "tile progress DROPS label scales responsively and bottom-aligns with the value");
assert.match(publicUiCss, /@media \(max-width: 680px\)[\s\S]*public-ui-has-secondary-nav\.public-event-shell\.public-ui-pass1 \.public-team-board \{ padding-top: 0\.75rem; \}/, "TeamBoard uses the shared reduced narrow view-navigation gap");
assert.doesNotMatch(publicUiCss, /\.tile-context-sidebar__rail/, "tile sidebar CSS has no removed rail selector");
assert.match(publicUiCss, /\.tile-context-sidebar__drop-disclosure \+ \.tile-context-sidebar__drop-disclosure[\s\S]*margin-top: 0\.5rem;[\s\S]*padding-top: 0\.35rem;[\s\S]*border-top: 1px solid var\(--board-blue\);[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow\s*\{[\s\S]*display: flex;[\s\S]*flex-direction: column;[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow > summary[\s\S]*order: 2;[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow > \.tile-context-sidebar__drop-rows[\s\S]*order: 1;[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow\[open\][\s\S]*border-top: 0;[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow\[open\] > \.tile-context-sidebar__drop-rows[\s\S]*margin-top: 0;[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow\[open\] > \.tile-context-sidebar__drop-rows \.tile-context-sidebar__drop-row:first-child[\s\S]*border-top: 1px solid var\(--board-rule\);[\s\S]*\.tile-context-sidebar__drop-disclosure--overflow\[open\] > summary[\s\S]*border-top: 1px solid var\(--board-rule\);/, "eligible-drop boss boundaries and open overflow ordering remain explicit");
assert.match(publicUiCss, /\.tile-context-sidebar__evidence-row[\s\S]*grid-template-columns: 3\.5rem minmax\(0, 1fr\)/, "approved submissions use a compact single-column list");
assert.doesNotMatch(publicUiCss, /team-board-overlay|team-board-overlay-content/, "Public UI CSS has no retired TeamBoard popup selectors");
assert.doesNotMatch(foundationCss, /team-board-overlay|overlay-page-frame|overlay-document/, "transitional foundation CSS has no retired TeamBoard popup selectors");

console.log("TeamBoard ordinary-navigation contract checks passed.");
