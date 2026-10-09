'use strict';
// The public alpha website never invents a binary URL. Only published,
// explicitly marked alpha prereleases from the canonical repository qualify.
const API='https://api.github.com/repos/totoro0419/TtroClient/releases?per_page=30';
const BASE='https://github.com/totoro0419/TtroClient/releases/download/';
const names=['TtroClient-Setup.exe','checksums.txt'];
const alphaTag=/^v(\d+)\.(\d+)\.(\d+)-alpha\.(\d+)$/;
const tuple=tag=>{const match=alphaTag.exec(tag||'');return match?match.slice(1).map(Number):null;};
const assetFor=(release,name)=>{
 const a=release.assets.find(x=>x.name===name);
 if(!a||typeof a.browser_download_url!=='string')return null;
 const expected=BASE+release.tag_name+'/'+encodeURIComponent(name);
 return a.browser_download_url===expected?expected:null;
};
async function loadRelease(){
 const download=document.getElementById('download');
 const state=document.getElementById('release-state');
 const sums=document.getElementById('checksums');
 try{
  const response=await fetch(API,{signal:AbortSignal.timeout(10000)});
  if(!response.ok)throw new Error('Release metadata unavailable');
  const data=await response.json();
  if(!Array.isArray(data))throw new Error('Invalid release metadata');
  const release=data.filter(r=>r&&!r.draft&&r.prerelease===true&&Array.isArray(r.assets)&&tuple(r.tag_name)&&names.every(n=>assetFor(r,n)))
   .sort((a,b)=>{const av=tuple(a.tag_name),bv=tuple(b.tag_name);for(let i=0;i<4;i++)if(av[i]!==bv[i])return bv[i]-av[i];return 0;})[0];
  if(!release){
   download.textContent='Download for Windows · 準備中';
   state.textContent='公開済みPublic Alphaはまだありません。完成を偽装したインストーラーへのリンクは表示しません。';
   return;
  }
  download.href=assetFor(release,names[0]);
  download.removeAttribute('aria-disabled');
  download.removeAttribute('tabindex');
  download.textContent='Download for Windows · '+release.tag_name;
  sums.href=assetFor(release,names[1]);
  sums.removeAttribute('aria-disabled');
  sums.removeAttribute('tabindex');
  sums.textContent='SHA256 checksums.txt ↗';
  state.textContent='Public Alpha '+release.tag_name+' · GitHub Releasesから配布。Release notesの既知の問題とSHA256を確認してください。';
 }catch{
  download.textContent='Download for Windows · 確認できません';
  state.textContent='配布状況の取得に失敗しました。公式GitHub Releasesで最新状況を確認してください。';
 }
}
loadRelease();
