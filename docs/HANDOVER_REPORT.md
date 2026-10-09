> 履歴資料: 0.2.0-alpha.2の引き継ぎ報告。現在の正式Repository / Native Launcher / 0.3.0-alpha.1の状態は [INTEGRATION_REPORT.md](INTEGRATION_REPORT.md) を参照してください。ここにある古い配布物・QAは現版の完成証拠ではありません。

# Ttro Client — 1.8.9再構築・引継ぎ報告

2026-10-08 / 0.2.0-alpha.2。Minecraft Java Edition **1.8.9 / Forge 11.15.1.2318 / Java 8** を固定した。

**再構築したAlphaであり、Lunar級の完成版ではない。** 内蔵21項目・外部連携8項目（常設Input Foundation 2件を含む）・未実装20候補。標準ForgeGradle buildとPatcher/OneConfigのローカル起動は成立。実Hypixel、ハードウェアGPU性能、残りの既存Mod統合は未完了。未実行の検証をPASSにしていない。

## 1. 1.21.1から再利用したもの

Launcherのatomic保存、revision競合検出、Profile、Backup/Restore、ハッシュ照合付きdownload、Content transaction、検索、Prismへの認証引渡し、loopback/Host/Origin/token保護を再利用した。明るい色調・Context編集の設計資産を残し、画面構成は作り直した。

監査入力は旧project ZIPのversion 2。詳細とSHA情報は `LEGACY_AUDIT.md`。旧Minecraft描画・FPS・QA証拠を1.8.9の実績には転用していない。

## 2. 破棄・再構築したもの

Fabric / Yarn / Java 21、1.21.1ゲームintegration、Fabric Bridge、Sodium/Iris前提、pack_format 34、旧HUD/HeadFX/Module/Mod検査を置換した。1.8.9 Compatibility Modeや版ごとのif分岐で延命していない。

正式名称は `launcher/product.json` が管理するTtro Client。新技術ID `ttro189`、専用data root `TtroClient189` は旧環境を誤って上書きしないための隔離。Config filename等は互換用の内部IDとして分離した。

## 3. Minecraft 1.8.9 Architecture

| 層 | 所有する機能 | 主な実装 |
|---|---|---|
| Client Core | Branding / Profiles / Content / Search / Backup | product.json, core.py, modules.py |
| 1.8.9 platform | Forge metadata / 版検査 / pack_format 1 / Prism components | minecraft189.py |
| Forge lifecycle | 設定、イベント登録/解除、キー、メインスレッドへの反映 | Core.java, Config.java |
| Modules | HUD / PvP / Inventory / Server | modules.json、Moduleごとの実装 |
| Original rendering | HeadFX / semantic Scoreboard / Adaptive HUD / Identity | HeadFx, Scoreboard, Hud, Identity |
| External integration | PolyPatcher config / Hypixel公式Mod API | PatcherBridge, HypixelBridge |

Forge公式Recommended 2318と公式hashを確認。Java 8 / ForgeGradle 2.1 / MCP stable_22を採用。CoremodはForge deobfuscation後の2描画箇所だけに限定した。描画バッチは1.8.9のMinecraft既存bufferを使う。切断処理と公式API callbackはMinecraftのメインスレッドへ渡す。

## 4. 採用ModとLicense

| 資産 | 状態 | 条件 / 配布 |
|---|---|---|
| Mouse Tweaks 2.6.2 | ソース統合済み | 公式minecraft-1.8.9 commit 804515efa43c908d166fb404389942a73462d6d2、BSD-3-Clause。著作権・License同梱 |
| Hypixel Mod API 1.0.2 | 公式handler連携済み | MIT、Modrinth version VtDhN4ZW。API binaryは同梱せず任意取得。offline登録QA済み |
| PolyPatcher 1.10.4 | 外部連携・常設基盤として採用 | CC-BY-NC-SA-4.0、version iNjGeSxM。コード/バイナリ非同梱。公式OneConfig cacheのhash照合を行いローカル実起動成立 |
| OneConfig 0.2.2-alpha230 / loader beta17 | Patcher依存として起動検証 | 公式配布を改変せず使用。非同梱。動的update通信はこの実行環境で未成立 |
| VanillaHUD / Hytils / PolySprint / OldAnimations | 調査済み候補 | GPL / AGPL / LGPL等の条件・依存・機能重複を個別評価。未同梱 |
| MWE | 機能調査のみ | 独自noncommercial条件。コード/アルゴリズムを取り込まない |
| OptiFine | 利用者の正規取得JARをimport可能 | 再配布許可を取得していないため非同梱。実起動/Shader QA未確認 |

