# Ttro Client

Minecraft Java Edition **1.8.9 / Forge 11.15.1.2318 / Java 8**向けPvP Client。正式Repositoryは https://github.com/totoro0419/TtroClient 。現在は **0.3.0-alpha.1（統合開発版）**。Lunar級以上の完成・性能優位・全サーバーでの許可を証明した製品ではありません。

Windows製品経路は `TtroClient-Setup.exe → Ttro Client → Microsoft Login → Profile → PLAY`。WPF / .NET 8のネイティブLauncherを自己完結配布します。ユーザー側のPython、Prism、.NET手動インストールは不要です。Minecraft・Forge・JavaはLauncherが上流から取得します。世界・設定は `%LOCALAPPDATA%\TtroClient189\native\profiles` に分離します。

**公開版の認証設定:** [Microsoft Login登録・配布Gate手順](docs/MICROSOFT_AUTH_SETUP.md) に従い、Ttro専用のMicrosoft public-client applicationを登録し、GitHub Repository Variablesの `TTRO_MICROSOFT_CLIENT_ID` にApplication (client) IDを設定すると、Windows CIが配布Launcherへ埋め込みます。既存プレビューを後から修復する設定ではなく、再ビルドが必要です。Release Draft作成はGUID未設定のPortableを拒否します。

**現時点の起動制限:** Ttro Client専用Microsoft OAuth application IDは未提供です。登録済みの自社application IDを `launcher/native/Ttro.Launcher/launcher-settings.json` に製品設定する必要があります。Windows Microsoft認証からPLAYまでの実機検証も未完了です。認証できる完成品と誤認してこのalphaを配布しないでください。メンテナー用の環境変数は `TTRO_MICROSOFT_CLIENT_ID`。Mojangのapplication IDを流用しません。

内蔵22機能、Patcher等の外部設定統合21項目、未実装候補19項目。後二者を搭載済み22機能へ加算しません。Mouse Tweaks 2.6.2由来のInventory Controls、Adaptive HUD、HeadFX、Smart Scoreboard、Modern Tab、Identity UIを統一設定から操作できます。外部連携は対応Modが必要です。ゲーム内設定は **Right Ctrl**、ZoomはC、Toggle SprintはR。

Resource Pack / Forge ModのローカルImport、Modrinth 1.8.9検索・SHA512検証付きInstall、Pack順序・ON/OFF・Preview・DeleteとBackup、Profile、RAM/JVM、Performance Preset、ログ、環境修復、公式ReleaseのSHA256検証付きUpdate & Restartを実装。Contentの差分Updateと完全なインゲームLive Previewは未完了です。

## Build

ClientはJDK 8と標準ForgeGradleで `python tools/build.py`。Windows Launcherは.NET SDK 8で以下を実行します。

```powershell
dotnet run --project tests/native/Ttro.Launcher.Tests.csproj -c Release
dotnet restore launcher/native/Ttro.Launcher/Ttro.Launcher.csproj -r win-x64
python tools/native_notices.py
dotnet publish launcher/native/Ttro.Launcher/Ttro.Launcher.csproj -c Release -r win-x64 --self-contained true -o dist/portable
python tools/package_windows.py --makensis "C:\Program Files (x86)\NSIS\makensis.exe"
```

`dist/TtroClient-Setup.exe`、`TtroClient-Portable.zip`、Client JAR、`checksums.txt`を生成します。GitHub Actionsはbranch/PRでbuild・test・packageを実行し、Releaseは手動triggerからdraftだけを作ります。Pagesはmainにレビュー済み変更を統合した後に公開する設定です。

Python Launcher (`launcher/app.py`) は旧資産と開発検証用。正式Windows製品の認証・起動には使いません。旧alphaの検証結果は `research/archive-alpha2/` に保存し、今回の検証とは分離しています。

検証: Windows実インストール/アンインストール・native UI/keyboard操作、実Minecraft設定/HUD21 checks、Patcher付きLocal World57 checks、公式Mouse Tweaks9操作比較を確認。Microsoft Login→PLAYと実GPU性能gateは未達です。

詳細は [監査・統合・QA報告](docs/INTEGRATION_REPORT.md)、[Architecture](docs/ARCHITECTURE.md)、[Catalog](docs/MODULE_CATALOG.md)、[UI品質](docs/UI_DESIGN.md)、[配布手順](docs/RELEASE_PROCESS.md)。License: Ttro MIT、統合Mouse Tweaks BSD-3-Clause。第三者通知は [THIRD_PARTY.md](THIRD_PARTY.md)。Minecraft本体、Forge、OptiFine、Patcher、認証情報を同梱しません。
