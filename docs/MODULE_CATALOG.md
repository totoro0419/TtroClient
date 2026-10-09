# Ttro Client 1.8.9 Module Catalog

0.3.0-alpha.1: 内蔵 22 / 外部連携 21 / 未実装候補 19。Catalog項目総数は62。外部連携の数は独立したMod JARの数ではなく公開設定の統合項目数です。候補はONにできません。

| Group | Module | Status | Provider | Safety | Note |
|---|---|---|---|---|---|
| HUD | FPS | implemented | Ttro Client | cosmetic |  |
| HUD | Ping | implemented | Ttro Client | cosmetic |  |
| HUD | CPS L/R | implemented | Ttro Client | cosmetic |  |
| HUD | Keystrokes | implemented | Ttro Client | cosmetic |  |
| HUD | BPS | implemented | Ttro Client | cosmetic |  |
| HUD | Coordinates | implemented | Ttro Client | cosmetic |  |
| HUD | Memory | implemented | Ttro Client | cosmetic |  |
| HUD | Armor & Durability | implemented | Ttro Client | cosmetic |  |
| HUD | Potion Effects | implemented | Ttro Client | cosmetic |  |
| HUD | Item Counters | implemented | Ttro Client | cosmetic |  |
| HUD | Clock | implemented | Ttro Client | cosmetic |  |
| HUD | Connection Quality | implemented | Ttro Client | cosmetic | サーバーから通知されたtablist latencyの変化を分析。実RTT測定とは異なります。 |
| HUD | Adaptive HUD | implemented | Ttro Client | cosmetic |  |
| PvP | Crosshair | implemented | Ttro Client | cosmetic | TargetはVanillaが指しているobjectMouseOverのみ。 |
| PvP | Zoom | implemented | Ttro Client | cosmetic | Cキーを押している間だけ表示倍率を変更。 |
| PvP | Toggle Sprint | implemented | Ttro Client | uncertain | 利用者のキー操作による切替。Hypixelでは許可を断定せず制限。 |
| PvP | Toggle Sneak | candidate | Ttro Client | uncertain |  |
| HUD | Combo Counter | candidate | Ttro Client | uncertain | AttackEntityEventは命中確定ではないため、クリック回数をComboと偽って表示しない。 |
| HUD | Reach Display | candidate | Ttro Client | restricted | Hypixel公式規約のplayer distance/rangeにより制限。 |
| PvP | Attack Cooldown representation | candidate | Ttro Client | cosmetic | 1.8.9には1.9式attack cooldownがない。攻撃速度を捏造しない。 |
| Original | HeadFX | implemented | Ttro Client | cosmetic | 1.8.9 skull metadata=3。Inventory/hotbarにのみ装飾。 Optional 3DはVanillaの3D skull modelを拡大・傾斜する表示。独自skin modelではありません。 |
| Original | Smart Scoreboard | implemented | Ttro Client | cosmetic | 既知の英語Bed Wars schemaを完全に解釈した場合だけ再構成。不明行はVanilla表示。 |
| Original | Player Identity Capsule | implemented | Ttro Client | cosmetic | 通常受信した名前とteam prefixのみ。外部レベル未取得・推測しない。 |
| Inventory | Inventory Controls | implemented | Mouse Tweaks 2.6.2 / BSD | uncertain | 1.8.9版ソースを保持。独立ON/OFFと方向設定だけ拡張。Hypixelでは安全側に制限。 |
| Input Foundation | Mouse Delay Fix | external | PolyPatcher | review | PolyPatcher 1.10.4の常設Mixin修正。基盤として導入時に有効。個別ON/OFFは提供しません。 |
| Input Foundation | Mouse Bind Fix | external | PolyPatcher | review | PolyPatcher 1.10.4の常設Mixin修正。基盤として導入時に有効。個別ON/OFFは提供しません。 |
| Input Foundation | Input Fix / Foreign Keyboard | external | PolyPatcher | review | GUIを閉じた後のキー再登録はPatcherへ委譲。macOSでは上流のLWJGL制限によりキー再登録は非対応。Foreign Keyboard設定はLinuxのみ。OFFは再登録OFF/QWERTYへ戻します。 |
| Input Foundation | Hotbar Scroll Overflow Fix | external | PolyPatcher | cosmetic | PolyPatcherのpreventOverflowHotbarScrollingを統一設定へ接続。 |
| Clear View | Fullbright | external | PolyPatcher | cosmetic | Ttroがこの設定を管理。OFFはPatcherの初期値によらず通常照明へ戻します。 |
| Clear View | Reduced Fire | external | PolyPatcher | cosmetic | Ttroが火の高さ・不透明度を管理。OFFはVanillaの高さ/100%へ戻します。 |
| Clear View | Time Changer | candidate | existing OSS / review | cosmetic | PolyPatcher 1.10.4の公開設定に該当機能はありません。別の1.8.9資産を調査して統合します。 |
| Clear View | Weather | candidate | existing OSS / review | cosmetic | PolyPatcher 1.10.4の公開設定に該当機能はありません。別の1.8.9資産を調査して統合します。 |
| Clear View | Static FOV | external | PolyPatcher | cosmetic | PatcherのSprint/Flying/Bow/Speed/Slowness倍率を0へ設定。OFFはVanilla変化へ戻します。Zoomとの併用を実ゲームで検証します。 |
| Clear View | Hit Color | candidate | MWE | cosmetic |  |
| Clear View | ViewModel | candidate | existing OSS / review | review |  |
| Clear View | Block Overlay | candidate | existing OSS / review | cosmetic |  |
| Clear View | Hurt Camera | candidate | existing OSS / review | review |  |
| Clear View | Particle Controls | candidate | PolyPatcher / MWE | review | PolyPatcher 1.10.4の粒子設定は上流でOverflowParticlesへ分離。古い互換フィールドに値を入れて実装済みとは扱いません。 |
| Clear View | 1.7 Animations | candidate | Sk1er OldAnimations / LGPL-3.0 | cosmetic |  |
| PvP | Freelook / Perspective | candidate | existing OSS / review | restricted |  |
| Visual | Nick Hider | candidate | existing OSS / review | cosmetic |  |
| Awareness | Damage Tint | candidate | MWE / review | cosmetic |  |
| Awareness | Low HP Alert | candidate | MWE / review | cosmetic |  |
| Awareness | Durability Warning | candidate | MWE / review | cosmetic |  |
| Awareness | Potion Expiry Warning | candidate | MWE / review | cosmetic |  |
| Protection | Sword / Important Item Drop Protection | candidate | MWE | uncertain |  |
| Server | Hypixel Server / Game / Party | implemented | Hypixel Official Mod API / MIT | cosmetic | 公式Mod API導入時だけ有効。Party要求はユーザー操作時のみ。 |
| Clear View | Water FOV Cleanup | external | PolyPatcher | cosmetic | 水中のFOV変化をPatcherで軽減。OFFは水中のVanilla FOVへ戻します。 |
| Clear View | Night Vision Cleanup | external | PolyPatcher | cosmetic | PatcherでNight Vision終了前の点滅を穏やかな減衰へ変更。Potionそのものは保持します。 |
| QoL | Disable Enchantment Glint | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のdisableEnchantmentGlintを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Smooth Scrolling | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のsmoothScrollingを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Numerical Enchants | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のnumericalEnchantsを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Chat Timestamps | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のtimestampsを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Smart Disconnect | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のsmartDisconnectを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Confirm Quit | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のconfirmQuitを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Tab Hat Layers | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のlayersInTabを利用。実設定連携と表示の検証は個別に記録します。 |
| Performance | Pack Preview Downscale | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のdownscalePackImagesを利用。実設定連携と表示の検証は個別に記録します。 |
| Performance | Font Cache | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のcacheFontDataを利用。実設定連携と表示の検証は個別に記録します。 |
| Performance | Model Rendering Batch | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のbatchModelRenderingを利用。実設定連携と表示の検証は個別に記録します。 |
| Performance | Forge Entrypoint Cache | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のcacheEntrypointsを利用。実設定連携と表示の検証は個別に記録します。 |
| QoL | Windowed Fullscreen | external | PolyPatcher | cosmetic | PolyPatcher 1.10.4のwindowedFullscreenを利用。実設定連携と表示の検証は個別に記録します。 |
| Server | Modern Tab / Player List | implemented | Ttro Client | cosmetic | 通常のtablist display name・team prefix・通知pingのみ。ScoreObjective・header/footer付き・80人超はVanillaへ戻す。外部APIからlevelやrankを推測しません。 |
