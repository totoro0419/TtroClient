"""Local UI transport. Bound to loopback; Origin + session token required for writes."""
from __future__ import annotations
import argparse, html, base64, json, mimetypes, os, secrets, shutil, sys, threading, time, urllib.parse, webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from core import *
from brand import default_data_path
from modules import CATALOG

class App:
 def __init__(self,path):
  self.store=Store(path);self.token=secrets.token_urlsafe(32);self.plans={};self.jobs={};self.busy=set();self.mutex=threading.Lock()
  self.catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'));self.started=time.perf_counter();self.mod_cache={};self.integrations={}
  for p in self.store.state()['profiles']:self.builtin(p['id'])
 def builtin(self,pid):
  src=ROOT/'payload'/JAR_NAME
  if not src.exists():return
  with self.store.lock:
   mods=self.store.game(pid)/'mods';existing=[];record=self.store.base(pid)/COMPAT['integrationRecord']
   for old in mods.iterdir():
    if old.name.endswith(('.jar','.jar.disabled')):
     try:
      meta=jar_metadata(old.read_bytes())[0]
      if meta.get('modid')==PRODUCT['internalId']:existing.append((old,meta))
     except (ValueError,OSError,zipfile.BadZipFile,KeyError):pass
   if len(existing)>1:self.integrations[pid]={'status':'review','message':'Ttro ClientのJARが複数あります。起動前チェックで確認してください。'};return
   payload=src.read_bytes();digest=hashlib.sha256(payload).hexdigest();backup=None
   if existing:
    old,meta=existing[0];old_digest=hashlib.sha256(old.read_bytes()).hexdigest()
    if old_digest==digest:
     atomic_json(record,{'filename':old.name,'sha256':digest,'version':PRODUCT['version']})
     self.integrations[pid]={'status':'current','message':'Ttro Client '+PRODUCT['version']};return
    try:owned=json.loads(record.read_text())
    except (OSError,ValueError):owned={}
    history=ROOT/'payload/history.json'
    known=json.loads(history.read_text()) if history.exists() else []
    managed=owned.get('sha256')==old_digest or any(x['sha256']==old_digest for x in known)
    try:older=version_compare(meta.get('version','0'),PRODUCT['version'])<0
    except (TypeError,ValueError):older=False
    if not managed or not older:
     self.integrations[pid]={'status':'review','message':'既存のTtro Client JARを保持しました。独自変更版または新しい版のため自動更新しません。'};return
    backup=self.store.backup(pid)
   disabled=bool(existing and existing[0][0].name.endswith('.disabled'));dest=mods/(src.name+('.disabled' if disabled else ''))
   # Stage outside mods so Forge never sees two copies after an interruption.
   with tempfile.NamedTemporaryFile(dir=self.store.base(pid),delete=False) as f:f.write(payload);f.flush();os.fsync(f.fileno());staged=Path(f.name)
   old_path=existing[0][0] if existing else None
   try:
    if old_path:old_path.unlink()
    os.replace(staged,dest)
    atomic_json(record,{'filename':dest.name,'sha256':digest,'version':PRODUCT['version']})
   except OSError:
    staged.unlink(missing_ok=True)
    if dest.exists() and hashlib.sha256(dest.read_bytes()).hexdigest()==digest:dest.unlink()
    if backup and old_path and not old_path.exists():shutil.copy2(self.store.base(pid)/'backups'/backup/'game/mods'/old_path.name,old_path)
    self.integrations[pid]={'status':'review','message':'Ttro Client更新に失敗しました。ゲームを閉じて再起動してください。Backupは保持しています。'};return
   self.integrations[pid]={'status':'updated' if backup else 'current','message':'Ttro Client '+PRODUCT['version']+('へ更新しました。変更前のBackupを保持しています。' if backup else ''),'backup':backup}
 def sync_game_settings(self):
  for p in self.store.state()['profiles']:
   if p['id'] in self.busy:continue
   f=self.store.game(p['id'])/'config'/COMPAT['configFile']
   try:
    if f.exists():
     settings=validate_settings(json.loads(f.read_text(encoding='utf-8')))
     if settings!=p['settings']:self.store.update(p['id'],lambda q:q.update(settings=settings))
   except Exception:pass
 def integration_info(self,pid):
  info=dict(self.integrations.get(pid,{}))
  if self.availability(pid).get('mousedelay'):
   options=self.store.game(pid)/'options.txt'
   try:conflict=('key_'+PRODUCT['name']+' Settings:54') in options.read_text(encoding='utf-8').splitlines()
   except OSError:conflict=False
   if conflict:
    info['status']='review';info['message']=info.get('message','')+' 設定キーが右Shiftのままです。OneConfigとの競合を避けるにはControlsで右Ctrl等へ変更してください。Mods → Ttro Client → Configからも開けます。'
  return info
 def availability(self,pid):
  files=sorted((self.store.game(pid)/'mods').glob('*.jar'));stamp=tuple((p.name,p.stat().st_mtime_ns,p.stat().st_size) for p in files)
  cached=self.mod_cache.get(pid)
  if cached and cached[0]==stamp:return cached[1]
  ids=set()
  for p in files:
   try:
    with zipfile.ZipFile(p) as z:
     ids.update(m.get('modid','') for m in forge_metadata(z))
   except (ValueError,OSError,zipfile.BadZipFile):pass
  result={m['id']:m['requires'] in ids for m in CATALOG if m.get('requires')};self.mod_cache[pid]=(stamp,result);return result
 def job(self,pid,fn):
  with self.mutex:
   if pid in self.busy:raise ClientError('このProfileで処理中です。完了後に再試行してください。')
   self.busy.add(pid);jid=secrets.token_hex(10);self.jobs[jid]={'id':jid,'status':'running','progress':{'phase':'prepare'}}
  def progress(v):self.jobs[jid]['progress']=v
  def run():
   try:self.jobs[jid].update(status='done',result=fn(progress))
   except Exception as e:self.jobs[jid].update(status='error',error=str(e) if isinstance(e,ClientError) else '処理に失敗しました: '+type(e).__name__)
   finally:
    with self.mutex:self.busy.discard(pid)
  threading.Thread(target=run,daemon=True).start();return {'job':jid}
 def action(self,b):
  a=b.get('action');pid=b.get('profile')
  if pid in self.busy:raise ClientError('このProfileで処理中です。')
  st=self.store
  if a=='create':
   p=st.create(b['name'],b.get('source'));self.builtin(p['id']);return p
  if a=='select':
   st.profile(pid);s=st.state();s['selected']=pid;st.save(s);return {}
  if a=='settings':
   with st.lock:
    p=st.profile(pid)
    if b.get('revision')!=p['revision']:raise ClientError('別の操作で設定が更新されました。再読み込みしてください。')
    v=validate_settings(b['settings']);st.update(pid,lambda p:p.update(settings=v));st.write_settings(pid);return st.profile(pid)
  if a=='preset':return st.preset(pid,b['name'])
  if a=='plan':
   return self.job(pid,lambda _:self.make_plan(pid,b['project'],b['kind']))
  if a=='install':
   plan=self.plans.pop(b['plan'],None)
   if not plan or plan['profile']!=pid:raise ClientError('導入計画が期限切れです。')
   return self.job(pid,lambda progress:st.install(plan,progress))
  if a=='recommended-plan':
   return self.job(pid,lambda progress:self.recommended_plan(pid,progress))
  if a=='content':return st.content_action(pid,b['filename'],b['operation'])
  if a=='importContent':return st.import_content(pid,b['filename'],base64.b64decode(b['data'],validate=True),b['kind'])
  if a=='importProfile':return st.import_profile(base64.b64decode(b['data'],validate=True))
  if a=='migrate':return self.job(pid,lambda _:st.migration(pid,b['mc']))
  if a=='backup':return {'backup':st.backup(pid)}
  if a=='restore':st.restore(pid,b['backup']);return {}
  if a=='favorite':
   s=st.state();x=b['project']
   if x in s['favorites']:s['favorites'].remove(x)
   else:s['favorites'].append(x)
   st.save(s);return {}
  if a=='prism':
   s=st.state();s['prism']=b.get('exe','');s['prismRoot']=b.get('root','');st.save(s);return {}
  if a=='launch':return st.launch(pid)
  if a=='crash':return crash_analysis(b.get('text',''))
  if a=='updates':
   return self.job(pid,lambda _:self.updates(pid))
  raise ClientError('操作が不正です。')
 def make_plan(self,pid,proj,kind):
  p=self.store.plan(pid,proj,kind);key=secrets.token_hex(12);self.plans[key]=p;return dict(p,plan=key)
 def recommended_plan(self,pid,progress):
  # Pinned researched versions; planning never installs. User reviews exact licenses.
  nodes={};p=self.store.profile(pid)
  for c in self.catalog:
   if c['optional']:continue
   progress({'phase':'resolve','name':c['name']})
   for n in self.store.plan(pid,c['id'],'mod',c['version_id'])['nodes']:nodes[n['project']]=n
  plan={'profile':pid,'revision':p['revision'],'nodes':list(nodes.values()),'total':sum(n['file']['size'] for n in nodes.values()),'notice':'調査済みのVersionを使用します。Licenseと依存Modを確認してください。PolyPatcher 1.10.4とOneConfigの起動は検証済み。GPU性能・全競合組合せは未確認です。'}
  key=secrets.token_hex(12);self.plans[key]=plan;return dict(plan,plan=key)
 def updates(self,pid):
  p=self.store.profile(pid);out=[]
  for c in p['content']:
   if not c.get('project'):continue
   try:
    plan=self.make_plan(pid,c['project'],c['kind']);latest=next(n for n in plan['nodes'] if n['project']==c['project'])
    if latest['version_id']!=c['version_id']:out.append({'name':c['name'],'from':c['version'],'to':latest['version'],'classification':'Need Review','plan':plan})
   except ClientError as e:out.append({'name':c['name'],'classification':'Incompatible / unavailable','message':str(e)})
  return out

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--data');parser.add_argument('--port',type=int,default=0);parser.add_argument('--no-browser',action='store_true');args=parser.parse_args()
 default=default_data_path()
 app=App(args.data or default)
 class Handler(BaseHTTPRequestHandler):
  def log_message(self,*_):pass
  def send(self,status,data,ctype='application/json',extra=None):
   blob=json.dumps(data,ensure_ascii=False).encode() if ctype=='application/json' else data
   self.send_response(status);self.send_header('Content-Type',ctype);self.send_header('Content-Length',str(len(blob)));self.send_header('Cache-Control','no-store');self.send_header('X-Content-Type-Options','nosniff')
   self.send_header('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; object-src 'none'")
   for k,v in (extra or {}).items():self.send_header(k,v)
   self.end_headers();self.wfile.write(blob)
  def valid_host(self):return self.headers.get('Host')==f'127.0.0.1:{self.server.server_port}'
  def do_GET(self):
   if not self.valid_host():self.send(403,{'error':'Host rejected'});return
   u=urllib.parse.urlparse(self.path);q=urllib.parse.parse_qs(u.query)
   try:
    if u.path=='/api/state':
     app.sync_game_settings()
     s=app.store.state()
     for p in s['profiles']:p['availability']=app.availability(p['id']);p['integration']=app.integration_info(p['id'])
     s.update(modules=__import__('modules').CATALOG,product=PRODUCT,token=app.token,presets=PRESETS,catalog=app.catalog,busy=list(app.busy),defaultPrismRoot=default_prism_root());self.send(200,s)
    elif u.path=='/api/check':
     pid=q['profile'][0];p=app.store.profile(pid);self.send(200,check_mods(app.store.game(pid),p['mc'],p['loader']))
    elif u.path=='/api/search':
     kind=q.get('kind',['mod'])[0];mc=q.get('mc',[MC])[0];category=q.get('category',[''])[0]
     if kind not in ['mod','resourcepack','shader']:raise ClientError('種類が不正です。')
     facets=[['project_type:'+kind],['versions:'+mc]]
     if kind=='mod':facets.append(['categories:forge'])
     if category:facets.append(['categories:'+category])
     data=request_json('search',{'query':q.get('q',[''])[0][:100],'facets':json.dumps(facets),'limit':'16','index':'downloads'})
     self.send(200,{'hits':[{k:h.get(k) for k in ['project_id','slug','title','description','categories','downloads']} for h in data['hits']]})
    elif u.path=='/api/job':self.send(200,app.jobs.get(q['id'][0],{'status':'error','error':'Job missing'}))
    elif u.path=='/api/diagnostics':
     try:
      import resource
      peak=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss/(1048576 if sys.platform=='darwin' else 1024)
     except ImportError:peak=None
     self.send(200,{'cpu_seconds':time.process_time(),'peak_rss_mib':peak,'uptime_seconds':time.perf_counter()-app.started})
    elif u.path=='/api/export':self.send(200,app.store.export(q['profile'][0]),'application/zip',{'Content-Disposition':'attachment; filename="'+PRODUCT['bundleName']+'-Profile.zip"'})
    elif u.path=='/api/mrpack':self.send(200,app.store.mrpack(q['profile'][0]),'application/x-modrinth-modpack+zip',{'Content-Disposition':'attachment; filename="'+PRODUCT['bundleName']+'-Profile.mrpack"'})
    elif u.path=='/api/backups':self.send(200,[p.name for p in sorted((app.store.base(q['profile'][0])/'backups').glob('*'),reverse=True)])
    elif u.path=='/api/pack-preview':
     pid=q['profile'][0];name=safe_name(q['filename'][0]);p=app.store.profile(pid)
     if not any(c['filename']==name and c['kind']=='resourcepack' for c in p['content']):raise ClientError('Packがありません。')
     z=zipfile.ZipFile(app.store.game(pid)/'resourcepacks'/name);imgs=[]
     names=['pack.png','assets/minecraft/textures/items/diamond_sword.png','assets/minecraft/textures/items/bow_standby.png','assets/minecraft/textures/blocks/stone.png','assets/minecraft/textures/blocks/dirt.png']
     for name in names:
      if name in z.namelist() and z.getinfo(name).file_size<=2*1024*1024:imgs.append({'name':name,'data':'data:image/png;base64,'+base64.b64encode(z.read(name)).decode()})
     self.send(200,{'images':imgs,'notice':'Pack内に存在する画像のみ表示。ゲーム全体の描画結果ではありません。'})
    else:
     name='index.html' if u.path=='/' else u.path.lstrip('/')
     if name not in ['index.html','app.js','style.css','prototypes.html','prototypes.js',PRODUCT['logo'],PRODUCT['icon']]:self.send(404,{'error':'Not found'});return
     path=ROOT/'web'/name;blob=path.read_bytes()
     if name.endswith('.html'):
      page=blob.decode('utf-8')
      for k in ['name','shortName','version','logo','icon','accent']:page=page.replace('{{product.'+k+'}}',html.escape(PRODUCT[k],quote=True))
      blob=page.encode('utf-8')
     self.send(200,blob,mimetypes.guess_type(name)[0] or 'text/plain')
   except Exception as e:self.send(400,{'error':str(e) if isinstance(e,ClientError) else '取得に失敗しました。'})
  def do_POST(self):
   origin=f'http://127.0.0.1:{self.server.server_port}'
   if not self.valid_host() or self.headers.get('Origin')!=origin or self.headers.get('X-Client-Token')!=app.token:self.send(403,{'error':'Origin/session rejected'});return
   if self.path!='/api/action':self.send(404,{'error':'Not found'});return
   try:
    length=int(self.headers.get('Content-Length','0'))
    if length<1 or length>90*1024*1024:raise ClientError('リクエストサイズが不正です。')
    b=json.loads(self.rfile.read(length));self.send(200,app.action(b))
   except Exception as e:self.send(400,{'error':str(e) if isinstance(e,ClientError) else '処理に失敗しました: '+type(e).__name__})
 server=ThreadingHTTPServer(('127.0.0.1',args.port),Handler);server.daemon_threads=True
 url=f'http://127.0.0.1:{server.server_port}'
 print(json.dumps({'url':url,'data':str(app.store.path),'ready_ms':round((time.perf_counter()-app.started)*1000,2)}),flush=True)
 if not args.no_browser:webbrowser.open(url)
 try:server.serve_forever()
 except KeyboardInterrupt:pass
 finally:server.server_close()

if __name__=='__main__':main()
