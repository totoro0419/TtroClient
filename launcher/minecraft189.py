"""The sole Minecraft platform contract: Forge 1.8.9, Java 8. No multi-version bridge."""
import io,json,re,zipfile
MC='1.8.9'
LOADER='11.15.1.2318'
PACK_FORMAT=1
PRESETS={
 'Maximum FPS':{'renderDistance':6,'fancyGraphics':False,'particles':2,'maxFps':260,'enableVsync':False,'ao':0,'useVbo':True},
 'Competitive':{'renderDistance':8,'fancyGraphics':False,'particles':1,'maxFps':240,'enableVsync':False,'ao':0,'useVbo':True},
 'Balanced':{'renderDistance':12,'fancyGraphics':True,'particles':1,'maxFps':144,'enableVsync':False,'ao':2,'useVbo':True},
 'Quality':{'renderDistance':16,'fancyGraphics':True,'particles':0,'maxFps':120,'enableVsync':True,'ao':2,'useVbo':True}}

def forge_metadata(z):
 if 'fabric.mod.json' in z.namelist():raise ValueError('Fabric Modは対象外です。Forge 1.8.9版が必要です。')
 if 'mcmod.info' in z.namelist():
  raw=json.loads(z.read('mcmod.info'));items=raw if isinstance(raw,list) else raw.get('modList',[raw])
  return [m for m in items if m.get('modid')]
 manifest=z.read('META-INF/MANIFEST.MF').decode('utf-8','replace') if 'META-INF/MANIFEST.MF' in z.namelist() else ''
 if 'FMLCorePlugin:' in manifest or 'TweakClass:' in manifest:
  return [{'modid':'<coremod>','version':'unknown','mcversion':'','unverified':True}]
 raise ValueError('Forge metadataを確認できません。')

def forge_range(value,version):
 """Numeric Maven ranges, including exact [version]. Unknown stays None."""
 if not value:return None
 if value==version:return True
 if value.startswith('${') or value.startswith('@'):return None
 if value.startswith('[') and value.endswith(']') and ',' not in value:return value[1:-1]==version
 m=re.fullmatch(r'([\[(])([^,]*),([^\])]*)([\])])',value)
 if not m:return False if re.fullmatch(r'\d+(\.\d+)+',value) else None
 def v(s):return tuple(int(x) for x in s.split('.'))
 try:
  l,lo,hi,h=m.groups();cur=v(version)
  return (not lo or cur>v(lo) or (l=='[' and cur==v(lo))) and (not hi or cur<v(hi) or (h==']' and cur==v(hi)))
 except ValueError:return None

def check(game,mc=MC,loader=LOADER):
 issues=[];ids=set();metas=[]
 for f in sorted((game/'mods').glob('*.jar')):
  try:
   with zipfile.ZipFile(f) as z:ms=forge_metadata(z)
   for m in ms:
    mid=m['modid'];metas.append((m,f.name))
    if mid in ids and mid!='<coremod>':issues.append({'level':'error','file':f.name,'message':'重複Mod ID: '+mid})
    ids.add(mid)
    match=forge_range(m.get('mcversion',''),mc)
    if match is False:issues.append({'level':'error','file':f.name,'message':f'{mid}: Minecraft {m.get("mcversion")} 用です。'})
    elif match is None:issues.append({'level':'review','file':f.name,'message':mid+': 1.8.9対応範囲はmetadataだけでは未確認。'})
    if m.get('unverified'):issues.append({'level':'review','file':f.name,'message':'Coremodの互換性は実起動で確認してください。'})
  except Exception as e:issues.append({'level':'error','file':f.name,'message':str(e)})
 if 'ttro189' in ids and any(i.lower()=='mousetweaks' for i in ids):issues.append({'level':'error','file':'mods','message':'Ttro Client Inventory Controlsと単体Mouse Tweaksは二重導入できません。'})
 for m,name in metas:
  for dep in m.get('dependencies',[]) or []:
   if isinstance(dep,str):
    mid=dep.split('@')[0].split(':')[-1]
    if mid not in ids and mid not in ['Forge','forge','Minecraft','minecraft']:issues.append({'level':'review','file':name,'message':'依存候補の確認が必要: '+dep})
 return issues

def prism_components():
 return [{'uid':'net.minecraft','version':MC,'important':True},{'uid':'net.minecraftforge','version':LOADER}]

def resource_pack_order(content):
 # Legacy 1.8.9 stores zip BASENAMES, not modern file/ prefixes or "vanilla".
 return [c['filename'] for c in reversed(content) if c['kind']=='resourcepack' and c['enabled']]
