"""Public build: JDK 8 + official ForgeGradle 2.1, Minecraft 1.8.9 only."""
import argparse,hashlib,json,subprocess,shutil,os,zipfile
from pathlib import Path
from urllib.parse import urlparse
root=Path(__file__).resolve().parents[1];brand=json.loads((root/'launcher/product.json').read_text())
parser=argparse.ArgumentParser(description='Build the Forge 1.8.9 integration with ForgeGradle; JDK 8 required.')
parser.add_argument('--gradle',help='Installed Gradle 2.14.1 executable; default uses the bundled wrapper.')
parser.add_argument('--proxy-env',action='store_true',help='Forward HTTP_PROXY/HTTPS_PROXY to Java. No proxy configuration is embedded in the product.')
parser.add_argument('--trust-store',help='Administered Java trust store. Optional password comes from TTRO_TRUSTSTORE_PASSWORD (default changeit).')
args=parser.parse_args()
cmd=[str(Path(args.gradle).resolve())] if args.gradle else (['cmd','/c','gradlew.bat'] if os.name=='nt' else ['bash','gradlew'])
flags=[]
if args.proxy_env:
 for protocol in ('http','https'):
  proxy=urlparse(os.environ.get(protocol.upper()+'_PROXY',''))
  if proxy.username or proxy.password:raise SystemExit('Configure authenticated proxies in your local Gradle settings; credentials are not forwarded by this helper.')
  if proxy.hostname and proxy.port:flags+=['-D'+protocol+'.proxyHost='+proxy.hostname,'-D'+protocol+'.proxyPort='+str(proxy.port)]
if args.trust_store:flags+=['-Djavax.net.ssl.trustStore='+str(Path(args.trust_store).resolve()),'-Djavax.net.ssl.trustStorePassword='+os.environ.get('TTRO_TRUSTSTORE_PASSWORD','changeit')]
subprocess.run(cmd+['clean','build','--no-daemon']+flags,cwd=root/'forge',check=True)
jar=brand['artifact']+'-'+brand['version']+'.jar';dest=root/'launcher/payload';dest.mkdir(exist_ok=True)
artifact=root/'forge/build/libs'/jar
with zipfile.ZipFile(artifact) as z:
 for n in z.namelist():
  if n.endswith('.class'):assert int.from_bytes(z.read(n)[6:8],'big')==52,'Java 8 required'
 assert not any(n.startswith(('net/minecraft/','net/minecraftforge/','dev/ttro/qa/')) for n in z.namelist()),'Upstream/game/QA classes must not ship'
for old in dest.glob('*.jar'):old.unlink()
shutil.copy2(artifact,dest/jar)
(dest/'client-manifest.json').write_text(json.dumps({'filename':jar,'sha256':hashlib.sha256(artifact.read_bytes()).hexdigest(),'minecraft':'1.8.9','forge':'11.15.1.2318'},indent=2)+'\n')
result={'status':'PASS','forgegradle_clean_build':'PASS','build_system':'ForgeGradle 2.1 / Gradle 2.14.1','java_target':8,'artifact':jar,'sha256':hashlib.sha256(artifact.read_bytes()).hexdigest(),'api_compile_baseline':'Official Forge 1.8.9-11.15.1.2318 with MCP stable_22','runtime_target':'Forge 11.15.1.2318','reobfuscation':'ForgeGradle reobfJar','network_configuration':'Environment proxy and administered trust store' if flags else 'Default Java network configuration'}
(root/'research/build-result.json').write_text(json.dumps(result,indent=2)+'\n')
print('Ttro Client integration JAR:',jar)
