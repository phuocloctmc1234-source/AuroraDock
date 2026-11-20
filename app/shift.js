'use strict';
/* Chấm công & giờ ngủ. Mỗi lần bấm được ghi VÀO FILE THẬT trên máy (timesheet.jsonl, qua host),
   và nếu có URL bridge thì gửi thêm lên hệ thống chấm công của bạn. Không còn máy chủ giả. */
const S={pEnd:0,pRem:1500000,play:true};
const POMO=1500000,pomoLeft=()=>S.pEnd?Math.max(0,S.pEnd-Date.now()):S.pRem;
S.sl=Object.assign({on:false,since:0,manualOff:false},store.get('sl',{}));
S.wk=Object.assign({phase:'idle',since:0,brkSince:0,brkDone:false,total:0,day:'',seq:0},store.get('wk',{}));
const SCH=Object.assign({start:'',brkFrom:'',brkTo:'',ver:'0',at:0},store.get('sch',{}));
const hasSch=()=>!!SCH.start;
const saveWk=()=>{const w=S.wk;store.set('wk',{phase:w.phase,since:w.since,brkSince:w.brkSince,brkDone:w.brkDone,total:w.total,day:w.day,seq:w.seq})};
const saveSl=()=>store.set('sl',{on:S.sl.on,since:S.sl.since,manualOff:S.sl.manualOff});
const LABEL={sdEnd:'Kết thúc nghỉ (Shift Desk)',startShift:'Bắt đầu ca',startBreak:'Bắt đầu break',endBreak:'Kết thúc break',endShift:'Kết thúc ca',startSleep:'Bắt đầu giờ ngủ',endSleep:'Kết thúc giờ ngủ'};

/* ---------- timesheet trên máy ---------- */
function deriveState(evs){
 const idle={phase:'idle',since:0,brkSince:0,brkDone:false},s=(evs||[]).slice().sort((a,b)=>a.at-b.at);let li=-1;
 s.forEach((e,i)=>{if(e.name==='startShift')li=i});if(li<0)return idle;
 const st={phase:'working',since:s[li].at,brkSince:0,brkDone:false};
 for(let j=li+1;j<s.length;j++){const e=s[j];
  if(e.name==='startBreak'&&st.phase==='working'){st.phase='break';st.brkSince=e.at}
  else if(e.name==='endBreak'&&st.phase==='break'){st.phase='working';st.brkDone=true}
  else if(e.name==='endShift'){st.phase='done'}}
 if(st.phase!=='done'&&Date.now()-st.since>20*36e5)return idle;   // quên kết thúc ca từ hôm trước
 return st}
function calcWorked(evs){const s=(evs||[]).slice().sort((a,b)=>a.at-b.at),out={};let st=null,brk=null,bt=0;
 const close=end=>{if(st==null)return;const k=dayKeyOf(st);out[k]=(out[k]||0)+Math.max(0,end-st-bt);st=null;brk=null;bt=0};
 for(const e of s){if(e.name==='startShift'){if(st!=null)close(e.at);st=e.at;bt=0;brk=null}
  else if(e.name==='startBreak'&&st!=null)brk=e.at;
  else if(e.name==='endBreak'&&brk!=null){bt+=e.at-brk;brk=null}
  else if(e.name==='endShift')close(e.at)}
 if(st!=null){const n=Date.now();if(brk!=null)bt+=n-brk;close(n)}return out}
async function tsLoad(){DATA.ts=await host('tsList',{since:Date.now()-9*864e5});TSAMP.ts=Date.now();ERR.ts=''}

async function localApi(action,p){
 if(action==='act'){const r=await host('tsAppend',{id:p.requestId,name:p.name,at:p.at});return {ok:true,duplicate:!!r.duplicate,applied:true}}
 if(action==='getState')return deriveState(await host('tsList',{since:Date.now()-36*36e5}));
 if(action==='getSchedule'){const m=CFG.man,ss=(typeof schedStart==='function')?schedStart():null,st=ss!=null?ss:(m.start||'');return {start:st,brkFrom:m.brkFrom||'',brkTo:m.brkTo||'',ver:[st,m.brkFrom,m.brkTo].join('|'),at:Date.now()}}
 throw new Error('unknown action')}
async function remoteApi(action,payload){
 const r=await host('http',{url:CFG.url,method:'POST',headers:{'Content-Type':'text/plain;charset=utf-8'},body:JSON.stringify(Object.assign({token:CFG.token,action},payload||{}))},25000);
 if(r.status<200||r.status>=300)throw new Error('HTTP '+r.status);let j;try{j=JSON.parse(r.body)}catch(e){throw new Error('server trả về không phải JSON')}
 if(j&&j.error)throw new Error(j.error);return j}
