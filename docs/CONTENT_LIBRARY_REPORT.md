# Content Library / Public Alpha readiness — 2026-10-09

Canonical branch: `integration/1.8.9-native-launcher-2026-10-09`, Draft PR #1. Latest CI and source SHA are recorded in the PR; historical evidence below is tied to its explicit SHA.

## Implemented

Resource Packs and Mods share Discover cards, empty-query popular browsing, accurate Modrinth metadata, preinstall images, optional details, direct INSTALL, local Favorites, pagination, error/retry and cancellable progress. Installed separates Local/Managed and provides offline ON/OFF, Pack order/Preview, updates and backup removal. No invented Featured/rating/resolution. Three UI designs and the applied UI-quality rules are documented in [CONTENT_LIBRARY_DESIGN](CONTENT_LIBRARY_DESIGN.md).

`IContentProvider` / `ModrinthProvider` separate catalog access from `ContentService` validation and Profile management. The previous validation, SyncPacks and backup contracts remain in use. Download SHA-512, safe HTTPS origin/filename, 256MiB limit, pack_format=1, Java <=8, Forge 1.8.9, Fabric/modern Forge rejection and Mouse Tweaks duplicate rejection are enforced.

Managed records persist project/version/provider/hash/file/dependencies per profile. Install stages every required pinned dependency and rejects unpinned/ambiguous/incompatible dependencies. Update stages and verifies complete replacements, preserves ON/OFF and Pack priority, records a durable transaction, restores on failure/interrupted startup, then backs up the prior bytes. Local imports are never adopted by provider matching. User-modified managed files are preserved and updates refused. Binary delta transfer is not implemented or advertised.

Metadata uses bounded cache and serialized requests. Previews are loaded for visible cards, capped at 3 concurrent requests, 2MiB/image, 16MiB/48 memory and 32MiB/128 disk; galleries open on demand. Search debounces/cancels and rejects stale profile results. No startup bulk API calls or polling. Filters are expandable to preserve content space at 620px width.

## Executed evidence and scope

- CI [37907966762](https://github.com/totoro0419/TtroClient/actions/runs/37907966762), source `f34962a5854e18ebbd93e2db56b01473a313150f`: client/windows/game/web all success.
- Native Content: 35 deterministic checks PASS, including hash/archive/loader/Java, dependency groups, state/order, update, failed/cancelled downloads, modified files/collision/profile isolation and crash recovery.
- Live Modrinth: 10 checks PASS in `research/content-live-qa.json`, actual LowOnFire_1.8.9.zip and audited Hypixel Mod API. A subsequent live rerun was denied access to cdn.modrinth.com by the execution environment; it is not counted as another PASS.
- Real WPF Library: 26 checks PASS on that CI, OS keyboard/UI Automation, deterministic provider. Native previews, direct INSTALL, UPDATE/backup, offline/Retry, debounce/Favorites/cancel/profile isolation. The latest code adds Mod-card coverage and strengthens narrow-window visibility; those results must come from its new CI.
- Real Minecraft 1.8.9 / Forge / Java8: 57 stack checks PASS; includes Local World and two enabled Pack priority. Native shipping ContentService downloaded/validated QA packs and generated game options; Minecraft Resource Manager consumed the highest-priority texture. Source/provenance in `research/content-integration-evidence.json`. This uses QA launch and fixture transport, not authenticated consumer PLAY.
- Existing native Windows UI, Setup/install/uninstall/data retention, Client/Forge, original Mouse Tweaks comparison and responsive website tests remain active. Setup/Portable/JAR/checksums and bundled notices were independently inspected for this CI; no test host/provider is shipped.

UI screenshots were inspected. The 620px view's expanded controls consumed too much card space; filters were collapsed and the search/sort layout reduced. Library uses one scrolling surface so short windows cannot collapse the card area. Automation requires the entire INSTALL bounding rectangle inside the viewport after scrolling; IsOffscreen alone was insufficient. Starting an install scrolls to visible progress/cancel. Native screen-reader names and keyboard semantics are applied; Narrator, high DPI/text-scale and physical mouse are not certified.

## Public release gate

**BLOCKED BY OWNER ACTION: dedicated Microsoft app registration/configuration and authenticated Windows E2E.** The audited actual Portable ID is empty. Login and PLAY explicitly require JE profile plus entitlement, use the owner's dedicated public client and system browser, never embed secrets or third-party IDs. This is compiled code, not login evidence.

`build-info.json` binds the exact package to source/CI. `record_owner_qa.ps1` records only actual Windows checks and asset hashes. `owner-e2e.yml` validates that attestation; `release-draft.yml` rejects missing/wrong-source/wrong-asset/blank-ID E2E evidence. Neither workflow fakes account login. See [MICROSOFT_AUTH_SETUP](MICROSOFT_AUTH_SETUP.md) for the minimum owner steps.

main remains the bootstrap, current Pages serves bootstrap README, and GitHub Releases is empty. Website source now contains actual game screenshots and the verified Library feature; download remains disabled without an actual canonical alpha release. Do not call this Public Alpha complete or mark Microsoft Login, ownership, fresh authenticated PLAY, restart/Repair or Pages deployment PASS.

## General-user route after gate passes

Official Pages → Download for Windows → canonical GitHub alpha release/Setup.exe → per-user install → Ttro Client Launcher → Microsoft Login → PLAY → Minecraft 1.8.9/Ttro Client. LIBRARY → Discover → preview → card INSTALL → verified/enabled Pack → next PLAY. Existing profile/world data survives uninstall.
