// 목표 달성 팝 장면: 첫 판매 → 비차단 상자 팝. 헤드리스는 1~2fps라 팝(1.5s)이 촬영 전에 끝나므로 performance.now를 팝 중간(0.7s)에 고정해 둔다
S.tut=3;S.handShown=1;S.pads={};S.sold=0;S.bells=12;S.shelf=5;S.bottles=2;S.p.x=352;S.p.y=540;S.p.dir=[0,-1];dbg.restoreBots();document.getElementById('start').click();
setTimeout(()=>{const n0=performance.now();S.sold=1;dbg.questTick();performance.now=()=>n0+700},600);
