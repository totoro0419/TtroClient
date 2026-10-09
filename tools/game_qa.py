"""Audit-workspace real Forge QA. Required runtime paths are intentionally explicit.
The QA Mod never enters product packages. Needs Xvfb, real 1.8.9 runtime,
JDK 8, official MCP mappings and upstream real Forge compile API.
"""
import argparse,os,subprocess,shutil,time,zipfile,json,sys,tempfile,secrets,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];WORK=ROOT.parent
parser=argparse.ArgumentParser();parser.add_argument('--game',default='qa-final-smoke');parser.add_argument('--fast',action='store_true');parser.add_argument('--baseline',action='store_true');parser.add_argument('--patcher',action='store_true');parser.add_argument('--inventory-only',action='store_true');parser.add_argument('--bench-only',action='store_true');parser.add_argument('--fps',type=int,default=60);parser.add_argument('--pairs',type=int,default=1);parser.add_argument('--packs',action='store_true');parser.add_argument('--native-packs',action='store_true');args=parser.parse_args()
JDK=Path(os.environ.get('JAVA_HOME',WORK/'toolchain/jdk8u504-b01'))/'bin'
gradle_cache=Path(os.environ.get('GRADLE_USER_HOME',Path.home()/'.gradle'))/'caches/minecraft'
api=Path(os.environ.get('TTRO_FORGE_API',gradle_cache/'net/minecraftforge/forge/1.8.9-11.15.1.2318-1.8.9/stable/22/forgeBin-1.8.9-11.15.1.2318-1.8.9.jar'))
if not api.exists():raise RuntimeError('Run the standard ForgeGradle build first, or supply TTRO_FORGE_API as a real Forge 2318 MCP stable_22 API JAR')
mapping=gradle_cache/'de/oceanlabs/mcp/mcp_stable/22/srgs/mcp-srg.srg'
if not mapping.exists():mapping=ROOT/'forge/build/mcp-to-srg.srg'
libs=list((WORK/'runtime189/libraries').rglob('*.jar'))+[api,WORK/'toolchain/asm.jar'];cp=os.pathsep.join(map(str,libs))
game=WORK/args.game
if game.parent!=WORK or not args.game.startswith('qa-'):raise ValueError('Use an isolated qa-* directory')
qa_build=game/'qa-build';classes=qa_build/'classes';shutil.rmtree(classes,ignore_errors=True);classes.mkdir(parents=True)
subprocess.run([str(JDK/'javac'),'-source','8','-target','8','-cp',cp,'-d',str(classes),str(ROOT/'tests/game/Probe.java')],check=True)
dev=qa_build/'qa-dev.jar';jar=qa_build/'ttro-qa.jar'
with zipfile.ZipFile(dev,'w',zipfile.ZIP_DEFLATED) as z:
 for p in classes.rglob('*.class'):z.write(p,p.relative_to(classes))
subprocess.run([str(JDK/'java'),'-cp',os.pathsep.join([str(WORK/'toolchain/specialsource.jar'),str(dev),cp]),'net.md_5.specialsource.SpecialSource','-i',str(dev),'-o',str(jar),'-m',str(mapping),'--live'],check=True)
mods=game/'mods';mods.mkdir(parents=True,exist_ok=True);shutil.copy2(jar,mods/jar.name)
product=json.loads((ROOT/'launcher/product.json').read_text());client_name=product['artifact']+'-'+product['version']+'.jar'
if args.baseline:shutil.copy2(WORK/'toolchain/mousetweaks.jar',mods/'mousetweaks.jar')
else:
 shutil.copy2(ROOT/'launcher/payload'/client_name,mods/client_name)
 if args.fast:shutil.copy2(WORK/'toolchain/hypixel-mod-api.jar',mods/'hypixel-mod-api.jar')
 if args.patcher:shutil.copy2(WORK/'toolchain/patcher.jar',mods/'patcher.jar')
 if args.patcher:
  # Private QA cache only. Do not disable upstream autoUpdate: the legacy
  # loader then skips addToClasspath as well as updating. No binary patches.
  metadata=json.loads((WORK/'toolchain/oneconfig-metadata.json').read_text());import hashlib
  for key,source,target in [('loader','oneconfig-loader.jar','launchwrapper/OneConfig-Loader.jar'),('release','oneconfig-release.jar','OneConfig (1.8.9-forge).jar')]:
   payload=WORK/'toolchain'/source
   if hashlib.sha256(payload.read_bytes()).hexdigest()!=metadata[key]['sha256']:raise ValueError('Official OneConfig cache hash mismatch')
   dest=game/'OneConfig'/target;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(payload,dest)
  (game/'OneConfig/OneConfig.json').write_text(json.dumps({'autoUpdate':True,'updateChannel':0}))
