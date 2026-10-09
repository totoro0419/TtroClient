"""Deterministic Java 8 compile/remap against real Forge classes, never API stubs.
For this QA run, dependencies are explicitly supplied by the audit workspace.
Standard public build is forge/gradlew clean build (ForgeGradle 2.1).
"""
import subprocess,os,json,zipfile,hashlib,shutil,csv,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];TASK=ROOT.parent
JDK=TASK/'toolchain/jdk8u504-b01/bin'
API=TASK/'upstream/sources/MouseTweaks-minecraft-1.8.9/forgeSrc-1.8.9-11.15.1.1764.jar'
def run(args):subprocess.run([str(x) for x in args],check=True)
def main():
 out=ROOT/'forge/build';shutil.rmtree(out,ignore_errors=True);classes=out/'classes';classes.mkdir(parents=True)
 brand=json.load(open(ROOT/'launcher/product.json'));src=out/'sources';shutil.copytree(ROOT/'forge/src/main/java',src)
 for p in src.rglob('*.java'):p.write_text(p.read_text().replace('@VERSION@',brand['version']).replace('@BRAND@',brand['name']),encoding='utf-8')
 libs=list((TASK/'runtime189/libraries').rglob('*.jar'))+[API,TASK/'toolchain/asm.jar']
 cp=os.pathsep.join(str(p) for p in libs)
 argsfile=out/'javac.args';argsfile.write_text('\n'.join(str(p) for p in src.rglob('*.java')))
 run([JDK/'javac','-source','8','-target','8','-encoding','UTF-8','-cp',cp,'-d',classes,'@'+str(argsfile)])
 shutil.copytree(ROOT/'forge/src/main/resources',classes,dirs_exist_ok=True)
 for n in ['product.json','modules.json']:shutil.copy2(ROOT/'launcher'/n,classes/n)
 (classes/'LICENSE_TTRO').write_bytes((ROOT/'LICENSE').read_bytes());(classes/'LICENSE_MOUSE_TWEAKS').write_bytes((ROOT/'docs/MOUSE_TWEAKS_LICENSE.txt').read_bytes())
 manifest='Manifest-Version: 1.0\nFMLCorePlugin: dev.ttro.LoadingPlugin\nFMLCorePluginContainsFMLMod: true\n\n'
 (classes/'META-INF').mkdir(exist_ok=True);(classes/'META-INF/MANIFEST.MF').write_text(manifest)
 (classes/'mcmod.info').write_text(json.dumps([{'modid':brand['internalId'],'name':brand['name'],'version':brand['version'],'mcversion':'1.8.9','description':'Ttro Client for Forge 1.8.9. Includes attributed Mouse Tweaks inventory controls.','authorList':['Ttro Client contributors'],'credits':'Mouse Tweaks by YaLTeR (BSD-3-Clause)'}],indent=2))
 dev=out/'ttro-dev.jar'
 with zipfile.ZipFile(dev,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(classes.rglob('*')):
   if p.is_file():info=zipfile.ZipInfo(str(p.relative_to(classes)),(2026,10,8,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,p.read_bytes())
 # Compile MCP stable_22 -> SRG field/method map from the official MCP archives.
 stable=zipfile.ZipFile(TASK/'toolchain/mcp-stable.zip');srg=zipfile.ZipFile(TASK/'toolchain/mcp-srg.zip')
 mapping={}
 for f in ['methods.csv','fields.csv']:
  for r in csv.DictReader(stable.read(f).decode('utf8').splitlines()):mapping[r['searge']]=r['name']
 lines=[]
 for line in srg.read('joined.srg').decode().splitlines():
  s=line.split()
  if s[0]=='FD:':
   path=s[2];parent,member=path.rsplit('/',1);lines.append('FD: '+parent+'/'+mapping.get(member,member)+' '+path)
  elif s[0]=='MD:':
   path=s[3];parent,member=path.rsplit('/',1);lines.append('MD: '+parent+'/'+mapping.get(member,member)+' '+s[4]+' '+path+' '+s[4])
 remap=out/'mcp-to-srg.srg';remap.write_text('\n'.join(lines)+'\n')
 jar=out/(brand['artifact']+'-'+brand['version']+'.jar')
 run([JDK/'java','-cp',os.pathsep.join([str(TASK/'toolchain/specialsource.jar'),str(dev),cp]),'net.md_5.specialsource.SpecialSource','-i',dev,'-o',jar,'-m',remap,'--live','--stable'])
 with zipfile.ZipFile(jar) as z:
  for n in z.namelist():
   if n.endswith('.class'):
    assert int.from_bytes(z.read(n)[6:8],'big')==52,'Not Java 8'
  assert not any(n.startswith('net/minecraft/') for n in z.namelist()),'Minecraft bytes must never ship'
 shutil.copy2(jar,ROOT/'launcher/payload'/jar.name)
 result={'status':'PASS','java_target':8,'artifact':jar.name,'sha256':hashlib.sha256(jar.read_bytes()).hexdigest(),'api_compile_baseline':'Forge 1.8.9-11.15.1.1764 real forgeSrc (upstream MouseTweaks repository)','runtime_target':'Forge 11.15.1.2318','reobfuscation':'official MCP stable_22 -> SRG','forgegradle_clean_build':'FAIL - Java Maven network access blocked in this environment'}
 (ROOT/'research/build-result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
if __name__=='__main__':main()
