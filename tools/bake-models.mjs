#!/usr/bin/env node
// GLB 저폴리 모델 → 게임용 압축 정점 데이터로 굽기 (의존성 없음, node 18+)
// 사용: node tools/bake-models.mjs            → app/index.html 의 /*@MODELS*/ ... /*@/MODELS*/ 사이를 갱신
//       node tools/bake-models.mjs --stats    → 굽지 않고 모델별 통계(정점·삼각형·바이트·색)만 출력
// 포맷: 전역 팔레트(sRGB hex) + 모델별 {o:오프셋, nv, nt, min:[x,y,z], s:스케일, piv:[x,y,z]?}
//       정점 = uint16 x3(양자화 위치) + uint8(팔레트 색) = 7B, 삼각형 인덱스 = uint16 x3 (nv>65535면 uint32)
//       법선은 저장하지 않는다: 게임이 비인덱스로 펼친 뒤 면 법선을 계산한다 (플랫 셰이딩).
import fs from 'node:fs';import path from 'node:path';import zlib from 'node:zlib';import {fileURLToPath} from 'node:url';
const ROOT=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const SRC=path.join(ROOT,'assets','models');
// 구울 모델 목록. q:[x,y,z,w] 면 그 쿼터니언으로 먼저 돌린다. parts:true 면 메시 노드마다 "이름.노드명"으로 따로 굽고 피벗(노드의 루트 기준 위치)을 함께 적는다 (팔다리 애니메이션용).
const LIST=[
  {name:'coin',file:'coin.glb'},
  {name:'trees',file:'grass-trees.glb'},{name:'treesTall',file:'grass-trees-tall.glb'},{name:'fountain',file:'pavement-fountain.glb'},
  {name:'barrel',file:'barrel.glb',q:[0,0,0.7071068,0.7071068]}, // 누워 있는 통을 세운다 (z축 +90°)
  {name:'rockA',file:'formation-rock.glb'},{name:'rockB',file:'formation-large-rock.glb'},{name:'rockC',file:'formation-stone.glb'},{name:'rockD',file:'formation-large-stone.glb'},
];
/* ---------- PNG (8bit RGB/RGBA, 비인터레이스) ---------- */
function png(buf){if(buf.readUInt32BE(0)!==0x89504e47)throw new Error('not png');let p=8,w=0,h=0,ct=0,idat=[];
  while(p<buf.length){const len=buf.readUInt32BE(p),type=buf.toString('ascii',p+4,p+8),data=buf.subarray(p+8,p+8+len);if(type==='IHDR'){w=data.readUInt32BE(0);h=data.readUInt32BE(4);if(data[8]!==8||data[12]!==0)throw new Error('png: 8bit non-interlaced only');ct=data[9]}else if(type==='IDAT')idat.push(data);p+=12+len}
  const bpp=ct===6?4:ct===2?3:ct===0?1:ct===4?2:0;if(!bpp)throw new Error('png: color type '+ct);const raw=zlib.inflateSync(Buffer.concat(idat));const stride=w*bpp,out=Buffer.alloc(w*h*bpp);let prev=Buffer.alloc(stride);
  for(let y=0;y<h;y++){const f=raw[y*(stride+1)],line=raw.subarray(y*(stride+1)+1,(y+1)*(stride+1)),cur=out.subarray(y*stride,(y+1)*stride);
    for(let i=0;i<stride;i++){const a=i>=bpp?cur[i-bpp]:0,b=prev[i],c=i>=bpp?prev[i-bpp]:0;let v=line[i];if(f===1)v+=a;else if(f===2)v+=b;else if(f===3)v+=(a+b)>>1;else if(f===4){const pp=a+b-c,pa=Math.abs(pp-a),pb=Math.abs(pp-b),pc=Math.abs(pp-c);v+=pa<=pb&&pa<=pc?a:pb<=pc?b:c}cur[i]=v&255}prev=cur}
  return {w,h,bpp,data:out}}
/* ---------- GLB ---------- */
function glb(file){const b=fs.readFileSync(file);if(b.readUInt32LE(0)!==0x46546c67)throw new Error('not glb: '+file);const jl=b.readUInt32LE(12);const json=JSON.parse(b.toString('utf8',20,20+jl));let bin=null;let p=20+jl;if(p<b.length){const bl=b.readUInt32LE(p);bin=b.subarray(p+8,p+8+bl)}return {json,bin,dir:path.dirname(file)}}
function accessor(g,i){const a=g.json.accessors[i],bv=g.json.bufferViews[a.bufferView];const n={SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type];const T={5126:Float32Array,5123:Uint16Array,5125:Uint32Array,5121:Uint8Array,5122:Int16Array,5120:Int8Array}[a.componentType];const bytes=T.BYTES_PER_ELEMENT;const off=(bv.byteOffset||0)+(a.byteOffset||0);const stride=bv.byteStride||n*bytes;const out=new (T===Float32Array?Float32Array:Uint32Array)(a.count*n);
  for(let k=0;k<a.count;k++){const base=g.bin.byteOffset+off+k*stride;for(let c=0;c<n;c++){out[k*n+c]=T===Float32Array?g.bin.buffer.slice(0).byteLength&&new DataView(g.bin.buffer,base+c*bytes,bytes).getFloat32(0,true):new DataView(g.bin.buffer,base+c*bytes,bytes)[bytes===1?'getUint8':bytes===2?'getUint16':'getUint32'](0,true)}}
  return {data:out,n,count:a.count}}