if args.packs:
 if args.native_packs:
  subprocess.run(['dotnet','run','--project',str(ROOT/'tests/native/Ttro.Launcher.Tests.csproj'),'-c','Release','--','--export-game-packs',str(game)],check=True)
 else:
  sys.path.insert(0,str(ROOT/'launcher'));from core import Store
  with tempfile.TemporaryDirectory(prefix='ttro-qa-packs-') as directory:
   store=Store(directory);pid=store.state()['selected']
   for name in ['qa-pack-low.zip','qa-pack-high.zip']:store.import_content(pid,name,(ROOT/'tests/fixtures'/name).read_bytes(),'resourcepack')
   store.content_action(pid,'qa-pack-high.zip','up');shutil.copytree(store.game(pid)/'resourcepacks',game/'resourcepacks',dirs_exist_ok=True);shutil.copy2(store.game(pid)/'options.txt',game/'options.txt')
log=ROOT/'research'/(args.game+'.log');gc=ROOT/'research'/(args.game+'-gc.log');start=time.monotonic()
with log.open('w') as output,(ROOT/'research/xvfb.log').open('w') as xlog:
 display=200+secrets.randbelow(4000)
 while Path('/tmp/.X'+str(display)+'-lock').exists():display=200+secrets.randbelow(4000)
 x=subprocess.Popen(['Xvfb',':'+str(display),'-screen','0','1280x800x24','-ac','-listen','tcp','-nolisten','unix','+iglx'],stdout=xlog,stderr=xlog);time.sleep(.5)
 if x.poll() is not None:raise RuntimeError('QA virtual display did not start')
 env=os.environ.copy();env.update(DISPLAY='127.0.0.1:'+str(display),LIBGL_ALWAYS_SOFTWARE='1')
 flags=['-Dttro.qa.fps='+str(args.fps),'-Dttro.qa.windows='+str(2*args.pairs-1),'-XX:+PrintGCDetails','-XX:+PrintGCTimeStamps','-Xloggc:'+str(gc)]
 if args.fast:flags+=['-Dttro.qa.fast=true']
 if args.inventory_only:flags+=['-Dttro.qa.inventoryOnly=true']
 if args.bench_only:flags+=['-Dttro.qa.benchOnly=true']
 if args.packs:flags+=['-Dttro.qa.packs=true']
 if args.patcher:flags+=['-Dttro.qa.patcher=true']
 command=[str(JDK/'java'),'-Xms256m','-Xmx1024m']+flags+['-Djava.library.path='+str(WORK/'runtime189/natives'),'-cp',(WORK/'runtime189/classpath.txt').read_text(),'net.minecraft.launchwrapper.Launch','--username','TtroQA','--version','1.8.9','--gameDir',str(game),'--assetsDir',str(WORK/'runtime189/assets'),'--assetIndex','1.8','--uuid','00000000000000000000000000000001','--accessToken','0','--userProperties','{}','--userType','legacy','--tweakClass','net.minecraftforge.fml.common.launcher.FMLTweaker','--width','960','--height','540']
 try:run=subprocess.run(command,env=env,cwd=game,stdout=output,stderr=output,timeout=260)
 finally:x.terminate();x.wait(timeout=10)
result=game/'qa-results/result.json'
if result.exists():
 target=ROOT/'research/game-qa'/args.game;target.mkdir(parents=True,exist_ok=True);shutil.copy2(result,target/'result.json');shutil.copytree(game/'qa-results/screenshots',target/'screenshots',dirs_exist_ok=True)
 data=json.loads(result.read_text());checks=data['checks'];print(json.dumps({'game':args.game,'exit_code':run.returncode,'seconds':round(time.monotonic()-start,1),'checks':[{k:v for k,v in c.items() if k in ['name','status']} for c in checks],'frames':len(data['frames'])},indent=2))
 from PIL import Image
 captures=list((target/'screenshots').glob('*.png'))
 for capture in captures:
  with Image.open(capture) as picture:picture.verify()
 data['screenshot_validation']={'status':'PASS','png_count':len(captures)}
 if args.native_packs:
  data['native_pack_provenance']=json.loads((game/'native-pack-provenance.json').read_text())
 data['launch']={'exit_code':run.returncode,'test_seconds':round(time.monotonic()-start,1),'fps_limit':args.fps,'patcher':args.patcher,'packs':args.packs,'baseline':args.baseline}
 if not args.baseline:
  data['artifact_sha256']=hashlib.sha256((mods/client_name).read_bytes()).hexdigest()
  expected=json.loads((ROOT/'launcher/payload/client-manifest.json').read_text())['sha256']
  if data['artifact_sha256']!=expected:raise ValueError('QA runtime payload does not match packaged client manifest')
 (target/'result.json').write_text(json.dumps(data,indent=2)+'\n')
 if run.returncode!=0 or any(c['status']=='FAIL' for c in checks):raise SystemExit(1)
else:raise RuntimeError('No real game QA result. See '+str(log))
