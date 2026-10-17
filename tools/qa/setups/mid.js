S.quest=QUESTS.length;S.tut=3;S.handShown=1;S.pads={hand:2,ice2:1,bottler:1,melter:1,cashier:1,runner:1};S.sold=18;S.bells=180;S.shelf=6;S.bottles=3;S.meltQ=4;S.water=20;S.cash=24;S.p.x=150;S.p.y=330;S.p.dir=[1,.3];S.p.carry=['ice','ice','ice','ice','ice'];dbg.restoreBots();bots.find(b=>b.k==='runner').carry=3;
const cols=[0x6fb1e0,0xf2c14e,0x9bd36b,0xe88cc0,0xc49bf0,0xff8c69];for(let i=0;i<6;i++)custs.push({id:100+i,x:216,y:584+i*46,st:'queue',want:1+(i%2),col:cols[i],acc:i,vip:i===5,wait:i===0?26:0,t:0,dir:[0,-1]});
document.getElementById('start').click();
