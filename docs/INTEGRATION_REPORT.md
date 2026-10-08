# Ttro Client 正式Repository統合報告

0.3.0-alpha.1 / Minecraft Java Edition 1.8.9 / Forge 11.15.1.2318 / Java 8。

## 監査と正本

正式Repository: https://github.com/totoro0419/TtroClient 。最初のAPI監査ではbranchなし・contentsなしの空Repositoryでした。PRのbaseを作るためREADMEだけをmainへbootstrapし、以後の実装は `integration/1.8.9-native-launcher-2026-10-09` に限定します。mainのbootstrap commit: `08c673f0cd5d0f876f91fe0ef6efda56ca25a380`。別Repositoryは作成していません。

最新保存済み旧成果はTtro-Client-0.2.0-alpha.2-project.zip。残っていた別workspaceのalpha.1とは162箇所の差分があり、alpha.2を統合の基準にしました。保存済みJARはSHA256 `5b338f9172f039eb89745d2d4b7684351e13dc506815a2fef9b6e43e95249f8d`。現行Clientは標準ForgeGradleのclean buildで別途再生成しています。

| 判定 | 対象 |
|---|---|
| Repository側 | 空Repository。既存Client codeはなかった |
| ローカル／保存済み側 | Forge1.8.9 Core、Inventory、HUD、HeadFX、Scoreboard、Python Launcher、UI/テスト資産 |
| 古いもの | scratchのalpha.1、1.21.1/Fabric製品前提、旧Prism依存の完成品扱い |
| 再利用 | alpha.2の1.8.9 Client、Mouse Tweaks出典・操作、typed Catalog、branding、profile/content保護設計、UI3案・テスト |
| 再構築 | 製品LauncherをWPF/CmlLib/MSALに変更。Prism/Pythonは製品起動経路から除外 |
| 保管 | 過去QAはresearch/archive-alpha2へ。invalidだった旧package-build.jsonは証拠に採用しない |

## Architecture / 機能

詳しくはARCHITECTURE.mdとMODULE_CATALOG.md。Clientは1.8.9専用の単一Forge Mod。Launcher UI / services / Catalog / external config mapping / website / tests / CIを分離。1.21.1やFabricの互換adapterはありません。

Catalog62項目: **内蔵22 / 外部統合21 / 未実装候補19**。外部統合数は独立Mod数ではなくpublic configへの操作数です。Lunar級の網羅性はまだ未達です。候補を実装済みと表示しません。

既存Mod: Mouse Tweaks2.6.2（BSD-3）をsource統合。Patcher1.10.4（CC-BY-NC-SA4.0）とHypixel Mod API1.0.2（MIT）はoriginalからdownload、同梱なし。PatcherのOneConfig依存も同梱なし。OptiFineは自動再配布しない。MWE/Hytilities等はlicenseと機能調査済み候補で採用済みとはしない。LauncherはCmlLib3libraries＋MSAL/XboxAuthNetを利用し、full noticesをpackageへ同梱。

独自機能: HeadFX、Adaptive HUD clusters＋従来型配置、厳密BedWars Smart Scoreboard＋fallback、Modern Tab＋fallback、Player Identity表示、Context設定UI、Profile/Content管理。Modern Tabはserverの正常なdisplay/team/ping情報のみ。未知のrank/levelを作らない。

## 今回の独立検証

| 対象 | 確認結果と範囲 |
|---|---|
| Client clean build | PASS。JDK8 / ForgeGradle2.1 / Gradle2.14.1、Java class major52、reobfJar。research/build-result.json |
| Python既存logic | 15 tests PASS。native Windows実行の代用ではない |
| Native core | 22 checks PASS。Profile/typed設定、collision、backup、pack順序、誤版、zip traversal、candidate/実行中edit阻止、update SHA/origin |
| Native WPF / win-x64 | buildとself-contained publish成功。Windows実行は未確認 |
| 旧Web UI | regression browser checks PASS。ネイティブUIの実機QAではない |
| Pages | Playwright10 checks PASS。幅320/390/768/1280、keyboard skip、未公開/通信失敗、prerelease/正規Releaseリンク |
| Windows package | NSIS Setup.exe、Portable.zip、Client JAR、checksums.txtを生成。最終再package/整合性確認は継続中 |
| Mouse Tweaks実操作 | alpha.2ではRobot実mouse9casesと公式2.6.2比較の記録あり。今回の1.8.9再実行は継続中 |
| HeadFX / HUD / Scoreboard / Pack | 過去QA記録あり。今回の実ゲーム再確認は継続中 |
| Patcher追加13項目 / Modern Tab | standard build成功。実field/描画QAは継続中 |
| Performance | 過去ソフトウェアGPU測定は改善・無回帰を証明しなかった。今回の同条件測定は未完了 |

## UI Skill

UI Implementation Quality Skill本体・pattern-selection・review-pipeline・runtime-qa・core/patterns/platforms rulesを使用。3案を比較して常設上部nav＋Contextを選択。IA、Navigation、visual hierarchy、状態分離、keyboard/focus、PerMonitorV2、high contrast、Resize、operation countを設計・実装へ適用しました。具体的な変更と未確認事項はUI_DESIGN.md。

Skillによる変更: 未公開DLをfake linkにしない、未知fileをRepairで上書きしない、実行中Profile編集阻止、成否のpersistent表示、provider不在と未実装を区別、MS認証未設定を明示。Windows keyboard/screenreader/DPI/text sizeの実機QAは未確認です。

## Launcher / 配布 / 残課題

Windows LauncherはMicrosoft Login、1.8.9/Forge/Java環境管理、Profile/RAM/JVM、PLAY、修復、modules、pack/mod import/search/install/preview/enable/reorder/delete、preset、log、SHA確認付きUpdate & Restartを実装。最初の準備で監査済みPatcher/Hypixel APIを取得します。すでにdisableしたModを強制で有効化しません。

専用Microsoft public-client application IDが未提供です。WindowsのMicrosoft Login→PLAYとfirst-run online dependency取得は未確認。認証が動く完成版としてRelease公開しません。

GitHub Pagesのsourceとmain-only deploy workflowを実装。予定URLは https://totoro0419.github.io/TtroClient/ 。mainへのレビュー統合とPages Settingsが必要で、公開済みとはしません。Releaseは手動workflowでdraft/prereleaseのみ。Release公開は未実施です。コード署名、差分content update、既存Python Profileの自動migration、hardware FPS/1%Low/Input latency、実Hypixel、scoreboard多言語、全Mod組合せは未完了。

## 実際の起動方法

開発Windows環境: Setup.exeを実行（またはPortable.zipを展開してTtroClient.exe）。メンテナーがTtro専用application IDを設定していればMicrosoft Login→Profile→PLAY。設定がない現alphaではログインが明示的なエラーとなるため、ゲーム起動まで一般ユーザーが完結できる完成品ではありません。

Client単体の開発QAは、正規Minecraft1.8.9＋Forge2318環境へ生成JARを入れる経路です。これは最終一般ユーザー導線ではありません。ゲーム内Right Ctrlで統一設定、CでZoom。hypixelではrestricted/uncertain機能をsession中制限し、BAN免除を断定しません。

branch / commit / PRと今回の最終QA結果は、Repository反映と再検証後にこの報告を更新します。
