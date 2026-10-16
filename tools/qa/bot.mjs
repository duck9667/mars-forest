// 자동 플레이: node bot.mjs <out.png> [wallSecs] [steps] — 루프 완주(판매·발판 구매) 검증. 조향은 게임 스텝마다 hook으로. 데드존 5 (> 스텝 보폭 8.25/2, 발판 위 진동 방지), 보물상자 모달은 자동으로 닫음.
import { openPage, BASE } from './lib.mjs';
const secs=+(process.argv[3]||120),steps=+(process.argv[4]||8);
const {b,p,logs}=await openPage({dpr:1});
await p.evaluate(()=>document.getElementById('start').click());
await p.evaluate((steps)=>{dbg.renderer.shadowMap.enabled=false;dbg.steps=steps;window.__gt=0;dbg.hook=()=>{window.__gt+=1/60;{const m=document.getElementById('chestM');if(m&&m.classList.contains('on'))document.getElementById('chestOk').click()}const p=S.p,c=p.carry;let tg=null;
  const ice=c.length&&c[0]==='ice',bot=c.length&&c[0]==='bottle';
  const PRI=['hand','ice2','cashier','runner','bottler','melter','shelf','pen','ads','price','drill','dome','runspd','runner2'];
  const pad=PRI.map(id=>PADS.find(p=>p.id===id)).find(pd=>padLv(pd.id)<pd.max&&pd.req(S)&&S.bells>=pd.cost(padLv(pd.id))-(S.padProg[pd.id]||0));
  const freeSpot=S.spots.findIndex((v,i)=>v<=0&&spotOpen(i));
  if(ice&&(c.length>=carryMax()||freeSpot<0||c.length>=3))tg={x:STN.melter.x-64,y:STN.melter.y+10};
  else if(ice&&freeSpot>=0)tg={x:ICE_SPOTS[freeSpot][0],y:ICE_SPOTS[freeSpot][1]};
  else if(bot&&S.shelf<8+4*padLv('shelf'))tg={x:STN.counter.x,y:STN.counter.y-44};
  else if(bot)tg={x:STN.register.x,y:STN.register.y-44};
  else if(S.cash>0)tg={x:STN.register.x,y:STN.register.y-44};
  else if(pad)tg={x:pad.x,y:pad.y};
  else if(S.bottles>0&&S.shelf<8+4*padLv('shelf')&&!padLv('runner'))tg={x:STN.bottler.x-64,y:STN.bottler.y+10};
  else if(S.shelf>0&&custs.some(x=>x.st==='queue')&&!padLv('cashier'))tg={x:STN.register.x,y:STN.register.y-44};
  else if(!padLv('drill')&&c.length<carryMax()&&freeSpot>=0)tg={x:ICE_SPOTS[freeSpot][0],y:ICE_SPOTS[freeSpot][1]};
  if(!tg){joyVec=null;return}if(p.y<414&&tg.y>414&&Math.abs(p.x-246)>8)tg={x:246,y:430};else if([STN.melter,STN.bottler].some(s=>Math.abs(tg.x-s.x)<s.w/2+12&&Math.abs(p.x-s.x)<s.w/2+12&&((p.y<s.y)!==(tg.y<s.y))))tg=Math.abs(p.x-246)>8?{x:246,y:p.y}:{x:246,y:tg.y}; // 건물 사이 복도로 우회 (게임엔 길찾기 없음)
  const dx=tg.x-p.x,dy=tg.y-p.y,L=Math.hypot(dx,dy);joyVec=L<5?null:[dx/L,dy/L]}},steps);
const t0=Date.now();let lastLog=0;
while(Date.now()-t0<secs*1000){await p.waitForTimeout(1000);
  if(Date.now()-lastLog>15000){lastLog=Date.now();const s=await p.evaluate(()=>({gt:Math.round(window.__gt),bells:Math.floor(S.bells),sold:S.sold,shelf:S.shelf,water:S.water,bottles:S.bottles,meltQ:S.meltQ,q:custs.length,pads:S.pads,carry:S.p.carry.join(","),px:Math.round(S.p.x),py:Math.round(S.p.y),lost:S.lost||0}));console.log(Math.round((Date.now()-t0)/1000)+'s',JSON.stringify(s))}}
await p.evaluate(()=>{dbg.renderer.shadowMap.enabled=true;dbg.steps=1;joyVec=null});await p.waitForTimeout(1500);
await p.screenshot({path:process.argv[2]});
const errs=logs.filter(l=>!/GL Driver|swiftshader/.test(l));console.log('errors:',errs.length,errs.slice(0,5).join('\n'));await b.close();
