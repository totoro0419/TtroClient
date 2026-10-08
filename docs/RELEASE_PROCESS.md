# Windows alpha / Pages / Release

Canonical: https://github.com/totoro0419/TtroClient 。別Repositoryを使いません。

1. 専用integration branchのCIを通し、PRでClient・Launcher・License・QAをレビューする。
2. メンテナーがTtro用Microsoft public-client applicationを登録し、正規Minecraftサービスのアクセス要件を確認する。製品の`microsoftClientId`に公開application IDを設定する（client secretを生成・埋め込みしない）。この情報は現時点で未提供。
3. WindowsでSetup/Portable、Microsoft Login、所有アカウント、PLAY、Java/Forge初回ダウンロード、Local World、Module/Pack/Profile/Repair/Updateを検証する。キャンセル、通信失敗、125/150/200% DPI、文字サイズ、画面Resize、高コントラスト、Keyboard/Screen Readerを含む。
4. 正常終了した`ci.yml`のrun IDと同じcommitを指定し、`release-draft.yml`を手動実行する。SHA256確認後、**draft/prerelease**のみを作る。タグpushや通常commitはReleaseを公開しない。
5. Release notesで未確認・既知問題を明記してメンテナーが公開する。Setup.exe、Portable.zip、checksums.txt、1.8.9 Client JARが必要。コード署名は未導入。
6. Pages SettingsでSourceをGitHub Actionsに設定し、レビュー後mainへ統合。`pages.yml`が`website/`のみを配布する。巨大EXEをPagesへ置かない。

Installerは管理者権限不要のユーザー単位。versionごとのアプリdirectoryを使い、走っているLauncherのDLLを上書きしない。Update & Restartは同じ正式RepositoryのReleasesのみを見てSHA256を確認する。UninstallはProfile/worldを消さない。過去版directoryの自動削除と複数版Uninstallerの管理は今後の改善項目。

自動artifact生成は公開Releaseや実機QA成功と同義ではない。OAuth設定やWindows QAが未完了のbuildを一般利用可能な完成版として公開しない。
