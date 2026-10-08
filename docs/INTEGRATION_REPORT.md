# Ttro Client 正式Repository統合・QA報告

2026-10-09 JST / **0.3.0-alpha.1（統合開発版）** / Minecraft Java Edition **1.8.9** / Forge **11.15.1.2318** / Java **8**。

Windows専用Launcherと実インストーラーを実装し、正式RepositoryのDraft PRへ統合した。一般ユーザーのMicrosoft Login→PLAY、Lunar級の網羅性、性能優位を達成した完成版ではない。

## 最終成果20項目

| # | 項目 | 状態・成果 |
|---|---|---|
| 1 | Repository branch | 正本は [totoro0419/TtroClient](https://github.com/totoro0419/TtroClient)。`integration/1.8.9-native-launcher-2026-10-09`。別Repositoryなし |
| 2 | Commit | 製品コード・QA実行経路の対象: `28fe0d31bc474547cea08c5547b9f8aec6659cb0`。以後の報告/QA集約変更はPR履歴を参照 |
| 3 | PR | [Draft PR #1](https://github.com/totoro0419/TtroClient/pull/1)。mainへ製品コードを直接push/mergeしていない |
| 4 | 再利用した旧実装 | 保存済みalpha.2のForge 1.8.9 Core、Mouse Tweaks source、HUD/HeadFX/Scoreboard、typed Catalog、branding、Profile/Content保護設計、UI3案・テスト |
| 5 | 廃棄/再構築 | 1.21.1/Fabricを最終製品とする前提を撤去。製品LauncherをWPF/CmlLib/MSALへ再設計。Python/Prismは最終起動経路から除外。旧Pythonはreference/logic regression用途 |
| 6 | Architecture | `forge/`は1.8.9専用Core＋機能別class＋上流Mouse Tweaks。`launcher/native/`はWPF presentationと独立Core services。Catalog、external config mapping、website、tests、CIを分離。[ARCHITECTURE.md](ARCHITECTURE.md) |
| 7 | PvP Modules数 | Catalog **62項目: 内蔵22 / 外部設定統合21 / 未実装候補19**。外部21は独立Mod数ではない。基盤2項目は常設fix。候補を搭載済みとして加算しない。[MODULE_CATALOG.md](MODULE_CATALOG.md) |
| 8 | 既存Mod | Mouse Tweaks2.6.2 BSD-3-Clauseをsource統合。Patcher1.10.4 CC-BY-NC-SA-4.0とHypixel Mod API1.0.2 MITは原配布元から取得、JAR同梱なし。OneConfigはPatcher依存。OptiFine同梱/自動再配布なし。MWE/Hytils等は候補 |
| 9 | 独自機能 | HeadFX、Adaptive HUDとIndividual配置、Smart Scoreboardとfallback、Modern Tabとfallback、Player Identity、Context設定UI、Profile/Content管理 |
| 10 | UI Skill | **UI Implementation Quality**本体・engines・core/pattern/platform rulesを適用。常設上部nav、Floating Dock、順序型Play Flowの3案を比較。常設PLAY/TUNE/STYLE/LIBRARY＋Contextを採用。Windows操作・状態・保存の欠陥を実QAで修正。[UI_DESIGN.md](UI_DESIGN.md) |
| 11 | Mouse Tweaks QA | **9操作比較PASS**。実Forge GuiChestへAWT RobotでOSマウス入力。45slotとcursor個数が公式2.6.2単体と一致。人間の物理マウス、NBT identity、crafting output、他Mod GUI、live serverは未確認 |
| 12 | HeadFX QA | Local WorldでCompetitive/Clean/Glow/OFF/Optional 3Dを表示・captureし、画面を検査。Vanilla skullの拡大/傾斜を使う3Dで、独自skin modelではない。Inventory操作を伴う一連の起動でcrashなし。全GPU・全skin・全item renderer互換は未確認 |
| 13 | Launcher | .NET8 WPF native。Profile、RAM/JVM、Java/Forge管理、PLAY、Repair、Mod/Pack管理、preset、log、hash検証付きUpdate & Restartを実装。Microsoft認証codeは実装済みだが専用application ID未設定、Login→PLAY未確認 |
| 14 | Windows exe | Setup.exeとPortable.zipをclean CIで生成。Windows runnerでnative UI操作、Space/Ctrl+K、実インストール→起動→アンインストール、Profile保持を確認。コード署名・SmartScreen・一般ユーザーPC・DPI全範囲は未確認 |
| 15 | Performance | 同一processの選択Module OFF/ONを2組測定。software GPUの結果は下表。**性能回帰なし/改善/Lunarより軽いとは判定しない**。実GPU、総RAM/CPU、input-to-photon、起動時間、Vanilla比較は未測定 |
| 16 | GitHub Pages | [公式URL](https://totoro0419.github.io/TtroClient/)はHTTP200、現在はmain bootstrap READMEのJekyllページ。設計したDLページとmain-only workflowはPR上、まだ公開されていない |
| 17 | Release | 公開Releaseは**0件**。手動trigger・同一commitの成功CI・SHA256確認・draft/prerelease限定workflowを準備。一般向け公開なし |
| 18 | 未確認項目 | Microsoft所有アカウント認証→PLAY、初回Java/Forge/Patcher/OneConfig online導線、Repair/Update & RestartのWindows end-to-end、実Hypixel、DPI/text scaling/screenreader、hardware performance、全Mod組合せ |
| 19 | 既知の問題 | 19候補未実装、Contentの差分Update、旧Python Profile自動migration、汎用keybind editor、全言語Scoreboard、複数旧版installerのregistry/cleanup、OneConfig初回通知と外部UIの残存。QA環境ではaudio device/online skin/外部metadata取得失敗あり |
| 20 | 実際の起動方法 | 開発Windows版: Setup.exeを実行、またはPortable.zipを展開してTtroClient.exe。専用application ID設定済みbuildならMicrosoft Login→Profile→PLAY。現配布previewはID未設定のため認証エラーを表示し、ゲームまで一般ユーザーが完結できる完成品ではない |

## 監査の根拠

最初のRepository API監査ではbranchなし・contentsなしの空Repositoryだった。PRのbaseを作るためREADMEだけをmainへbootstrapしたcommitは `08c673f0cd5d0f876f91fe0ef6efda56ca25a380`。正常な既存Client履歴を上書きしたものではない。

旧保存済み成果はTtro-Client-0.2.0-alpha.2-project.zip。残っていたalpha.1 workspaceとは162箇所の差分があり、alpha.2を統合の基準にした。旧JAR SHA256は `5b338f9172f039eb89745d2d4b7684351e13dc506815a2fef9b6e43e95249f8d`。旧QAは`research/archive-alpha2`に保存し、現在の証拠と分離した。

## 実行した検証

| 検証 | 結果 / 範囲 |
|---|---|
| Client clean build | JDK8＋標準ForgeGradle2.1/Gradle2.14.1のbuild・reobfJar PASS。Java class major52、ゲーム/API/QA classの非同梱確認 |
| Native core | **22 checks PASS**。Profile保存、schema保護、typed設定、Pack順序、collision/backup、zip traversal、誤版/Fabric/二重Mouse Tweaks拒否、update origin/hash |
| Python reference | **15 tests PASS**。Windows nativeの代用にしない |
| Website | **10 checks PASS**。320/390/768/1280px、keyboard skip、空Release/通信失敗、prerelease、正規asset/checksum URL |
| Windows native | **13 checks PASS**。UI automationとOS keyboard input。Profile作成、Space toggle保存、Ctrl+K focus、error recovery、Resize、終了。Setup実インストール後も同じ操作、uninstallでProfile保持 |
| Minecraft native UI | **21 checks PASS**。Ttro load、native Module pointer ON/OFF、Context/Escape、HUD drag/Tab/arrow/保存、Modern Tabのsupported/fallback条件、公式API handler登録offline |
| Patcher付きLocal World | **57 checks PASS＋9 inventory captures**。Patcher/OneConfig load、追加13設定とscroll fix ON/OFF、input/FOV/nightvision、Pack優先順位、Scoreboard意味解析/fallback |
| Mouse Tweaks reference | 公式file ID2287384、JAR SHA256 `5ac93656e71f1a7f38327b70e508249932d2d076ef139635dd6dcad502b21faa`。RMB Drag/LMB Collect/Shift Drag/wheel両方向/search両方向/scroll reverseの9比較が一致 |
| Runtime bytes | 3本のlocal Ttro QAはpayload SHA256 `1145c4680e46dd59927aa6155d9046576e09e69f18209989e93439ef8bac0f63`と一致。CIもWindows packageと同じclient artifactをgame QAへ渡し、API復元buildで上書きしない |

Windows QAでCheckBoxのClick eventだけに依存した保存欠落を見つけ、Checked/Uncheckedへ変更した。実行画面で一覧OFFに対してContext見出しONが残る問題を見つけ、状態表示の更新と回帰検査を追加した。NSISのWindows wildcard path、quote、子uninstaller終了待ちも実CI failureに基づいて修正した。

## Performanceの実測と限界

Local Xvfb/Mesa software renderer、960×540、flat world seed12345、Java8 Xmx1024m、FPS制限なし。package圧縮処理終了後の隔離した試行。Patcherはこのbenchmarkに未導入。各窓で最初の100tickを除外。1% Lowは遅い1%の平均frametimeの逆数。

| Window | Mode | Samples | Average FPS | 1% Low | p99 ms | 最大frame ms | >50ms spikes | 使用Heap peak MiB |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | OFF | 1743 | 173.52 | 56.75 | 13.35 | 29.64 | 0 | 459.40 |
| 1 | ON | 1782 | 178.17 | 68.30 | 11.78 | 25.76 | 0 | 474.50 |
| 2 | OFF | 1889 | 188.93 | 69.85 | 10.68 | 48.36 | 0 | 488.37 |
| 3 | ON | 1797 | 179.67 | 68.16 | 10.42 | 51.85 | 1 | 486.28 |

起動全体のGC: 21回、合計513.55ms、最大156.03ms。modeごとの帰属は未計測。測定順序、JIT/GC、共有実行環境の変動を排除できず、実GPUでもないため、性能gateは未達。以前の圧縮処理と重なった試行も`performance-alpha3.json`に保持し、良い結果だけを採用しない。詳細は`research/qa-summary-alpha3.json`。

CI配布previewのClient SHA256は `3a80d91bc3f13fbcf6245a10989b5e171ef6ad426749b0ae8efd5b0a83b157a4`。local QAのJARとはZIP metadataでbyte hashが異なるが、59 entryすべての名前と内容が一致することを別途確認した。CIでは配布と同じbytesをゲームで検証する。assetごとのhashと出所は`research/package-provenance-alpha3.json`。

## 配布と残りの完成条件

配布previewにはMinecraft本体、Forge、Java、Patcher、OptiFine、private QA runtimeを同梱しない。上流library/runtime licenseと依存一覧を含める。GitHub Actionsのartifact生成はRelease公開や認証成功と同義ではない。

専用Microsoft public-client applicationの登録とMinecraftサービスの利用要件を満たした設定が必要。client secretや他社のIDを埋め込まない。設定後は所有アカウントで初回Install→Login→PLAY→Local Worldを実行し、WindowsでRepair/Update、DPI/入力、hardware regressionを確認する。

ゲーム内Right Ctrlで統一設定、CでZoom、RでToggle Sprint。Hypixelではrestricted/uncertain項目をsession中制限する。Modern Tabは通常のserver display/team/pingのみを使い、外部rank/levelを推測しない。Smart Scoreboardは既知英語Bed Wars schemaを厳密に認識できない場合Vanillaへ戻す。
