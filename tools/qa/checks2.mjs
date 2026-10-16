// 신규 기능 검증: 목표 달성+상자, 콤보, VIP, 장식, 낮밤, 러시, 새 필드 저장 왕복, 콘솔 에러
import { openPage, BASE } from './lib.mjs';
const out=[];const ok=(name,c,extra='')=>out.push((c?'PASS ':'FAIL ')+name+(extra?' '+extra:''));
const {b,p,logs}=await openPage();
await p.evaluate(()=>document.getElementById('start').click());await p.waitForTimeout(300);
// 1) 목표: 물병 1개 팔기 → 상자 모달 + 보상
const q1=await p.evaluate(()=>{const b0=S.bells;S.sold=1;dbg.questTick();return{quest:S.quest,bells:S.bells-b0,chest:dbg.chestOpen(),txt:document.getElementById('chestTxt').textContent}});
ok('quest 1 complete + chest',q1.quest===1&&q1.bells===30&&q1.chest,JSON.stringify(q1));
await p.waitForTimeout(1200);await p.screenshot({path:'chk_chest.png'});
const q2=await p.evaluate(()=>{document.getElementById('chestOk').click();S.sold=0;dbg.questTick();return{chest:dbg.chestOpen(),quest:S.quest}});ok('chest closes, no re-trigger',!q2.chest&&q2.quest===1,JSON.stringify(q2));
// 2) 콤보: 삐걱 + 선반 재고 + 손님 3명 → 2초 안에 3판매
await p.evaluate(()=>{S.pads.cashier=1;dbg.restoreBots();S.shelf=12;S.sold=0;S.quest=99;S.spawnT=999;for(let i=0;i<3;i++)dbg.spawnCust(false,1);dbg.steps=30});
await p.waitForTimeout(2500);
const cb=await p.evaluate(()=>({comboMax:S.qc.comboMax,sold:S.sold,streak:S.qc.streak,comboEl:document.getElementById('combo').textContent}));
ok('combo x3 triggered',cb.comboMax>=3&&cb.sold>=3,JSON.stringify(cb));
// 3) VIP: 가격 x3 + 팁 (콤보 영향 없도록 시계 전진)
await p.evaluate(()=>{dbg.steps=1;S.clock+=10;S.cash=0;S.sold=25;S.spawnT=999;custs.length=0;dbg.spawnCust(true,2);dbg.steps=30});
await p.waitForTimeout(1800);
const vp=await p.evaluate(()=>({vip:S.qc.vip,cash:S.cash,sold:S.sold,lab:[...document.querySelectorAll('#labels .nm.vip')].length}));
ok('VIP pays x3 + tip (12*2*3+24=96)',vp.vip===1&&vp.cash===96,JSON.stringify(vp));
await p.screenshot({path:'chk_vip.png'});
// 4) 장식 구매 → S.deco + 차양 텍스처 교체
const dc=await p.evaluate(()=>{dbg.steps=1;S.bells=1000;const r=dbg.buyDeco('awning');return{r,deco:S.deco.awning,map:dbg.canopyMap(),bells:S.bells,again:dbg.buyDeco('awning')}});
ok('deco awning bought + applied',dc.r===true&&dc.deco===1&&dc.map==='blue'&&dc.bells===850&&dc.again===false,JSON.stringify(dc));
// 5) 낮밤: 시계를 밤으로 → 해 색·반구광 변화, 바닥 가시성(반구광 세기) 유지
const dn=await p.evaluate(()=>{S.clock=0;dbg.applyDay();const noon=dbg.sun.color.getHex(),ni=dbg.sun.intensity;S.clock=175;dbg.applyDay();return{noon:noon.toString(16),night:dbg.sun.color.getHex().toString(16),nf:dbg.night(),hemi:dbg.hemi.intensity,sunI:dbg.sun.intensity,noonI:ni}});
ok('day/night tint changes',dn.noon!==dn.night&&dn.nf>.9&&dn.hemi>=1.1&&dn.sunI<dn.noonI,JSON.stringify(dn));
await p.waitForTimeout(600);await p.screenshot({path:'chk_night.png'});
// 6) 러시: 타이머 강제 → 배너 on → 20초 뒤 종료 카운트
await p.evaluate(()=>{S.clock=0;dbg.applyDay();dbg.startRush();dbg.steps=20});await p.waitForTimeout(1200);
const r1=await p.evaluate(()=>({rush:dbg.rush(),on:document.getElementById('rush').classList.contains('on')}));
await p.evaluate(()=>{dbg.steps=100});await p.waitForTimeout(4000); // 100스텝(프레임당 5게임초): 20초 러시는 4프레임에 끝나고, 다음 러시(240초)는 4초 안에 못 온다 (400스텝이면 빠른 머신에서 두 번째 러시까지 돌아 cnt=2)
const r2=await p.evaluate(()=>{dbg.steps=1;return{rush:dbg.rush(),on:document.getElementById('rush').classList.contains('on'),cnt:S.qc.rush}});
ok('rush hour starts and ends',r1.rush>0&&r1.on&&r2.rush===0&&!r2.on&&r2.cnt===1,JSON.stringify({r1,r2}));
// 7) 저장 왕복: 새 필드
await p.evaluate(()=>{S.quest=11;S.qc.comboMax=4;S.qc.dayBest=321;S.clock=123;S.prestige=0;S.bells=555;dbg.save()}); // 11번(간판 Lv.2)은 미달성이라 리로드 직후 상자가 열리지 않는다await p.reload({waitUntil:'load'});await p.waitForTimeout(800);
const rt=await p.evaluate(()=>({quest:S.quest,comboMax:S.qc.comboMax,dayBest:S.qc.dayBest,clock:Math.round(S.clock),deco:S.deco.awning,map:dbg.canopyMap(),bells:Math.floor(S.bells)}));
ok('save roundtrip new fields',rt.quest===11&&rt.comboMax===4&&rt.dayBest===321&&rt.clock===123&&rt.deco===1&&rt.map==='blue'&&rt.bells===555,JSON.stringify(rt));
// 8) 옛 저장(목표 없음) → 이미 이룬 목표는 건너뜀
await p.goto(BASE+'/README.md');await p.evaluate(()=>{const s={bells:1234,sold:40,custs:30,pads:{hand:2,cashier:1,runner:1,drill:1,dome:1},padProg:{},p:{x:200,y:430,dir:[0,1],carry:[]},water:20,bottles:2,shelf:5,cash:36,meltQ:3,meltT:0,botT:0,spots:[0,0,5,0,0,0],savedAt:Date.now(),muted:false,spawnT:3};localStorage.setItem('mars-bottle-v1',JSON.stringify(s))});
await p.goto(BASE+'/app/index.html',{waitUntil:'load'});await p.waitForTimeout(800);
const mg=await p.evaluate(()=>({quest:S.quest,qc:typeof S.qc.streak,deco:typeof S.deco.tile,t:QUESTS[S.quest].t}));ok('old save fast-forwards satisfied quests',mg.quest===6&&mg.qc==='number'&&mg.deco==='number',JSON.stringify(mg));
// 9) 시트 탭 렌더
await p.evaluate(()=>{document.getElementById('start').click();dbg.openSheet('deco')});await p.waitForTimeout(400);
const sh=await p.evaluate(()=>({deco:document.querySelectorAll('.buy[data-deco]').length,tabOn:document.querySelector('.tab.on').dataset.tab}));
await p.screenshot({path:'chk_deco.png'});
await p.evaluate(()=>dbg.setTab('quest'));await p.waitForTimeout(300);const sq=await p.evaluate(()=>({rows:document.querySelectorAll('#rows .row').length,cur:document.querySelectorAll('#rows .row.cur').length}));
await p.screenshot({path:'chk_quest.png'});
ok('sheet tabs deco/quest render',sh.deco===6&&sh.tabOn==='deco'&&sq.cur===1&&sq.rows>=6,JSON.stringify({sh,sq}));
// 10) 이주(프레스티지): 모든 발판 MAX → 목표 탭에 이주 행 → 실행하면 시설 초기화·장식 유지·가격 ×1.5
await p.evaluate(()=>{PADS.forEach(pd=>S.pads[pd.id]=pd.max);S.deco.awning=1;S.bells=50;dbg.applyDeco();dbg.setTab('quest')});await p.waitForTimeout(300);
const pr=await p.evaluate(()=>{const row=document.querySelector('[data-prestige]');const p0=12;row.click();row.click();return{row:!!row,prestige:S.prestige,pads:Object.keys(S.pads).length,deco:S.deco.awning,map:dbg.canopyMap(),bells:S.bells,price:(()=>{return Math.round((12+0)*1.5)})(),sheet:document.getElementById('sheet').classList.contains('on')}});
ok('prestige resets base, keeps deco, x1.5',pr.row&&pr.prestige===1&&pr.pads===0&&pr.deco===1&&pr.map==='blue'&&pr.bells===0&&!pr.sheet,JSON.stringify(pr));
const errs=logs.filter(l=>!/GL Driver|swiftshader/.test(l));ok('no console errors',errs.length===0,errs.join(' | '));
console.log(out.join('\n'));await b.close();
