"""Summarize current evidence; never certify a stale artifact or unexecuted gate."""
import hashlib,json,math,re,statistics
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];R=ROOT/'research';WORK=ROOT.parent
read=lambda p:json.loads(p.read_text())
def write(p,data):p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
product=read(ROOT/'launcher/product.json');index=read(R/'evidence-index.json');build=read(R/'build-result.json')
jar=ROOT/'launcher/payload'/(product['artifact']+'-'+product['version']+'.jar');sha=hashlib.sha256(jar.read_bytes()).hexdigest()
assert sha==build['sha256'] and build['forgegradle_clean_build']=='PASS'
native=read(R/'game-qa'/index['native']/'result.json');world=read(R/'game-qa'/index['world']/'result.json');bench=read(R/'game-qa'/index['benchmark']/'result.json');baseline=read(R/'game-qa'/index['baseline']/'result.json')
for result in [native,world,bench]:assert not any(c['status']=='FAIL' for c in result['checks'])
hashes={name:hashlib.sha256((WORK/name/'mods'/jar.name).read_bytes()).hexdigest()==sha for name in [index['native'],index['world'],index['benchmark']]};assert all(hashes.values())
a={c['name']:c for c in world['checks'] if c['status']=='CAPTURED'};b={c['name']:c for c in baseline['checks'] if c['status']=='CAPTURED'}
comparisons=[dict(name=k,status='PASS' if a[k]['slots']==b[k]['slots'] and a[k]['held']==b[k]['held'] else 'FAIL') for k in a]
assert len(comparisons)==9 and all(c['status']=='PASS' for c in comparisons)
compat={'status':'PASS','comparisons':comparisons,'current':index['world'],'baseline':index['baseline'],'scenario':'Actual Forge GuiChest input; 45 slots + cursor count. Current Ttro runs with PolyPatcher/OneConfig. Reverse compared to opposite physical wheel input in upstream baseline.','unverified':['crafting output','NBT item identity','third-party GUI','touchscreen','live multiplayer']};write(R/'inventory-compatibility.json',compat)
def metrics(frames):
 values=[f['ms'] for f in frames];n=len(values);slow=sorted(values,reverse=True)
 return dict(samples=n,average_fps=1000/statistics.mean(values),one_percent_low=1000/statistics.mean(slow[:max(1,math.ceil(n*.01))]),mean_frametime_ms=statistics.mean(values),p99_ms=sorted(values)[min(n-1,math.ceil(n*.99)-1)],max_frametime_ms=max(values),spikes_over_50ms=sum(v>50 for v in values),max_used_heap_mib=max(f['heap'] for f in frames)/1048576)
windows={str(w):metrics([f for f in bench['frames'] if f['window']==w]) for w in sorted({f['window'] for f in bench['frames']})}
pauses=[]
for line in (R/(index['benchmark']+'-gc.log')).read_text().splitlines():
 if '[GC ' in line or '[Full GC ' in line:
  found=re.findall(r'([0-9.]+) secs\]',line)
  if found:pauses.append(float(found[0])*1000)
