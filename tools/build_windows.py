"""Optional onedir Windows packaging. Run after tools/build.py on Windows."""
import json,os,sys,subprocess,shutil,importlib.metadata,sysconfig
from pathlib import Path
if sys.platform!='win32':raise SystemExit('Windows Python is required; this script is not a cross-compiler.')
r=Path(__file__).resolve().parents[1];p=json.loads((r/'launcher/product.json').read_text());out=r/'windows-build';out.mkdir(exist_ok=True)
args=[sys.executable,'-m','PyInstaller','--noconfirm','--clean','--onedir','--name',p['bundleName'],'--distpath',str(out/'dist'),'--workpath',str(out/'work'),'--specpath',str(out),'--paths',str(r/'launcher')]
for src,dest in [('launcher/product.json','.'),('launcher/catalog.json','.'),('launcher/modules.json','.'),('launcher/web','web'),('launcher/payload','payload'),('LICENSE','.'),('THIRD_PARTY.md','.'),('LICENSE_NOTES.md','.')]:args+=['--add-data',str(r/src)+os.pathsep+dest]
subprocess.run(args+[str(r/'tools/windows_entry.py')],check=True,cwd=r)
bundle=out/'dist'/p['bundleName'];licenses=bundle/'licenses';licenses.mkdir(exist_ok=True)
python_license=Path(sys.base_prefix)/'LICENSE.txt'
if not python_license.exists():raise RuntimeError('Python distribution license missing: packaging stopped')
shutil.copy2(python_license,licenses/'PYTHON-LICENSE.txt')
found=False
for f in importlib.metadata.files('pyinstaller') or []:
 if f.name=='COPYING.txt':shutil.copy2(f.locate(),licenses/'PYINSTALLER-COPYING.txt');found=True;break
if not found:raise RuntimeError('PyInstaller license missing: packaging stopped')
shutil.make_archive(str(out/(p['bundleName']+'-'+p['version']+'-windows')), 'zip',bundle.parent,bundle.name)
print('Windows package generated:',bundle)
