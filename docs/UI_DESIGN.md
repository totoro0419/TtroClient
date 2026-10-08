# Ttro Client UI設計と品質適用

使用Skill: UI Implementation Quality (`skill-6aa41c46022c8191981a612e9a5fe342`)。SKILL全文、pattern-selection / review-pipeline / conflict-resolution、runtime QAと該当Core / WEB / native適用規則を読んで適用した。単なる読み込みを完了の根拠にしていない。

対象ユーザーは1.8.9 PvPプレイヤー。主要タスクはProfile選択→起動、競技中の少数設定変更、Pack追加と順序変更。二次タスクはMod依存の確認、復元、外部連携の設定。

## 3案比較

`launcher/web/prototypes.html` で3案を切り替え、Profile変更・照準編集・コンテンツ検索を操作できる。実装機能とは切り離した保存しない試作。以下は構造を基にした設計評価で、ユーザーテストやベンチマークの実測値ではない。高/中/低はこの3案の相対評価。

| 評価軸 | 常設ナビゲーション | Floating Dock | 順序型Play Flow |
|---|---|---|---|
| 新規性 | 高: Profile常設とContext編集 | 高: 空間主体 | 中: 準備手順主体 |
| 操作速度 | 高: 直接到達 | 中: Dock発見に依存 | 低: ステップ移動 |
| 分かりやすさ | 高: 役割と場所が固定 | 中: 省略ラベル依存 | 高: 初回のみ |
| 情報密度 | 高: 一覧とContext | 中: 切替が多い | 低: 手順ごと |
| PvP中操作 | 高: 検索と少数設定へ直行 | 中: 隠れた機能 | 低: 毎回順序を辿る |
| 設定発見性 | 高: 目的検索とカテゴリ | 中 | 中 |
| 視線移動 | 中: 一覧→隣のContext | 低: Dock→中央が離れる | 高: 中央集中 |
| 拡張性 | 高: Catalog駆動 | 中: Dockに限界 | 低: 手順肥大 |
| 軽さ | 高: 表示先だけ描画 | 高: 同等の静的構造 | 高: 同等の静的構造 |

最終案は常設ナビゲーション。PLAY=起動とProfile、TUNE=PvP・Input Foundation・Inventory Controls、STYLE=HUDと独自描画、LIBRARY=Pack/Mod/Shader/Profile。大量のカードではなく機能名・ON/OFFの一覧と選択した機能のContextを使う。Ctrl+Kで目的検索。狭い画面では一覧とContextを縦に並べ、Minecraftでは単一一覧からContextへ進む。

## Skillの適用と修正

| 項目 | 適用基準 | 実装/検証 |
|---|---|---|
| 要件/IA/Navigation | PURPOSE.01/.03, NAV.01 | 主要タスク常設、Profile常設、4ワークスペース、目的検索 |
| Hierarchy/Interaction | DISCLOSURE.01, STATE.01/.02 | 一覧とContext、候補はdisabled、選択=amber、focus=blue。checkbox/select/dialogはWebネイティブ。提供Mod未導入なのにONへできる問題をavailability照合と導入案内へ修正 |
| データ保護 | DATA.01/.03 | atomic write、revision競合、連続編集の欠落修正、変更前Backup、復元もBackup |
| 状態/回復 | FEEDBACK.01, ERROR.01 | 保存中/失敗、Empty、検索Loading/通信Errorと再試行、候補・外部依存の区別 |
| Keyboard/Focus | KEY.01, FOCUS.01/.03 | Tab/Enter、Ctrl+K、Escape、dialogから復帰。search input内のEscapeが一度で閉じない問題を修正 |
| Accessibility | A11Y.03 | label、aria-live、nav、skip link、focus-visible、forced colors。ネイティブMinecraftにWeb ARIAを移植しない |
| Reflow/DPI | WEB適用規則 | 1280/768/390/320幅と200%相当縮小viewport。Minecraft長いボタンのVanillaテクスチャ崩れをFlatButton描画へ修正 |
| Performance | PERF.01 | モジュール毎イベント登録/解除、OFFリスナー停止、HeadFX OFFは定数判定だけ、HUDはtickキャッシュ、Scoreboard 200msキャッシュ、blur/particleなし |
| Prototype/QA | review-pipeline, runtime-qa | 3試作を操作、実Launcher操作テスト、実Minecraftスクリーンショット、修正後再実行 |

Web実行テスト結果は `research/ui-result.json`。これはスクリーンリーダー、人間による理解度評価、Windows 125/150/200% DPI、全言語、ネイティブMinecraftの支援技術対応をPASSとする根拠ではない。これらはUNVERIFIED。

`Individual` HUDを、12種のHUDごとの座標・倍率保存へ再構築した。Webの選択Contextで位置を変更でき、ゲーム内HUD Editorはdrag / Tab / Shift+Tab / arrow / +/- / Escapeを扱う。実Forge上のdrag、Tab→arrow、保存と復帰を追加検証。小型GUIで下部toolbarが初期HUDに重なる問題を2列初期配置へ修正した。通常renderは固定Entry配列を使い、文字列生成をtickに限定。Crosshair/HeadFXの矩形をMinecraft既存bufferでbatch化し、1項目だけのClusterは1行にまとめる。Context設定が小型画面で下にはみ出す問題にはページ切替とwheelを追加。実Windows DPI、スクリーンリーダー、ゲームパッド、全設定の極小viewport操作は未検証。


