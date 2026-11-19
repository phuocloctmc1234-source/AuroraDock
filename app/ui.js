'use strict';
/* Giao diện: dựng dock, popover, bảng cài đặt, kiểu kính, vòng lặp chính. */
let POP=null,lastSig='';
const shown=w=>w.on&&(!w.hw||w.hw())&&(!CFG.ctx||!w.vis||w.vis());
const sig=()=>W.filter(shown).map(w=>w.id).join();
function build(){lastSig=sig();track.innerHTML='';
 W.filter(shown).forEach(w=>{const e=document.createElement('div');e.className='w';e.dataset.id=w.id;const h=renderW(w);e._h=h;e.innerHTML=h;
  e.onclick=ev=>{const ok=wstate(w)==='ok';
   if(ok&&w.act){w.act(ev);tick();return}
   if(ok&&w.click){w.click();tick()}
   if(w.p||(w.prov&&!ok))openPop(w.id,()=>renderP(w),e);else closePop()};
  if(w.ctx)e.oncontextmenu=ev=>{if(wstate(w)==='ok')w.ctx(ev)};
  track.appendChild(e)})}
function tick(){
 if(sig()!==lastSig){if(POP&&!track.querySelector(POP.sel))closePop();build();return}
 W.forEach(w=>{if(!shown(w))return;const e=track.querySelector(`[data-id="${w.id}"]`);if(e){const h=renderW(w);if(e._h!==h){e._h=h;e.innerHTML=h}}});
 if(POP){const h=POP.fn();if(h==null)closePop();else if(pop._h!==h){const had=document.activeElement&&document.activeElement.id==='todoIn';pop._h=h;pop.innerHTML=h;placePop();if(had){const n=$('#todoIn');if(n)n.focus()}}}}
function placePop(){if(!POP)return;const el=track.querySelector(POP.sel)||dock,r=el.getBoundingClientRect(),d=dock.getBoundingClientRect(),pw=270;
 pop.style.left=clamp(r.left+r.width/2-pw/2,10,innerWidth-pw-10)+'px';
 pop.style.top=(dock.classList.contains('top')?d.bottom+12:Math.max(10,d.top-12-pop.offsetHeight))+'px'}
function openPop(id,fn,anchor){if(POP&&POP.id===id){closePop();return}
 const k=anchor.closest('.task'),sel=k?`.task[data-k="${CSS.escape(k.dataset.k)}"]`:`[data-id="${CSS.escape(anchor.closest('.w').dataset.id)}"]`;
 const h=fn();if(h==null){closePop();return}POP={id,fn,sel};pop._h=h;pop.innerHTML=h;
 track.querySelectorAll('.w').forEach(x=>x.classList.toggle('act',!!x.querySelector(sel)||x.matches(sel)));
 placePop();pop.classList.add('show');requestAnimationFrame(placePop)}
function closePop(){POP=null;pop.classList.remove('show');track.querySelectorAll('.w').forEach(x=>x.classList.remove('act'))}
document.addEventListener('click',e=>{if(!e.target.closest('.w')&&!e.target.closest('#pop'))closePop();
 if(panel.classList.contains('show')&&!e.target.closest('#panel')&&!e.target.closest('#gear'))closePanel()});
pop.addEventListener('click',e=>{const a=e.target.closest('[data-act]');if(a){const f=ACT[a.dataset.act];if(f)f(a.dataset.arg);tick();return}
 const o=e.target.closest('[data-open]');if(o)host('launch',{target:o.dataset.open}).catch(()=>{})});
pop.addEventListener('keydown',e=>{if(e.target.id==='todoIn'&&e.key==='Enter'&&e.target.value.trim()){CFG.todos.push({t:e.target.value.trim(),d:0});saveCfg();e.target.value='';tick()}});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){closePop();closePanel()}});

function openPanel(sel){panel.classList.add('show');setInteractive(true);kick(1200);if(sel){setTimeout(()=>{const t=$(sel);if(t)t.scrollIntoView({behavior:'smooth',block:'start'})},200)}}
function closePanel(){panel.classList.remove('show');kick(1200);setTimeout(()=>{if(!(document.activeElement&&document.activeElement.matches('input,textarea')))setInteractive(false)},100)}
$('#gear').onclick=()=>panel.classList.contains('show')?closePanel():openPanel();$('#close').onclick=closePanel;
$('#startb').onclick=()=>host('key',{k:'start'}).catch(e=>toast(e.message));
document.addEventListener('pointerdown',e=>{if(e.target.closest('input,textarea'))setInteractive(true)},true);
document.addEventListener('focusout',()=>setTimeout(()=>{if(!panel.classList.contains('show')&&!(document.activeElement&&document.activeElement.matches('input,textarea')))setInteractive(false)},80));

