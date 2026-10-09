# 1.21.1成果物監査

入力: PvP-Client-0.1.0-project.zip, Library version 2, modified 2026-10-08 07:11:21 UTC.

- 配布名だけでなくproduct.jsonもMinecraft PvP Clientを示し、正式名称は未承認と記録されていた。Ttro Clientへ正本を修正。
- fabric/build.gradleはMinecraft 1.21.1 / Yarn / Fabric / Java21専用。全Minecraft連携を置換。旧JARとmrpackは新配布物へ含めない。
- launcher/core.pyにもFabric metadata、loader constraint、Sodium/Iris、pack_format34が埋め込まれていた。Forge検査・1.8.9設定を別モジュールへ分離。
- 再利用: atomic_json、Profileのrevision照合、snapshot backup/restore、ハッシュ検証付きdownload、Content transaction、Prism認証への引き渡し、loopback/Origin/token保護。
- 再設計: Profile schema、Mod互換検査、Prism component、1.8.9 resource pack paths、shader設定、ゲーム内GUI/HUD/HeadFX、Module registry。
- 旧UI資産から明るい色調、主navigation、context editingの考えを保持。画面を新規構成。
- 旧research/のFPSやスクリーンショットは1.8.9の検証に転用しない。
- 旧データを自動で移動・上書きしない。新しいTtroClient189領域へ保存。
