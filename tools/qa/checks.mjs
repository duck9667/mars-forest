// 기능 검증: 구저장 마이그레이션, 게이트 해제 팝, 시트 구매, 부재 중 수익, 콘솔 에러
import { openPage, BASE } from './lib.mjs';
const out=[];const ok=(name,c,extra='')=>out.push((c?'PASS ':'FAIL ')+name+(extra?' '+extra:''));
// 1) 구 저장 형식 (spots 6개, drill 구매, ice2 없음)
const {b,p,logs}=await openPage();
await p.goto(BASE+'/README.md');await p.evaluate(()=>{const s={bells:1234,sold:40,custs:30,pads:{hand:2,cashier:1,runner:1,drill:1,dome:1},padProg:{},p:{x:200,y:430,dir:[0,1],carry:[]},water:20,bottles:2,shelf:5,cash:36,meltQ:3,meltT:0,botT:0,spots:[0,0,5,0,0,0],savedAt:Date.now()-40*60*1000,muted:false,spawnT:3};localStorage.setItem('mars-bottle-v1',JSON.stringify(s))});
await p.goto(BASE+'/app/index.html',{waitUntil:'load'});await p.waitForTimeout(800);
const m=await p.evaluate(()=>({spots:S.spots.length,ice2:S.pads.ice2,drillVis:dbg.renderer&&true,sold:S.sold,bells:Math.floor(S.bells),intro:document.querySelector('#intro h1').textContent,zoneIce2:ZONES.ice2&&true,bots:bots.length}));
ok('migrate spots 6->10',m.spots===10,JSON.stringify(m));ok('migrate drill->ice2',m.ice2===1);ok('bots restored',m.bots===2);ok('intro resume title',m.intro==='다시 오셨군요');
await p.evaluate(()=>{S.savedAt=Date.now()-40*60*1000;document.getElementById('start').click()});await p.waitForTimeout(400);
const off=await p.evaluate(()=>({on:document.getElementById('offline').classList.contains('on'),amt:document.getElementById('offamt').textContent,txt:document.getElementById('offtxt').textContent,bells:Math.floor(S.bells)}));
ok('offline modal shown',off.on,JSON.stringify(off));await p.screenshot({path:'out/chk_offline.png'});
await p.evaluate(()=>document.getElementById('offok').click());
// 2) 게이트 해제 팝 (pen): buyPad 후 0.25초 시점 스케일<1, 1초 후 1
await p.evaluate(()=>{S.bells=5000;S.sold=40});
const r1=await p.evaluate(()=>{const before={vis:window.ZONES&&true};const ok=dbg.buyPad('pen');return{ok,lv:padLv('pen'),hop:0}});
ok('buyPad pen',r1.ok&&r1.lv===1,JSON.stringify(r1));
await p.waitForTimeout(250);await p.screenshot({path:'out/chk_pop.png'});
await p.waitForTimeout(1500);
const r2=await p.evaluate(()=>({pen:padLv('pen'),bells:Math.floor(S.bells)}));ok('pen bought, bells deducted',r2.pen===1&&r2.bells===5000-450,JSON.stringify(r2));
// 3) 시트에서 구매 (hand Lv.3 -> 4): 버튼 클릭
await p.evaluate(()=>dbg.openSheet());await p.waitForTimeout(400);
const before=await p.evaluate(()=>padLv('hand'));
const clicked=await p.evaluate(()=>{const b=document.querySelector('.buy[data-id="hand"]');if(!b||b.disabled)return false;b.click();return true});
await p.waitForTimeout(300);const after=await p.evaluate(()=>padLv('hand'));ok('sheet buy hand',clicked&&after===before+1,`${before}->${after}`);
await p.screenshot({path:'out/chk_sheet.png'});
await p.evaluate(()=>document.getElementById('sheetClose').click());
// 4) 벨 부족 시 구매 실패 + 토스트
const fail=await p.evaluate(()=>{S.bells=0;const r=dbg.buyPad('ads');return{r,toast:document.getElementById('toast').textContent}});ok('buy fails w/o bells',fail.r===false&&fail.toast==='벨이 부족해요',JSON.stringify(fail));
// 5) 저장 왕복: save -> reload -> 같은 값
await p.evaluate(()=>{S.bells=777;dbg.save()});await p.reload({waitUntil:'load'});await p.waitForTimeout(800);
const rt=await p.evaluate(()=>({bells:Math.floor(S.bells),pen:padLv('pen'),hand:padLv('hand'),hoppers:document.querySelectorAll('#labels>div').length>=0}));ok('save roundtrip',rt.bells===777&&rt.pen===1,JSON.stringify(rt));
// 6) 리셋
await p.evaluate(()=>{document.getElementById('start').click()});await p.waitForTimeout(200);
await p.evaluate(()=>{window.confirm=()=>true;document.getElementById('gear').click();document.getElementById('resetBtn').click();document.getElementById('resetBtn').click()});await p.waitForTimeout(300);
const rs=await p.evaluate(()=>({bells:S.bells,sold:S.sold,pads:Object.keys(S.pads).length,bots:bots.length}));ok('reset',rs.bells===0&&rs.sold===0&&rs.pads===0&&rs.bots===0,JSON.stringify(rs));
const errs=logs.filter(l=>!/GL Driver|swiftshader/.test(l));ok('no console errors',errs.length===0,errs.join(' | '));
console.log(out.join('\n'));await b.close();