/* ---------- kiểu kính: blur/khúc xạ là Acrylic THẬT của Windows; ở đây chỉ chỉnh màu phủ ---------- */
const STY=[
 {n:'Clear',rgb:'255,255,255',a:.08,fg:'#fff',edge:60,sw:'rgba(255,255,255,.15)'},
 {n:'Frosted',rgb:'255,255,255',a:.22,fg:'#fff',edge:40,sw:'rgba(255,255,255,.4)'},
 {n:'Smoke',rgb:'18,20,40',a:.38,fg:'#fff',edge:30,sw:'rgba(18,20,40,.7)'},
 {n:'Milk',rgb:'255,255,255',a:.5,fg:'#14172b',edge:70,sw:'rgba(255,255,255,.85)'},
 {n:'Aurora',rgb:'160,130,255',a:.2,fg:'#fff',edge:55,sw:'linear-gradient(90deg,#a58bff,#5ee0ff)'},
 {n:'Crystal',rgb:'200,235,255',a:.05,fg:'#fff',edge:85,sw:'linear-gradient(90deg,#d6f1ff,#fff)'}];
let cur={sty:clamp(CFG.sty||0,0,STY.length-1)},THEME_LIGHT=false,lastGlass=-1;
function effStyle(st){if(!CFG.autoContrast)return st;
 if(THEME_LIGHT&&st.fg==='#fff')return Object.assign({},st,{rgb:'255,255,255',a:Math.max(st.a,.4),fg:'#14172b'});
 if(!THEME_LIGHT&&st.fg!=='#fff')return Object.assign({},st,{rgb:'18,20,40',a:Math.max(st.a,.38),fg:'#fff'});return st}
function setSliders(v){$('#sA').value=Math.round(v.a*100);$('#sEdge').value=v.edge}
function apply(st,fromPreset){const d=dock.style;if(fromPreset){CFG.sliders=null;setSliders(st)}
 const e=effStyle(st);let a=$('#sA').value/100;const edge=+$('#sEdge').value;if(e!==st)a=Math.max(a,e.a);
 d.setProperty('--a',a);d.setProperty('--rgb',e.rgb);d.setProperty('--fg',e.fg);d.setProperty('--ba',edge/100);
 const R=document.documentElement.style;R.setProperty('--rgb',e.rgb);R.setProperty('--a',Math.max(a,.1));R.setProperty('--fg',e.fg);
 [pop,panel,$('#toasts'),$('#cd'),$('#upd')].forEach(x=>x.style.setProperty('--fg',e.fg));
 $('#vA').textContent=Math.round(a*100)+'%';$('#vEdge').textContent=edge;
 const [r,g,b]=e.rgb.split(',').map(Number),abgr=(((0x3a<<24)|(b<<16)|(g<<8)|r)>>>0);if(abgr!==lastGlass){lastGlass=abgr;fire('glass',{c:abgr})}
 if(!fromPreset)CFG.sliders={a:$('#sA').value/100,edge};saveCfg()}
$('#styles').innerHTML=STY.map((s,i)=>`<div class="sty ${i===cur.sty?'on':''}" data-i="${i}"><i style="background:${s.sw}"></i>${s.n}</div>`).join('');
$('#styles').onclick=e=>{const t=e.target.closest('.sty');if(!t)return;document.querySelectorAll('.sty').forEach(x=>x.classList.remove('on'));t.classList.add('on');cur.sty=+t.dataset.i;CFG.sty=cur.sty;apply(STY[cur.sty],true)};
['sA','sEdge'].forEach(id=>$('#'+id).oninput=()=>apply(STY[cur.sty],false));
let reduced=false,lowBat=false;try{reduced=matchMedia('(prefers-reduced-motion: reduce)').matches}catch(e){}
function applyEco(){document.body.classList.toggle('eco',!!(CFG.eco||(CFG.autoEco&&(reduced||lowBat))))}
async function pollTheme(){try{const r=await host('theme');if(r.light!==THEME_LIGHT){THEME_LIGHT=r.light;apply(STY[cur.sty],false)}}catch(e){}}

