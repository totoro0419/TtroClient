# Content Library — UI decision and implementation contract

Target: native Windows WPF, Minecraft 1.8.9, keyboard/mouse/touchpad, light Ttro PLAY/TUNE/STYLE/LIBRARY language. Provider: Modrinth. No CurseForge keys or unofficial scraping.

## Three alternatives

| Candidate | First-use clarity | Install speed | Image comparison | Installed management | Cost / narrow window |
|---|---|---|---|---|---|
| A. Grid for every state | High in Discover | One card button | Best | Priority/actions become crowded | Many repeated buttons; narrow cards reflow |
| B. Dense rows + permanent detail pane | Moderate | Selection then pane action | Limited on small windows | Strong | Two panes constrain minimum width |
| C. Discover cards / Installed rows / optional details | High with explicit section labels | One card button | Strong | ON/OFF, order, update/remove grouped per file | Bounded pages; one column at narrow widths |

**Selected C.** Discover optimizes visual choice; Installed optimizes frequent local management. Details are optional and never prerequisite to INSTALL. A repeating rich card is an ordinary layout with native buttons, not a ListBox option containing interactive descendants.

## UI Implementation Quality applied

Read `ui-implementation-quality` v0.3 SKILL.md, pattern selection, review pipeline, runtime/regression checklists, relevant Core bodies and Windows overlay. Applied SEARCH.01–03 (scope, visible filters, distinct states), STATE.01–03 and A11Y.01–03 (truthful synchronized semantics), KEY.01/FOCUS.01–03 (standard native controls and logical continuation), ERROR.01 (recovery), DATA.01 (preservation), ADAPT.01/06 (reflow/context), FLOWCORE.01 (bounded pagination). Windows automation/keyboard contract applies; Web ARIA bindings are not imported. WCAG-specific conformance is not claimed for native WPF.

- DISCOVER / INSTALLED / FAVORITES use labeled native radio navigation; native Automation exposes selected state.
- Empty search opens provider `downloads` ordering; `updated`, `newest`, `relevance` are explicit. No invented Featured/rating data.
- Combat/PvP uses Modrinth `combat`, resolution uses provider `16x/32x/64x/128x` category tags. The tags were verified against `/v2/tag/category`; titles are never parsed for resolution.
- Card: provider preview, title/details, author when supplied, description, version/category/downloads/date, install/installed/update, local favorite. Missing image has text fallback.
- INSTALL means install and enable, highest priority for a new pack; UI states the consequence. UPDATE preserves ON/OFF and existing priority. Resource Packs changes apply next launch.
- Installed: local/managed identity, version/file, enabled state, priority, move up/down, preview/details/update/remove. Non-drag buttons provide both pointer and keyboard reordering; no unnecessary drag interaction.
- Progress reports download bytes when available, verification/commit as indeterminate stages. Cancel works before the short commit; incomplete work never reports installed.
- Search debounces 400 ms, cancels superseded work and rejects stale profile/kind responses. 12-item pages; no startup content queries or continuous polling. Explicit compatible-update checks.
- Preview: viewport-triggered request, cancel on view change/unload, maximum 3 downloads, 2 MiB per image, decode 640×360, memory 16 MiB/48 entries, disk 32 MiB/128 entries. Gallery fetched only when details opens, capped at 4 images. Cache failure cannot disable install.
- Modrinth JSON cache: 32 entries / 2 minutes, bounded metadata response, serialized calls and rate-limit retry messaging. Previously readable results are explicitly labeled stale on connection errors.
- Offline: Installed/import/profile/enable/order/remove available; Discover exposes error/retry. Favorites local only, capped at 200; cached metadata is explicitly stale.
- Removal is profile-scoped, defaults confirmation to Cancel and retains backup. Provider body displayed as text; no active HTML.

## Validation evidence

`tests/native/ContentQa.cs`: deterministic safety/persistence/update/cancel/recovery checks. `LiveContentQa.cs`: optional real provider search/download/validation. `tests/windows_library_qa.ps1`: real WPF/native Automation and OS keyboard, fixture provider; screenshots and JSON uploaded by Windows CI. Existing Windows installer/UI, Client/Game and responsive website QA remain required.

Do not infer Narrator, physical mouse, Windows DPI/text-scale/high contrast, live authenticated PLAY or actual game pack reading from these tests. Record each executed gate independently. Build alone is not UI verification.

## Official provider references (checked 2026-10-09)

- https://docs.modrinth.com/api/ — User-Agent, stable IDs, rate-limit headers.
- https://docs.modrinth.com/api/operations/searchprojects/ — facets, index, offset/limit, exact metadata.
- https://docs.modrinth.com/api/operations/getproject/ — gallery/license/body.
- https://docs.modrinth.com/api/operations/getprojectversions/ — compatible versions, files/hashes/dependencies.

CurseForge is deferred: this request permits Modrinth as primary and does not require a second provider. The interface separates catalog/version access for future legitimate providers; no unsupported credentials or download rights are assumed.
