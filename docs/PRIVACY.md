# Ttro Client Privacy — Development Alpha

## アカウント

Ttro ClientはMicrosoftの正規ログインを使います。利用者のパスワードはTtro Clientに入力・保存させません。WPF Launcherの認証はMSAL／XboxAuthNet／CmlLib.Core系ライブラリを使用します。MSALの暗号化キャッシュはユーザーローカルのauth Directory、Minecraft用セッションはアプリ実行中のメモリで扱う設計です。実アカウントを使った認証のEnd-to-Endは未検証です。

Minecraftを起動する際のaccess tokenは、Launcher側で標準ゲームログへの直接出力をマスクする設計です。全ての依存ライブラリ・外部Modのログまで秘匿できると保証するものではありません。サポートへログを投稿する前に、認証情報、PCユーザー名、個人情報、サーバー情報を確認・削除してください。

## ネットワーク

初回導入時にMicrosoft/Xbox/Minecraft、Minecraft・Forge・Java等の正規配布元、追加コンテンツ取得のためのModrinthへアクセスする設計です。更新確認時にはGitHub Releases APIへアクセスします。任意の追加Modにも独自の通信がある場合があります。

Ttro Client独自の任意Telemetry・広告プロファイリング機能を今回新設していません。第三者の配布元・Microsoft等のプライバシーポリシーにはそのサービスの規定が適用されます。

## 保持・削除

LauncherのProfile、World、Resource Packs、Screenshot、設定、ログ等はユーザー環境内に保持されます。通常Uninstallではユーザーデータを自動削除しない設計です。データを削除する場合はバックアップを取得し、本人が保存先を確認してください。

安全性、個人情報、ログの不備を見つけた場合は [GitHub Issues](https://github.com/totoro0419/TtroClient/issues) に**実Tokenを掲載せず**報告してください。