/* ---------- vị trí & bố cục ---------- */
function segSet(id,v){document.querySelectorAll(id+' button').forEach(x=>x.classList.toggle('on',x.dataset.v===v))}
function setPos(v){dock.classList.remove('top','bottom');dock.classList.add(v);document.body.classList.remove('pos-top','pos-bottom');document.body.classList.add('pos-'+v);$('#toasts').className='toasts '+v;const cd=$('#cd'),on=cd.classList.contains('on');cd.className='cd glass '+v+(on?' on':'');CFG.pos=v;segSet('#segPos',v);saveCfg();syncBar();kick(1200)}
function setLay(v){dock.classList.remove('pill','full');dock.classList.add(v);CFG.lay=v;segSet('#segLay',v);saveCfg();syncBar();kick(1200)}
$('#segPos').onclick=e=>{const b=e.target.closest('button');if(b){setPos(b.dataset.v);closePop()}};
$('#segLay').onclick=e=>{const b=e.target.closest('button');if(b){setLay(b.dataset.v);closePop()}};
$('#segTb').onclick=e=>{const b=e.target.closest('button');if(!b)return;CFG.taskbar=b.dataset.v;saveCfg();segSet('#segTb',CFG.taskbar);
 host('taskbar',{mode:CFG.taskbar}).then(()=>toast(CFG.taskbar==='keep'?'Đã trả taskbar Windows về như cũ':CFG.taskbar==='hide'?'Đã ẩn hẳn taskbar Windows':'Taskbar Windows sẽ tự ẩn')).catch(err=>toast('Lỗi: '+err.message))};

/* ---------- widget & cài đặt ---------- */
function widgetList(){let h='',g='';W.forEach((w,i)=>{if(w.g!==g){g=w.g;h+=`<div class="grp">${g}</div>`}h+=`<div class="tg"><span>${esc(w.name)}</span><div class="sw ${w.on?'on':''}" data-i="${i}"></div></div>`});$('#wlist').innerHTML=h}
function setWidgetOn(id,on){const w=W.find(x=>x.id===id);if(!w)return;w.on=on;CFG.wOn[id]=on;saveCfg();widgetList();build()}
$('#wlist').onclick=e=>{const s=e.target.closest('.sw');if(!s)return;const w=W[+s.dataset.i];w.on=!w.on;CFG.wOn[w.id]=w.on;saveCfg();s.classList.toggle('on',w.on);closePop();build();runProviders()};

const NUMS={leadStart:1,graceMin:1,leadBrk:1,cdSec:1,pollSched:1,pollState:1,sdLead:1};
function onCfg(k){
 if(k==='sdUrl'){DATA.sd=null;ERR.sd='';if(CFG.sdUrl){if(!('sd' in CFG.wOn)){setWidgetOn('sd',true);const wk=W.find(x=>x.id==='wk');if(wk&&wk.on){setWidgetOn('wk',false);toast('Đã bật widget Shift Desk · Break và tắt widget "Ca làm" cục bộ (tránh ghi trùng)')}}runProviders(true,'sd')}refreshSdUi()}
 if(k==='compact'){document.body.classList.toggle('compact',CFG.compact);syncBar()}
 if(k==='autoContrast')apply(STY[cur.sty],false);
 if(k==='eco'||k==='autoEco')applyEco();
 if(k==='reserve')syncBar();
 if(k==='city'){CFG.geo=null;DATA.wea=null;ERR.wea='';if(CFG.city)setWidgetOn('wea',true);runProviders(true,'wea');refreshDsrc()}
 if(k==='url'){if(QUEUE.length){log('sys','Đổi nguồn dữ liệu — đã bỏ '+QUEUE.length+' thao tác đang chờ gửi (tránh gửi nhầm sang server khác)','skip');QUEUE=[];saveQ()}SCH.at=0;SYNC.err='';tSched=tState=0;updateModeUi()}
 tick()}
