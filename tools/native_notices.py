"""Collect shipped NuGet/runtime notices from the restored packages, retaining source attribution."""
import json, os, shutil, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1]
assets=json.loads((root/'launcher/native/Ttro.Launcher/obj/project.assets.json').read_text())
cache=Path(next(iter(assets['packageFolders'])))
out=root/'launcher/native/licenses';out.mkdir(exist_ok=True)
inventory=[]
for identity,lib in sorted(assets['libraries'].items()):
 if lib['type']!='package':continue
 folder=cache/lib['path']; nuspec=next(folder.glob('*.nuspec'));xml=ET.parse(nuspec).getroot()
 def tag(name):return next((e for e in xml.iter() if e.tag.split('}')[-1]==name),None)
 license=tag('license'); repo=tag('repository'); authors=tag('authors')
 notices=[]
 for p in folder.rglob('*'):
  if p.is_file() and (p.name.lower().startswith(('license','third-party-notices','thirdpartynotices')) or license is not None and license.attrib.get('type')=='file' and p.relative_to(folder).as_posix()==license.text):
   dest=out/(identity.replace('/','-')+'-'+p.name);shutil.copy2(p,dest);notices.append(dest.name)
 inventory.append({'package':identity,'authors':authors.text if authors is not None else None,'license':license.text if license is not None else None,'license_type':license.attrib.get('type') if license is not None else None,'source':repo.attrib.get('url') if repo is not None else None,'included_notices':notices})
(out/'DEPENDENCIES.json').write_text(json.dumps(inventory,indent=2)+'\n')
print('Collected package attribution for',len(inventory),'dependencies.')
