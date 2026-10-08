"""Verify final code-only packages and boot the distributed Python zipapp."""
import zipfile,json,hashlib,subprocess,sys,tempfile,re,urllib.request
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];product=json.loads((ROOT/'launcher/product.json').read_text());D=ROOT/'dist';bundle=product['bundleName'];version=product['version'];checks=[]
def check(name,ok):
 checks.append({'name':name,'status':'PASS' if ok else 'FAIL'})
 if not ok:raise AssertionError(name)
jar=D/(product['artifact']+'-'+version+'.jar')
with zipfile.ZipFile(jar) as z:
 names=z.namelist();meta=json.loads(z.read('mcmod.info'))[0]
 check('Forge 1.8.9 metadata and Ttro Client brand',meta['name']=='Ttro Client' and meta['mcversion']=='1.8.9' and meta['modid']==product['internalId'])
 check('Java 8 class target',all(int.from_bytes(z.read(n)[6:8],'big')==52 for n in names if n.endswith('.class')))
 check('No game/API/QA classes or Fabric manifest',not any(n.startswith(('net/minecraft/','net/minecraftforge/','dev/ttro/qa/')) or n=='fabric.mod.json' for n in names))
 check('Mouse Tweaks attribution included','LICENSE_MOUSE_TWEAKS' in names and 'LICENSE_TTRO' in names)
with zipfile.ZipFile(D/(bundle+'-Competitive-1.8.9.mrpack')) as z:
 index=json.loads(z.read('modrinth.index.json'));check('mrpack targets only Forge 1.8.9',index['dependencies']=={'minecraft':'1.8.9','forge':'11.15.1.2318'})
 check('mrpack built-in matches final JAR',z.read('overrides/mods/'+jar.name)==jar.read_bytes())
 check('No external binaries in mrpack',all(not n.endswith('.jar') or n=='overrides/mods/'+jar.name for n in z.namelist()))
with zipfile.ZipFile(D/(bundle+'-'+version+'-project.zip')) as z:
 check('Source has no runtime/toolchain/external cache',not any('/OneConfig/' in n or '/toolchain/' in n or '/runtime189/' in n for n in z.namelist()))
 report=z.read(bundle+'/docs/HANDOVER_REPORT.md').decode();check('16-part report complete','@@' not in report and '## 16.' in report)
 checksums=json.loads((D/'SHA256.json').read_text());check('All final package hashes match',all(hashlib.sha256((D/name).read_bytes()).hexdigest()==value['sha256'] for name,value in checksums.items()))
with tempfile.TemporaryDirectory(prefix='ttro-distribution-qa-') as data:
 process=subprocess.Popen([sys.executable,str(D/(bundle+'-'+version+'.pyz')),'--no-browser','--data',data,'--port','0'],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
 try:
  line=process.stdout.readline();url=re.search(r'http://127\.0\.0\.1:\d+',line).group(0)
  with urllib.request.urlopen(url+'/api/state',timeout=10) as response:state=json.load(response)
  check('Distributed zipapp starts with canonical brand/platform',state['product']['name']=='Ttro Client' and all(p['mc']=='1.8.9' and p['loader']=='11.15.1.2318' for p in state['profiles']))
  mods=Path(data)/'profiles'/state['selected']/'game/mods';check('Distributed launcher automatically injects final Mod',len(list(mods.glob('*.jar')))==1 and (mods/jar.name).read_bytes()==jar.read_bytes())
 finally:process.terminate();process.wait(timeout=10)
result={'status':'PASS','version':version,'artifact_sha256':hashlib.sha256(jar.read_bytes()).hexdigest(),'checks':checks};(ROOT/'research/package-qa.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
