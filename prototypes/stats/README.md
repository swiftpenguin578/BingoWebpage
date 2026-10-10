# Stats page prototype

Standalone design proof of concept based on the user-selected
[Stats reference](../../docs/references/public-ui/stats-reference.png).
Uses illustrative data only; displayed GP values and luck percentages are not a pricing
or luck-calculation contract. No application or database connection is required.

Open `outputs/stats-page-prototype.html` directly, or serve the output directory:

```sh
python3 -m http.server 8766 --bind 127.0.0.1 --directory outputs
```

Visit http://localhost:8766/stats-page-prototype.html.

Fonts and branding are copied from the existing BingoWebpage assets. Item artwork was
retrieved from the public Old School RuneScape Wiki for this local prototype:

- Twisted bow: https://oldschool.runescape.wiki/Special:Redirect/file/Twisted_bow_detail.png
- Tumeken's shadow: https://oldschool.runescape.wiki/Special:Redirect/file/Tumeken%27s_shadow_%28uncharged%29_detail.png
- Torva platebody (damaged): https://oldschool.runescape.wiki/Special:Redirect/file/Torva_platebody_%28damaged%29_detail.png
- Dragon warhammer: https://oldschool.runescape.wiki/images/Dragon_warhammer_detail.png?7f65a

- Abyssal whip: https://oldschool.runescape.wiki/images/Abyssal_whip_detail.png
- Bandos chestplate: https://oldschool.runescape.wiki/images/Bandos_chestplate_detail.png
- Berserker ring: https://oldschool.runescape.wiki/images/Berserker_ring_detail.png

The footer provides 2/3/4/5/8/15-team sample events and seven independent drop previews.
The expected common range is 3–5 teams; five remains the default.

Choose a Drop preview, then use **Adjust artwork** to drag, zoom and rotate its silhouette.
The editor has desktop/mobile width previews and keyboard/slider controls. Save stores
that item's relative placement in this browser (`bingo-stats-artwork-v1` in localStorage);
Cancel discards the draft. Reset to default followed by Save removes the item's override.
This demonstrates a future Superadmin interaction; production permissions and shared
persistence are not implemented.


The base prototype UI and final **Drop value exploration and polish** are user-approved
(2026-09-15):

- Click a team in its donut or list to see the team's contributors and valuable drops.
  **All teams** returns; **My team** opens the illustrative Maya's team.
- The chart shows the top five contributors. Hover/focus another player for a sixth
  comparison; click/tap to pin it. A new preview temporarily replaces a pin; leaving
  restores it. Use the pinned chip to clear the comparison.
- The magnifier in the header divider searches only in the event-wide **Players** tab.
  Both team views use their existing clickable donut/list without search. The divider contracts around the field on wide
  panels; narrow panels use a popover. Escape closes it. Results are capped at six.
  **Players** shows the event's top five plus an **Others** donut slice; **Show me**
  highlights Maya. Eight illustrative contributors per team exercise the overflow.
- Guidance starts open with an **×** to dismiss; **Info** reopens it. It overlays the
  content without adding a help row. Totals and valuable drops stay within
  the selected team/event while previewing players. Everyone retains its aggregate view.

Drop value, Luck and Event milestones are approved. Keeps on dropping and Most versatile
remain locked. Board progress motion/guidance and final responsive polish are approved. Values remain illustrative; this is not a production
Stats implementation. docs/UI_PAGE_MATRIX.md owns acceptance and CURRENT_STATUS.md the handoff.

### Approved animations

Drop value now draws its chart once on load, morphs chart/donut shapes between views,
fades comparison lines in/out and pulses a newly pinned legend entry. Donut hover and
help disclosure also have brief transitions. Existing divider-search motion stays the
same. The user approved this batch. A subsequent one-time clockwise donut reveal and
slice-shaped keyboard focus are also approved; clicks use the existing highlight.
Reduced-motion preferences disable the added motion.

Most valuable drops updates immediately when switching scope. The user-approved artwork
hover lift remains and respects reduced motion; layout stays fixed.

### Luck exploration — user-approved 2026-09-15

Click a team name/bar for its complete sample roster; the back arrow returns. Players
shows five unluckiest and five luckiest, with a gap between groups and scrolling inside
the existing card height. The magnifier beside Players searches by player/team. Selecting
someone outside these ten adds one removable comparison; an existing player is highlighted
and scrolled into view. Bars and signed percentages grow/count together from zero (420ms),
with immediate final states for reduced motion. All scores are illustrative. The user approved Luck’s final design and interactions.

Luck’s ⓘ explains team exploration and player comparison. Guidance starts open; × or
Escape dismisses it and ⓘ reopens it. It opens again on each page load, like Drop value.
The shared page-level Hide tooltips setting and account persistence are deferred.

### Event milestones — user-approved 2026-09-15

Eleven event-wide milestones include four collective moments: all teams have an approved
submission, tile, row and board. Individual team views keep seven milestones and omit
these collective stops. Start stays first; reached milestones sort by timestamp, ties
keep default order, and Upcoming stops follow in default order without timestamps.
Solid dots show reached milestones; the latest has an outer ring, and upcoming dots are
hollow. Dot centers align with timestamps and titles. Timestamps stay above the rail; one short detail stays below the title. The detached
item artwork was removed. Snap padding protects the first dot; the last stop fits its content. The rail is muted after the latest
milestone. Wider stops fit the collective labels while preserving compact height.
Use **Milestone stage** in the footer to try initial/early/first-week/current/ended snapshots;
it only changes the timeline. Values remain illustrative; Event milestones is user-approved.

Event milestones has a short first-load rail draw, team-switch fade and milestone
completion/reordering transitions. Use Milestone stage to try them. Motion respects
reduced-motion settings; final visual timing is user-approved.

### Board progress polish — user-approved 2026-09-15

A shared 1200ms entrance sweep reveals completions and advances tile/row totals together.
It runs on load and event-size changes. Hover/focus or scrolling immediately finishes the
entrance so the existing date cursor and completion details remain responsive. Reduced
motion shows final states. The new info overlay starts open, with ×/Escape dismissal and
ⓘ reopening. All existing section info icons now share Luck's styling and state colors.

The user approved the completed Stats prototype UI on 2026-09-15, including final narrow
header and milestone spacing refinements. docs/UI_PAGE_MATRIX.md owns page acceptance.
Production implementation, real data/calculations and account preferences remain deferred.