document.querySelectorAll('[data-cfg]').forEach(el=>{const k=el.dataset.cfg;
 if(el.classList.contains('sw')){el.classList.toggle('on',!!CFG[k]);el.onclick=()=>{CFG[k]=!CFG[k];el.classList.toggle('on',CFG[k]);saveCfg();onCfg(k)}}
 else{el.value=CFG[k]==null?'':CFG[k];el.onchange=()=>{let v=el.value;
  if(k==='claudeLimit')v=+v>0?Math.round(+v):'';
  else if(NUMS[k])v=Math.max(+el.min||0,Math.min(+el.max||99999,+v||0));else if(el.type==='time'&&!v)v=CFG[k];else if(el.type==='text'||el.type==='password'||el.type==='')v=String(v).trim();
  CFG[k]=v;el.value=v;saveCfg();onCfg(k)}}});

/* khóa & kết nối dữ liệu (lưu mã hóa DPAPI trong host) */
const SEC2={github:{prov:'git',w:'gh'},stripe:{prov:'stripe',w:'stripe'},calendar:{prov:'cal',w:'cal'}};
function refreshDsrc(){for(const k in SEC2){const el=$('#st-'+k);if(el)el.textContent=SECS.has(k)?'✓ đã lưu':'chưa kết nối'}
 $('#st-city').textContent=CFG.city?(DATA.wea?'✓ '+CFG.city:ERR.wea?'✗ '+ERR.wea:'…'):'chưa đặt';
 const c=DATA.claude;$('#st-claude').textContent=c?(c.found?'✓ đọc được log ('+c.events+' tin nhắn)':'không thấy log Claude Code'):(ERR.claude?'✗ '+ERR.claude:'…')}
async function loadSecrets(){try{(await host('secretList')).forEach(n=>SECS.add(n))}catch(e){}refreshDsrc()}
document.querySelectorAll('[data-save]').forEach(b=>b.onclick=async()=>{const k=b.dataset.save,inp=$('#in-'+k),v=inp.value.trim();if(!v)return;
 try{await host('secretSet',{name:k,value:v});inp.value='';SECS.add(k);DATA[SEC2[k].prov]=null;ERR[SEC2[k].prov]='';setWidgetOn(SEC2[k].w,true);runProviders(true,SEC2[k].prov);refreshDsrc();toast('✓ Đã lưu '+k+' (mã hóa bằng Windows)')}catch(e){toast('Lỗi: '+e.message)}});
document.querySelectorAll('[data-del]').forEach(b=>b.onclick=async()=>{const k=b.dataset.del;try{await host('secretDel',{name:k});SECS.delete(k);DATA[SEC2[k].prov]=null;refreshDsrc();tick();toast('Đã xóa '+k)}catch(e){toast('Lỗi: '+e.message)}});

/* chấm công: nguồn & lịch */
function syncSchedUi(){const live=!!CFG.url;[['tStart','start'],['tBF','brkFrom'],['tBT','brkTo']].forEach(([id,k])=>{const el=$('#'+id);el.value=SCH[k]||'';el.disabled=live});
 const el=$('#schedStat');if(el)el.textContent=(live?'Lịch lấy từ server':'Lịch do bạn nhập tay (để trống = chỉ bấm tay, không tự động)')+' · cập nhật '+(SCH.at?new Date(SCH.at).toLocaleTimeString('vi-VN',{hour12:false}):'—')}
function updateModeUi(){const live=!!CFG.url;$('#lkMode').textContent=live?'Nguồn: timesheet trên máy + gửi lên server của bạn':'Nguồn: chỉ timesheet trên máy';syncSchedUi();tick()}
$('#lkBtn').onclick=async()=>{const st=$('#lkStat');if(!CFG.url){st.textContent='Chưa nhập URL — đang ghi trên máy';return}st.textContent='Đang kiểm tra…';const ok=await refreshSchedule(true);if(ok)await pullState();
 st.textContent=ok?'✓ Kết nối OK · '+hhmm(Date.now()):'✗ Lỗi: '+SYNC.err;tick()};
$('#lkOff').onclick=()=>{CFG.url='';CFG.token='';$('#cfgUrl').value='';$('#cfgToken').value='';saveCfg();onCfg('url');$('#lkStat').textContent=''};
[['tStart','start'],['tBF','brkFrom'],['tBT','brkTo']].forEach(([id,k])=>{$('#'+id).onchange=e=>{if(CFG.url){syncSchedUi();return}CFG.man[k]=e.target.value||'';saveCfg();refreshSchedule(true)}});
$('#logClear').onclick=()=>{LOG.length=0;store.set('log',LOG);renderLog()};
$('#logCopy').onclick=()=>{const t=LOG.map(e=>new Date(e.t).toLocaleString('vi-VN')+' | '+e.s+' | '+e.x).join('\n');try{navigator.clipboard.writeText(t).then(()=>toast('Đã sao chép nhật ký'),()=>toast('Không sao chép được'))}catch(e){toast('Không sao chép được')}};

