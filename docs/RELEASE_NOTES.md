# Ttro Client 0.3.0-alpha.1 — integration development alpha

Minecraft Java Edition 1.8.9 / Forge 2318 / Java 8。

WPF native launcher、profile/content管理、official-release hash verification、Windows installer/portable packaging、Patcher public-config integration、Modern Tab fallbackとPagesを追加。

開発中です。Lunar級以上の機能・軽さを達成した完成版ではありません。専用Microsoft OAuth application IDの製品設定とWindows Login→PLAY実機QAが未完了です。既存Modの初回online依存ダウンロード、実Hypixel接続、全言語Scoreboard、hardware performance/input latencyも未確認。詳細はdocs/INTEGRATION_REPORT.md。

SHA256は同じReleaseのchecksums.txtを確認してください。コード署名は未導入。Server Safety Profileは使用許可・処罰されないことの保証ではありません。

## Content Library

- 1.8.9の画像付きDiscover、人気/更新順、providerのカテゴリ・resolution tag、検索、詳細、ローカルFavorites。
- 各カードのINSTALL一回でCurrent Profileへ検証・導入。新しいPackはON・最上位。
- InstalledでON/OFF・Pack優先順位・Preview・Remove・バックアップ。Local/Managedを区別。
- Managed Pack/Modの互換安定版UPDATE。SHA-512・archive検査、stage、transaction、失敗復旧。更新はON/OFFとPack順を維持。
- Forge 1.8.9 / Java 8、pin済み必須dependencyだけ安全に解決。曖昧な依存は拒否。
- 同一配布物に対する実所有Windows E2E記録がなければRelease workflowは拒否。

認証済み一般ユーザーE2E、Narrator、高DPI、実GPU、実HypixelのPASSを上記テストから推定していません。検証範囲はdocs/CONTENT_LIBRARY_REPORT.md。
