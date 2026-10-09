# Ttro Client Architecture — 0.3.0-alpha.1

対象はMinecraft Java Edition 1.8.9 / Forge 2318 / Java 8のみ。Fabric / 1.21.1互換層は製品に存在しません。

| Directory | 責務 |
|---|---|
| launcher/native/Ttro.Launcher | WPF Windows UI、Profile/PLAY/Context/Contentの操作 |
| launcher/native/Ttro.Launcher.Core | CmlLib認証・Java/Forge配備、Profile永続化、ダウンロード・hash・atomic更新 |
| launcher/modules.json / product.json | ClientとLauncher共通の型付き機能Catalogとbranding |
| forge/src/main/java/dev/ttro | 1.8.9 Core、Forgeイベント、HUD/Input/描画、設定、限定ASM hook |
| forge/.../inventory | Mouse Tweaks 2.6.2 BSD由来のInventory操作 |
| integrations/existing-mods | 監査済みPatcher公開config fieldとの対応。外部JARを含まない |
| website | GitHub Pages用static DLページ。実assetsはGitHub Releases |
| tests | Python旧資産、native core、browser、独立game probeとfixture |
| research/archive-alpha2 | 過去結果。今回の検証の代用にしない |
| .github/workflows | clean build、test、Windows package、main-only Pages、手動draft Release |

責務別packageは小規模な1.8.9単一Forge Modとしてまとめています。HUD/Input等を別々のJARに分割してイベント互換層を増やしません。外部Modは原本のまま上流から取得し、public config/APIだけをbridgeします。

AuthはCmlLib + MSALのsystem browser、Windows暗号化cache。Xbox/JE bearer tokenはmemoryだけ。独自OAuth暗号protocolやpassword入力は実装しません。専用application IDがない場合は明示的に停止。Public API keyを要求しません。

Profileは固有IDごとにgame directory、mods、packs、HUD、Crosshair、Keybinds、Client configとperformanceを保持。launcher.jsonとschema2 Client configはatomic保存。実行中のLauncher編集を禁止し、Minecraft終了後は再読込。未知schema・改変済みmanaged JAR・collisionは保持してエラーにします。

Playは認証→1.8.9＋Java配備→Forge2318→hash確認済みTtro JAR→監査済みPatcher/HypixelAPI→game process。ダウンロード元の検証とキャンセルは配備処理で行い、PLAY後のCancelでworld保存を中断しません。各依存のonline first-runは引き続き実機QAが必要。

UIはbright surface＋常設上部ナビ＋選択した機能のContext。大量Mod card/left sidebarを再現しません。WPF native keyboard/focus、PerMonitorV2、system high contrast、persistent statusとprogress/errorを使用。完全なWindows操作検証は別工程です。

HeadFXは1.8.9 Items.skull metadata3のVanilla描画とGL状態復元。Smart Scoreboardは既知英語BedWars行だけをtyped modelへ再構成し、未知データは原rendererへ戻す。Modern Tabは通常のNetworkPlayerInfoのname/team/pingだけ、score/header/footerがある場合は元表示へfallback。Identityはnametag visibility/sneak/不可視を尊重。

Hypixelは公式Mod APIのHello/Location/Partyイベントを利用。通常取得するserver displayと公式APIを区別する。Public APIによるrank/level補完は未実装。毎frame HTTPはない。Tab cacheは250ms、disconnect時破棄。将来のPublic API cache/TTL/rate-limitはdocs/RESEARCH_UPDATE.mdで設計を明記する。

Python Web Launcherは正常な旧logic/UXを検証・参照する開発用資産。正式製品経路へPrism Adapterを残しません。
