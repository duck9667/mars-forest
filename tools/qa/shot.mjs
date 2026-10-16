// usage: node shot.mjs <out.png> [setup.js] [waitMs]
import { openPage, BASE } from './lib.mjs';
import fs from 'fs';
const [out,script,waitMs='800']=process.argv.slice(2);
const {b,p,logs}=await openPage();
if(script){await p.evaluate(fs.readFileSync(script,'utf8'))}
await p.waitForTimeout(+waitMs);
await p.screenshot({path:out});
console.log(logs.join('\n')||'(no console errors)');await b.close();
