const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.join(__dirname, "../..");
const css = [
  fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/css/site.transitional.foundation.css"), "utf8"),
  fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/css/site.public-ui.css"), "utf8")
].join("\n");
const script = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/public-evidence.js"), "utf8");

assert.match(css, /\.public-lightbox:not\(\[open\]\) \{ display: none; \}/, "closed evidence dialogs stay out of document flow");
assert.match(css, /\.public-lightbox\[open\] \{ display: grid; \}/, "open evidence dialogs use the image-only layout");
assert.match(css, /\.public-ui-viewport-close \{ position: fixed;[\s\S]*width: 44px; height: 44px;[\s\S]*background: transparent; border: 0;/, "evidence close uses the shared PublicUi viewport primitive");
assert.match(css, /\.public-ui-recent-drop-footer[^\n]*margin-top: 0\.75rem/, "recent-drop pagination divider has breathing room");
assert.match(script, /dialog\.addEventListener\("close", clearImage\)/, "closing clears the enlarged evidence image");
assert.match(script, /if \(dialog\.open\) dialog\.close\(\);/, "reload recovery closes a restored open dialog");
assert.match(script, /document\.addEventListener\("click"[\s\S]*trigger\.dataset\.evidenceImage[\s\S]*updateMetadata\(trigger\)/, "delegated evidence clicks support dynamically inserted board sidebars and previews");
assert.match(script, /data-evidence-dialog-drop[\s\S]*trigger\.dataset\.evidenceDrop[\s\S]*data-evidence-dialog-source/, "shared evidence viewer preserves submission metadata");
assert.match(css, /html\[data-public-theme="dark"\] body\.public-event-shell\.public-ui-pass1 \.public-lightbox::backdrop \{[^}]*background: color-mix\(in srgb, #000 72%, transparent\);[^}]*backdrop-filter: none;[^}]*\}[\s\S]*html\[data-public-theme="dark"\] body\.public-event-shell\.public-ui-pass1 \.public-evidence-viewer,[\s\S]*\.public-evidence-viewer__figure,[\s\S]*\.public-evidence-viewer__meta \{[^}]*background: var\(--board-surface\);[^}]*filter: none;/, "dark Public UI evidence lightboxes use a neutral translucent backdrop and continuous Board surface");
