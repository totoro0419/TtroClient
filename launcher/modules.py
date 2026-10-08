"""Declarative capabilities. Unimplemented features cannot be enabled."""
import copy,json,math,re
from pathlib import Path
CATALOG=json.loads((Path(__file__).parent/'modules.json').read_text(encoding='utf-8'))
BY_ID={x['id']:x for x in CATALOG}
HUD_IDS=('fps','ping','cps','keystrokes','bps','coordinates','memory','armor','potions','items','clock','connection')
DEFAULT_SETTINGS={'schema':2,'modules':{m['id']:{'enabled':m['default'] if m['status']!='candidate' else False,**{k:v['default'] for k,v in m.get('settings',{}).items()}} for m in CATALOG},'hud':{'layout':'clusters','x':12,'y':12,'scale':1.0,'elements':{mid:{'x':12+(i//6)*220,'y':12+(i%6)*26,'scale':1.0} for i,mid in enumerate(HUD_IDS)}}}

def validate_settings(s):
 from core import ClientError
 if not isinstance(s,dict) or s.get('schema')!=2:raise ClientError('Forge 1.8.9用の設定schema 2が必要です。')
 out=copy.deepcopy(DEFAULT_SETTINGS)
 hud=s.get('hud',{})
 if not isinstance(hud,dict):raise ClientError('HUD設定が不正です。')
 mods=s.get('modules',{})
 if not isinstance(mods,dict):raise ClientError('Module設定が不正です。')
 for mid,props in mods.items():
  if mid not in BY_ID or not isinstance(props,dict):raise ClientError('未知のModuleまたは設定形式です。')
  m=BY_ID[mid];enabled=props.get('enabled',out['modules'][mid]['enabled'])
  if not isinstance(enabled,bool):raise ClientError('ON/OFFには真偽値が必要です。')
  if enabled and m['status']=='candidate':raise ClientError(m['name']+' は未実装です。')
  out['modules'][mid]['enabled']=True if m.get('control')=='foundation' else enabled
  for k,defn in m.get('settings',{}).items():
   v=props.get(k,defn['default']);t=defn['type']
   if t=='boolean' and not isinstance(v,bool):raise ClientError('真偽値が必要です。')
   if t=='number':
    if isinstance(v,bool) or not isinstance(v,(int,float)) or not math.isfinite(v) or not defn['min']<=v<=defn['max']:raise ClientError('数値が範囲外です。')
   if t=='select' and v not in defn['options']:raise ClientError('選択値が不正です。')
   out['modules'][mid][k]=v
 for k,defn in [('layout',['clusters','individual'])]:
  v=s.get('hud',{}).get(k,out['hud'][k])
  if v not in defn:raise ClientError('HUD layoutが不正です。')
  out['hud'][k]=v
 for k,lo,hi in [('x',0,10000),('y',0,10000),('scale',.5,3)]:
  v=s.get('hud',{}).get(k,out['hud'][k])
  if isinstance(v,bool) or not isinstance(v,(int,float)) or not math.isfinite(v) or not lo<=v<=hi:raise ClientError('HUD数値が不正です。')
  out['hud'][k]=v
 elements=hud.get('elements',{})
 if not isinstance(elements,dict):raise ClientError('個別HUD配置が不正です。')
 for mid,position in elements.items():
  if mid not in HUD_IDS or not isinstance(position,dict):raise ClientError('未知のHUDです。')
  for k,lo,hi in [('x',0,10000),('y',0,10000),('scale',.5,3)]:
   v=position.get(k,out['hud']['elements'][mid][k])
   if isinstance(v,bool) or not isinstance(v,(int,float)) or not math.isfinite(v) or not lo<=v<=hi:raise ClientError('個別HUD数値が範囲外です。')
   out['hud']['elements'][mid][k]=v
 return out
