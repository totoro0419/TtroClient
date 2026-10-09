# Ttro Client 1.8.9調査・採用判断

調査日: 2026-10-08。公式資料を優先し、現代ForgeのJava 21ドキュメントを1.8.9に適用しない。

## Forge

公式1.8.9配布一覧のRecommended/Latest: **11.15.1.2318**。https://files.minecraftforge.net/net/minecraftforge/forge/index_1.8.9.html

QAで使ったuniversalのSHA-1 `beda619c465af293e63952dd573c137c17c0a4cd`、installer `ec0293ff0776b8831f2ed90511bab76e635dda0c` は公式表示と一致。公式Mavenアクセス制限時の検証用mirror取得物も公式hashで照合した。配布物には含めない。

信頼できる1.8.9実装: Mouse Tweaksの `minecraft-1.8.9` branch、HypixelDev/ForgeModAPI、Polyfrost/PolyPatcher。ForgeGradle 2.1、Java 8、1.8.9 MCP stable_22を選定。旧1.21.1のYarn/Mixin/Fabric APIを引き継がない。Coremodのソート順1001でForgeのdeobfuscation後に限定した2個の描画フックを適用する。入力やネットワークの挙動を書き換えるTransformerは追加していない。

## Mod/Client評価

| 資産 | 確認した用途 | 1.8.9 / License | 判断 |
|---|---|---|---|
| Mouse Tweaks 2.6.2 | RMB/LMB/Shift/wheel/search order | 公式1.8.9 / BSD-3-Clause | ソース統合。単体Modとの二重導入をエラーにする |
| PolyPatcher 1.10.4 | rendering/input修正、Fullbright、fire、particles等 | 1.8.9 / CC-BY-NC-SA-4.0 | 正規CDNから任意取得。コードをコピーしない。NCをOSS一般扱いしない |
| Hypixel Mod API 1.0.2 | Hello、Location、Party packets | 公式Forge 1.8.9 / MIT | 任意導入、公式handler bridge。実サーバーQA別途 |
| Hytils Reborn | Hypixel chat・lobby・UI/QoL | 1.8.9 / GPL-3.0 | 重複機能・規約・依存評価後の候補 |
| VanillaHUD 2.2.12 | FPS/CPS/armor/potion等 | 1.8.9 / GPL-3.0+linking exception | 個別HUDの実装参照。OneConfig依存とHUD重複を評価。現在未同梱 |
| PolySprint 1.0.2 | toggle sprint/sneak | 1.8.9 / AGPL-3.0 | upstreamを改造する場合のsource配布要件も評価。現在未同梱 |
| Sk1er OldAnimations | 1.7 animation | Forge ecosystem / LGPL-3.0 | 対応binary/versionを固定して検証する候補 |
| MWE (Alexdoru) | armor/potions、input、particles、drop guard、warnings、chat | 1.8.9 / custom noncommercial | 機能調査のみ。条件が厳しくコード/アルゴリズムを取り込まない |
| OptiFine 1.8.9系 | shaders、custom textures、render options | 再配布は作者許可が必要 | 同梱・自動取得しない。正規取得済みJARのimport候補 |
| Lunar Client | 統合ON/OFF、PvP HUD、zoom/crosshair/animations等 | proprietary | 機能の到達目標。コピー/同梱/逆コンパイルなし |
| Badlion | FPS、CPS、Ping、Armor、Zoom、Crosshair、ToggleSprint等 | proprietary | 統合UXの比較。広告FPS値を性能目標の実測に転用しない |

UI選定は比較試作とSkillに基づく独自設計。Lunar/Badlion実機の全機能比較、Feather/Dawn詳細比較、全CurseForge資産の網羅は未完了。

## Primary sources

- Mouse Tweaks: https://github.com/YaLTeR/MouseTweaks ; https://www.curseforge.com/minecraft/mc-mods/mouse-tweaks/files/2287384
- PolyPatcher: https://github.com/Polyfrost/PolyPatcher
- Hytils: https://github.com/Polyfrost/Hytils-Reborn
- VanillaHUD: https://github.com/Polyfrost/VanillaHUD
- PolySprint: https://github.com/Polyfrost/PolySprint
- OldAnimations: https://github.com/Sk1erLLC/OldAnimations
- MWE: https://github.com/Alexdoru/MWE
- Hypixel API: https://github.com/HypixelDev/ModAPI ; https://github.com/HypixelDev/ForgeModAPI
- Lunar PvP features: https://www.lunarclient.com/features/pvp
- Badlion: https://www.badlion.net/minecraft-client
- OptiFine license: https://optifine.net/copyright

## Server policy / API

公式Allowed Modifications: https://support.hypixel.net/hc/en-us/articles/6472550754962-Hypixel-Allowed-Modifications

自身の通常情報・描画/性能の許容カテゴリと、enemy health/distance、freelook、ゲーム操作自動化、通信変更を区別する。許可を確認できない機能は安全側で無効にする。Inventory Controls/Toggle Sprint等はこのalphaのHypixel policyでは慎重に無効化する。カテゴリ該当はBAN免責の保証ではない。

公式Public API policy: https://developer.hypixel.net/policies/ (確認時2026-09-30更新)、v2 reference: https://api.hypixel.net/ 。Public modへユーザーのAPIキー入力を要求しない、APIキーをbinaryへ埋め込まない、nickを解除しない。Public APIからlevel等を取得する機能は今回提供しない。将来は登録済みサービス側でUUID cache + metric別TTL・rate limit・negative cacheを実装する。Mod API由来・通常サーバー情報・外部Public API値の出所を混ぜない。

Connection Qualityはserver tablistのlatency変化をローカルに分析する。真のRTT jitter/packet loss/input responsiveness測定と称しない。サーバーの通常通信を変更しない。


## Alpha.2実装照合

公式PolyPatcher 1.10.4 JARのfieldと公開sourceを照合し、Fullbright / Fire Overlay / FovHandler / BetterKeybindHandling / Linux layout / NightVisionEffectを独立Modへのconfig連携として採用。MouseDelayとMouseBindは上流の常設Mixinであり、架空の切替を作らない。Time/WeatherをPatcher機能とする分類は誤りとして修正。粒子設定の旧互換fieldは上流がOverflowParticlesへの導入案内に置換しており、連携実装の根拠に使わない。

OneConfig official API `https://api.polyfrost.org/oneconfig/1.8.9-forge` のURL/hashに従うloader beta17 / runtime alpha230 cacheを使用。ローダーの実bytecodeでは、autoUpdate=falseがdownloadだけでなくaddToClasspathも分岐で省く。QA用のfalse設定を撤去し、デフォルトtrueで起動成立。上流binary/sourceは改変しない。Java direct networkとonline replacement detectionは未成立。公式cache取得とゲームによるfresh online取得を区別する。

ForgeGradle 2.1-SNAPSHOTの公式Maven metadataを取得し、実解決は2.1-20211118.174922-42 / plugin 2.1.3-g6a7c715。標準clean buildのログはresearch/forgegradle-final.log。環境proxy/trust storeの指定で2318 API、stable_22、reobfJarのpipelineが成功した。Maven/Minecraft依存は正規の配布元で取得し、API stubを追加しない。
