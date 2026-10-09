"""Ttro Client version-independent profile/content coordinator."""
from __future__ import annotations
import base64, copy, hashlib, io, json, os, re, shutil, subprocess, tempfile, threading, time, urllib.parse, urllib.request, uuid, zipfile
from pathlib import Path
from brand import PRODUCT, COMPAT, JAR_NAME

from minecraft189 import MC,LOADER,PRESETS,forge_metadata,forge_range,prism_components,resource_pack_order,check as check_mods
from modules import DEFAULT_SETTINGS,validate_settings
API='https://api.modrinth.com/v2/'
ROOT=Path(__file__).resolve().parent
BASE_MODS=['patcher','hypixel-mod-api']

class ClientError(Exception): pass

def atomic_json(path, data):
 path = Path(path); path.parent.mkdir(parents=True,exist_ok=True)
 with tempfile.NamedTemporaryFile('w',encoding='utf-8',dir=path.parent,delete=False) as f:
  json.dump(data,f,ensure_ascii=False,indent=2); f.flush(); os.fsync(f.fileno()); temp=f.name
 os.replace(temp,path)

def safe_name(name):
 if not isinstance(name,str) or not name or name in ('.','..') or Path(name).name != name or any(c in name for c in '/\\:\x00'):
  raise ClientError('ファイル名が不正です。')
 return name