Alpha.2ではSTATE.01 / PATTERN.SWITCHに基づき、常設のMouse Delay/Bindを偽の切替スイッチから導入状態表示へ変更。任意のInventory Controlsは独立スイッチを維持。外部Mod不在時の要求設定と実際の利用可能性を区別し、Keyboard layoutはnative selectで編集・保存。Fullbright OFFがPatcher default=trueを復元してONのままになる問題を実際のfalse設定へ修正し、Reduced Fire OFFはVanillaの高さ/不透明度へ戻す。DATA.01/.03とFEEDBACK.01に沿って管理対象JAR更新にBackup・hash所有確認・disabled保持・失敗rollback・未知版保持・PLAY状態説明を追加。Coremodの互換分岐ではなく、公式外部Modの設定連携で実装。照準を描画のみのlistenerへ分離し、HUD OFFでtick収集が登録されないことを実ゲームで検査する。Web支援技術やWindows DPIの未検証範囲は引き続き保持する。

最終画面QAで基盤状態ラベルが固定36px列で縦に折り返す問題を発見し、内容幅のgrid列とnowrapへ修正。OneConfigとの既定キー衝突は右Ctrlへ分離し、Forge ModsのConfig入口を追加（NAV/KEYの到達性）。旧ユーザー設定は保持しPLAYで競合を説明する。実screen reader/Windows DPI/物理Right Ctrl入力の全OS検証は未実行。検証用入力はMinecraftの登録KeyBinding eventでTtro画面へ到達し、pointer/Tab/Escapeによる編集を実行した。


## 0.3.0-alpha.1 Windows再評価（今回）

今回もUI Implementation Quality Skill本体とpattern-selection、review-pipeline、runtime-qa、core/patterns/platforms規則を読んで適用した。3案のinteractive prototypeを維持し、同じ比較をnative用途で再評価。Floating DockはKeyboard・発見性、順序型Play Flowは再起動時の操作数で不利。常設PLAY/TUNE/STYLE/LIBRARY＋Contextを選択した。

| 評価項目 | 適用基準と変更 | 実行状態 |
|---|---|---|
| IA / Navigation | 一次task PLAYを常設、settingsはContext、contentはLibrary | 実装 |
| Visual hierarchy | light cream/green/yellow、起動CTAと進捗を優先、未実装候補を区別 | 実装 |
| Interaction / operation count | 設定switchは即時永続化、Profile選択1操作＋PLAY1操作（認証後） | 実装、Windows操作測定未完 |
| STATE.01 loading/error/empty | indeterminate progress＋cancel、消えないerror、空content/search説明 | native実装、web状態QA |
| ERROR.01 / DATA.01 | collision拒否、delete backup、未知schema保持、実行中editing禁止 | native regression22checks |
| KEY.01 / FOCUS.01 | 標準TabControl/ComboBox/ListBox/Button、automation name、visible native focus、Enter search / Escape preview | 実装、Windows keyboard/screenreader未確認 |
| DPI / Resize | PerMonitorV2、scroll/wrap、最小620x500、独立text-size QA計画 | 実装、Windows125/150/200%未確認 |
| Accessibility | System high-contrast resource、native accessible controls、status live region | 実装、実assistive tech未確認 |
| Consistency | game/launcher共通typed Catalog、foundationをfake toggleにしない | 実装 |
| Live Preview | crosshair形状/サイズ/gap、Pack thumbnail、in-game HUD direct manipulation | 実装。HeadFX previewはgame確認 |

Skillによる具体的変更: 成功するまでPLAY/Installの成功表示を出さない。設定の書き込み中とgame実行中にProfileを切替えさせない。修復が未知fileを上書きしない。配布ページはReleaseがなければDLを無効の状態にし、通信失敗と未公開を別の説明にする。MS認証未設定を黙ってoffline起動に置き換えない。

古いWeb prototypeのbrowser QAとネイティブWPFのbuildをWindows実機UI QAとして扱わない。Pagesは320/390/768/1280px reflow、keyboard skip、空Release、取得失敗、prerelease表示をPlaywrightで検証。


### Windows実行QAによる修正

GitHub ActionsのWindows desktopでnative Window、760×620 resize、OAuth未設定のerrorと復帰、Profile作成、設定保存、終了を実行。UIAで発見したCheckbox Click依存の保存欠落をChecked/Uncheckedへ変更し、支援技術経由の切替も保存されるよう修正した。OSへのキーボード入力によるSpace切替、Ctrl+K検索focusは再検証PASS。実行画面で一覧OFFに対してContext見出しONが残る不整合も発見し、保存後に同じstateを反映する修正と回帰チェックを追加した。

この結果は実行画面と操作・永続化を組み合わせた証拠であり、125/150/200% DPI、Windows text scaling、長文/多言語、スクリーンリーダー、physical mouse、実Microsoft accountのQAを代替しない。対応する証拠はresearch/current-windows-qa、現版の範囲はINTEGRATION_REPORT.mdへ記載する。
