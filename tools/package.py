"""Package Ttro Client code only; licensed game and external Mod binaries stay external."""
import json,zipfile,shutil,sys,hashlib,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'launcher'))
from core import Store
from brand import PRODUCT,JAR_NAME
out=ROOT/'dist';out.mkdir(exist_ok=True);bundle=PRODUCT['bundleName'];version=PRODUCT['version']
pyz=out/(bundle+'-'+version+'.pyz');pack=out/(bundle+'-Competitive-1.8.9.mrpack');source=out/(bundle+'-'+version+'-project.zip')
main='''import tempfile,zipfile,pathlib,sys,runpy
with tempfile.TemporaryDirectory(prefix="ttro-client-") as d:
 with zipfile.ZipFile(sys.argv[0]) as z:z.extractall(d)
 sys.path.insert(0,d)
 runpy.run_path(str(pathlib.Path(d)/"app.py"),run_name="__main__")
'''
with zipfile.ZipFile(pyz,'w',zipfile.ZIP_DEFLATED) as z:
 z.writestr('__main__.py',main)
 for name in ['LICENSE','THIRD_PARTY.md','LICENSE_NOTES.md']:z.write(ROOT/name,name)
 for f in (ROOT/'launcher').rglob('*'):
  if f.is_file() and '__pycache__' not in f.parts:z.write(f,f.relative_to(ROOT/'launcher'))
with tempfile.TemporaryDirectory(prefix='ttro-package-') as d:
 state=Store(d);pid=state.state()['selected'];state.preset(pid,'Competitive');state.write_settings(pid);pack.write_bytes(state.mrpack(pid))
shutil.copy2(ROOT/'launcher/payload'/JAR_NAME,out/JAR_NAME)
exclude={'.git','.gradle','build','run','dist','__pycache__','node_modules','.venv','venv','windows-build','OneConfig'}
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(ROOT.rglob('*')):
  rel=f.relative_to(ROOT)
  if f.is_file() and not any(p in exclude for p in rel.parts):z.write(f,Path(bundle)/rel)
 z.write(pyz,str(Path(bundle)/pyz.name));z.write(pack,str(Path(bundle)/pack.name))
checks={p.name:{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [source,pack,out/JAR_NAME,pyz]}
(out/'SHA256.json').write_text(json.dumps(checks,indent=2)+'\n');print(json.dumps(checks,indent=2))
