'use strict';
const officialPrefix = 'https://github.com/totoro0419/TtroClient/releases/download/';
async function loadRelease() {
 const download=document.getElementById('download'), state=document.getElementById('release-state'), sums=document.getElementById('checksums');
 try {
  const response=await fetch('https://api.github.com/repos/totoro0419/TtroClient/releases?per_page=20',{signal:AbortSignal.timeout(10000)});
  if(!response.ok) throw new Error('Release lookup unavailable');
  const releases=await response.json();
  const release=releases.find(r=>!r.draft&&Array.isArray(r.assets)&&r.assets.some(a=>a.name==='TtroClient-Setup.exe')&&r.assets.some(a=>a.name==='checksums.txt'));
  if(!release){download.textContent='Windows Download · 準備中';state.textContent='公開済みのWindows配布物はまだありません。開発・QAの進捗はGitHubで公開しています。';return;}
  const setup=release.assets.find(a=>a.name==='TtroClient-Setup.exe'), checksums=release.assets.find(a=>a.name==='checksums.txt');
  if(!setup.browser_download_url.startsWith(officialPrefix)||!checksums.browser_download_url.startsWith(officialPrefix))throw new Error('Invalid release origin');
  download.href=setup.browser_download_url;download.removeAttribute('aria-disabled');download.textContent='Windows Download · '+release.tag_name;
  sums.href=checksums.browser_download_url;sums.removeAttribute('aria-disabled');sums.textContent='checksums.txt ↗';
  state.textContent='最新公開Version: '+release.tag_name+(release.prerelease?' · 開発中のprerelease':'')+'。Release notesの既知の問題を確認してください。';
 }catch(e){download.textContent='Windows Download · 確認できません';state.textContent='配布状況を取得できませんでした。GitHub Releasesで確認してください。';}
}
loadRelease();
