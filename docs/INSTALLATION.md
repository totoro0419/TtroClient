# Ttro Client インストール案内 — Public Alpha（公開準備中）

## 一般ユーザー向け

**2026-10-09時点ではPublic Alphaの正式Releaseは未公開です。** CIのテスト用Setupを一般向け製品として配布していません。正式公開後は以下の導線になります。

1. [公式サイト](https://totoro0419.github.io/TtroClient/)を開く。
2. **Download for Windows**でTtroClient-Setup.exeをGitHub Releasesからダウンロード。公開前はリンクが無効で正常です。
3. 対応するReleaseにある`checksums.txt` とPowerShellの `Get-FileHash .\TtroClient-Setup.exe -Algorithm SHA256` を照合する。
4. Setup.exeを実行し、Start Menuから **Ttro Client** を開く。
5. **Microsoft Login** でMinecraft Java Editionの所有アカウントへ正規ログイン、Profileを選択、**PLAY**。
6. 初回のみMinecraft Java 1.8.9・Forge 11.15.1.2318・Java・必要なlibraryの取得をLauncherで実行。ローカル環境・ネットワーク・配布条件により失敗する可能性があり、初回導線は実アカウントで未検証です。

Prism Launcher、Python、手動Forge/JAR導入は製品の標準導線ではありません。Minecraftを所有していない場合、ログイン回避の方法は提供しません。

## 対応・留意事項

Windows x64向けの.NET 8自己完結WPF Launcherです。Minecraft 1.8.9はForgeとJava 8を使用します。Windows Code Signingは未実装で、SmartScreen警告があり得ます。出所とSHA256を確認してください。信頼できない警告画面で無条件に実行しないでください。

Update & Restartは公式ReleaseのSetup.exeとSHA256を検証します。Repairは管理ファイルを対象とし、未知のユーザーMods/Resource Packs/Worldsを勝手に消しません。既知の問題は [KNOWN_ISSUES](KNOWN_ISSUES.md)へ。

UninstallはインストールしたversionのアプリケーションDirectoryを削除します。Profile、World、Screenshots、Resource Packs、ユーザー設定は `%LOCALAPPDATA%\TtroClient189\native\` 側に保持する設計です。複数版のUninstallerと旧版Directory整理は現在の既知の問題です。
## Resource Packs / Mods

LauncherのLIBRARY → Resource Packs / Mods → DISCOVERへ。検索なしで人気順を表示します。画像を見てカードのINSTALLを一回押すと、現在のProfileへ検証して追加します。新しいPackは有効・最上位で、次のPLAY時に反映されます。INSTALLEDではON/OFF、順序、Preview、Check updates → UPDATE、Removeを操作できます。UPDATEは既存の有効状態と順序を維持します。Local Importはprovider更新対象になりません。ゲーム実行中は変更できません。
