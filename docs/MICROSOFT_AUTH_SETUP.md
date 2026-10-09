# Ttro Client — Microsoft Login release setup

対象: Windows WPF/.NET 8 Launcher / Minecraft Java Edition 1.8.9 / **Public Alpha**。この文書はアプリ登録とパッケージ設定の手順であり、実アカウント認証のPASS記録ではありません。

## Microsoft公式登録（メンテナー操作）

1. [Microsoft Entra 管理センターのアプリ登録](https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)を開き、`Ttro Client` を**新規**登録します。Minecraftユーザー向けに **Personal Microsoft accounts** を含む supported account types を選択します。Microsoft公式: [登録手順](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)。
2. **Authentication → Add a platform → Mobile and desktop applications** にて、public clientのsystem browser用redirect URI **`http://localhost`** を登録します。選択したOAuth provider / MSALの実際のredirectをWindows Login QAで照合します。[Microsoft desktop configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)。
3. Overviewに表示される **Application (client) ID** を取得します。パスワードやclient secretは作成・配布しません。Desktopはpublic clientです。[Microsoft public client仕様](https://learn.microsoft.com/en-us/entra/identity-platform/msal-client-applications)。
4. 正式Repository [Settings → Secrets and variables → Actions → Variables](https://github.com/totoro0419/TtroClient/settings/variables/actions) で、`TTRO_MICROSOFT_CLIENT_ID` にこの公開Application ID（GUID）を登録します。※これはGitHub Secretではなく公開識別子ですが、ID以外の認証情報を登録しないでください。
5. 対象commitのCIを再実行**または新しいcommitで新たにWindows package**を生成します。Actionsの`windows` jobは配布時の `launcher-settings.json` にIDを組み込みます。リポジトリ内の空欄の雛形は維持します。前のCI artifactにIDは遡及しません。

## ID設定後の最短QA

1. 新しい成功CIのWindows artifactを展開し、Setup.exe・Portable.zip・checksumsを同じfolderへ置く。GUID設定前のartifactを使わない。
2. WindowsでFresh Setup → Microsoft Login → PLAY → Ttro設定 → Local World。LIBRARYの実provider Pack導入とゲーム反映、Mod、終了→再PLAY、Repair、Uninstall保持を実確認する。
3. `pwsh tools/record_owner_qa.ps1 -Assets <artifact展開folder>` を実行。実施済み項目だけYESと答える。JSONはTokenを含まない。
4. 同じsourceの **Owner E2E evidence for exact Windows build** を実行し、`run_id`へCI ID、`evidence_json`へ生成JSONを入力する。Release draftにはCI IDとowner evidenceのrun IDが両方必要。source SHA・Setup/Portable SHA256・公開IDが一致しない証拠を拒否する。

Launcherは専用MSAL public client・system browser・暗号化キャッシュを使用し、Loginと毎PLAYのrefreshでJE profileとMinecraft entitlementの両方を要求します。ライブラリの既定のownership-check OFFに依存しません。ただしコードとbuild成功は実アカウントの成功証拠ではありません。Microsoft/Minecraft側で利用申請が必要な場合はownerが対応し、成功を確認するまで公開しません。

## 明示的なRelease gate

- `release-draft.yml` はmainからの **手動 workflow_dispatch** 専用です。`run_id` にはmain上の同一commitから成功したpush CI実行を指定します。タグは `launcher/product.json` のversionに一致させます。
- `TtroClient-Portable.zip` 内の `launcher-settings.json` のGUID非空を検査し、セットアップ生成物とclient JARのSHA256を検証します。
- **GUIDがあってもログイン成功は証明されません。** Minecraftの所有確認・Xbox/Game OAuth権限の利用可否は実アカウントで確認が必要です。プロバイダー側の追加承認・制限がある場合はそれに従います。
- 実機でFresh Setup → Microsoft Login → ownership → PLAY → 1.8.9 → Local World → Restart → PLAY → Repair → Uninstallを通すまでRelease公開を保留します。手動で作るReleaseは**draft/prerelease**に限定します。

## 安全上の注意

ユーザーのMicrosoft password、access token、refresh tokenをIssue、GitHub Actionsログ、スクリーンショット、コミットへ載せないこと。LauncherはMSALキャッシュを使い、ゲームのaccess tokenを通常ログに記録しない設計です。ログ・認証エラーも実機QAで再監査してください。

## 現状（2026-10-09）

成功CI 37907966762の実PortableもIDが空欄と再確認済みです。実機Microsoft Login → PLAYは未確認です。Public Alphaの公開判定は **BLOCKED**。