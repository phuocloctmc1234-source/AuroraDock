'use strict';
/* Aurora Dock — lõi: tiện ích, cấu hình, cầu nối tới AuroraHost.exe, đồng bộ vùng vẽ + vùng làm việc.
   KHÔNG có dữ liệu giả ở đây: mọi số liệu đến từ host (Windows) hoặc từ API thật. */
const $=s=>document.querySelector(s);
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const esc=s=>String(s==null?'':s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const pad=n=>String(n).padStart(2,'0');
const store={get(k,d){try{const v=localStorage.getItem('ad.'+k);return v==null?d:JSON.parse(v)}catch(e){return d}},set(k,v){try{localStorage.setItem('ad.'+k,JSON.stringify(v))}catch(e){}}};
const Q=new URLSearchParams(location.search);
const HOSTED=!!(window.chrome&&chrome.webview);
if(HOSTED)document.body.classList.add('hosted');
const dock=$('#dock'),track=$('#track'),pop=$('#pop'),panel=$('#panel');

/* ---------- cấu hình (lưu trong hồ sơ WebView của app) ---------- */
const CFG=Object.assign({autoStart:true,leadStart:10,graceMin:60,autoBrk:true,leadBrk:1,cdSec:10,url:'',token:'',pollSched:60,pollState:60,
 sty:0,sliders:null,eco:true,autoEco:true,pos:'bottom',lay:'pill',autoContrast:true,compact:false,ctx:true,slAuto:false,bed:'23:00',wake:'06:30',
 wOn:{},taskbar:'autohide',reserve:true,pins:null,city:'',geo:null,claudeLimit:'',man:{start:'',brkFrom:'',brkTo:''},todos:[],sdUrl:'',sdAuto:false,sdLead:1,nApps:{},nSeen:{},hideBannerExp:true,sdSched:true,bubPrivate:false,dnd:false},store.get('cfg',{}));
if(!store.get('bnMig2')){CFG.hideBannerExp=true;store.set('bnMig2',1);try{store.set('cfg',CFG)}catch(e){}}
CFG.autoStart=CFG.autoBrk=false; if(!store.get('sdAutoMig')){CFG.sdAuto=true;store.set('sdAutoMig',1);try{store.set('cfg',CFG)}catch(e){}} /* da go bo tinh nang tu cham cong */
CFG.nApps=CFG.nApps||{};CFG.nSeen=CFG.nSeen||{};CFG.wOn=CFG.wOn||{};CFG.man=CFG.man||{start:'',brkFrom:'',brkTo:''};CFG.todos=CFG.todos||[];
const saveCfg=()=>store.set('cfg',CFG);

/* ---------- cầu nối tới host ---------- */
const _p=new Map();let _seq=0;
function host(op,a,ms){return new Promise((res,rej)=>{
 if(!HOSTED)return rej(new Error('Chưa chạy trong AuroraHost.exe'));
 const id=++_seq,t=setTimeout(()=>{_p.delete(id);rej(new Error('host không trả lời ('+op+')'))},ms||25000);
 _p.set(id,{res,rej,t});chrome.webview.postMessage(JSON.stringify({id,op,a:a||{}}))})}
function fire(op,a){if(HOSTED)chrome.webview.postMessage(JSON.stringify({id:0,op,a:a||{}}))}
/* host chủ động báo: vùng làm việc đã bị chiếm (VD taskbar Windows) để dock nằm trên đó, không đè lên */
function showUpd(m){const u=$('#upd');if(!u)return;u.innerHTML=`<span>⬆ <b>Có bản cập nhật</b> <small>${esc(m.msg||'')}</small></span><button class="btn" id="updGo">Cập nhật</button><button class="btn" id="updX">Để sau</button>`;u.classList.add('on');setUpdFg();kick(1200);
 $('#updX').onclick=()=>{u.classList.remove('on');kick()};
 $('#updGo').onclick=async()=>{u.innerHTML='<span>⏳ Đang cập nhật… app sẽ tự khởi động lại</span>';kick();try{await host('updateApply',{},30000)}catch(e){u.classList.remove('on');toast('Lỗi cập nhật: '+e.message);kick()}}}
function setUpdFg(){}
let _warnT=0;
function onHostEvent(m){if(m.ev==='pause'){window.PAUSED=!!m.on;if(!m.on)try{runProviders(true)}catch(e){}return}if(m.ev==='warn'){if(Date.now()-_warnT>30*60*1000){_warnT=Date.now();toast('⚠ '+m.msg)}return}if(m.ev==='notifNew'){try{pump('notif')}catch(e){}return}if(m.ev==='update'){showUpd(m);return}if(m.ev==='updateNone'){toast('Đang dùng bản mới nhất');return}if(m.ev==='openPanel'){openPanel();return}if(m.ev==='inset'){const R=document.documentElement.style;R.setProperty('--it',(m.top||0)+'px');R.setProperty('--ib',(m.bottom||0)+'px');kick(1200)}}
if(HOSTED)chrome.webview.addEventListener('message',e=>{let m=e.data;if(typeof m==='string'){try{m=JSON.parse(m)}catch(_){return}}
 if(m.ev){onHostEvent(m);return}
 const p=_p.get(m.id);if(!p)return;_p.delete(m.id);clearTimeout(p.t);m.ok?p.res(m.d):p.rej(new Error(m.e||'lỗi'))});
async function hget(url,headers){const r=await host('http',{url,headers});if(r.status<200||r.status>=300)throw new Error('HTTP '+r.status+(r.body?': '+String(r.body).slice(0,100).replace(/\s+/g,' '):''));return r.body}
const hjson=async(url,headers)=>JSON.parse(await hget(url,headers));

/* ---------- dữ liệu thật + trạng thái kết nối ---------- */
const DATA={sys:null,wins:null,media:null,scr:null,files:null,claude:null,git:null,stripe:null,cal:null,wea:null,ts:null,lark:null,larkStatus:null,sd:null,notif:null,crate:null,chook:null};
const ERR={},TSAMP={};
const SECS=new Set();
const have=k=>k==='city'?!!CFG.city:k==='sd'?!!CFG.sdUrl:k==='lark'?!!(DATA.larkStatus&&DATA.larkStatus.loggedIn):SECS.has(k);
const H={};const hist=(k,v)=>{(H[k]=H[k]||[]).push(v);if(H[k].length>40)H[k].shift()};

/* ---------- thời gian ---------- */
const hm=s=>{const [h,m]=s.split(':').map(Number);return h*60+m};
const nowMin=()=>{const d=new Date();return d.getHours()*60+d.getMinutes()};
const inRange=(c,a,b)=>a<=b?(c>=a&&c<b):(c>=a||c<b);
const nextAt=str=>{const d=new Date(),[h,m]=str.split(':').map(Number);d.setHours(h,m,0,0);if(d<=Date.now())d.setDate(d.getDate()+1);return d.getTime()};
const occ=(str,base)=>{const [h,m]=str.split(':').map(Number);const d=new Date(base);d.setHours(h,m,0,0);return d.getTime()};
const occNear=str=>{const n=Date.now();let best=0,bd=Infinity;for(const k of [-1,0,1]){const t=occ(str,n)+k*864e5;if(Math.abs(t-n)<bd){bd=Math.abs(t-n);best=t}}return best};
const occAfter=(str,base)=>{let t=occ(str,base);if(t<base-36e5)t+=864e5;return t};
const dur=ms=>{ms=Math.max(0,Math.floor(ms/1000));return [Math.floor(ms/3600),Math.floor(ms%3600/60),ms%60].map((x,i)=>i?pad(x):x).join(':')};
const hhmm=t=>new Date(t).toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit'});
const dayKeyOf=t=>{const d=new Date(t);return d.getFullYear()+'-'+pad(d.getMonth()+1)+'-'+pad(d.getDate())};
const dayKey=()=>dayKeyOf(Date.now());
const hmShort=s=>{s=Math.round(s);const h=Math.floor(s/3600),m=Math.floor(s%3600/60);return h?h+'h '+pad(m)+'m':m+'m'};
const fmtTok=n=>n>=1e6?(n/1e6).toFixed(1)+'M':n>=1e3?Math.round(n/1e3)+'k':String(Math.round(n));
const fmtBytes=n=>n>=1e12?(n/1e12).toFixed(1)+' TB':n>=1e9?(n/1e9).toFixed(1)+' GB':n>=1e6?Math.round(n/1e6)+' MB':Math.round(n/1e3)+' KB';
const baseName=p=>String(p||'').split(/[\\/]/).pop().replace(/\.exe$/i,'');

/* ---------- nhật ký ---------- */
const LOG=store.get('log',[]);
function log(k,x,s){LOG.unshift({t:Date.now(),k,x,s:s||'ok'});if(LOG.length>200)LOG.length=200;store.set('log',LOG);renderLog()}
function renderLog(){const el=$('#logv');if(!el)return;const ic={ok:'✓',err:'✗',pend:'⏳',skip:'⊘',info:'•'};
 el.innerHTML=LOG.slice(0,40).map(e=>`<div><time>${new Date(e.t).toLocaleTimeString('vi-VN',{hour12:false})}</time><span>${ic[e.s]||'•'} ${esc(e.x)}</span></div>`).join('')||'<div>Chưa có hoạt động</div>'}
function toast(t){const c=$('#toasts');const e=document.createElement('div');e.className='toast glass';e.textContent=t;c.appendChild(e);while(c.children.length>4)c.firstChild.remove();kick();
 setTimeout(()=>{e.style.transition='opacity .4s';e.style.opacity=0;setTimeout(()=>{e.remove();kick()},400)},4800)}

/* ---------- vùng vẽ: chỉ dock/popover/panel là “cửa sổ”, chỗ khác chuột xuyên xuống app phía sau ---------- */
let animUntil=0,rafId=0,regTimer=0,lastReg='';
function rects(){const out=[];
 const add=(e,r,p)=>{if(!e)return;const cs=getComputedStyle(e);if(cs.display==='none'||cs.visibility==='hidden'||+cs.opacity<.02)return;const b=e.getBoundingClientRect();if(b.width<2||b.height<2)return;
  const q=v=>Math.round(v*2)/2,g=p||0;out.push({x:q(b.left-g),y:q(b.top-g),w:q(b.width+2*g),h:q(b.height+2*g),r:r+g})};
 add(dock,parseFloat(getComputedStyle(dock).borderTopLeftRadius)||26);add(pop,22);add(panel,26);add($('#cd'),18);add($('#upd'),18);add($('#bcard'),20);add($('#tip'),8);
 document.querySelectorAll('.toast').forEach(e=>add(e,16));document.querySelectorAll('.bubble').forEach(e=>add(e,26,6));   // +6px: huy hiệu số và dấu @ nhô ra ngoài mép
 if(window.DRAG||(typeof POP!=='undefined'&&POP)||(panel&&panel.classList.contains('show')))out.unshift({x:0,y:0,w:innerWidth,h:innerHeight,r:0,hit:1});   // popup/cài đặt đang mở: bắt click ra ngoài để tự đóng
 if(window.DRAG)out.push({x:0,y:0,w:innerWidth,h:innerHeight,r:0,hit:1});   // đang kéo bong bóng: nhận chuột toàn màn hình (không làm mờ)
 return out}
function pushRegion(){if(!HOSTED)return;const s=JSON.stringify(rects());if(s!==lastReg){lastReg=s;fire('region',{rects:JSON.parse(s)})}}
function regFrame(){rafId=0;pushRegion();if(performance.now()<animUntil)rafId=requestAnimationFrame(regFrame);else regTimer=setTimeout(regFrame,700)}
function kick(ms){animUntil=Math.max(animUntil,performance.now()+(ms||900));if(!rafId){clearTimeout(regTimer);rafId=requestAnimationFrame(regFrame)}}
['transitionrun','animationstart','click','pointerup','keyup'].forEach(ev=>document.addEventListener(ev,()=>kick(),true));

/* ---------- vùng làm việc (AppBar) ---------- */
let barT=0;
function syncBar(){clearTimeout(barT);barT=setTimeout(()=>{if(!HOSTED)return;fire('bar',{edge:CFG.pos,size:dock.offsetHeight+(CFG.lay==='full'?8:14)+6,reserve:!!CFG.reserve})},150)}
try{new ResizeObserver(()=>{syncBar();kick()}).observe(dock)}catch(e){}

/* ---------- bàn phím: nhập chữ cần cửa sổ nhận phím (bình thường dock không cướp focus của app đang dùng) ---------- */
let interactive=false;
function setInteractive(on){if(on===interactive)return;interactive=on;fire('interactive',{on})}