const api=(action,payload)=>CFG.url?remoteApi(action,payload):localApi(action,payload||{});

const SYNC={last:0,err:''};
let QUEUE=store.get('queue',[]);const saveQ=()=>store.set('queue',QUEUE);
const syncState=()=>SYNC.err?'off':(QUEUE.length?'pend':(CFG.url?'ok':'local'));
const syncDot=()=>`<i class="sd ${syncState()}"></i>`;
const syncText=()=>{const s=syncState();return s==='local'?'Đã ghi vào timesheet trên máy':s==='off'?'Chưa đồng bộ · '+QUEUE.length+' chờ gửi':s==='pend'?'Đang gửi '+QUEUE.length:'Đã đồng bộ '+(SYNC.last?hhmm(SYNC.last):'')};
const BACKOFF=[2,5,15,30,60,120];
let flushing=false;
async function flush(){if(flushing)return;flushing=true;
 try{for(const it of QUEUE.slice()){if(it.next>Date.now())continue;
  try{
   if(!it.local){await localApi('act',{name:it.name,requestId:it.id,at:it.at});it.local=1;saveQ()}
   let note='đã ghi vào timesheet trên máy';
   if(CFG.url){const r=await remoteApi('act',{name:it.name,requestId:it.id,at:it.at});note=r.duplicate?'server đã có, bỏ qua trùng':r.applied===false?'server đang ở trạng thái đó':'đã đồng bộ lên server'}
   QUEUE=QUEUE.filter(q=>q.id!==it.id);saveQ();SYNC.last=Date.now();SYNC.err='';
   log(it.name,LABEL[it.name]+' → '+note,'ok');tsLoad().catch(()=>{})}
  catch(e){it.tries=(it.tries||0)+1;it.next=Date.now()+BACKOFF[Math.min(it.tries-1,BACKOFF.length-1)]*1000;SYNC.err=String(e.message||e);saveQ();
   if(it.tries===1||it.tries%5===0)log(it.name,LABEL[it.name]+' — CHƯA ĐỒNG BỘ ('+SYNC.err+'), sẽ tự thử lại','pend')}}}
 finally{flushing=false;tick()}}

/* ---------- hành động (thủ công & tự động) ---------- */
const VALID={startShift:()=>S.wk.phase==='idle'||S.wk.phase==='done',startBreak:()=>S.wk.phase==='working'&&!S.wk.brkDone,endBreak:()=>S.wk.phase==='break',endShift:()=>S.wk.phase==='working',
 startSleep:()=>!S.sl.on,endSleep:()=>S.sl.on};
const busy={};
function doAction(name,opt){opt=opt||{};const w=S.wk,now=Date.now();
 if(busy[name]||!VALID[name]()){if(!opt.auto)toast('Thao tác không hợp lệ ở trạng thái hiện tại');return false}
 busy[name]=1;setTimeout(()=>{busy[name]=0},1500);
 if(name==='startShift'){w.phase='working';w.since=now;w.brkDone=false;const dk=dayKey();w.seq=(w.day===dk?w.seq:0)+1;w.day=dk;saveWk()}
 else if(name==='startBreak'){w.phase='break';w.brkSince=now;saveWk()}
 else if(name==='endBreak'){w.phase='working';w.brkDone=true;saveWk()}
 else if(name==='endShift'){w.total=now-w.since;w.phase='done';saveWk()}
 else if(name==='startSleep'){S.sl.on=true;S.sl.since=now;S.sl.manualOff=false;saveSl()}
 else if(name==='endSleep'){S.sl.on=false;if(!opt.auto)S.sl.manualOff=true;saveSl()}
 const id=(name.indexOf('Sleep')>0?dayKey()+'-sleep-'+(now%1e8)+'-':(w.day+'-'+w.seq+'-'))+name;
 QUEUE.push({id,name,at:now,tries:0,next:0,auto:!!opt.auto});saveQ();
 log(name,LABEL[name]+(opt.auto?' (tự động'+(opt.note?' · '+opt.note:'')+')':' (thủ công)'),'pend');
 toast((opt.auto?'🤖 ':'')+LABEL[name]+(opt.auto?' — đã tự động':''));
 tick();flush();return true}