Lunar / Badlionは到達機能とUXの比較対象であり、proprietary実装をコピーしていない。詳細・一次資料URLは `RESEARCH.md`、Licenseは `THIRD_PARTY.md`。

## 5. Ttro Client Modules

**内蔵21項目、外部連携8項目（常設基盤2件を含む）、未実装20候補**。Catalogの「実装」は実サーバーでの全挙動を保証する意味ではない。

- HUD: FPS、Ping、CPS L/R、Keystrokes、BPS、Coordinates、Memory、Armor/Durability、Potion Effects、Item Counters、Clock、Connection Quality、Adaptive HUD。
- PvP: Crosshair/Vanilla target色、Zoom、Toggle Sprint。
- Original: HeadFX、Smart Scoreboard、Player Identity Capsule。
- Inventory: Inventory Controls。
- Server: Hypixel Server/Game/Party bridge（公式Mod API必要）。
- External: Fullbright、Reduced Fire、Static FOV、Water FOV Cleanup、Night Vision Cleanup、Input Fix / Foreign Keyboard、Mouse Delay、Mouse Bind（PolyPatcher必要）。後2件は常設基盤の状態表示で、偽のON/OFFスイッチは提供しない。提供Mod不在時は要求設定と利用可能状態を区別する。

20候補の一覧は `MODULE_CATALOG.md`。Mouse Delay/Bindの実local aim/GUI操作を検証し、Input Fixは既存のキー再登録とLinux配列設定へ連携。Scroll Fixは未実装候補として残す。Time/WeatherをPatcherの機能とした前提を修正。Particle controlsは上流がOverflowParticlesへ分離しており、旧互換fieldを設定して実装済みとは扱わない。1.8.9に1.9式Attack Cooldownを捏造しない。Comboもclickを命中と偽って表示しない。

## 6. Mouse Tweaks互換状況

PolyPatcher/OneConfigを載せた最終JARの実ForgeのGuiChestに実pointer/key/wheel入力を送り、公式2.6.2と全45 slot＋cursor stackを比較した。RMBの同slot再訪を含むplacement、LMB collect、Shift drag、wheel out/in、First→Last / Last→First search、Reverse out/inの **9ケースが一致**。

逆方向設定の基準は、公式版へ反対向きの物理wheelを送り、Ttro側Reverseの結果と比較した。独立ON/OFFと方向設定は元の有用な動作を維持する範囲で追加。Standalone Mouse Tweaksとの二重導入は検出する。

crafting output、NBT固有stack、third-party GUI、touchscreen、live multiplayer inventoryはUNVERIFIED。証拠は `research/inventory-compatibility.json` と実ゲームのresult/screenshots。

## 7. 独自機能

HeadFX: Competitive / Clean / Glow / OFF、Optional 3D。1.8.9 `Items.skull` metadata 3、Vanilla skin/modelを使う。3Dは拡大・傾斜であり、新しいskin modelではない。OFF時は描画hookの定数判定だけ。常時scan・外部skin要求・particleはない。

Smart Scoreboard: 既知英語Bed Wars行を、次イベント/Timer、Team状態、YOU統計のtyped modelへ解析し、2列のContext Panelへ再構成。未知行・未知状態・重複・未対応serverは全体をVanillaへ戻す。Combatでは自己統計を省く。全Hypixel mode・言語への対応は未完成。

Adaptive HUD: COMBAT / STATUS / NETWORK / GAME。1項目Groupはcompact表示。Combat focusで補助情報を省く。Individualは12 HUDごとの位置/倍率。Web Contextとゲーム内drag/Tab/arrows/+/-で編集・保存できる。

Identity: 通常server display nameとteam prefixを使う小型Capsule。invisibility/sneak/team visibility/below-name objectiveを尊重。レベル推測・nick解除・外部API値はない。実他player表示は未確認。

Connection Quality: server tablist latencyの変化から偏差とspikeをローカル計算。実RTT jitter、packet loss、input latencyの測定とは呼ばない。通信を変更しない。