/* Windows */
$('#swAuto').onclick=async e=>{try{const on=!e.currentTarget.classList.contains('on');const r=await host('autostart',{on});e.currentTarget.classList.toggle('on',!!r);toast(r?'Sẽ tự chạy cùng Windows':'Đã tắt tự chạy')}catch(err){toast('Lỗi: '+err.message)}};
$('#wpPick').onclick=()=>host('wallpaper',{},600000).then(ok=>{if(ok)toast('🖼 Đã đổi hình nền Windows')}).catch(e=>toast('Lỗi: '+e.message));
$('#quit').onclick=()=>host('quit').catch(()=>{});

let _pm=0;dock.addEventListener('pointermove',e=>{if(_pm||document.body.classList.contains('eco'))return;_pm=requestAnimationFrame(()=>{_pm=0;const r=dock.getBoundingClientRect();dock.style.setProperty('--mx',(e.clientX-r.left)+'px');dock.style.setProperty('--my',(e.clientY-r.top)+'px')})},{passive:true});

/* ---------- khởi động ---------- */
document.body.classList.toggle('compact',CFG.compact);
dock.classList.remove('top','bottom','pill','full');dock.classList.add(CFG.pos,CFG.lay);document.body.classList.add('pos-'+CFG.pos);segSet('#segPos',CFG.pos);segSet('#segLay',CFG.lay);segSet('#segTb',CFG.taskbar);
$('#toasts').className='toasts '+CFG.pos;$('#cd').className='cd glass '+CFG.pos;
if(CFG.sliders)setSliders(CFG.sliders);else setSliders(STY[cur.sty]);
applyEco();apply(STY[cur.sty],false);widgetList();build();renderLog();updateModeUi();refreshDsrc();
function pomoCheck(){if(S.pEnd&&Date.now()>=S.pEnd){S.pEnd=0;S.pRem=POMO;toast('🍅 Pomodoro xong — nghỉ 5 phút nhé')}}
let lastLoop=0,tickN=0;
function loop(){const n=Date.now();if(n-lastLoop<800)return;lastLoop=n;heartbeat();shiftPollers();runProviders();pomoCheck();
 const b=DATA.sys&&DATA.sys.bat,lb=!!(b&&!b.charging&&b.pct<20);if(lb!==lowBat){lowBat=lb;applyEco()}
 if((++tickN)%2===0)evalAuto();if(tickN%20===0){refreshDsrc();pollTheme()}if(tickN%5===0)refreshSdUi();renderBubbles();tick()}
/* Worker làm đồng hồ: ít bị trình duyệt làm chậm; tự dùng setInterval nếu Worker không chạy được */
(function startLoop(){let fb=null,got=false;const fallback=()=>{if(!fb)fb=setInterval(loop,1000)};
 try{const w=new Worker(URL.createObjectURL(new Blob(['setInterval(function(){postMessage(1)},1000)'],{type:'text/javascript'})));w.onmessage=()=>{got=true;loop()};w.onerror=fallback;setTimeout(()=>{if(!got)fallback()},3000)}catch(e){fallback()}})();
(async()=>{
 if(HOSTED){
  try{const i=await host('info');$('#ver').textContent='Liquid Glass · bản '+i.version+' · '+i.screen+' · dữ liệu thật'}catch(e){}
  await loadSecrets();
  if(CFG.pins==null){try{CFG.pins=await host('defaults')}catch(e){CFG.pins=[]}saveCfg()}
  try{$('#swAuto').classList.toggle('on',!!(await host('autostart')))}catch(e){}
  await pollTheme();
  if(!Q.get('notb'))host('taskbar',{mode:CFG.taskbar}).catch(e=>log('sys','Không đổi được taskbar: '+e.message,'err'));
  syncBar();kick(1500);
  if(!store.get('seen')){store.set('seen',1);setTimeout(()=>openPanel(),700)}
 }
 runProviders(true);
 refreshSchedule().then(()=>{pullState();evalAuto()});if(QUEUE.length)flush();
})();
