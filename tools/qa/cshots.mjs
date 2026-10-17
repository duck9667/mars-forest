// C단계 장면 촬영: 구역 해제 중간 프레임, 테라포밍(초반/후반), 계산대 지폐 더미, 손님이 병을 이고 떠나기
import { openPage } from './lib.mjs';
const shot=async(name,setup,after,wait=2200)=>{const {b,p,logs}=await openPage();await p.evaluate(setup);await p.waitForTimeout(800);if(after)await p.evaluate(after);await p.waitForTimeout(wait);
  const info=await p.evaluate(()=>({calls:dbg.renderer.info.render.calls,terra:+dbg.terra().toFixed(2),lit:dbg.terraG.slots.filter(s=>s.k).length,risers:0}));await p.screenshot({path:`out/${name}.png`});console.log(name,JSON.stringify(info),logs.length?'LOGS '+logs.slice(0,3).join(' | '):'');await b.close()};
const BASE="S.tut=3;S.handShown=1;S.quest=QUESTS.length;";
await shot('C_unlock_a',BASE+"S.pads={cashier:1,bottler:1};S.sold=12;S.bells=600;S.p.x=126;S.p.y=250;dbg.restoreBots();document.getElementById('start').click();",
  "dbg.steps=2;S.padProg.ice2=0;const pd=PADS.find(p=>p.id==='ice2');S.pads.ice2=0;dbg.onPad&&0;S.bells-=pd.cost(0);S.pads.ice2=1;dbg.unlockZone('ice2');",900);
await shot('C_unlock_b',BASE+"S.pads={cashier:1,bottler:1};S.sold=12;S.bells=600;S.p.x=126;S.p.y=250;dbg.restoreBots();document.getElementById('start').click();",
  "dbg.steps=4;S.pads.ice2=1;dbg.unlockZone('ice2');",2600);
await shot('C_terra_start',BASE+"dbg.restoreBots();document.getElementById('start').click();",null,1500);
await shot('C_terra_late',BASE+"S.pads={hand:3,ice2:1,bottler:3,melter:3,shelf:2,cashier:1,ads:3,pen:1,runner:1,price:2,drill:1,dome:1,runspd:1};S.sold=180;S.p.x=200;S.p.y=600;dbg.restoreBots();document.getElementById('start').click();",null,1500);
await shot('C_cash',BASE+"S.pads={cashier:1,bottler:1};S.sold=40;S.cash=330;S.p.x=250;S.p.y=600;dbg.restoreBots();document.getElementById('start').click();",null,1200);
await shot('C_cash_pick',BASE+"S.pads={bottler:1};S.sold=40;S.cash=330;S.p.x=360;S.p.y=540;dbg.restoreBots();document.getElementById('start').click();","dbg.steps=1;",700);
await shot('C_hold',BASE+"S.pads={cashier:1,bottler:1,runner:1};S.sold=40;S.shelf=30;S.p.x=120;S.p.y=330;dbg.restoreBots();const cols=[0x6fb1e0,0xf2c14e,0x9bd36b,0xe88cc0];for(let i=0;i<4;i++)custs.push({id:200+i,x:216,y:584+i*46,st:'queue',want:2+(i%2),col:cols[i],acc:i,vip:false,wait:0,t:0,dir:[0,-1]});document.getElementById('start').click();",
  "dbg.steps=8;",4500);
