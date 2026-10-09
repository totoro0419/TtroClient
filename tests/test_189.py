import sys,unittest,tempfile,zipfile,io,json,copy,hashlib
from unittest.mock import patch
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'launcher'))
from core import Store,ClientError,validate_zip
from app import App
from minecraft189 import forge_range,check,prism_components,resource_pack_order
from modules import DEFAULT_SETTINGS,validate_settings
from brand import PRODUCT,JAR_NAME,COMPAT
class PlatformTests(unittest.TestCase):
 def test_version_range(self):
  for value,expected in [('[1.8.9]',True),('[1.8,1.9)',True),('[1.9,)',False),('(,1.8.9]',True),('1.21.1',False),('${mcversion}',None)]:self.assertEqual(forge_range(value,'1.8.9'),expected,value)
 def test_prism_is_forge(self):
  self.assertEqual(prism_components(),[{'uid':'net.minecraft','version':'1.8.9','important':True},{'uid':'net.minecraftforge','version':'11.15.1.2318'}])
 def test_resource_order(self):
  content=[dict(kind='resourcepack',filename='high.zip',enabled=True),dict(kind='resourcepack',filename='off.zip',enabled=False),dict(kind='resourcepack',filename='low.zip',enabled=True)]
  self.assertEqual(resource_pack_order(content),['low.zip','high.zip'])
 def test_candidate_and_nonfinite_rejected(self):
  s=copy.deepcopy(DEFAULT_SETTINGS);s['modules']['reach']['enabled']=True
  with self.assertRaises(ClientError):validate_settings(s)
  s=copy.deepcopy(DEFAULT_SETTINGS);s['hud']['scale']=float('nan')
  with self.assertRaises(ClientError):validate_settings(s)
 def test_individual_hud_positions(self):
  s=copy.deepcopy(DEFAULT_SETTINGS);s['hud']['layout']='individual';s['hud']['elements']['fps']={'x':220,'y':40,'scale':1.5}
  validated=validate_settings(s);self.assertEqual(validated['hud']['elements']['fps']['x'],220)
  old=copy.deepcopy(DEFAULT_SETTINGS);old['hud'].pop('elements');self.assertEqual(len(validate_settings(old)['hud']['elements']),12)
  s['hud']['elements']['fps']['scale']=float('inf')
  with self.assertRaises(ClientError):validate_settings(s)
  s=copy.deepcopy(DEFAULT_SETTINGS);s['hud']['elements']['enemy-distance']={'x':1,'y':1,'scale':1}
  with self.assertRaises(ClientError):validate_settings(s)
 def test_zip_traversal_and_fabric_rejected(self):
  for name,data in [('../outside','x'),('fabric.mod.json','{"id":"fabric","version":"1"}')]:
   b=io.BytesIO()
   with zipfile.ZipFile(b,'w') as z:z.writestr(name,data)
   with self.assertRaises((ClientError,ValueError)):validate_zip(b.getvalue(),'mod')
 def test_duplicate_mousetweaks(self):
  with tempfile.TemporaryDirectory() as d:
   game=Path(d);(game/'mods').mkdir()
   for mid in ['ttro189','MouseTweaks']:
    with zipfile.ZipFile(game/'mods'/(mid+'.jar'),'w') as z:z.writestr('mcmod.info',json.dumps([dict(modid=mid,mcversion='1.8.9')]))
   self.assertTrue(any(x['level']=='error' and '二重' in x['message'] for x in check(game)))
 def test_external_availability_tracks_disabled_jar(self):
  with tempfile.TemporaryDirectory() as d:
   app=App(d);pid=app.store.state()['selected'];self.assertFalse(app.availability(pid)['fullbright'])
   f=app.store.game(pid)/'mods/patcher-fixture.jar'
   with zipfile.ZipFile(f,'w') as z:z.writestr('mcmod.info',json.dumps([dict(modid='patcher',mcversion='1.8.9')]))
   self.assertTrue(app.availability(pid)['fullbright']);f.rename(f.with_name(f.name+'.disabled'));self.assertFalse(app.availability(pid)['fullbright'])
 def test_foundation_and_catalog_upgrade(self):
  old=copy.deepcopy(DEFAULT_SETTINGS);old['modules'].pop('waterfov');old['modules']['inputfix'].pop('layout');old['modules']['mousebind']['enabled']=False
  upgraded=validate_settings(old)
  self.assertIn('waterfov',upgraded['modules']);self.assertEqual(upgraded['modules']['inputfix']['layout'],'QWERTY');self.assertTrue(upgraded['modules']['mousebind']['enabled'])
  old['modules']['inputfix']['layout']='made-up'
  with self.assertRaises(ClientError):validate_settings(old)
 def test_owned_integration_upgrade_preserves_disabled_and_backup(self):
  self.integration_upgrade(disabled=True,owned=True)
 def test_unknown_integration_is_retained(self):
  self.integration_upgrade(disabled=False,owned=False)
 def test_integration_update_record_failure_rolls_back(self):
  self.integration_upgrade(disabled=False,owned=True,fail=True)
 def test_legacy_shortcut_conflict_is_explained_not_overwritten(self):
  with tempfile.TemporaryDirectory() as d:
   app=App(d);pid=app.store.state()['selected'];game=app.store.game(pid);text='key_'+PRODUCT['name']+' Settings:54\n';(game/'options.txt').write_text(text)
   with zipfile.ZipFile(game/'mods/patcher-fixture.jar','w') as z:z.writestr('mcmod.info',json.dumps([dict(modid='patcher',mcversion='1.8.9')]))
   info=app.integration_info(pid);self.assertEqual(info['status'],'review');self.assertIn('右Shift',info['message']);self.assertIn('Config',info['message']);self.assertEqual((game/'options.txt').read_text(),text)
 def integration_upgrade(self,disabled,owned,fail=False):
  with tempfile.TemporaryDirectory() as d,tempfile.TemporaryDirectory() as upstream:
   root=Path(upstream);(root/'payload').mkdir();(root/'catalog.json').write_text('[]')
   def payload(version):
    b=io.BytesIO()
    with zipfile.ZipFile(b,'w') as z:z.writestr('mcmod.info',json.dumps([dict(modid=PRODUCT['internalId'],name=PRODUCT['name'],version=version,mcversion='1.8.9')]))
    return b.getvalue()
   current=payload(PRODUCT['version']);previous=payload('0.1.0');(root/'payload'/JAR_NAME).write_bytes(current)
   store=Store(d);pid=store.state()['selected'];old=store.game(pid)/('mods/old.jar'+('.disabled' if disabled else ''));old.write_bytes(previous)
   if owned:(store.base(pid)/COMPAT['integrationRecord']).write_text(json.dumps(dict(sha256=hashlib.sha256(previous).hexdigest())))
   import os
   replace=os.replace
   def replace_record(src,dst):
    if fail and str(dst).endswith(COMPAT['integrationRecord']):raise OSError('QA simulated record failure')
    return replace(src,dst)
   with patch('app.ROOT',root),patch('app.os.replace',replace_record):app=App(d)
   if fail:
    self.assertEqual(old.read_bytes(),previous);self.assertEqual(app.integrations[pid]['status'],'review');self.assertEqual(len(list((store.game(pid)/'mods').iterdir())),1)
   elif owned:
    dest=store.game(pid)/('mods/'+JAR_NAME+('.disabled' if disabled else ''));self.assertFalse(old.exists());self.assertEqual(dest.read_bytes(),current)
    backup=store.base(pid)/'backups'/app.integrations[pid]['backup']/'game/mods'/old.name;self.assertEqual(backup.read_bytes(),previous)
    self.assertEqual(len(list((store.game(pid)/'mods').iterdir())),1)
   else:self.assertEqual(old.read_bytes(),previous);self.assertEqual(app.integrations[pid]['status'],'review')
 def test_pack_import_toggle_order(self):
  with tempfile.TemporaryDirectory() as d:
   st=Store(d);pid=st.state()['selected']
   def pack():
    b=io.BytesIO()
    with zipfile.ZipFile(b,'w') as z:z.writestr('pack.mcmeta',json.dumps({'pack':{'pack_format':1,'description':'Ttro QA fixture'}}))
    return b.getvalue()
   st.import_content(pid,'a.zip',pack(),'resourcepack');st.import_content(pid,'b.zip',pack(),'resourcepack')
   st.content_action(pid,'a.zip','disable');self.assertFalse(next(c for c in st.profile(pid)['content'] if c['filename']=='a.zip')['enabled'])
   st.content_action(pid,'a.zip','enable');st.content_action(pid,'b.zip','up')
   options=(st.game(pid)/'options.txt').read_text();self.assertEqual(json.loads(options.split('resourcePacks:')[1].splitlines()[0]),['a.zip','b.zip'])
   bad=io.BytesIO()
   with zipfile.ZipFile(bad,'w') as z:z.writestr('pack.mcmeta',json.dumps({'pack':{'pack_format':34,'description':'wrong version'}}))
   with self.assertRaises(ClientError):st.import_content(pid,'modern.zip',bad.getvalue(),'resourcepack')
 def test_backup_shader_options_and_revision(self):
  with tempfile.TemporaryDirectory() as d:
   app=App(Path(d));store=app.store;state=store.state();p=state['profiles'][0];pid=p['id'];game=store.game(pid);game.mkdir(parents=True,exist_ok=True)
   (game/'optionsshaders.txt').write_text('shaderPack:old.zip');bid=store.backup(pid);(game/'optionsshaders.txt').write_text('shaderPack:new.zip');store.restore(pid,bid)
   self.assertEqual((game/'optionsshaders.txt').read_text(),'shaderPack:old.zip')
   rev=store.profile(pid)['revision'];s=copy.deepcopy(DEFAULT_SETTINGS);s['modules']['cps']['enabled']=False;app.action(dict(action='settings',profile=pid,settings=s,revision=rev))
   with self.assertRaises(ClientError):app.action(dict(action='settings',profile=pid,settings=s,revision=rev))
if __name__=='__main__':unittest.main()