const m4=(()=>{const mul=(a,b)=>{const o=new Array(16).fill(0);for(let r=0;r<4;r++)for(let c=0;c<4;c++)for(let k=0;k<4;k++)o[c*4+r]+=a[k*4+r]*b[c*4+k];return o};
  const trs=(t=[0,0,0],q=[0,0,0,1],s=[1,1,1])=>{const [x,y,z,w]=q;const xx=x*x,yy=y*y,zz=z*z,xy=x*y,xz=x*z,yz=y*z,wx=w*x,wy=w*y,wz=w*z;return [(1-2*(yy+zz))*s[0],2*(xy+wz)*s[0],2*(xz-wy)*s[0],0, 2*(xy-wz)*s[1],(1-2*(xx+zz))*s[1],2*(yz+wx)*s[1],0, 2*(xz+wy)*s[2],2*(yz-wx)*s[2],(1-2*(xx+yy))*s[2],0, t[0],t[1],t[2],1]};
  const ap=(m,x,y,z)=>[m[0]*x+m[4]*y+m[8]*z+m[12],m[1]*x+m[5]*y+m[9]*z+m[13],m[2]*x+m[6]*y+m[10]*z+m[14]];return {mul,trs,ap,I:[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}})();
const lin2srgb=v=>Math.round(255*(v<=.0031308?v*12.92:1.055*Math.pow(v,1/2.4)-.055));
const texCache=new Map();
function texOf(g,mat){const t=mat?.pbrMetallicRoughness?.baseColorTexture;if(!t)return null;const img=g.json.images[g.json.textures[t.index].source];const key=g.dir+'|'+(img.uri||img.bufferView);if(texCache.has(key))return texCache.get(key);let buf;if(img.uri){buf=fs.readFileSync(path.join(g.dir,decodeURIComponent(img.uri)))}else{const bv=g.json.bufferViews[img.bufferView];buf=Buffer.from(g.bin.subarray(bv.byteOffset||0,(bv.byteOffset||0)+bv.byteLength))}const p=png(buf);texCache.set(key,p);return p}
function sample(tex,u,v){u-=Math.floor(u);v-=Math.floor(v);const x=Math.min(tex.w-1,Math.floor(u*tex.w)),y=Math.min(tex.h-1,Math.floor(v*tex.h));const o=(y*tex.w+x)*tex.bpp;return (tex.data[o]<<16)|(tex.data[o+1]<<8)|tex.data[o+2]}
function matColor(mat){const f=mat?.pbrMetallicRoughness?.baseColorFactor||[1,1,1,1];return (lin2srgb(f[0])<<16)|(lin2srgb(f[1])<<8)|lin2srgb(f[2])}
// 모델 → 삼각형 목록 [{p:[x,y,z]x3, c:hex x3}] (노드 변환 적용). parts면 메시 노드별로 나눈다.
function triangles(g,parts){const out=parts?{}:{'':{tris:[],piv:[0,0,0]}};const scenes=g.json.scenes[g.json.scene||0].nodes;
  const walk=(ni,parent)=>{const n=g.json.nodes[ni];const local=n.matrix||m4.trs(n.translation,n.rotation,n.scale);const world=m4.mul(parent,local);
    if(n.mesh!==undefined){const key=parts?(n.name||'n'+ni):'';const piv=parts?[world[12],world[13],world[14]]:[0,0,0];const slot=out[key]||(out[key]={tris:[],piv});
      for(const pr of g.json.meshes[n.mesh].primitives){if((pr.mode??4)!==4)continue;const pos=accessor(g,pr.attributes.POSITION),uv=pr.attributes.TEXCOORD_0!==undefined?accessor(g,pr.attributes.TEXCOORD_0):null;const mat=pr.material!==undefined?g.json.materials[pr.material]:null;const tex=texOf(g,mat),mc=matColor(mat);
        const idx=pr.indices!==undefined?accessor(g,pr.indices).data:Uint32Array.from({length:pos.count},(_,i)=>i);
        const vert=i=>{const w=m4.ap(world,pos.data[i*3],pos.data[i*3+1],pos.data[i*3+2]);return {p:[w[0]-piv[0],w[1]-piv[1],w[2]-piv[2]],c:tex&&uv?sample(tex,uv.data[i*2],uv.data[i*2+1]):mc}};
        for(let t=0;t<idx.length;t+=3)slot.tris.push([vert(idx[t]),vert(idx[t+1]),vert(idx[t+2])])}}
    for(const c of n.children||[])walk(c,world)};
  for(const s of scenes)walk(s,m4.I);return out}
/* ---------- 굽기 ---------- */
const palette=[],palIdx=new Map();const colorIndex=c=>{if(palIdx.has(c))return palIdx.get(c);if(palette.length>=255){let best=0,bd=1e9;for(let i=0;i<palette.length;i++){const p=palette[i],d=((p>>16)-(c>>16))**2+(((p>>8)&255)-((c>>8)&255))**2+((p&255)-(c&255))**2;if(d<bd){bd=d;best=i}}return best}palIdx.set(c,palette.length);palette.push(c);return palette.length-1};
const chunks=[];let offset=0;const index={};const stats=[];
for(const m of LIST){const g=glb(path.join(SRC,m.file));const sets=triangles(g,!!m.parts);
  for(const key in sets){const {tris,piv}=sets[key];if(!tris.length)continue;const name=key?`${m.name}.${key}`:m.name;
    if(m.q){const R=m4.trs([0,0,0],m.q,[1,1,1]);for(const t of tris)for(const v of t)v.p=m4.ap(R,v.p[0],v.p[1],v.p[2])} // 선택: 사전 회전(쿼터니언)
    const bbox=()=>{mn=[1e9,1e9,1e9];mx=[-1e9,-1e9,-1e9];for(const t of tris)for(const v of t)for(let k=0;k<3;k++){mn[k]=Math.min(mn[k],v.p[k]);mx[k]=Math.max(mx[k],v.p[k])}};let mn,mx;bbox();
    if(!m.parts){const cx=(mn[0]+mx[0])/2,cz=(mn[2]+mx[2])/2,y0=mn[1];for(const t of tris)for(const v of t){v.p[0]-=cx;v.p[1]-=y0;v.p[2]-=cz}bbox()} // 통모델은 바닥 중심을 원점으로
    const ext=Math.max(mx[0]-mn[0],mx[1]-mn[1],mx[2]-mn[2])||1,s=ext/65535;const q=(v,k)=>Math.max(0,Math.min(65535,Math.round((v-mn[k])/s)));
    const vmap=new Map(),verts=[],idx=[];const cols=new Map();
    for(const t of tris){for(const v of t){const ci=colorIndex(v.c);cols.set(v.c,(cols.get(v.c)||0)+1);const qx=q(v.p[0],0),qy=q(v.p[1],1),qz=q(v.p[2],2);const k=qx+','+qy+','+qz+','+ci;let vi=vmap.get(k);if(vi===undefined){vi=verts.length;vmap.set(k,vi);verts.push([qx,qy,qz,ci])}idx.push(vi)}}
    const wide=verts.length>65535;const bytes=verts.length*7+idx.length*(wide?4:2);const buf=Buffer.alloc(bytes);let p=0;for(const v of verts){buf.writeUInt16LE(v[0],p);buf.writeUInt16LE(v[1],p+2);buf.writeUInt16LE(v[2],p+4);buf[p+6]=v[3];p+=7}for(const i of idx){if(wide)buf.writeUInt32LE(i,p),p+=4;else buf.writeUInt16LE(i,p),p+=2}
    const r=n=>+n.toFixed(4);index[name]={o:offset,nv:verts.length,nt:idx.length/3,min:mn.map(r),s:+s.toPrecision(6),size:[mx[0]-mn[0],mx[1]-mn[1],mx[2]-mn[2]].map(r)};if(m.parts)index[name].piv=piv.map(r);if(wide)index[name].w=1;
    chunks.push(buf);offset+=bytes;stats.push({name,nv:verts.length,nt:idx.length/3,bytes,size:index[name].size.map(x=>x.toFixed(2)).join('×')+' min '+mn.map(x=>x.toFixed(2)).join(','),piv:m.parts?piv.map(x=>x.toFixed(3)).join(','):'',colors:[...cols.entries()].sort((a,b)=>b[1]-a[1]).map(([c,n])=>'#'+c.toString(16).padStart(6,'0')+':'+n).join(' ')})}}
const blob=Buffer.concat(chunks);
for(const s of stats)console.log(`${s.name.padEnd(16)} v${String(s.nv).padStart(5)} t${String(s.nt).padStart(5)} ${String(s.bytes).padStart(6)}B ${s.size.padEnd(16)} ${s.piv?'piv '+s.piv+' ':''}${s.colors}`);
console.log(`합계 ${blob.length} B (base64 ${Math.ceil(blob.length/3)*4} B), 팔레트 ${palette.length}색`);
if(process.argv.includes('--stats'))process.exit(0);
const snippet=`/*@MODELS*/const MDL_PAL=[${palette.map(c=>'0x'+c.toString(16).padStart(6,'0')).join(',')}],MDL_IDX=${JSON.stringify(index)},MDL_B64="${blob.toString('base64')}";/*@/MODELS*/`;
const target=path.join(ROOT,'app','index.html');let html=fs.readFileSync(target,'utf8');const re=/\/\*@MODELS\*\/[\s\S]*?\/\*@\/MODELS\*\//;
if(!re.test(html)){console.error('app/index.html 에 /*@MODELS*/ ... /*@/MODELS*/ 표식이 없어요');process.exit(1)}
html=html.replace(re,()=>snippet);fs.writeFileSync(target,html);console.log('✓ app/index.html 갱신');