old=read(R/'performance-result.json');historical=old.get('historical_alpha1',old)
performance=dict(status='INCONCLUSIVE_RELEASE_GATE_NOT_PASSED',artifact_sha256=sha,environment=bench['environment'],fps_cap=260,scenario='Flat world seed12345, render distance2, 8 skulls; PolyPatcher/OneConfig retained in both modes; OFF/ON twice. ON=FPS/CPS/Ping/Crosshair/HeadFX/Inventory.',windows=windows,gc=dict(scope='whole JVM launch; not attributed by window',events=len(pauses),total_pause_ms=sum(pauses),max_pause_ms=max(pauses) if pauses else 0),historical_alpha1=historical,limitations=['software GPU/Xvfb','temporal drift/shared worker','heap is not process RAM','no input-to-photon measurement','no real GPU/VRAM','no matched Lunar baseline'])
write(R/'performance-result.json',performance)
web=read(R/'ui-result.json');backend=(R/'backend.log').read_text();match=re.search(r'Ran (\d+) tests',backend);assert match and backend.rstrip().endswith('OK')
def count(result):return sum(c['status']=='PASS' for c in result['checks'])
pack=read(R/'package-qa.json') if (R/'package-qa.json').exists() else {};package_current=pack.get('version')==product['version'] and pack.get('artifact_sha256')==sha
summary=dict(status='PARTIAL_ALPHA',artifact_sha256=sha,build=build,backend=dict(status='PASS',tests=int(match[1])),web=dict(status=web['status'],checks=len(web['checks'])),native=dict(status='PASS',pass_count=count(native),screenshots=native.get('screenshot_validation',{})),world=dict(status='PASS',pass_count=count(world),screenshots=world.get('screenshot_validation',{})),inventory=compat,packages=pack if package_current else {'status':'UNVERIFIED_CURRENT_PACKAGE'},final_jar_runtime_hash_matches=hashes,performance=performance['status'],external_stack=read(R/'external-stack-result.json')['status']);write(R/'qa-summary.json',summary)
template=(ROOT/'docs/HANDOVER_TEMPLATE.md').read_text();text=template.replace('@@VERSION@@',product['version'])
buildtext=f"JDK 8 / Gradle 2.14.1 / ForgeGradle 2.1による **clean build PASS**。compile APIも実行targetもForge 11.15.1.2318。MCP stable_22とForgeGradle reobfJarを使用。最終JAR SHA-256 `{sha}`。Native / 外部基盤付きworld / benchmarkの実行JARはすべてこのhashと一致。\n\n停止原因はJavaが環境proxyを使っていなかったこととtrust storeの設定。環境proxyと管理された証明書を指定して修復し、TLS検証は維持。productに環境固有設定を埋め込まない。旧1764 APIによるlocal_compileは過去の代替経路として区別。ゲーム/API/QA classを配布JARへ含めない。一般ユーザー環境のWindows buildはUNVERIFIED。"
lines=['| Window | Mode | FPS | 1% Low | p99 ms | 最大frame ms | Heap peak MiB |','|---|---|---:|---:|---:|---:|---:|']
for w,m in windows.items():lines.append(f"| {w} | {'OFF' if int(w)%2==0 else 'ON'} | {m['average_fps']:.2f} | {m['one_percent_low']:.2f} | {m['p99_ms']:.2f} | {m['max_frametime_ms']:.2f} | {m['max_used_heap_mib']:.2f} |")
perftext='最終JAR、FPS制限なし、外部Patcher/OneConfigを維持した2組OFF/ONのsoftware GPU記録。固定配列でframeを記録。\n\n'+'\n'.join(lines)+f"\n\nGCはJVM launch全体で{len(pauses)}回、合計{sum(pauses):.2f}ms、最大{max(pauses) if pauses else 0:.2f}ms。mode別帰属は未計測。旧Alpha.1の60FPS試行で1% Lowが41.69→36.33へ悪化した記録も保持。時間変動とspikeがあり、**性能回帰なし・改善・Lunarより軽いとは判定しない**。\n\nHeapはJVM使用heapであり、process全体RAMではない。実GPU/VRAM/Input-to-photon/他Client比較は未測定。性能release gateは未達。描画のみのCrosshairをHUD収集から分離し、HUD全OFFでtick listener未登録を実ゲームで確認したが、それだけでFPS優位を主張しない。"
qat=f"Backend {summary['backend']['tests']}テストPASS、Web {summary['web']['checks']}チェックPASS、最終JAR Native {count(native)}チェックPASS、外部基盤付きworld {count(world)}チェックPASS、Mouse Tweaks実入力9比較PASS。Resource Pack優先順位、typed Scoreboard/fallback、HeadFX preset/3D、HUD編集を検証。PNG完全性も検査。\n\nPatcher/OneConfig起動成立。Mouse Delayは実local player aim、Mouse BindはRMBにbindしたInventory closeを検査。Static FOV/水中FOVは実Forge rendering event、Night Visionは変換済みrendererを検査。Fullbright/Fire/Input設定は実config fieldへの反映とOFF復帰を検査。Fullbrightの全world照明や全input bug、全配列、macOS対応を保証するものではない。公式Hypixel handler登録はofflineで、live handshake/party responseは未確認。\n\nOneConfig cacheは公式URL/hashから取得し改変せず使用。autoUpdate=falseがクラスパス追加も省く検証用設定の誤りを修正。cacheを用いたローカル起動のPASSであり、fresh online更新/duplicate service/audio deviceは未確認。検証の画像を上流の非同期writerに任せず、Probeだけで同期framebuffer captureを行う。Probeはproduct JARに含まない。\n\n正式Alpha.1の有効/無効Profile更新と独自版保持を実JARで検査し、失敗rollbackもテスト。"
qat+='\n\n配布物 '+(str(len(pack['checks']))+'チェックPASS。metadata/Java8/license/非同梱binary/mrpack/zipapp実起動/自動Mod配置/hashを検査。' if package_current else 'は生成後に最終チェックを実行し、package-qa.jsonへ記録する。未実行状態はPASSにしない。')
qat+=' Web PASSはscreen readerやWindows DPI、Minecraft支援技術のPASSではない。'
text=text.replace('@@BUILD@@',buildtext).replace('@@PERFORMANCE@@',perftext).replace('@@QA@@',qat);assert '@@' not in text;(ROOT/'docs/HANDOVER_REPORT.md').write_text(text)
print(json.dumps({'jar_sha256':sha,'native':count(native),'world':count(world),'inventory':len(comparisons),'performance':performance['status']},indent=2))