function newShift(){S.wk.phase='idle';saveWk();tick()}

/* ---------- tự động: đếm ngược + Hủy, đọc lại lịch trước khi bấm ---------- */
let PEND=null;const FIRED=store.get('fired',{});
const markFired=(k,v)=>{FIRED[k]=v;const keys=Object.keys(FIRED);if(keys.length>60)keys.slice(0,keys.length-60).forEach(x=>delete FIRED[x]);store.set('fired',FIRED)};
function slWin(){const c=nowMin(),b=hm(CFG.bed),w=hm(CFG.wake);return b>w?(c>=b||c<w):(c>=b&&c<w)}
function dueInfo(name){const w=S.wk,n=Date.now();
 if(name==='sdEnd')return sdDue();
 if(name==='startSleep'){if(!CFG.slAuto||S.sl.on||S.sl.manualOff||!slWin())return null;return {key:dayKey()+'-sleep-start'}}
 if(name==='endSleep'){if(!CFG.slAuto||!S.sl.on)return null;const e=occAfter(CFG.wake,S.sl.since);if(Date.now()<e)return null;return {key:dayKey()+'-sleep-end',over:Date.now()-e}}
 return null}
function showCd(){const c=$('#cd');c.innerHTML=`<span>⏱ <b id="cdL"></b> tự động sau <b id="cdS"></b>s</span><span class="bar"><i id="cdB"></i></span><button class="btn" id="cdNow">Làm ngay</button><button class="btn" id="cdCancel">Hủy</button>`;
 c.classList.add('on');$('#cdNow').onclick=()=>{if(PEND)PEND.fireAt=Date.now()};$('#cdCancel').onclick=()=>cancelPend(true);kick()}
function hideCd(){$('#cd').classList.remove('on');kick()}
function startPend(name,d,force){if(PEND)return;PEND={name,key:d.key,force:!!force,fireAt:Date.now()+CFG.cdSec*1000,total:CFG.cdSec*1000,over:d.over||0};
 showCd();$('#cdL').textContent=LABEL[name];pendTick();PEND.t=setInterval(pendTick,250);
 log(name,'Sắp '+LABEL[name].toLowerCase()+' tự động — đếm ngược '+CFG.cdSec+'s (có thể Hủy)','info');
 refreshSchedule(true)}
function pendTick(){const p=PEND;if(!p)return;const left=p.fireAt-Date.now();const s=$('#cdS'),b=$('#cdB');if(s)s.textContent=Math.max(0,Math.ceil(left/1000));if(b)b.style.width=Math.max(0,Math.min(100,left/p.total*100))+'%';
 if(left<=0&&!p.firing){p.firing=true;firePend(p)}}
function endPend(){if(PEND){clearInterval(PEND.t);PEND=null}hideCd()}
function cancelPend(byUser,reason){const p=PEND;if(!p)return;endPend();
 if(byUser){markFired(p.key,'cancelled');log(p.name,'Đã HỦY '+LABEL[p.name].toLowerCase()+' tự động (bạn bấm Hủy)','skip');toast('⊘ Đã hủy '+LABEL[p.name].toLowerCase()+' tự động')}
 else{log(p.name,reason||'Điều kiện đổi — hủy tự động','skip');toast('⊘ '+(reason||'Đã hủy tự động'))}}
async function firePend(p){
 const ok=p.name==='sdEnd'?await sdRefresh(0).then(()=>true,()=>false):await refreshSchedule(true);if(PEND!==p)return;
 if(!p.force){const d=dueInfo(p.name);if(!d||d.late){cancelPend(false,'Lịch/trạng thái đã đổi — hủy '+LABEL[p.name].toLowerCase()+' tự động');return}p.over=d.over||0}
 endPend();markFired(p.key,'done');
 const notes=[];if(p.over>120000)notes.push('quá giờ '+Math.round(p.over/60000)+' phút (bù)');
 if(p.name==='sdEnd'){if(!ok){log('sd','Không đọc lại được Shift Desk trước khi bấm — HỦY, hãy bấm tay','skip');toast('⚠ Không đọc lại được Shift Desk — hủy tự động');return}sdDo('endBreak',{auto:true,note:notes.join(' · '),skipRead:true});return}
 if(!ok)notes.push('dùng lịch đã lưu lúc '+(SCH.at?hhmm(SCH.at):'—'));
 doAction(p.name,{auto:true,note:notes.join(' · ')})}
