# Known Issues — 0.3.0-alpha.1

Status: **Development Alpha / public download blocked** (2026-10-09).

## Release blockers
- Ttro Client専用Microsoft public-client Application IDが未設定。実所有アカウントのMicrosoft Login → PLAYは未実行。
- First-runのMinecraft 1.8.9/Forge/Java/Patcher/Mod API自動取得をWindows一般ユーザー環境で未検証。
- Fresh SetupからLocal World、終了、再起動、再PLAY、Repair、Update、Uninstallまでの一連の実機QA未達。
- 正式mainとGitHub Pages配布ページへの統合前。Public GitHub Releaseなし。

## Alpha制限・未検証
- 性能測定はMesa software renderer中心で、実GPUのframetime/input優位性は証明していない。
- Hypixel live runtime、実サーバー許容可否は未検証。各サーバーのルールを確認すること。
- 19候補機能は未実装。未実装を完成済みとは表示しない。
- Windows Code Signingなし。SmartScreen警告の可能性。
- 125/150/200% DPI、screen reader、物理マウス、全言語のScoreboard、追加Mod GUI互換は未検証。
- Contentの差分Updateと旧Python Profile自動移行は未実装。
- 複数Installer版のWindows registry/Uninstall管理と旧版削除は不完全。保存済みユーザーデータ保護を優先。
- 依存Modやオンライン外部データの取得に失敗すると、UI表示や一部連携が利用できない場合がある。

根拠・QAの範囲: [INTEGRATION_REPORT](INTEGRATION_REPORT.md)、[RELEASE_PROCESS](RELEASE_PROCESS.md)。未確認はPASSではありません。