## 8. Hypixel機能

公式Mod API 1.0.2のHello / Location / Party handlerを使用。Partyはユーザー操作時だけ要求。実公式JARを使うoffline登録のQA済み。実Hypixel handshake、Location、Party responseはUNVERIFIED。

通常server情報、公式Mod API、Public APIを区別する。Public APIのlevel取得は未提供。user API key入力・binaryへのkey埋め込み・nick解除を行わない。将来の外部値は登録済みservice側でUUID cache / TTL / rate limitが必要。

## 9. Server Safety

Hypixel hostnameまたは公式Helloでpolicyを適用。Inventory Controls / Toggle Sprint / Sneak / Reach / Freelook / Drop Protection / Comboをrestricted/uncertainとして安全側で止める。Server fixtureでblockとserver変更後の解除を確認。

分類は規約上の許容カテゴリ/制限/不確実性。BAN免責を表示しない。全serverの規約database、自動更新、実Hypixel適用QAは未完成。

## 10. UI Skill適用

必須 **UI Implementation Quality**（skill-6aa41c46022c8191981a612e9a5fe342）の全文、該当engine/rules/runtime QAを読み、要件・IA・navigation・hierarchy・interaction・prototype・実装・accessibility・keyboard・reflow・states・最終QAへ適用した。

PURPOSE / NAV / DISCLOSURE / STATE / DATA / FEEDBACK / ERROR / KEY / FOCUS / A11Y / PERFを設計基準とした。Alpha.2では常設基盤の偽スイッチ、Patcher defaultによってFullbright OFFでもONのままになる問題、外部Mod不在の要求ON表示、基盤ラベルの折返し、古い管理対象JARが更新されない問題、OneConfigと右Shiftの競合、閉じたInventory画面へ戻る親画面保持を修正。管理対象JAR更新はhash確認・Backup・disabled保持・失敗rollbackを行い、独自変更版と新しい版を保持する。右Ctrlを新しい既定キーとし、Mods → Ttro Client → Configを通常の代替入口とする。旧右Shift設定は勝手に上書きせず、PLAYに競合理由を表示する。照準のみONでHUD pollingが登録される構造も分離した。以前に修正した具体的問題は、連続編集の設定欠落、Escapeが一度で閉じない検索dialog、Vanillaの長いbutton texture崩れ、Individualが実個別配置ではない問題、HUD Editor toolbarの重なり、小型Contextの設定はみ出し、外部Mod不在でもONへできる表示、過剰な矩形draw call。

Webはlabel/aria-live/skip link/focus/reduced motion/forced colorsを扱う。実screen reader、Windows 125/150/200% DPI、Minecraft支援技術、全言語/全極小viewportはUNVERIFIED。詳細は `UI_DESIGN.md`。

## 11. UI最終案

常設navigation、Floating Dock、順序型Play Flowの3操作可能案を作成し、9軸で比較。常設navigationを操作速度・発見性・拡張性から選定した。評価は設計判断であり、人間の実測テストではない。

PLAY / TUNE / STYLE / LIBRARY + 常設Profile + 目的検索 + 機能一覧 + 選択Context。Mod cardを大量に並べる構成にはしない。ゲーム内は一覧→ContextとHUD Editor。Blur/particle/無意味な常時animationはない。

## 12. Build結果

JDK 8 / Gradle 2.14.1 / ForgeGradle 2.1による **clean build PASS**。compile APIも実行targetもForge 11.15.1.2318。MCP stable_22とForgeGradle reobfJarを使用。最終JAR SHA-256 `5b338f9172f039eb89745d2d4b7684351e13dc506815a2fef9b6e43e95249f8d`。Native / 外部基盤付きworld / benchmarkの実行JARはすべてこのhashと一致。

停止原因はJavaが環境proxyを使っていなかったこととtrust storeの設定。環境proxyと管理された証明書を指定して修復し、TLS検証は維持。productに環境固有設定を埋め込まない。旧1764 APIによるlocal_compileは過去の代替経路として区別。ゲーム/API/QA classを配布JARへ含めない。一般ユーザー環境のWindows buildはUNVERIFIED。

## 13. Performance結果

最終JAR、FPS制限なし、外部Patcher/OneConfigを維持した2組OFF/ONのsoftware GPU記録。固定配列でframeを記録。

