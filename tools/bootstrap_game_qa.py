"""Restore a private Linux 1.8.9 QA runtime from official metadata. Never package these binaries."""
import concurrent.futures, hashlib, json, os, shutil, urllib.request, zipfile, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; WORK=ROOT.parent
TOOLS=WORK/'toolchain'; RUNTIME=WORK/'runtime189'; CACHE=TOOLS/'gradle-home/caches'
if RUNTIME.is_symlink(): RUNTIME.unlink()
RUNTIME.mkdir(exist_ok=True)
cache_files={}
for p in (CACHE/'modules-2/files-2.1').rglob('*.jar'): cache_files.setdefault(p.name,[]).append(p)
def open_url(url,timeout=45):
 return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'TtroClient-QA/0.3.0-alpha.1 (+https://github.com/totoro0419/TtroClient)'}),timeout=timeout)
def get(url,path,expected=None,algorithm='sha1'):
 path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
 temp=path.with_name(path.name+'.download-'+uuid.uuid4().hex)
 def write(data):temp.write_bytes(data);os.replace(temp,path)
 def valid(p):return p.is_file() and (expected is None or hashlib.new(algorithm,p.read_bytes()).hexdigest()==expected)
 if valid(path):return path
 if expected:
  for p in cache_files.get(path.name,[]):
   if valid(p):write(p.read_bytes());return path
 data=open_url(url.replace('http://','https://')).read()
 if expected and hashlib.new(algorithm,data).hexdigest()!=expected:raise ValueError('Hash mismatch '+path.name)
 write(data);return path
base='https://maven.minecraftforge.net/net/minecraftforge/forge/1.8.9-11.15.1.2318-1.8.9/forge-1.8.9-11.15.1.2318-1.8.9'
for suffix,name in [('-installer.jar','forge-installer.jar'),('-universal.jar','forge-universal.jar')]:
 url=base+suffix; sha=open_url(url+'.sha1',30).read().decode().split()[0];get(url,TOOLS/name,sha)
with zipfile.ZipFile(TOOLS/'forge-installer.jar') as z:forge=json.loads(z.read('install_profile.json'))['versionInfo']
version=json.loads((CACHE/'minecraft/versionJsons/1.8.9.json').read_text())
client=CACHE/'minecraft/net/minecraft/minecraft/1.8.9/minecraft-1.8.9.jar'
assert hashlib.sha1(client.read_bytes()).hexdigest()==version['downloads']['client']['sha1'];shutil.copy2(client,RUNTIME/'client.jar')
libraries=[]; native_archives=[]; jobs=[]
def allowed(lib):
 rules=lib.get('rules');yes=not rules
 for rule in rules or []:
  if 'os' not in rule or rule['os'].get('name')=='linux':yes=rule['action']=='allow'
 return yes
for lib in version['libraries']:
 if not allowed(lib):continue
 for kind,d in lib.get('downloads',{}).items():
  if kind=='artifact':libraries.append(RUNTIME/'libraries'/d['path']);jobs.append((d['url'],libraries[-1],d['sha1']))
  elif kind=='classifiers' and lib.get('natives',{}).get('linux'):
   key=lib['natives']['linux'].replace('${arch}','64');f=d[key];native_archives.append(RUNTIME/'libraries'/f['path']);jobs.append((f['url'],native_archives[-1],f['sha1']))
for lib in forge['libraries']:
 if not allowed(lib) or lib['name'].startswith('net.minecraftforge:forge:'):continue
 group,artifact,ver=lib['name'].split(':')[:3];rel=group.replace('.','/')+'/'+artifact+'/'+ver+'/'+artifact+'-'+ver+'.jar'
 path=RUNTIME/'libraries'/rel
 if path in libraries:continue
 url=lib.get('url','https://libraries.minecraft.net/').replace('http://','https://').rstrip('/')+'/'+rel
 # Forge metadata predates download hash fields; use the original Maven sidecar when offered.
 try:sha=open_url(url+'.sha1',20).read().decode().split()[0]
 except Exception:sha=None
 libraries.append(path);jobs.append((url,path,sha))
with concurrent.futures.ThreadPoolExecutor(max_workers=10) as ex:
 for result in ex.map(lambda x:get(*x),jobs):pass
natives=RUNTIME/'natives';natives.mkdir(exist_ok=True)
for archive in native_archives:
 with zipfile.ZipFile(archive) as z:
  for e in z.infolist():
   if e.filename.endswith('.so'): (natives/Path(e.filename).name).write_bytes(z.read(e))
index=version['assetIndex'];ip=RUNTIME/'assets/indexes'/('1.8.json');get(index['url'],ip,index['sha1'])
objects=json.loads(ip.read_text())['objects']
with concurrent.futures.ThreadPoolExecutor(max_workers=12) as ex:
 for result in ex.map(lambda v:get('https://resources.download.minecraft.net/'+v['hash'][:2]+'/'+v['hash'],RUNTIME/'assets/objects'/v['hash'][:2]/v['hash'],v['hash']),objects.values()):pass
# Runtime uses only the declared launch libraries, not development/build dependencies.
(RUNTIME/'classpath.txt').write_text(os.pathsep.join(map(str,[RUNTIME/'client.jar',TOOLS/'forge-universal.jar']+libraries)))
asm=next(p for p in libraries if p.name.startswith('asm-'))
dest=TOOLS/'asm.jar'
if dest.is_symlink():dest.unlink()
shutil.copy2(asm,dest)
url='https://repo.md-5.net/content/repositories/releases/net/md-5/SpecialSource/1.7.4/SpecialSource-1.7.4-shaded.jar'
get(url,TOOLS/'specialsource.jar')
mouse_url='https://media.forgecdn.net/files/2287/384/MouseTweaks-2.6.2-mc1.8.9.jar'
get(mouse_url,TOOLS/'mousetweaks.jar')
for slug,vid,name in [('patcher','iNjGeSxM','patcher.jar'),('hypixel-mod-api','VtDhN4ZW','hypixel-mod-api.jar')]:
 v=json.load(open_url('https://api.modrinth.com/v2/version/'+vid,30));f=next(f for f in v['files'] if f['primary']);get(f['url'],TOOLS/name,f['hashes']['sha512'],'sha512')
metadata=json.loads((ROOT/'research/archive-alpha2/external-stack-result.json').read_text())['official_cache_metadata']
for key,name in [('release','oneconfig-release.jar'),('loader','oneconfig-loader.jar')]:get(metadata[key]['url'],TOOLS/name,metadata[key]['sha256'],'sha256')
mp=TOOLS/'oneconfig-metadata.json'
if mp.is_symlink():mp.unlink()
mp.write_text(json.dumps(metadata,indent=2)+'\n')
(RUNTIME/'qa-bootstrap.json').write_text(json.dumps({'minecraft':'1.8.9','forge':'11.15.1.2318','libraries':len(libraries),'assets':len(objects),'source':'official version metadata / Forge installer / audited original Modrinth releases'},indent=2)+'\n')
print('Private real-game QA runtime restored; no Minecraft or third-party JAR is added to product packages.')
