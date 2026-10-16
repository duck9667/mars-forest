S.quest=QUESTS.length;S.pads={hand:5,ice2:1,bottler:1,melter:1,shelf:2,runner:1};S.sold=30;S.bells=300;S.shelf=16;S.bottles=6;S.meltQ=5;S.water=20;S.cash=24;S.p.x=250;S.p.y=420;S.p.dir=[0,1];S.p.carry=Array(20).fill('ice');dbg.restoreBots();bots.find(b=>b.k==='runner').carry=8;
document.getElementById('start').click();
