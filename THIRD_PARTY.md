# Ttro Client third party code and integrations

Bundled code: Mouse Tweaks 2.6.2 for Minecraft 1.8.9, YaLTeR / Ivan Molodetskikh, BSD-3-Clause. Source: https://github.com/YaLTeR/MouseTweaks/tree/minecraft-1.8.9 ; pinned commit `804515efa43c908d166fb404389942a73462d6d2`. Official 1.8.9 binary reference: https://www.curseforge.com/minecraft/mc-mods/mouse-tweaks/files/2287384 . Full license: `docs/MOUSE_TWEAKS_LICENSE.txt` and inside the JAR. Source notices retained. Changes: integrated event entry, independent Shift toggle, inverse wheel option, config from unified catalog. Original vanilla/Mod compatibility logic retained. Not endorsed by upstream.

Source package build tooling: Gradle Wrapper (Apache-2.0); full distribution license/notice are in `forge/gradle/LICENSE.txt` and `NOTICE.txt`.

Windows native launcher: CmlLib.Core 4.0.6, CmlLib.Core.Installer.Forge 2.0.0, CmlLib.Core.Auth.Microsoft 3.3.1 and XboxAuthNet.Game.Msal 0.1.3 (MIT) are used as libraries, not copied launcher implementations. Microsoft MSAL handles system-browser login and its encrypted Windows cache. NuGet dependencies, authors, licenses and included notices are recorded in `launcher/native/licenses/DEPENDENCIES.json`; full upstream licenses and .NET runtime notices are shipped in `licenses/`. The self-contained Windows package includes the .NET 8 Windows runtime (MIT), not Python or Prism. LZMA SDK decoding code is public domain (upstream https://www.7-zip.org/sdk.html); its NuGet wrapper declares MIT. NSIS compiler (zlib/libpng) creates the per-user installer; https://nsis.sourceforge.io/License . No third-party endorsement is implied.

Runtime-provided dependencies: Minecraft 1.8.9 (Mojang/Microsoft proprietary), Forge (LGPL-2.1), LWJGL (BSD), Gson (Apache-2.0), ASM (BSD). Minecraft, Forge, JDK, their libraries, credentials and assets are not bundled in the project distribution.

External optional downloads from original Modrinth CDN: PolyPatcher 1.10.4 (CC-BY-NC-SA-4.0), Hypixel Mod API 1.0.2 (MIT). These JARs are not included in Ttro Client distributions. Review license and dependency requirements before installation. PolyPatcher dynamically uses OneConfig; this dependency path has not been certified by Ttro Client QA. No universal redistribution permission is implied.

Other researched assets, currently not adopted/bundled: Hytils Reborn (GPL-3.0), VanillaHUD (GPL-3.0 with Minecraft linking exception), PolySprint (AGPL-3.0), Sk1er OldAnimations (LGPL-3.0), MWE / MegaWallsEnhancements (custom noncommercial conditions). All require pinned-license and integration review before use. MWE means Alexdoru/MWE, not Mouse Wheelie. No MWE code or algorithms are included.

OptiFine: redistribution requires explicit author permission. Ttro Client does not distribute it or fetch its JAR automatically. Import a legitimately obtained compatible 1.8.9 edition to use its resource-pack/shader features, with integration QA still required.

Lunar Client and Badlion are feature/UX references only. Their binaries, assets and private code are not used or redistributed.
