// 자동 플레이: node bot.mjs <out.png> [wallSecs] [steps] — 루프 완주(판매·발판 구매) 검증. 조향은 게임 스텝마다 hook으로. 데드존 5 (> 스텝 보폭 8.25/2, 발판 위 진동 방지), 보물상자 모달은 자동으로 닫음.
import { openPage, BASE } from './lib.mjs';
const secs=+(process.argv[3]||120),steps=+(process.argv[4]||8);
const {b,p,logs}=await openPage({dpr:1});
await p.evaluate(()=>document.getElementById('start').click());
await p.evaluate((steps)=>{dbg.renderer.shadowMap.enabled=false;dbg.steps=steps;window.__gt=0;dbg.hook=()=>{window.__gt+=1/60;{const m=document.getElementById('chestM');if(m&&m.classList.contains('on'))document.getElementById('chestOk').click()}const p=S.p,c=p.carry;let tg=null;
  const ice=c.length&&c[0]==='ice',bot=c.length&&c[0]==='bottle';
  if(ice&&c.length>=carryMax())window.__ul=1;if(bot&&(c.length>=carryMax()||S.bottles===0))window.__dl=1;if(!c.length)window.__ul=window.__dl=0; // 가득 차면 다 비울 때까지 제빙기에, 병은 가득 차거나 트레이가 빌 때까지 받는다
  const pad=visiblePads().find(pd=>S.bells>=pd.cost(padLv(pd.id))-(S.padProg[pd.id]||0)); // 월드에 노출된 발판(최대 2개)만 설 수 있다
  if(!pad)for(const id of ['hand','spd','price']){const pd=PADS.find(p=>p.id===id);const cost=pd.cost(padLv(id));if(padLv(id)<pd.max&&pd.req(S)&&S.bells>=cost*1.5){dbg.buyPad(id);break}} // 시트 전용 스탯은 시트 구매로(벨의 2/3 이하일 때)
  const freeSpot=S.spots.findIndex((v,i)=>v<=0&&spotOpen(i));
  if(ice&&(c.length>=carryMax()||freeSpot<0||window.__ul))tg={x:STN.melter.x-64,y:STN.melter.y+10};
  else if(ice&&freeSpot>=0)tg={x:ICE_SPOTS[freeSpot][0],y:ICE_SPOTS[freeSpot][1]};
  else if(bot&&!window.__dl)tg={x:STN.bottler.x-64,y:STN.bottler.y+10};
  else if(bot&&S.shelf<shelfCap())tg={x:STN.counter.x,y:STN.counter.y-44};
  else if(bot)tg={x:STN.register.x,y:STN.register.y-44};
  else if(pad)tg={x:pad.x,y:pad.y}; // 살 수 있는 월드 발판이 있으면 먼저 (삐걱이 있으면 계산대에 벨이 늘 쌓여 있어 발판에 못 가는 진동을 막는다)
  else if(S.cash>0&&(!padLv('cashier')||S.cash>=60))tg={x:STN.register.x,y:STN.register.y-44};
  else if(S.bottles>0&&S.shelf<shelfCap()&&!padLv('runner'))tg={x:STN.bottler.x-64,y:STN.bottler.y+10};
  else if(S.shelf>0&&custs.some(x=>x.st==='queue')&&!padLv('cashier'))tg={x:STN.register.x,y:STN.register.y-44};
  else if(!padLv('drill')&&c.length<carryMax()&&freeSpot>=0)tg={x:ICE_SPOTS[freeSpot][0],y:ICE_SPOTS[freeSpot][1]};
  if(!tg){joyVec=null;return}const span=x=>x>40&&x<352,cross=(p.y<478&&tg.y>530)||(p.y>530&&tg.y<478); // 카운터(x58~298)·계산대(x286~346)가 y478~530을 막으므로 위아래를 오갈 땐 옆(x364/x34)으로 돌아간다
  if(p.y<414&&tg.y>414&&Math.abs(p.x-246)>8)tg={x:246,y:430};else if(cross&&span(p.x))tg={x:p.x>200?364:34,y:p.y};else if(cross)tg={x:p.x,y:tg.y>p.y?544:466};else if([STN.melter,STN.bottler].some(s=>Math.abs(tg.x-s.x)<s.w/2+12&&Math.abs(p.x-s.x)<s.w/2+12&&((p.y<s.y)!==(tg.y<s.y))))tg=Math.abs(p.x-246)>8?{x:246,y:p.y}:{x:246,y:tg.y}; // 건물 사이 복도로 우회 (게임엔 길찾기 없음)
  const dx=tg.x-p.x,dy=tg.y-p.y,L=Math.hypot(dx,dy);joyVec=L<5?null:[dx/L,dy/L]}},steps);
const t0=Date.now();let lastLog=0;
while(Date.now()-t0<secs*1000){await p.waitForTimeout(1000);
  if(Date.now()-lastLog>15000){lastLog=Date.now();const s=await p.evaluate(()=>({gt:Math.round(window.__gt),bells:Math.floor(S.bells),sold:S.sold,shelf:S.shelf,water:S.water,bottles:S.bottles,meltQ:S.meltQ,q:custs.length,pads:S.pads,vis:visiblePads().map(p=>p.id).join('+'),carry:S.p.carry.join(','),actT:+dbg.actT().toFixed(2),ul:window.__ul|0,dl:window.__dl|0,px:Math.round(S.p.x),py:Math.round(S.p.y),lost:S.lost||0}));console.log(Math.round((Date.now()-t0)/1000)+'s',JSON.stringify(s))}}
await p.evaluate(()=>{dbg.renderer.shadowMap.enabled=true;dbg.steps=1;joyVec=null});await p.waitForTimeout(1500);
await p.screenshot({path:process.argv[2]});
const errs=logs.filter(l=>!/GL Driver|swiftshader/.test(l));console.log('errors:',errs.length,errs.slice(0,5).join('\n'));await b.close();
