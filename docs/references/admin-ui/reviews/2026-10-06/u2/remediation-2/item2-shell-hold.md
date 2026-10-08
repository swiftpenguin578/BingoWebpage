# U2 brief72 item2 — inherited loading hold and focus

Parent:7bd6660. Shared delayedLoading accepts the shown timestamp; second full
navigation replaces the overlay directly without revealing its saved children,
and uses the remaining400ms rather than delaying another150ms. Results updates
transfer their display ownership before aborting the older request, with distinct
ownership tokens so old finally cleanup cannot remove the successor's display.
Failure keeps the same held overlay until the minimum expires. A16 captures
focus before inert/hiding; cancel to the original URL restores that snapshot.
Back restores per-entry focus/selection/vertical scroll and named scroll regions;
Events marks its retained horizontal table wrapper as a shared named region.

Changed:admin-design-shell.js, Events/Index.cshtml (scroll-region marker),
admin-design-loading.browser.js, admin-design-events-update.browser.js, this file.

Executed checks (7 October2026):
- Node --check production shell:exit0; git diff --check:exit0.
- Shared fake-clock loading runner:Chromium/WebKit PASS.12 original exact boundary
  cases plus5 failure-hold/abort-hold/second-shown/Back-shown/cancel-Back cases.
  Shown at150; replacement at200; no old-content observer frame; still pending549,
  completes550. Cancel restores the original pre-inert input and clears inert.
  Back also restores nonzero named horizontal80/vertical50 scroll positions.
- Events exact fake-clock runner:Chromium/WebKit PASS. Prior11 connected cases
  plus3 second-shown/abort-hold/failure-hold cases. Same150/200/549/550 boundaries,
  retained nodes/focus and no visible old-results frame. Mid-flight typing waits
  the exact remaining300ms after the second settled request;299 still held.
- Unchanged shared shell runner:PASS both engines (dirty/sidebar/crumb/switcher,
  Back/Forward/disposal/URL/fallback/failure/busy/motion/toast cases).
- Rendered Events runner:26passed/0failed, loaded/failure differences0 both engines.
- Serial fixture Release rebuild:0warnings/0errors,0.93s. Frozen tokens/components
  byte comparisons:exit0. No frozen reference or shared component CSS edits.

Authoring checkpoint:the old mid-flight search test initially awaited immediate
completion without advancing the now-required inherited hold. Both engines timed
out at its microtask checkpoint. It now explicitly asserts299/300ms remaining
hold; assertions were extended, not relaxed. Display transfer ownership was also
made distinct to prevent predecessor cleanup races. Final paired reruns pass.

No independent/self-review or visual acceptance claimed. Final batch gates,
whole final-SHA suite and item8 three concurrent Integration runs remain required.