function evalAuto(){const w=S.wk;
 if(w.phase==='done'&&w.day!==dayKey()){w.phase='idle';saveWk()}
 if(!slWin()&&S.sl.manualOff){S.sl.manualOff=false;saveSl()}
 if(PEND){if(!PEND.force){const d=dueInfo(PEND.name);if(!d||d.late)cancelPend(false,'Lịch/trạng thái đã đổi — hủy '+LABEL[PEND.name].toLowerCase()+' tự động')}return}
 for(const nm of ['sdEnd','startSleep','endSleep']){const d=dueInfo(nm);if(!d)continue;
  if(d.late&&nm==='sdEnd'){const lk=dayKey()+'-sdlate';if(!FIRED[lk]){markFired(lk,1);log(nm,'Nghỉ đã quá giờ kết thúc hơn 60 phút — KHÔNG tự bấm, hãy bấm tay','skip');toast('⚠ Nghỉ đã quá giờ — hãy bấm "Kết thúc nghỉ" thủ công')}continue}
  if(FIRED[d.key])continue;startPend(nm,d);break}}

/* ---------- lịch ca: từ server (bridge) hoặc do bạn nhập tay ---------- */
let lastSchedErr=0;
async function refreshSchedule(force){
 try{const r=await api('getSchedule');SYNC.err='';SYNC.last=Date.now();
  const ch=r.start!==SCH.start||r.brkFrom!==SCH.brkFrom||r.brkTo!==SCH.brkTo,first=!SCH.at;
  if(ch){const parts=[];if(r.start!==SCH.start)parts.push('vào ca '+(SCH.start||'—')+' → '+(r.start||'—'));if(r.brkFrom!==SCH.brkFrom||r.brkTo!==SCH.brkTo)parts.push('break '+(SCH.brkFrom||'—')+'–'+(SCH.brkTo||'—')+' → '+(r.brkFrom||'—')+'–'+(r.brkTo||'—'));
   Object.assign(SCH,{start:r.start,brkFrom:r.brkFrom,brkTo:r.brkTo,ver:String(r.ver||'')});
   if(!first&&CFG.url){log('sched','📅 Lịch thay đổi: '+parts.join('; '),'info');toast('📅 Lịch thay đổi: '+parts.join('; '))}}
  SCH.at=Date.now();store.set('sch',SCH);if(typeof syncSchedUi==='function')syncSchedUi();return true}
 catch(e){SYNC.err=String(e.message||e);if(Date.now()-lastSchedErr>300000){lastSchedErr=Date.now();log('sched','Không cập nhật được lịch ('+SYNC.err+') — dùng lịch đã lưu lúc '+(SCH.at?hhmm(SCH.at):'—'),'pend')}return false}}
async function pullState(){if(QUEUE.length)return;
 try{const r=await api('getState');SYNC.err='';SYNC.last=Date.now();const w=S.wk;
  if(QUEUE.length)return;
  if(r.phase&&r.phase!==w.phase){log('sync','Nguồn dữ liệu đang ở "'+r.phase+'" (giao diện: "'+w.phase+'") — cập nhật theo','info');
   w.phase=r.phase;if(r.since)w.since=r.since;if(r.brkSince)w.brkSince=r.brkSince;w.brkDone=!!r.brkDone;if(!w.day||(w.phase!=='idle'&&w.day!==dayKey())){w.day=dayKey();w.seq=Math.max(1,w.seq)}
   saveWk();toast('🔄 Đã đồng bộ trạng thái ca');tick()}}
 catch(e){SYNC.err=String(e.message||e)}}
let tSched=0,tState=0,lastBeat=Date.now();
function onWake(){tSched=0;tState=0;QUEUE.forEach(q=>{q.next=0});flush();evalAuto()}
function heartbeat(){const n=Date.now();if(n-lastBeat>20000){log('sys','Ứng dụng/máy vừa hoạt động lại sau '+dur(n-lastBeat)+' — đang đối chiếu','info');onWake()}lastBeat=n}
function shiftPollers(){const n=Date.now(),c=nowMin(),hot=c>=30&&c<120&&!!CFG.url;
 if(n-tSched>=(hot?30:CFG.pollSched)*1000){tSched=n;refreshSchedule()}
 if(n-tState>=CFG.pollState*1000){tState=n;pullState()}
 if(QUEUE.length&&!flushing&&QUEUE.some(q=>q.next<=n))flush()}
addEventListener('online',()=>{log('sys','Đã có mạng trở lại','info');onWake()});
addEventListener('offline',()=>{SYNC.err='offline';tick()});