| Window | Mode | FPS | 1% Low | p99 ms | 最大frame ms | Heap peak MiB |
|---|---|---:|---:|---:|---:|---:|
| 0 | OFF | 136.84 | 37.84 | 18.68 | 53.77 | 515.30 |
| 1 | ON | 142.44 | 34.88 | 17.35 | 87.07 | 522.96 |
| 2 | OFF | 125.47 | 22.61 | 32.26 | 96.33 | 519.61 |
| 3 | ON | 141.46 | 40.84 | 17.68 | 49.55 | 526.95 |

GCはJVM launch全体で69回、合計2639.13ms、最大614.67ms。mode別帰属は未計測。旧Alpha.1の60FPS試行で1% Lowが41.69→36.33へ悪化した記録も保持。時間変動とspikeがあり、**性能回帰なし・改善・Lunarより軽いとは判定しない**。

HeapはJVM使用heapであり、process全体RAMではない。実GPU/VRAM/Input-to-photon/他Client比較は未測定。性能release gateは未達。描画のみのCrosshairをHUD収集から分離し、HUD全OFFでtick listener未登録を実ゲームで確認したが、それだけでFPS優位を主張しない。

## 14. QA結果

Backend 15テストPASS、Web 32チェックPASS、最終JAR Native 18チェックPASS、外部基盤付きworld 27チェックPASS、Mouse Tweaks実入力9比較PASS。Resource Pack優先順位、typed Scoreboard/fallback、HeadFX preset/3D、HUD編集を検証。PNG完全性も検査。

Patcher/OneConfig起動成立。Mouse Delayは実local player aim、Mouse BindはRMBにbindしたInventory closeを検査。Static FOV/水中FOVは実Forge rendering event、Night Visionは変換済みrendererを検査。Fullbright/Fire/Input設定は実config fieldへの反映とOFF復帰を検査。Fullbrightの全world照明や全input bug、全配列、macOS対応を保証するものではない。公式Hypixel handler登録はofflineで、live handshake/party responseは未確認。

OneConfig cacheは公式URL/hashから取得し改変せず使用。autoUpdate=falseがクラスパス追加も省く検証用設定の誤りを修正。cacheを用いたローカル起動のPASSであり、fresh online更新/duplicate service/audio deviceは未確認。検証の画像を上流の非同期writerに任せず、Probeだけで同期framebuffer captureを行う。Probeはproduct JARに含まない。

正式Alpha.1の有効/無効Profile更新と独自版保持を実JARで検査し、失敗rollbackもテスト。

配布物 12チェックPASS。metadata/Java8/license/非同梱binary/mrpack/zipapp実起動/自動Mod配置/hashを検査。 Web PASSはscreen readerやWindows DPI、Minecraft支援技術のPASSではない。

## 15. 未確認・残件

優先: hardware GPUで同一scenarioの繰り返し → 20候補の既存Mod統合・license/競合評価 → 実Prism/Microsoft認証経路 → Hypixel規約/公式API/live gameplay → crafting/NBT/third-party GUI → Windows/DPI/accessibilityと他Mod回帰。

Lunarとの同条件機能/性能比較、完全な機能網羅、Clear View/Combat Awareness/Mistake Protectionの全統合、Lobby/Waitingを含むAdaptive Scoreboard、外部API由来Player level、Shader実描画、Pack更新のonline QAは未達。Pack変更は次回起動への適用。ゲーム中即時Pack reloadは未提供。今回のInput/FOVのPASSは検査した挙動に限定し、全OS・全keyboard layout・入力遅延の実測をPASSにしない。Patcher fresh online依存取得、update/duplicate detectionサービス、sound deviceはこの環境では未確認。

## 16. 配布物

`Ttro-Client-0.2.0-alpha.2-project.zip`（source/QA/資料）、`Ttro-Client-0.2.0-alpha.2.pyz`（Python 3.10+ Launcher）、`Ttro-Client-Competitive-1.8.9.mrpack`（Prism import用）、`ttro-client-1.8.9-0.2.0-alpha.2.jar`、`SHA256.json`。

mrpackはForge 1.8.9と内蔵Ttro Modを指定し、外部Patcher/OptiFine/ゲーム本体を同梱しない。Windows native EXEは生成・QA未確認のため含めない。LauncherはPrismで正規認証・Java 8・ゲーム本体を管理する。
