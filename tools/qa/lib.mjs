// 공용: 헤드리스 크로미움(iPhone 390×844 에뮬레이션)으로 app/index.html 을 연다.
// - 저장소 루트를 http://localhost:8765 로 서빙하는 정적 서버가 없으면 직접 띄운다
// - three.js CDN 요청은 vendor/three.module.js 로 가로챈다 (없으면 한 번 내려받아 둔다; 실패하면 CDN 그대로)
import { chromium } from 'playwright';
import fs from 'node:fs';import path from 'node:path';import http from 'node:http';import { fileURLToPath } from 'node:url';
const ROOT=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..','..');
export const BASE='http://localhost:8765';export const OUT=path.join(ROOT,'tools','qa','out');fs.mkdirSync(OUT,{recursive:true});
const THREE_URL='https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js',THREE_SRC=path.join(ROOT,'vendor','three.module.js');
const MIME={'.html':'text/html; charset=utf-8','.js':'text/javascript','.mjs':'text/javascript','.png':'image/png','.md':'text/plain; charset=utf-8','.json':'application/json','.svg':'image/svg+xml'};
async function ensureServer(){try{const r=await fetch(BASE+'/README.md');if(r.ok)return}catch(e){}
  await new Promise(res=>http.createServer((q,s)=>{const f=path.join(ROOT,decodeURIComponent(q.url.split('?')[0]));if(!f.startsWith(ROOT)||!fs.existsSync(f)||fs.statSync(f).isDirectory()){s.writeHead(404);s.end();return}s.writeHead(200,{'content-type':MIME[path.extname(f)]||'application/octet-stream'});fs.createReadStream(f).pipe(s)}).on('error',()=>res()).listen(8765,'127.0.0.1',res).unref())}
async function ensureThree(){if(fs.existsSync(THREE_SRC))return true;try{const r=await fetch(THREE_URL);if(!r.ok)return false;fs.mkdirSync(path.dirname(THREE_SRC),{recursive:true});fs.writeFileSync(THREE_SRC,await r.text());return true}catch(e){return false}}
export async function openPage({w=390,h=844,url=BASE+'/app/index.html',clearStorage=true,dpr=2}={}){
  await ensureServer();const local=await ensureThree();
  const b=await chromium.launch();
  const p=await b.newPage({viewport:{width:w,height:h},deviceScaleFactor:dpr,hasTouch:true,isMobile:true});
  const logs=[];p.on('console',m=>{const t=m.type();if((t==='error'||t==='warning')&&!/GL Driver|swiftshader/i.test(m.text()))logs.push(t+': '+m.text())});p.on('pageerror',e=>logs.push('PAGEERROR: '+e.message));
  if(local)await p.route('**/three@0.160.0/build/three.module.js',r=>r.fulfill({status:200,contentType:'text/javascript',body:fs.readFileSync(THREE_SRC,'utf8')}));
  if(clearStorage){await p.goto(BASE+'/README.md');await p.evaluate(()=>localStorage.clear());}
  await p.goto(url,{waitUntil:'load'});await p.waitForTimeout(600);
  return {b,p,logs};
}
