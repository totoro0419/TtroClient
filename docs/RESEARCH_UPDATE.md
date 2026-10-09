# 0.3.0-alpha.1 採用判断の更新

確認日: 2026-10-09 JST。Minecraft 1.8.9向けの版と、プロジェクト全体の最新更新を区別する。広告のFPS値を測定結果に転用しない。

## 機能と既存資産

| 資産 | 今回の扱い | 根拠 / 未確認 |
|---|---|---|
| Mouse Tweaks 2.6.2 | 1.8.9公式sourceをBSD-3-Clause条件で統合 | [公式版](https://www.curseforge.com/minecraft/mc-mods/mouse-tweaks/files/2287384)、[source](https://github.com/YaLTeR/MouseTweaks/tree/minecraft-1.8.9)。現在の新Minecraft向け版を1.8.9へ流用しない |
| PolyPatcher 1.10.4 | 原配布元から固定版を取得。公開configを21項目へ統合 | [source/license](https://github.com/Polyfrost/PolyPatcher)、Modrinth version `iNjGeSxM`。CC-BY-NC-SA-4.0を一般的な無制限OSS扱いしない。コードやJARの再配布なし |
| Hypixel Mod API 1.0.2 | Forge 1.8.9版を固定取得し、Hello / Location / Party handlerを接続 | [公式](https://github.com/HypixelDev/ModAPI)、Modrinth version `VtDhN4ZW`。MIT。実Hypixel接続は未確認 |
| OneConfig | Patcherの依存。原配布元が提供するloader/runtimeを使用 | 製品packageには同梱しない。QA用公式cacheと初回online取得を区別する |
| MWE | input / visual / alert等の機能調査のみ | [source/license](https://github.com/Alexdoru/MWE)。独自非商用条件。コードやアルゴリズムをコピーしていない |
| Hytilities / Hytils Reborn | Hypixel QoLの候補 | [source](https://github.com/Polyfrost/Hytils-Reborn)、GPL-3.0。現行Ttroに採用済みとはしない |
| Lunar / Badlion | HUD、visual、input、inventory、server QoLの到達目標を比較 | [Lunar PvP](https://www.lunarclient.com/features/pvp)、[Badlion mods](https://www.badlion.net/minecraft-client)。proprietary。全現行機能の1.8.9対応・同等性は未証明 |
| OptiFine | 利用者が正規取得した版のimport候補 | [copyright](https://optifine.net/copyright)。同梱・自動配布なし |

設定fieldの存在確認と、画面上の効果確認は別のQA項目とする。Patcherで削除/分離された機能へ架空のconfig adapterを追加しない。Particle、ViewModel、old animation等の未実装候補は候補のまま表示する。

## APIの出所・Cache・Rate limit

現在のMod APIはMinecraft plugin messageによる公式protocol。Public HTTP APIとは別であり、毎frame HTTP requestを発行しない。Hello / Locationはevent受信時だけ更新。Party requestは利用者の明示操作時だけ送信し、上流APIのhandshakeと送信可否を尊重する。自動pollingはない。現alphaはParty情報のTTL cacheを持たず、ライブサーバーでの連打/切断再接続QAも未確認。この範囲は完成判定を保留する。

通常のPlayer Identity / Modern Tabはserverが送ったdisplay name、team prefix、latencyのみ。Scoreboardは200ms、Modern Tabは250msの描画cacheを持ち、disconnectで破棄する。ランクを名前色から推測せず、levelやnickの正体を作らない。

Public APIは現alphaでは未実装。将来の設計条件は以下。これは現在搭載している機能の説明ではない。

| 項目 | 設計条件 |
|---|---|
| 取得trigger | 利用者が対象Profileを開く時のみ。Tabの全員や戦績の継続pollingをしない |
| Cache key / TTL | UUID＋endpoint。Profile/stat表示は15分、未存在は60秒。sessionを越えた保存は規約の再確認が必要 |
| 重複排除 | 同じkeyのin-flight requestを共有。client render threadでHTTPを待たない |
| Rate limit | サービスに割り当てられた上限とresponse headerを優先。429時はRetry-Afterに従い、表示をstale/unavailableへ戻す |
| 認証 | 登録されたサービス側で管理。API keyをClientに埋め込まず、公開mod利用者へkey入力を求めない |
| UI | 通常server情報 / Mod API / Public APIを表示上区別。期限と取得失敗を隠さない |

[Hypixel Public API policy](https://developer.hypixel.net/policies/)は自動データ収集、継続stat tracking、nick解除、keyの扱いを制限している。固定TTLを規約の許可と読み替えず、公開前に利用目的ごとの審査が必要。[Allowed Modifications](https://support.hypixel.net/hc/en-us/articles/6472550754962-Hypixel-Allowed-Modifications)を参照し、Server Safetyの分類はBAN免除の保証ではない。

## Native Launcher

CmlLibのMinecraft / Forge / Microsoft認証librariesとMSALを使用。バージョンはcsprojと配布DEPENDENCIES.jsonに固定。Ttro専用public-client application IDが未登録/未設定のため、Microsoft Login→PLAYは未確認。既存他社LauncherのIDを借用して成功したように見せない。

QA remapperは[SpecialSource公式](https://github.com/md-5/SpecialSource)が示すMaven Centralの1.7.4 shaded版を使用し、SHA256 `bec6091d89d47fb7b504045d87b19dfd50b4f7bea15b33adf522848127f7cdd2`で照合する。開発QA専用で、製品には含めない。