def request_json(path, params=None):
 url=API+path + ('?'+urllib.parse.urlencode(params) if params else '')
 req=urllib.request.Request(url,headers={'User-Agent':PRODUCT['artifact']+'/'+PRODUCT['version']+' (local Minecraft environment manager)','Accept':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=25) as r:return json.load(r)
 except Exception as e: raise ClientError('配布元への接続に失敗しました。再試行してください。') from e

def download_verified(file):
 url=file['url']; u=urllib.parse.urlparse(url)
 if u.scheme!='https' or u.hostname!='cdn.modrinth.com': raise ClientError('正規Modrinth CDN以外の取得はブロックしました。')
 if int(file['size'])>128*1024*1024: raise ClientError('ファイルサイズが上限を超えます。')
 try:
  with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':PRODUCT['artifact']+'/'+PRODUCT['version']}),timeout=45) as r:
   if urllib.parse.urlparse(r.url).hostname!='cdn.modrinth.com': raise ClientError('予期しないリダイレクトです。')
   blob=r.read(128*1024*1024+1)
 except ClientError: raise
 except Exception as e: raise ClientError('ダウンロードに失敗しました。変更は保存されません。') from e
 if len(blob)!=file['size']:raise ClientError('サイズ検証に失敗しました。')
 for alg in ('sha512','sha1'):
  if alg in file['hashes'] and hashlib.new(alg,blob).hexdigest()!=file['hashes'][alg]:raise ClientError('ハッシュ検証に失敗しました。')
 if not file['hashes'].get('sha512'):raise ClientError('SHA-512情報がありません。')
 return blob


def vtuple(v):
 m=re.match(r'^(\d+)(?:\.(\d+))?(?:\.(\d+))?',str(v));return tuple(int(x or 0) for x in m.groups()) if m else None

def version_compare(a,b):
 aa,bb=vtuple(a),vtuple(b)
 if aa!=bb:return (aa>bb)-(aa<bb)
 pa=str(a).split('+')[0].split('-',1);pb=str(b).split('+')[0].split('-',1)
 if len(pa)==1 or len(pb)==1:return (len(pa)==1)-(len(pb)==1)
 xa,xb=pa[1].split('.'),pb[1].split('.')
 for x,y in zip(xa,xb):
  if x==y:continue
  if not x:return -1
  if not y:return 1
  if x.isdigit() and y.isdigit():return (int(x)>int(y))-(int(x)<int(y))
  if x.isdigit()!=y.isdigit():return -1 if x.isdigit() else 1
  return (x>y)-(x<y)
 return (len(xa)>len(xb))-(len(xa)<len(xb))




def crash_analysis(text):
 text=text[-200000:];out=[]
 for pat,title in [(r'(?:requires|depends on)[^\n]{0,250}','依存関係エラーの記録'),(r'(?:incompatible|conflicts with)[^\n]{0,250}','非互換の記録'),(r'(?:OutOfMemoryError)[^\n]{0,250}','メモリ不足の記録'),(r'(?:UnsupportedClassVersionError)[^\n]{0,250}','Javaバージョン不一致の記録'),(r'(?:Mixin apply|Mixin transformation)[^\n]{0,250}','Mixin失敗の記録')]:
  matches=re.findall(pat,text,re.I)
  if matches:out.append({'title':title,'evidence':matches[:3],'certainty':'ログに該当記録あり。根本原因の断定ではありません。'})
 return out or [{'title':'原因は特定できませんでした','evidence':[],'certainty':'直前の変更を確認し、詳細ログを調べてください。'}]

class Store:
 def __init__(self,path):
  self.path=Path(path);self.path.mkdir(parents=True,exist_ok=True);self.lock=threading.RLock()
  state=self.path/'state.json'
  if not state.exists():
   atomic_json(state,{'schema':2,'profiles':[],'selected':None,'prism':'','prismRoot':'','favorites':[]})
   self.create('Competitive PvP')
 def state(self):return json.loads((self.path/'state.json').read_text(encoding='utf-8'))
 def save(self,s):atomic_json(self.path/'state.json',s)
 def profile(self,pid):
  for p in self.state()['profiles']:
   if p['id']==pid:return p
  raise ClientError('Profileが見つかりません。')
 def base(self,pid):self.profile(pid);return self.path/'profiles'/pid
 def game(self,pid):
  p=self.profile(pid);return Path(p['livePath']) if p.get('livePath') else self.path/'profiles'/pid/'game'
 def update(self,pid,change):
  with self.lock:
   s=self.state();p=next((p for p in s['profiles'] if p['id']==pid),None)
   if not p:raise ClientError('Profileが見つかりません。')
   change(p);p['revision']=p.get('revision',0)+1;self.save(s);return p
 def create(self,name,source=None):
  with self.lock:
   if len(str(name))>80 or not str(name).strip():raise ClientError('名前は1〜80文字です。')
   s=self.state();pid=uuid.uuid4().hex[:12]
   p={'id':pid,'name':name,'mc':MC,'loader':LOADER,'preset':'Competitive','settings':copy.deepcopy(DEFAULT_SETTINGS),'content':[],'revision':0,'migration':[]}
   if source:
    old=self.profile(source);p.update(copy.deepcopy({k:v for k,v in old.items() if k not in ['id','name','livePath','revision']}))
    shutil.copytree(self.game(source),self.path/'profiles'/pid/'game')
   for d in ['mods','resourcepacks','shaderpacks','config','logs']:(self.path/'profiles'/pid/'game'/d).mkdir(parents=True,exist_ok=True)
   s['profiles'].append(p);s['selected']=pid;self.save(s);self.write_settings(pid);return p
 def write_settings(self,pid):
  p=self.profile(pid);game=self.game(pid);(game/'config').mkdir(parents=True,exist_ok=True)
  atomic_json(game/'config'/COMPAT['configFile'],p['settings'])
 def preset(self,pid,name):
  if name not in PRESETS:raise ClientError('Presetが不正です。')
  self.backup(pid)
  path=self.game(pid)/'options.txt';rows=path.read_text(encoding='utf-8').splitlines() if path.exists() else []
  options=dict(x.split(':',1) for x in rows if ':' in x);options.update({k:str(v).lower() for k,v in PRESETS[name].items()})
  path.write_text('\n'.join(f'{k}:{v}' for k,v in options.items())+'\n',encoding='utf-8');self.update(pid,lambda p:p.update(preset=name));return PRESETS[name]
 def export(self,pid):
  p=copy.deepcopy(self.profile(pid));p.pop('livePath',None)
  with io.BytesIO() as b:
   with zipfile.ZipFile(b,'w',zipfile.ZIP_DEFLATED) as z:
    z.writestr(COMPAT['profileFile'],json.dumps({'schema':2,'profile':p},ensure_ascii=False))
    game=self.game(pid)
    for f in [game/'options.txt',game/'config'/COMPAT['configFile'],game/'optionsshaders.txt']:
     if f.exists():z.write(f,'overrides/'+str(f.relative_to(game)))
   return b.getvalue()
 def mrpack(self,pid):
  p=self.profile(pid);files=[]
  for c in p['content']:
   if not c['enabled'] or not c.get('download'):continue
   d=c['download'];files.append({'path':folder(c['kind'])+'/'+c['filename'],'hashes':d['hashes'],'env':{'client':'required','server':'unsupported'},'downloads':[d['url']],'fileSize':d['size']})
  index={'formatVersion':1,'game':'minecraft','versionId':PRODUCT['version'],'name':PRODUCT['shortName']+' - '+p['name'],'summary':'Display-only PvP environment. External binaries fetched from official Modrinth CDN.','files':files,'dependencies':{'minecraft':p['mc'],'forge':p['loader']}}
  with io.BytesIO() as b:
   with zipfile.ZipFile(b,'w',zipfile.ZIP_DEFLATED) as z:
    z.writestr('modrinth.index.json',json.dumps(index,ensure_ascii=False,indent=2))
    payload=ROOT/'payload'/JAR_NAME
    if payload.exists():z.write(payload,'overrides/mods/'+payload.name)
    z.writestr('overrides/config/'+COMPAT['configFile'],json.dumps(p['settings'],ensure_ascii=False))
    for name in ['THIRD_PARTY.md','LICENSE_NOTES.md']:
     note=ROOT.parent/name
     if not note.exists():note=ROOT/name
     if note.exists():z.write(note,'overrides/CLIENT_'+name)
    options=self.game(pid)/'options.txt'
    if options.exists():z.write(options,'overrides/options.txt')
   return b.getvalue()
 def import_profile(self,blob):
  z=validate_zip(blob);data=json.loads(z.read(COMPAT['profileFile']))
  if data.get('schema')!=2:raise ClientError('Profile schemaが未対応です。')
  src=data['profile'];p=self.create(str(src.get('name','Imported'))[:70]+' (import)')
  settings=validate_settings(src.get('settings',DEFAULT_SETTINGS))
  self.update(p['id'],lambda q:q.update(settings=settings,content=[],migration=[{'status':'download required','content':x.get('name','Unknown'),'project':x.get('project'),'version_id':x.get('version_id')} for x in src.get('content',[])]))
  self.write_settings(p['id'])
  for name in ['options.txt','optionsshaders.txt']:
   entry='overrides/'+name
   if entry in z.namelist():
    blob=z.read(entry)
    if len(blob)>1024*1024:raise ClientError('設定ファイルが大きすぎます。')
    dest=self.game(p['id'])/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(blob.decode('utf-8'),encoding='utf-8')
  return self.profile(p['id'])
 def migration(self,pid,mc):
  raise ClientError('Ttro Clientの対象はMinecraft 1.8.9に固定されています。')
 def backup(self,pid):
  dest=self.base(pid)/'backups'/str(time.time_ns());dest.mkdir(parents=True)
  game=self.game(pid);(dest/'game').mkdir()
  for name in ['mods','resourcepacks','shaderpacks','config']:
   if (game/name).exists():shutil.copytree(game/name,dest/'game'/name)
  for name in ['options.txt','optionsshaders.txt']:
   if (game/name).exists():shutil.copy2(game/name,dest/'game'/name)
  atomic_json(dest/'profile.json',self.profile(pid))
  return dest.name
 def restore_snapshot(self,pid,base):
  game=self.game(pid)
  for name in ['mods','resourcepacks','shaderpacks','config']:
   if (game/name).exists():shutil.rmtree(game/name)
   if (base/'game'/name).exists():shutil.copytree(base/'game'/name,game/name)
   else:(game/name).mkdir(parents=True,exist_ok=True)
  for name in ['options.txt','optionsshaders.txt']:
   (game/name).unlink(missing_ok=True)
   if (base/'game'/name).exists():shutil.copy2(base/'game'/name,game/name)
  saved=json.loads((base/'profile.json').read_text(encoding='utf-8'));saved.pop('livePath',None)
  s=self.state();current=next(p for p in s['profiles'] if p['id']==pid);current.update({k:v for k,v in saved.items() if k not in ['id','revision']});current['revision']+=1;self.save(s)
 def restore(self,pid,bid):
  safe_name(bid);base=self.base(pid)/'backups'/bid
  if not (base/'profile.json').exists():raise ClientError('Backupがありません。')
  self.backup(pid);self.restore_snapshot(pid,base)
 def plan(self,pid,project,kind,version_id=None):
  p=self.profile(pid);seen=set();nodes=[];retained=[]
  def visit(proj,vid=None):
   if vid:ver=request_json('version/'+vid);meta=request_json('project/'+ver['project_id'])
   else:
    meta=request_json('project/'+proj)
    versions=request_json('project/'+meta['id']+'/version',{'game_versions':json.dumps([p['mc']]),**({'loaders':'["forge"]'} if meta['project_type']=='mod' else {})})
    ver=next((v for v in versions if v['version_type']=='release'),None)
    if not ver:raise ClientError(meta['title']+' の安定版がこの環境にありません。')
   if meta['id'] in seen:return
   seen.add(meta['id'])
   if not compatible(ver,p['mc'],meta['project_type']):raise ClientError(meta['title']+' は非互換です。')
   if meta.get('server_side')=='required' and meta.get('client_side')=='unsupported':raise ClientError('サーバー専用Modです。')
   for dep in ver['dependencies']:
    if dep['dependency_type']=='required':
     if not dep.get('version_id') and not dep.get('project_id'):raise ClientError('依存関係を解決できません。')
     existing=next((c for c in p['content'] if c.get('project')==dep.get('project_id') and c.get('enabled') and c.get('kind')=='mod' and (self.game(pid)/'mods'/c['filename']).is_file()),None)
     if existing and not dep.get('version_id'):
      if not any(c['project']==existing['project'] for c in retained):retained.append({k:existing[k] for k in ['project','name','version']})
      continue
     visit(dep.get('project_id'),dep.get('version_id'))
   f=next((f for f in ver['files'] if f['primary']),ver['files'][0]);safe_name(f['filename'])
   previous=next((c for c in p['content'] if c.get('project')==meta['id']),None)
   change='install'
   if previous:
    normalize=lambda v:re.sub(r'^mc[0-9.]+-','',v)
    old,new=normalize(previous['version']),normalize(ver['version_number'])
    change='same' if previous.get('version_id')==ver['id'] else ('downgrade' if vtuple(old) and vtuple(new) and version_compare(new,old)<0 else 'replace')
   nodes.append({'project':meta['id'],'slug':meta['slug'],'name':meta['title'],'kind':meta['project_type'],'version':ver['version_number'],'version_id':ver['id'],'license':meta['license'],'file':f,'enabled':True,'previous_version':previous['version'] if previous else None,'change':change})
  visit(project,version_id)
  if nodes[-1]['kind']!=kind:raise ClientError('種類が一致しません。')
  if any(n['kind'] not in ['mod','resourcepack','shader'] for n in nodes):raise ClientError('この種類のコンテンツは未対応です。')
  return {'profile':pid,'revision':p['revision'],'nodes':nodes,'retained_dependencies':retained,'total':sum(n['file']['size'] for n in nodes),'notice':'正規配布元から取得します。依存関係を含む変更一覧を確認してください。'}
 def install(self,plan,progress=lambda v:None):
  pid=plan['profile'];p=self.profile(pid)
  if p['revision']!=plan['revision']:raise ClientError('Profileが変更されました。導入内容をもう一度確認してください。')
  stage=Path(tempfile.mkdtemp(prefix='client-'));staged=[]
  try:
   for i,n in enumerate(plan['nodes']):
    progress({'phase':'download','current':i,'total':len(plan['nodes']),'name':n['name']})
    cache=self.path/'cache';cache.mkdir(exist_ok=True);digest=n['file'].get('hashes',{}).get('sha512','')
    if not re.fullmatch(r'[a-f0-9]{128}',digest):raise ClientError('SHA-512情報が不正です。')
    cached=cache/digest
    blob=cached.read_bytes() if cached.exists() else download_verified(n['file'])
    if len(blob)!=n['file']['size'] or hashlib.sha512(blob).hexdigest()!=digest:raise ClientError('キャッシュのハッシュ検証に失敗しました。')
    if not cached.exists():cached.write_bytes(blob)
    validate_zip(blob,n['kind']);f=stage/n['file']['filename'];f.write_bytes(blob);staged.append((n,f))
   with self.lock:
    if self.profile(pid)['revision']!=plan['revision']:raise ClientError('Profileが変更されました。')
    game=self.game(pid);probe=stage/'probe';shutil.copytree(game/'mods',probe/'mods')
    replaced={n['project'] for n,_ in staged}
    for c in p['content']:
     if c.get('project') in replaced and c['kind']=='mod':
      for fn in [c['filename'],c['filename']+'.disabled']:(probe/'mods'/fn).unlink(missing_ok=True)
    for n,f in staged:
     if n['kind']=='mod':shutil.copy2(f,probe/'mods'/f.name)
    errors=[x for x in check_mods(probe,p['mc'],p['loader']) if x['level']=='error']
    if errors:raise ClientError('導入を中止: '+'; '.join(x['message'] for x in errors[:6]))
    bid=self.backup(pid)
    try:
     for c in p['content']:
      if c.get('project') in replaced:
       d=game/folder(c['kind']);(d/c['filename']).unlink(missing_ok=True);(d/(c['filename']+'.disabled')).unlink(missing_ok=True)
     records=[c for c in p['content'] if c.get('project') not in replaced]
     for n,f in staged:
      dest=game/folder(n['kind']);dest.mkdir(exist_ok=True)
      if (dest/f.name).exists() or (dest/(f.name+'.disabled')).exists():raise ClientError('別コンテンツとファイル名が重複しています。')
      os.replace(f,dest/f.name)
      records.append({k:v for k,v in n.items() if k not in ['file','change','previous_version']} | {'filename':f.name,'download':n['file'],'installed_at':time.time(),'source':'Modrinth'})
     self.update(pid,lambda q:q.update(content=records));self.sync_packs(pid)
    except Exception:
     self.restore_snapshot(pid,self.base(pid)/'backups'/bid);raise
   return {'installed':[n['name'] for n,_ in staged]}
  finally:shutil.rmtree(stage,ignore_errors=True)
 def sync_packs(self,pid):
  p=self.profile(pid);path=self.game(pid)/'options.txt';rows=path.read_text(encoding='utf-8').splitlines() if path.exists() else []
  rows=[x for x in rows if not x.startswith('resourcePacks:')];rows.append('resourcePacks:'+json.dumps(resource_pack_order(p['content']),separators=(',',':')))
  path.write_text('\n'.join(rows)+'\n',encoding='utf-8')
  shaders=[c for c in p['content'] if c['kind']=='shader' and c['enabled']]
  path=self.game(pid)/'optionsshaders.txt'
  if shaders or path.exists():
   props=dict(x.split('=',1) for x in path.read_text(encoding='utf-8').splitlines() if '=' in x and not x.startswith('#')) if path.exists() else {}
   props['shaderPack']=shaders[0]['filename'] if shaders else 'OFF'
   path.write_text('\n'.join(k+'='+v for k,v in props.items())+'\n',encoding='utf-8')
 def content_action(self,pid,filename,action):
  safe_name(filename);p=self.profile(pid);c=next((c for c in p['content'] if c['filename']==filename),None)
  if not c:raise ClientError('対象がありません。')
  self.backup(pid);path=self.game(pid)/folder(c['kind'])/filename
  def change(q):
   entry=next(x for x in q['content'] if x['filename']==filename)
   if action in ['enable','disable']:
    enabled=action=='enable'
    if entry['kind']=='shader' and enabled:
     for other in q['content']:
      if other['kind']=='shader' and other!=entry and other['enabled']:
       opath=self.game(pid)/'shaderpacks'/other['filename']
       if opath.exists():opath.rename(opath.with_name(opath.name+'.disabled'))
       other['enabled']=False
    if entry['kind'] in ['mod','shader']:
     src=path if entry['enabled'] else path.with_name(path.name+'.disabled');dst=path if enabled else path.with_name(path.name+'.disabled')
     if src!=dst and src.exists():src.rename(dst)
    entry['enabled']=enabled
   elif action=='remove':
    path.unlink(missing_ok=True);path.with_name(path.name+'.disabled').unlink(missing_ok=True);q['content'].remove(entry)
   elif action in ['up','down']:
    i=q['content'].index(entry);j=i+(-1 if action=='up' else 1)
    if 0<=j<len(q['content']):q['content'][i],q['content'][j]=q['content'][j],q['content'][i]
   else:raise ClientError('操作が不正です。')
  self.update(pid,change);self.sync_packs(pid);return check_mods(self.game(pid),p['mc'],p['loader'])
 def import_content(self,pid,filename,blob,kind):
  safe_name(filename)
  if len(blob)>64*1024*1024:raise ClientError('64MiB以下にしてください。')
  if kind not in ['mod','resourcepack','shader']:raise ClientError('種類が不正です。')
  if not filename.endswith('.jar' if kind=='mod' else '.zip'):raise ClientError('拡張子が一致しません。')
  z=validate_zip(blob,kind);game=self.game(pid);dest=game/folder(kind)/filename
  if dest.exists() or dest.with_name(filename+'.disabled').exists():raise ClientError('同名ファイルがあります。')
  if kind=='mod':
   metas=jar_metadata(blob);dep=metas[0].get('depends',{})
   if forge_range(metas[0].get('mcversion',''),MC) is False:raise ClientError('Minecraftバージョンが非互換です。')
  if kind=='resourcepack':
   meta=json.loads(z.read('pack.mcmeta'))['pack']
   if meta.get('pack_format')!=1:raise ClientError('1.8.9のpack_format 1ではありません。')
  self.backup(pid);dest.write_bytes(blob)
  if kind=='shader':
   for c in self.profile(pid)['content']:
    if c['kind']=='shader' and c['enabled']:self.content_action(pid,c['filename'],'disable')
  self.update(pid,lambda p:p['content'].append({'name':filename,'filename':filename,'kind':kind,'enabled':True,'source':'Local','version':'local','project':None,'license':{'id':'UNVERIFIED'}}));self.sync_packs(pid)
  return check_mods(game,self.profile(pid)['mc'],self.profile(pid)['loader'])
 def launch(self,pid):
  p=self.profile(pid);s=self.state()
  if p['mc']!=MC or p['loader']!=LOADER:raise ClientError('Forge 1.8.9 Profileではありません。')
  exe=s.get('prism') or shutil.which('prismlauncher')
  if not exe or not Path(exe).is_file():raise ClientError('Prism Launcherの実行ファイルを設定してください。')
  root=Path(s.get('prismRoot') or default_prism_root());instance=COMPAT['instancePrefix']+pid;target=root/'instances'/instance
  if target.exists() and not (target/COMPAT['ownershipFile']).exists():raise ClientError('別のInstanceは上書きしません。')
  errors=[x['message'] for x in check_mods(self.game(pid)) if x['level']=='error']
  if errors:raise ClientError('起動前検査: '+'; '.join(errors[:6]))
  target.mkdir(parents=True,exist_ok=True)
  if not p.get('livePath'):
   if (target/'.minecraft').exists():raise ClientError('既存Instanceを上書きしません。')
   shutil.copytree(self.game(pid),target/'.minecraft');self.update(pid,lambda q:q.update(livePath=str(target/'.minecraft')))
  atomic_json(target/COMPAT['ownershipFile'],{'profile':pid})
  atomic_json(target/'mmc-pack.json',{'formatVersion':1,'components':prism_components()})
  (target/'instance.cfg').write_text('[General]\nInstanceType=OneSix\nname='+PRODUCT['name']+' - '+p['name'].replace('\n',' ')+'\nOverrideMemory=true\nMinMemAlloc=512\nMaxMemAlloc=2048\n',encoding='utf-8')
  process=subprocess.Popen([str(exe),'-d',str(root),'-l',instance],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
  return {'status':'handoff','pid':process.pid,'instance':instance,'message':'PrismへForge 1.8.9の起動要求を渡しました。Microsoft認証とJava 8はPrismで管理します。'}

def folder(kind):return {'mod':'mods','resourcepack':'resourcepacks','shader':'shaderpacks'}[kind]

def default_prism_root():
 if os.name=='nt':return str(Path(os.getenv('APPDATA',''))/'PrismLauncher')
 if __import__('sys').platform=='darwin':return str(Path.home()/'Library/Application Support/PrismLauncher')
 return str(Path(os.getenv('XDG_DATA_HOME',Path.home()/'.local/share'))/'PrismLauncher')


def validate_zip(blob,kind='zip'):
 try:
  z=zipfile.ZipFile(io.BytesIO(blob));entries=z.infolist()
  if len(entries)>10000 or sum(x.file_size for x in entries)>512*1024*1024:raise ClientError('展開サイズが上限を超えます。')
  for x in entries:
   p=Path(x.filename.replace('\\','/'))
   if p.is_absolute() or '..' in p.parts or ':' in x.filename or (x.external_attr>>16)&0o170000==0o120000:raise ClientError('危険なアーカイブパスです。')
  if kind=='mod':forge_metadata(z)
  if kind=='resourcepack' and 'pack.mcmeta' not in z.namelist():raise ClientError('pack.mcmetaがありません。')
  return z
 except (zipfile.BadZipFile,EOFError,ValueError) as e:raise ClientError(str(e)) from e

def jar_metadata(blob):return forge_metadata(validate_zip(blob,'mod'))
def compatible(version,mc,kind='mod'):
 return mc==MC and mc in version.get('game_versions',[]) and (kind!='mod' or 'forge' in version.get('loaders',[]))
