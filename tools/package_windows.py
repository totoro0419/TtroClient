"""Validate and package a self-contained Windows publish; no Minecraft/Forge binaries bundled."""
import hashlib, json, shutil, subprocess, argparse, os
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(); p.add_argument('--makensis',default='makensis'); args=p.parse_args()
product=json.loads((root/'launcher/product.json').read_text()); portable=root/'dist/portable'
assert (portable/'TtroClient.exe').read_bytes()[:2]==b'MZ', 'Windows launcher missing'
manifest=json.loads((portable/'payload/client-manifest.json').read_text())
client=portable/'payload'/manifest['filename']
assert hashlib.sha256(client.read_bytes()).hexdigest()==manifest['sha256'], 'Client payload hash mismatch'
assert product['version'] in manifest['filename'], 'Stale client payload'
for license in ('LICENSE','THIRD_PARTY.md'):
 assert (portable/license).exists(), 'Distribution notice missing: '+license
flag='/D' if os.name=='nt' else '-D'
subprocess.run([args.makensis,flag+'VERSION='+product['version'],flag+'PAYLOAD_FILES='+str(portable/'*'),flag+'OUTPUT='+str(root/'dist/TtroClient-Setup.exe'),str(root/'installer/TtroClient.nsi')],check=True)
shutil.make_archive(str(root/'dist/TtroClient-Portable'),'zip',portable)
shutil.copy2(client,root/'dist'/client.name)
files=[root/'dist/TtroClient-Setup.exe',root/'dist/TtroClient-Portable.zip',root/'dist'/client.name]
(root/'dist/checksums.txt').write_text(''.join(hashlib.sha256(f.read_bytes()).hexdigest()+'  '+f.name+'\n' for f in files))
print(json.dumps({'version':product['version'],'assets':[{ 'name':f.name,'bytes':f.stat().st_size} for f in files]},indent=2))
