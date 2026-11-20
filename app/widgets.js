'use strict';
/* Widget + nguồn dữ liệu THẬT. Widget nào chưa kết nối sẽ hiện “Kết nối”, không bao giờ hiện số giả. */
const ring=(p,c)=>`<svg class="ring" viewBox="0 0 36 36"><circle class="bg" cx="18" cy="18" r="15"/><circle cx="18" cy="18" r="15" stroke="${c}" stroke-dasharray="${(clamp(p,0,100)*.942).toFixed(1)} 100"/></svg>`;
const bar=(l,v)=>`<div style="display:flex;align-items:center;gap:6px;font-size:10px"><span style="width:22px;opacity:.7">${l}</span><div style="flex:1;height:5px;border-radius:3px;background:rgba(255,255,255,.2)"><i style="display:block;height:100%;width:${clamp(v,0,100)}%;border-radius:3px;background:linear-gradient(90deg,#8be9ff,#c9a8ff)"></i></div></div>`;
const chip=(t,c)=>`<span class="chip" style="background:${c}">${t}</span>`;
const GREY='linear-gradient(135deg,#8a93a8,#5a6378)';
const spark=k=>{const d=H[k]||[];if(d.length<3)return'';const mn=Math.min(...d),mx=Math.max(...d),r=(mx-mn)||1;const pts=d.map((v,i)=>`${(i/(d.length-1)*200).toFixed(1)},${(46-(v-mn)/r*40).toFixed(1)}`).join(' ');
 return `<svg class="sp" viewBox="0 0 200 50" preserveAspectRatio="none"><polyline points="${pts}" fill="none" stroke="#fff" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" opacity=".9"/></svg>`};
const rows=a=>a.map(([x,y])=>`<div class="row"><span>${esc(x)}</span><b title="${esc(y)}">${esc(y)}</b></div>`).join('');
const popC=(t,big,k,r,extra)=>`<h4>${esc(t)}</h4><div class="big">${esc(big)}</div>${k?spark(k):''}${rows(r||[])}${extra||''}`;
const btn=(label,act,arg)=>`<button class="btn" data-act="${act}"${arg!=null?` data-arg="${esc(arg)}"`:''}>${label}</button>`;
const ZERO=new Set(['JPY','VND','KRW','CLP','PYG','UGX','XAF','XOF','XPF','RWF','KMF','GNF','DJF','BIF','VUV']);
const money=(n,cur)=>{try{return new Intl.NumberFormat(undefined,{style:'currency',currency:cur,maximumFractionDigits:ZERO.has(cur)?0:2}).format(n/(ZERO.has(cur)?1:100))}catch(e){return (n/100).toFixed(2)+' '+cur}};

/* ---------- biểu tượng ứng dụng (lấy thật từ file .exe) ---------- */
const ICONS={},ICONS_REQ={};
function iconOf(path){if(!path)return null;if(ICONS[path])return ICONS[path];
 if(!ICONS_REQ[path]){ICONS_REQ[path]=1;host('icon',{path}).then(r=>{ICONS[path]=r;tick()}).catch(()=>{ICONS[path]={name:baseName(path),png:null}})}return null}
const appName=p=>{const i=iconOf(p);return (i&&i.name)||baseName(p)};

/* ---------- nguồn dữ liệu ---------- */
const pSys=async()=>{const r=await host('sys');DATA.sys=r;hist('cpu',r.cpu);hist('ram',r.ram);if(r.net)hist('net',r.net.down)};
const CHP={},CHPASK={};
const pWins=async()=>{DATA.wins=await host('windows');chromeProf()};
async function chromeProf(){const L=(DATA.wins&&DATA.wins.list)||[],hs=L.filter(x=>/chrome\.exe$/i.test(String(x.p||''))&&!(x.h in CHPASK)).map(x=>x.h);if(!hs.length)return;hs.forEach(h=>CHPASK[h]=1);
 try{const r=await host('chromeProf',{hs},30000);let n=0;for(const k in r){CHP[k]=r[k];n++}if(n)setTimeout(()=>pump('wins'),50);hs.forEach(h=>{if(!(h in CHP))setTimeout(()=>{delete CHPASK[h]},15000)})}catch(e){hs.forEach(h=>delete CHPASK[h])}}
const pMedia=async()=>{DATA.media=await host('media')};
const pScr=async()=>{DATA.scr=await host('screentime')};
const pFiles=async()=>{DATA.files=await host('files')};
const pClaude=async()=>{DATA.claude=await host('claude',{},90000)};
async function pGit(){const hd={Authorization:'Bearer $secret:github',Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28'};
 const q=s=>hjson('https://api.github.com/search/issues?per_page=5&q='+encodeURIComponent(s),hd);
 const [rev,iss,mine]=await Promise.all([q('is:pr is:open review-requested:@me archived:false'),q('is:issue is:open assignee:@me archived:false'),q('is:pr is:open author:@me archived:false')]);
 const it=x=>x.items.map(i=>({t:i.title,u:i.html_url,r:String(i.repository_url||'').split('/').slice(-2).join('/')}));
 DATA.git={rev:{n:rev.total_count,items:it(rev)},iss:{n:iss.total_count,items:it(iss)},mine:{n:mine.total_count,items:it(mine)}}}
async function pStripe(){const hd={Authorization:'Bearer $secret:stripe'};const d=new Date();d.setHours(0,0,0,0);const since=Math.floor(d/1000);let after='',pages=0;const all=[];
 while(pages++<6){const j=await hjson('https://api.stripe.com/v1/charges?limit=100&created%5Bgte%5D='+since+(after?'&starting_after='+after:''),hd);all.push(...j.data);if(!j.has_more||!j.data.length)break;after=j.data[j.data.length-1].id}
 const ok=all.filter(c=>c.status==='succeeded'&&c.paid),by={};
 ok.forEach(c=>{const cur=String(c.currency).toUpperCase();(by[cur]=by[cur]||{sum:0,n:0});by[cur].sum+=c.amount-(c.amount_refunded||0);by[cur].n++});
 DATA.stripe={by,n:ok.length,last:ok.slice(0,4).map(c=>({a:c.amount-(c.amount_refunded||0),c:String(c.currency).toUpperCase(),t:c.created*1000,d:c.description||(c.billing_details&&c.billing_details.email)||''}))}}
async function pWea(){
 if(!CFG.geo||CFG.geo.q!==CFG.city){const g=await hjson('https://geocoding-api.open-meteo.com/v1/search?count=1&language=vi&name='+encodeURIComponent(CFG.city));
  if(!g.results||!g.results.length)throw new Error('Không tìm thấy thành phố "'+CFG.city+'"');const x=g.results[0];CFG.geo={q:CFG.city,lat:x.latitude,lon:x.longitude,name:x.name};saveCfg()}
 const j=await hjson('https://api.open-meteo.com/v1/forecast?latitude='+CFG.geo.lat+'&longitude='+CFG.geo.lon+'&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m&daily=precipitation_probability_max&timezone=auto&forecast_days=1');
 DATA.wea=Object.assign({},j.current,{rain:j.daily&&j.daily.precipitation_probability_max?j.daily.precipitation_probability_max[0]:null,name:CFG.geo.name})}
async function pCal(){const r=await host('secretFetch',{name:'calendar'},40000);if(r.status!==200)throw new Error('HTTP '+r.status+' — link ICS sai hoặc đã bị đổi');DATA.cal=expandICS(r.body)}

/* ---------- đọc lịch ICS (Google/Outlook/Apple) ---------- */
function tzOffset(ms,tz){const f=new Intl.DateTimeFormat('en-US',{timeZone:tz,hourCycle:'h23',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit'});
 const p={};f.formatToParts(new Date(Math.floor(ms/1000)*1000)).forEach(x=>p[x.type]=x.value);return Date.UTC(+p.year,+p.month-1,+p.day,+p.hour%24,+p.minute,+p.second)-Math.floor(ms/1000)*1000}
function icsDate(val,params){const m=String(val).match(/^(\d{4})(\d{2})(\d{2})(?:T(\d{2})(\d{2})(\d{2})(Z)?)?$/);if(!m)return null;
 if(m[4]===undefined)return {t:new Date(+m[1],+m[2]-1,+m[3]).getTime(),allDay:true};
 const wall=Date.UTC(+m[1],+m[2]-1,+m[3],+m[4],+m[5],+m[6]);if(m[7])return {t:wall};
 if(params&&params.TZID){try{let t=wall-tzOffset(wall,params.TZID);t=wall-tzOffset(t,params.TZID);return {t}}catch(e){}}
 return {t:new Date(+m[1],+m[2]-1,+m[3],+m[4],+m[5],+m[6]).getTime()}}
function icsProp(line){const i=line.indexOf(':');if(i<0)return null;const parts=line.slice(0,i).split(';'),params={};parts.slice(1).forEach(p=>{const k=p.indexOf('=');if(k>0)params[p.slice(0,k).toUpperCase()]=p.slice(k+1).replace(/"/g,'')});return {name:parts[0].toUpperCase(),params,val:line.slice(i+1)}}
function expandICS(txt){
 const lines=String(txt).replace(/\r?\n[ \t]/g,'').split(/\r?\n/),evs=[];let cur=null;
 for(const ln of lines){if(ln==='BEGIN:VEVENT')cur={};else if(ln==='END:VEVENT'){if(cur)evs.push(cur);cur=null}
  else if(cur){const p=icsProp(ln);if(!p)continue;if(p.name==='EXDATE')(cur.ex=cur.ex||[]).push(...p.val.split(',').map(v=>(icsDate(v,p.params)||{}).t));else cur[p.name]=p}}
 const now=Date.now(),from=now-3*36e5,to=now+36*36e5,out=[],over={};
 const un=s=>String(s||'').replace(/\\n/gi,' ').replace(/\\([,;\\])/g,'$1');
 const cancelled=e=>e.STATUS&&/CANCELLED/i.test(e.STATUS.val);
 evs.forEach(e=>{if(e['RECURRENCE-ID']){const r=icsDate(e['RECURRENCE-ID'].val,e['RECURRENCE-ID'].params);if(r)over[(e.UID?e.UID.val:'')+'|'+r.t]=e}});
 const WD=['SU','MO','TU','WE','TH','FR','SA'];
 for(const e of evs){if(!e.DTSTART||e['RECURRENCE-ID']||cancelled(e))continue;
  const s=icsDate(e.DTSTART.val,e.DTSTART.params);if(!s||s.allDay)continue;
  const en=e.DTEND?icsDate(e.DTEND.val,e.DTEND.params):null,len=en&&!en.allDay?en.t-s.t:36e5;
  const push=st=>{if(st+len<from||st>to)return;if(e.ex&&e.ex.indexOf(st)>=0)return;let x=e,a=st,b=st+len;const o=over[(e.UID?e.UID.val:'')+'|'+st];
   if(o){if(cancelled(o))return;x=o;const ds=o.DTSTART&&icsDate(o.DTSTART.val,o.DTSTART.params),de=o.DTEND&&icsDate(o.DTEND.val,o.DTEND.params);if(ds){a=ds.t;b=de&&!de.allDay?de.t:a+len}}
   out.push({s:a,e:b,t:un((x.SUMMARY||{}).val)||'(Không tiêu đề)',loc:un((x.LOCATION||{}).val)})};
  if(!e.RRULE){push(s.t);continue}
  const rr={};e.RRULE.val.split(';').forEach(kv=>{const k=kv.indexOf('=');rr[kv.slice(0,k)]=kv.slice(k+1)});
  const until=rr.UNTIL?((icsDate(rr.UNTIL,{})||{}).t||Infinity):Infinity,cnt=rr.COUNT?+rr.COUNT:Infinity,iv=+rr.INTERVAL||1;
  const bd=rr.BYDAY?rr.BYDAY.split(',').map(x=>WD.indexOf(x.slice(-2))):null,d0=new Date(s.t);let n=0;
  for(let k=0;k<4000;k++){const t=s.t+k*864e5;if(t>to||t>until)break;const d=new Date(t);let m=false;
   if(rr.FREQ==='DAILY')m=k%iv===0;
   else if(rr.FREQ==='WEEKLY')m=bd?(bd.indexOf(d.getDay())>=0&&Math.floor(k/7)%iv===0):(k%(7*iv)===0);
   else if(rr.FREQ==='MONTHLY')m=d.getDate()===d0.getDate()&&((d.getFullYear()-d0.getFullYear())*12+d.getMonth()-d0.getMonth())%iv===0;
   else if(rr.FREQ==='YEARLY')m=d.getDate()===d0.getDate()&&d.getMonth()===d0.getMonth()&&(d.getFullYear()-d0.getFullYear())%iv===0;
   if(m){if(++n>cnt)break;push(t)}}}
 return out.filter(x=>x.e>now).sort((a,b)=>a.s-b.s)}

/* ---------- bộ lập lịch lấy dữ liệu: chỉ lấy cho widget đang bật ---------- */
const PROV={
 sys:{every:3,run:pSys},wins:{every:2,run:pWins,always:true},media:{every:4,run:pMedia},scr:{every:30,run:pScr},files:{every:120,run:pFiles},claude:{every:20,run:pClaude},
 crate:{every:10,run:async()=>{DATA.crate=await host('claudeRate')||null},always:true},
 git:{every:120,run:pGit,cfg:'github'},stripe:{every:120,run:pStripe,cfg:'stripe'},cal:{every:300,run:pCal,cfg:'calendar'},wea:{every:900,run:pWea,cfg:'city'},ts:{every:60,run:tsLoad,always:true}};
const PSTATE={};
function runProviders(force,only){if(window.PAUSED&&!only)return;const now=Date.now(),wanted=new Set(W.filter(w=>w.on&&w.prov).map(w=>w.prov));
 for(const k in PROV){const p=PROV[k];if(only&&only!==k)continue;if(!(p.always||wanted.has(k)))continue;if(p.cfg&&!have(p.cfg))continue;
  const s=PSTATE[k]=PSTATE[k]||{t:0,busy:false},ev=typeof p.every==='function'?p.every():p.every;if(s.busy||(!force&&now-s.t<ev*1000))continue;
  s.busy=true;s.t=now;p.run().then(()=>{ERR[k]='';TSAMP[k]=Date.now()}).catch(e=>{ERR[k]=String(e.message||e)}).finally(()=>{s.busy=false;tick()})}}
const pump=k=>runProviders(true,k);

function wstate(w){if(!w.prov)return 'ok';if(w.cfg&&!have(w.cfg))return 'need';
 if(DATA[w.prov]!=null)return 'ok';return ERR[w.prov]?'err':'load'}
function renderW(w){const s=wstate(w);
 if(s==='ok')return w.r();
 if(s==='need')return `${chip(w.ic||'＋',GREY)}<div class="t"><span class="v">Kết nối</span><span class="l">${esc(w.name)}</span></div>`;
 if(s==='err')return `${chip('!','linear-gradient(135deg,#ff8a8a,#d94a4a)')}<div class="t"><span class="v">Lỗi</span><span class="l" title="${esc(ERR[w.prov])}">${esc(w.name)}</span></div>`;
 return `${chip('…',GREY)}<div class="t"><span class="v">…</span><span class="l">${esc(w.name)}</span></div>`}
function renderP(w){const s=wstate(w);
 if(s==='ok')return w.p?w.p():null;
 if(s==='need')return `<h4>${esc(w.name)}</h4><div class="note">Chưa kết nối. Nhập khóa/đường dẫn trong Cài đặt → Kết nối dữ liệu để hiện số liệu thật.</div><div class="btns">${btn('Mở Cài đặt','openData',w.sec||'')}</div>`;
 if(s==='err')return `<h4>${esc(w.name)}</h4><div class="note" style="opacity:.9">Không lấy được dữ liệu:<br>${esc(ERR[w.prov])}</div><div class="btns">${btn('Thử lại','retry',w.prov)}${w.errAct?btn(w.errAct[0],w.errAct[1]):''}</div>`;
 return `<h4>${esc(w.name)}</h4><div class="note">Đang tải…</div>`}

/* ---------- thanh tác vụ thật: ghim + cửa sổ đang mở ---------- */
const SEEN={};let seenN=0;
function groups(){const wins=(DATA.wins&&DATA.wins.list)||[],by=new Map();
 wins.forEach(x=>{const uwp=/ApplicationFrameHost/i.test(x.p),key=uwp?'uwp:'+x.t:String(x.p||'').toLowerCase();let g=by.get(key);if(!g){g={key,path:x.p,wins:[],uwp};by.set(key,g)}g.wins.push(x)});
 const out=[];(CFG.pins||[]).forEach(p=>{const key=String(p).toLowerCase(),g=by.get(key);out.push({key,path:p,pinned:true,wins:g?g.wins:[]});by.delete(key)});
 const rest=[...by.values()];rest.forEach(g=>{if(!(g.key in SEEN))SEEN[g.key]=++seenN});rest.sort((a,b)=>SEEN[a.key]-SEEN[b.key]);const all=out.concat(rest),res=[];
 all.forEach(g=>{if(!(CFG.splitChrome!==false&&/chrome\.exe$/i.test(String(g.path||''))&&g.wins.length>0)){res.push(g);return}
  g.wins.slice().sort((x,y)=>x.h-y.h).forEach((w,i)=>res.push({key:g.key+'#'+w.h,path:g.path,pinned:g.pinned&&i===0,wins:[w],split:true,label:String(w.t||'').replace(/[\u00AD\u200B-\u200F\u202A-\u202E\u2060-\u206F\uFEFF]/g,'').replace(/\s+-\s+Google Chrome.*$/i,'').trim().slice(0,5)}))});
 return res}
function tasksHtml(){const fg=DATA.wins?String(DATA.wins.fg):'';
 return `<div class="tasks">${groups().map(g=>{const ic=g.uwp?null:iconOf(g.path),act=g.wins.some(x=>String(x.h)===fg),allMin=g.wins.length&&g.wins.every(x=>x.m),nm=g.uwp?g.wins[0].t:appName(g.path);
  return `<span class="task ${g.split?'split':''} ${g.wins.length?'run':''} ${act?'act':''} ${allMin?'min':''}" data-k="${esc(g.key)}" title="${esc(g.split?g.wins[0].t:nm)}">${ic&&ic.png?`<img src="data:image/png;base64,${ic.png}">`:`<b>${esc((nm||'?')[0].toUpperCase())}</b>`}${g.split&&g.label?`<i class="lb">${esc(g.label)}</i>`:''}${g.split&&CHP[g.wins[0].h]&&CHP[g.wins[0].h].png?`<img class="av" src="data:image/png;base64,${CHP[g.wins[0].h].png}" title="${esc(CHP[g.wins[0].h].name)}">`:''}${g.wins.length>1?`<em>${g.wins.length}</em>`:''}</span>`}).join('')}</div>`}
const groupOf=el=>{const t=el.target.closest('.task');return t?{t,g:groups().find(x=>x.key===t.dataset.k)}:null};
function taskClick(ev){const r=groupOf(ev);if(!r||!r.g)return;const g=r.g;
 if(!g.wins.length){if(g.path)host('launch',{target:g.path}).catch(e=>toast('Không mở được: '+e.message));return}
 if(g.wins.length===1){host('activate',{h:g.wins[0].h}).then(()=>setTimeout(()=>pump('wins'),200));return}
 openPop('menu:'+g.key,()=>winMenu(g.key),r.t)}
function taskMenu(ev){ev.preventDefault();const r=groupOf(ev);if(r&&r.g)openPop('menu:'+r.g.key,()=>winMenu(r.g.key),r.t)}
function winMenu(key){const g=groups().find(x=>x.key===key);if(!g)return '<h4>Ứng dụng</h4><div class="note">Đã đóng</div>';
 const nm=g.uwp?g.wins[0].t:appName(g.path),pinned=g.pinned;
 return `<h4>${esc(nm)}</h4>`+(g.wins.length?g.wins.map(w=>`<div class="win"><span data-act="win" data-arg="${w.h}" title="${esc(w.t)}">${esc(w.t)}</span><button data-act="closeWin" data-arg="${w.h}" title="Đóng cửa sổ">✕</button></div>`).join(''):'<div class="note">Chưa mở</div>')
  +`<div class="btns">${g.path&&!g.uwp?btn('Mở mới','launch',g.path):''}${g.path&&!g.uwp?(pinned?btn('Bỏ ghim','unpin',g.path):btn('Ghim vào dock','pin',g.path)):''}</div>`}

/* ---------- Claude (đọc log Claude Code trên máy) ---------- */
const CL='linear-gradient(135deg,#ff9b6a,#d97757)';
/* % hạn mức THẬT do Claude Code báo qua statusLine (chỉ gói Pro/Max/Ultra); hết hiệu lực khi cửa sổ reset */
const realRate=()=>{const c=DATA.crate;return c&&c.fiveHour&&c.fiveHour.resetsAt*1000>Date.now()?c:null};
function claudeEst(rr){const c=DATA.claude;if(!c||!c.active||rr.fiveHour.pct<5)return null;const limit=c.blockTok/rr.fiveHour.pct*100;return {limit,left:Math.max(0,limit-c.blockTok)}}
function claudeLimit(){const c=DATA.claude,u=+CFG.claudeLimit;if(u>0)return {v:u,src:'bạn đặt'};if(c&&c.prevMax>0)return {v:Math.max(c.prevMax,c.blockTok),src:'kỷ lục cũ'};return null}

/* ---------- thời tiết ---------- */
const WMO=c=>c===0?['☀','Trời quang']:c<=3?['⛅','Có mây']:c<=48?['🌫','Sương mù']:c<=57?['🌦','Mưa phùn']:c<=67?['🌧','Mưa']:c<=77?['❄','Tuyết']:c<=82?['🌧','Mưa rào']:['⛈','Dông'];
const nextEvent=()=>(DATA.cal||[]).find(e=>e.e>Date.now());
const todosLeft=()=>CFG.todos.filter(t=>!t.d).length;

const W=[
 {id:'tasks',g:'Hệ thống',name:'Ứng dụng đang mở',on:true,prov:'wins',r:tasksHtml,act:taskClick,ctx:taskMenu},
 {id:'claude',g:'AI',name:'Claude Code',on:true,prov:'claude',ic:'Cl',
  r:()=>{const rr=realRate();if(rr){const p=Math.round(rr.fiveHour.pct),w7=rr.sevenDay?' · tuần '+Math.round(rr.sevenDay.pct)+'%':'';
    return `${ring(p,p>=90?'#ff6b6b':p>=70?'#ffb020':'#ffb08a')}<div class="t"><span class="v">${p}% · còn ${100-p}%</span><span class="l">Claude · reset sau ${hmShort(rr.fiveHour.resetsAt-Date.now()/1000)}${w7}</span></div>`}
   const c=DATA.claude;if(!c.found)return `${chip('Cl',GREY)}<div class="t"><span class="v">Không thấy log</span><span class="l">Claude Code</span></div>`;
   const lim=claudeLimit(),pc=lim?Math.round(c.blockTok/lim.v*100):0;
   return `${c.active&&lim?ring(pc,'#ffb08a'):chip('Cl',CL)}<div class="t"><span class="v">${c.active?fmtTok(c.blockTok)+(lim?' · '+pc+'%':''):'Đang nghỉ'}</span><span class="l">${c.active?'Claude · reset sau '+hmShort((c.blockEnd-Date.now())/1000):'Claude · hôm nay '+fmtTok(c.dayTok)}</span></div>`},
  p:()=>{const rr=realRate();if(rr){const c0=DATA.claude,p=Math.round(rr.fiveHour.pct),est=claudeEst(rr),age=Math.round((Date.now()-rr.at)/6e4),s=rr.sevenDay;
    return popC('Claude · hạn mức THẬT',p+'% đã dùng',null,[['Còn lại (5 giờ)',(100-p)+'%'],['Reset 5 giờ',hhmm(rr.fiveHour.resetsAt*1000)+' · sau '+hmShort(rr.fiveHour.resetsAt-Date.now()/1000)],
     ...(s?[['7 ngày',Math.round(s.pct)+'% · còn '+(100-Math.round(s.pct))+'%'],['Reset 7 ngày',new Date(s.resetsAt*1000).toLocaleString('vi-VN',{weekday:'short',hour:'2-digit',minute:'2-digit'})]]:[]),
     ['Token đã dùng (block)',c0&&c0.active?fmtTok(c0.blockTok):'—'],['Ước tính hạn mức 5h',est?'~'+fmtTok(est.limit)+' token':'—'],['Ước tính còn lại',est?'~'+fmtTok(est.left)+' token':'—'],
     ['Model',rr.model||'—'],['Ngữ cảnh phiên',rr.ctxPct!=null?Math.round(rr.ctxPct)+'%':'—'],['Cập nhật',hhmm(rr.at)+(age>15?' (cũ '+age+' phút — Claude Code đang tắt?)':'')]],
     '<div class="note">% và giờ reset do chính Claude Code báo (thật). “Ước tính” = token đã dùng ÷ % × (100−%): chỉ đúng khi toàn bộ mức dùng là Claude Code trên máy này, không gồm claude.ai/máy khác.</div>')}
   const c=DATA.claude;if(!c.found)return '<h4>Claude Code</h4><div class="note">Không thấy thư mục log Claude Code (~/.claude/projects) trên máy này.</div>';
   const lim=claudeLimit(),pc=lim?Math.round(c.blockTok/lim.v*100):null,m=c.models[0];
   return popC('Claude Code',c.active?fmtTok(c.blockTok)+' token':'Đang nghỉ',null,[
    ['Block 5 giờ',c.active?c.blockMsgs+' tin nhắn'+(pc!=null?' · '+pc+'%':''):'chưa có block mới'],
    ['Reset lúc',c.active?hhmm(c.blockEnd):'—'],
    ['Mốc 100%',lim?fmtTok(lim.v)+' ('+lim.src+')':'chưa có (đặt trong Cài đặt)'],
    ['Hôm nay',fmtTok(c.dayTok)+' · '+c.dayMsgs+' tin'],['7 ngày',fmtTok(c.weekTok)],
    ['Model hôm nay',m?m.m.replace('claude-','')+' '+fmtTok(m.t):'—']],
    '<div class="note">Đọc từ log Claude Code trên máy này (không gồm claude.ai/app). Token = input + output + ghi cache; không tính đọc cache. Hạn mức thật của gói do Anthropic quản lý nên % chỉ là so sánh với mốc ở trên.</div>'+(DATA.chook&&DATA.chook.exeFound&&!DATA.chook.installed?'<div class="btns">'+btn('Cài hook để có % thật','chookInstall')+'</div>':''))}},
 {id:'stripe',g:'Tài chính',name:'Stripe',on:false,prov:'stripe',cfg:'stripe',ic:'S',
  r:()=>{const s=DATA.stripe,k=Object.keys(s.by).sort((a,b)=>s.by[b].n-s.by[a].n)[0];
   return `${chip('S','linear-gradient(135deg,#7a6bff,#a99bff)')}<div class="t"><span class="v">${k?money(s.by[k].sum,k):'—'}</span><span class="l">Stripe · ${s.n} đơn hôm nay</span></div>`},
  p:()=>{const s=DATA.stripe,ks=Object.keys(s.by);return popC('Stripe · hôm nay',ks.length?money(s.by[ks[0]].sum,ks[0]):'Chưa có đơn',null,
   [...ks.slice(1).map(k=>['Thêm ('+k+')',money(s.by[k].sum,k)]),['Số đơn',s.n],...s.last.map(c=>[hhmm(c.t),money(c.a,c.c)+(c.d?' · '+c.d:'')])])}},
 {id:'gh',g:'Lập trình',name:'GitHub',on:false,prov:'git',cfg:'github',ic:'GH',
  r:()=>{const g=DATA.git;return `${chip('GH','#2b2f3a')}<div class="t"><span class="v">${g.rev.n} PR chờ review</span><span class="l">${g.iss.n} issue · ${g.mine.n} PR của bạn</span></div>`},
  p:()=>{const g=DATA.git,list=(t,o)=>`<div class="row" style="margin-top:8px"><span>${t}</span><b>${o.n}</b></div>`+o.items.map(i=>`<div class="row"><span style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"><a data-open="${esc(i.u)}" title="${esc(i.r)}">${esc(i.t)}</a></span></div>`).join('');
   return `<h4>GitHub</h4>`+list('Chờ bạn review',g.rev)+list('Issue giao cho bạn',g.iss)+list('PR của bạn đang mở',g.mine)}},
 {id:'wk',g:'Chấm công & Giấc ngủ',name:'Ca làm',on:true,
  r:()=>{const w=S.wk,now=Date.now(),d=syncDot();
   if(w.phase==='idle')return `${chip('▶',GREY)}<div class="t"><span class="v">Bắt đầu ca</span><span class="l">${CFG.autoStart&&hasSch()?'tự động '+hhmm(occNear(SCH.start)-CFG.leadStart*6e4):'thủ công'}</span></div>${d}`;
   if(w.phase==='working')return `${chip('⏱','linear-gradient(135deg,#34c58a,#12a0b8)')}<div class="t"><span class="v">${dur(now-w.since)}</span><span class="l">${w.brkDone?'Đã break · đang làm':'Đang làm'+(SCH.brkFrom?' · break '+SCH.brkFrom:'')}</span></div>${d}`;
   if(w.phase==='break')return `${chip('☕','linear-gradient(135deg,#ffb020,#ff7a59)')}<div class="t"><span class="v">${dur(now-w.brkSince)}</span><span class="l">${CFG.autoBrk&&SCH.brkTo?'Break · tự kết thúc '+hhmm(occAfter(SCH.brkTo,w.brkSince)-CFG.leadBrk*6e4):'Break · thủ công'}</span></div>${d}`;
   return `${chip('✔','#34c58a')}<div class="t"><span class="v">Xong ca</span><span class="l">${dur(w.total)}</span></div>${d}`},
  p:()=>{const w=S.wk,now=Date.now(),ws=calcWorked(DATA.ts||[]);let wkSum=0;for(let i=0;i<7;i++)wkSum+=ws[dayKeyOf(now-i*864e5)]||0;
   const sched=[['Giờ vào ca',hasSch()?SCH.start:'chưa đặt'],['Tự bắt đầu',CFG.autoStart&&hasSch()?hhmm(occNear(SCH.start)-CFG.leadStart*6e4):'Tắt'],['Lịch break',SCH.brkFrom?SCH.brkFrom+' – '+SCH.brkTo:'chưa đặt'],
    ['Đã làm hôm nay',hmShort((ws[dayKey()]||0)/1000)],['7 ngày qua',hmShort(wkSum/1000)],['Ghi nhận',syncText()]];
   if(w.phase==='idle')return popC('Ca làm','Chưa bắt đầu',null,sched)+`<div class="btns">${btn('Bắt đầu ca','shift','startShift')}</div>`;
   if(w.phase==='working')return popC('Ca làm','Đang làm '+dur(now-w.since),null,sched)+`<div class="btns">${w.brkDone?'':btn('Bắt đầu giờ break','shift','startBreak')}${btn('Kết thúc ca','shift','endShift')}</div>`;
   if(w.phase==='break')return popC('Giờ break','Break '+dur(now-w.brkSince),null,sched)+`<div class="btns">${btn('Kết thúc giờ break','shift','endBreak')}</div>`;
   return popC('Ca làm','Xong · '+dur(w.total),null,sched)+`<div class="btns">${btn('Mở ca mới','newShift')}</div>`}},
 {id:'sl',g:'Chấm công & Giấc ngủ',name:'Giờ ngủ',on:true,vis:()=>S.sl.on||inRange(nowMin(),(hm(CFG.bed)+1320)%1440,(hm(CFG.wake)+30)%1440),
  r:()=>S.sl.on?`${chip('🌙','linear-gradient(135deg,#6a5cff,#a05bff)')}<div class="t"><span class="v">Ngủ ${dur(Date.now()-S.sl.since)}</span><span class="l">thức sau ${dur(occAfter(CFG.wake,S.sl.since)-Date.now())}</span></div>`:`${chip('🌙','linear-gradient(135deg,#3a3f70,#6a5cff)')}<div class="t"><span class="v">${dur(nextAt(CFG.bed)-Date.now())}</span><span class="l">đến giờ ngủ ${CFG.bed}</span></div>`,
  p:()=>popC('Giờ ngủ',S.sl.on?'Đang ngủ '+dur(Date.now()-S.sl.since):dur(nextAt(CFG.bed)-Date.now()),null,[['Đi ngủ',CFG.bed],['Thức dậy',CFG.wake],['Tự động',CFG.slAuto?'Bật':'Tắt']])+`<div class="btns">${btn(S.sl.on?'Kết thúc giờ ngủ':'Bắt đầu giờ ngủ','shift',S.sl.on?'endSleep':'startSleep')}</div>`},
 {id:'cal',g:'Năng suất',name:'Lịch',on:false,prov:'cal',cfg:'calendar',ic:'◷',
  r:()=>{const e=nextEvent();if(!e)return `${chip('◷','linear-gradient(135deg,#ff5f7a,#ff9a6a)')}<div class="t"><span class="v">Hết lịch</span><span class="l">Lịch · không còn sự kiện sắp tới</span></div>`;
   const m=Math.round((e.s-Date.now())/6e4);
   return `${chip('◷','linear-gradient(135deg,#ff5f7a,#ff9a6a)')}<div class="t"><span class="v" style="max-width:150px;overflow:hidden;text-overflow:ellipsis">${esc(e.t)}</span><span class="l">${m<=0?'đang diễn ra · đến '+hhmm(e.e):m<90?'sau '+m+' phút':'lúc '+hhmm(e.s)}</span></div>`},
  p:()=>{const l=(DATA.cal||[]).slice(0,6);return popC('Lịch sắp tới',l.length?l[0].t:'Không có sự kiện',null,l.map(e=>[hhmm(e.s)+(new Date(e.s).toDateString()!==new Date().toDateString()?' · mai':''),e.t+(e.loc?' · '+e.loc:'')]))}},
 {id:'pomo',g:'Năng suất',name:'Pomodoro',on:true,click:()=>{if(S.pEnd){S.pRem=Math.max(0,S.pEnd-Date.now());S.pEnd=0}else S.pEnd=Date.now()+S.pRem},
  r:()=>{const rem=pomoLeft(),s=Math.ceil(rem/1000);return `${ring((1-rem/POMO)*100+1,'#ff8a8a')}<div class="t"><span class="v">${pad(Math.floor(s/60))}:${pad(s%60)}</span><span class="l">${S.pEnd?'Đang tập trung':'Bấm để bắt đầu'}</span></div>`}},
 {id:'todo',g:'Năng suất',name:'Việc cần làm',on:false,
  r:()=>`${chip('✓','#34c58a')}<div class="t"><span class="v">${todosLeft()} còn lại</span><span class="l">${CFG.todos.length-todosLeft()}/${CFG.todos.length} xong</span></div>`,
  p:()=>`<h4>Việc cần làm</h4>`+CFG.todos.map((t,i)=>`<div class="todo ${t.d?'d':''}"><i data-act="todoTog" data-arg="${i}">${t.d?'✓':''}</i><span data-act="todoTog" data-arg="${i}">${esc(t.t)}</span><button data-act="todoDel" data-arg="${i}">✕</button></div>`).join('')+`<div class="todo"><input class="inp" id="todoIn" placeholder="Thêm việc… (Enter)" autocomplete="off"></div>`},
 {id:'scr',g:'Năng suất',name:'Screen Time',on:true,prov:'scr',
  r:()=>`${chip('◐','#6a8cff')}<div class="t"><span class="v">${hmShort(DATA.scr.today)}</span><span class="l">Screen Time hôm nay</span></div>`,
  p:()=>popC('Screen Time',hmShort(DATA.scr.today),null,[...DATA.scr.top.map(a=>[appName(a.p),hmShort(a.s)]),['7 ngày qua',hmShort(DATA.scr.week)]],'<div class="note">Cộng dồn thời gian cửa sổ đang dùng (bỏ qua lúc không chạm máy &gt; 2 phút), tính từ lúc Aurora Dock chạy.</div>')},
 {id:'sys',g:'Hệ thống',name:'CPU / RAM',on:true,prov:'sys',
  r:()=>{const s=DATA.sys;return `<div class="mini">${bar('CPU',s.cpu)}${bar('RAM',s.ram)}</div><div class="t"><span class="v">${Math.round(s.cpu)}%</span><span class="l">CPU · RAM ${Math.round(s.ram)}%</span></div>`},
  p:()=>{const s=DATA.sys;return popC('Hệ thống',Math.round(s.cpu)+'% CPU','cpu',[['RAM đang dùng',fmtBytes(s.ramTotal-s.ramFree)+' / '+fmtBytes(s.ramTotal)],['RAM',Math.round(s.ram)+'%'],['Tiến trình',s.procs]])}},
 {id:'bat',g:'Hệ thống',name:'Pin',on:true,prov:'sys',hw:()=>DATA.sys&&DATA.sys.bat,
  r:()=>{const b=DATA.sys.bat;return `${ring(b.pct,b.charging?'#7dffb2':'#ffd166')}<div class="t"><span class="v">${Math.round(b.pct)}%</span><span class="l">${b.charging?'Đang sạc':b.secs>0?'Còn '+hmShort(b.secs):'Pin'}</span></div>`},
  p:()=>{const b=DATA.sys.bat;return popC('Pin',Math.round(b.pct)+'%',null,[['Trạng thái',b.charging?'Đang sạc / cắm điện':'Dùng pin'],['Còn lại',b.secs>0?hmShort(b.secs):'—']])}},
 {id:'net',g:'Hệ thống',name:'Mạng',on:true,prov:'sys',
  r:()=>{const n=DATA.sys.net;return `${chip('⇅','linear-gradient(135deg,#2dd4ff,#5b8cff)')}<div class="t"><span class="v">${n.up?n.down.toFixed(1)+' Mb/s':'Mất mạng'}</span><span class="l">${n.up?(n.kind||'Mạng')+' · '+(n.ping>=0?n.ping+'ms':'—'):'Không có kết nối'}</span></div>`},
  p:()=>{const n=DATA.sys.net;return popC('Mạng',n.up?n.down.toFixed(1)+' Mb/s':'Mất mạng','net',[['Kết nối',n.kind||'—'],['Tải xuống',n.down.toFixed(2)+' Mb/s'],['Tải lên',n.upl.toFixed(2)+' Mb/s'],['Ping 1.1.1.1',n.ping>=0?n.ping+' ms':'không trả lời']])}},
 {id:'disk',g:'Hệ thống',name:'Ổ đĩa',on:false,prov:'sys',
  r:()=>{const d=DATA.sys.disks[0];if(!d)return `${chip('▤',GREY)}<div class="t"><span class="v">—</span><span class="l">Ổ đĩa</span></div>`;const u=(d.total-d.free)/d.total*100;return `${ring(u,'#ffd166')}<div class="t"><span class="v">${Math.round(u)}%</span><span class="l">${esc(d.name)} đã dùng</span></div>`},
  p:()=>popC('Lưu trữ',DATA.sys.disks[0]?Math.round((DATA.sys.disks[0].total-DATA.sys.disks[0].free)/DATA.sys.disks[0].total*100)+'%':'—',null,DATA.sys.disks.map(d=>[d.name,fmtBytes(d.total-d.free)+' / '+fmtBytes(d.total)]))},
 {id:'mus',g:'Nhạc',name:'Nhạc',on:true,prov:'media',hw:()=>DATA.media&&!DATA.media.none,
  r:()=>{const m=DATA.media;return `<div class="eq ${m.playing?'':'paused'}"><i></i><i></i><i></i></div><div class="t"><span class="v" style="max-width:130px;overflow:hidden;text-overflow:ellipsis">${esc(m.title||'—')}</span><span class="l">${esc(m.artist||m.app||'')}</span></div>`},
  p:()=>{const m=DATA.media;return popC('Đang phát',m.title||'—',null,[['Nghệ sĩ',m.artist||'—'],['Album',m.album||'—'],['Từ',baseName(m.app)||'—']])+`<div class="btns">${btn('⏮','media','prev')}${btn(m.playing?'⏸ Tạm dừng':'▶ Phát','media','toggle')}${btn('⏭','media','next')}</div>`}},
 {id:'wea',g:'Thời tiết',name:'Thời tiết',on:false,prov:'wea',cfg:'city',ic:'☀',
  r:()=>{const w=DATA.wea,c=WMO(w.weather_code);return `${chip(c[0],'linear-gradient(135deg,#ffc94a,#ff9a3d)')}<div class="t"><span class="v">${Math.round(w.temperature_2m)}°C</span><span class="l">${esc(w.name)} · ${c[1]}</span></div>`},
  p:()=>{const w=DATA.wea;return popC(w.name,Math.round(w.temperature_2m)+'°C',null,[['Cảm giác',Math.round(w.apparent_temperature)+'°C'],['Độ ẩm',w.relative_humidity_2m+'%'],['Gió',Math.round(w.wind_speed_10m)+' km/h'],['Khả năng mưa',w.rain!=null?w.rain+'%':'—']],'<div class="note">Nguồn: Open-Meteo</div>')}},
 {id:'clk',g:'Thời gian',name:'Đồng hồ',on:true,
  r:()=>{const d=new Date();return `<div class="t"><span class="v">${d.toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit'})}</span><span class="l">${d.toLocaleDateString('vi-VN',{weekday:'short',day:'2-digit',month:'2-digit'})}</span></div>`},
  p:()=>popC('Giờ thế giới',new Date().toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit'}),null,[['Tokyo','Asia/Tokyo'],['London','Europe/London'],['New York','America/New_York'],['Los Angeles','America/Los_Angeles']].map(([n,z])=>[n,new Date().toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit',timeZone:z})]))},
 {id:'dl',g:'Tệp',name:'Downloads',on:false,prov:'files',hw:()=>DATA.files&&DATA.files.dl,
  r:()=>{const f=DATA.files.dl;return `${chip('⇩','#4fa3ff')}<div class="t"><span class="v">${f.count} tệp</span><span class="l">Downloads · ${fmtBytes(f.size)}</span></div>`},
  p:()=>{const f=DATA.files.dl;return popC('Downloads',f.count+' tệp',null,f.recent.map(x=>[hhmm(x.t),x.n]))+`<div class="btns">${btn('Mở thư mục','launch',f.path)}</div>`}},
 {id:'bin',g:'Tệp',name:'Thùng rác',on:false,prov:'files',hw:()=>DATA.files&&DATA.files.bin,
  r:()=>{const b=DATA.files.bin;return `${chip('🗑','#8a93a8')}<div class="t"><span class="v">${fmtBytes(b.size)}</span><span class="l">Thùng rác · ${b.count} mục</span></div>`},
  p:()=>{const b=DATA.files.bin;return popC('Thùng rác',fmtBytes(b.size),null,[['Số mục',b.count]])+`<div class="btns">${btn('Mở Thùng rác','launch','shell:RecycleBinFolder')}</div>`}},
 {id:'srch',g:'Tệp',name:'Tìm kiếm',on:false,click:()=>host('key',{k:'search'}).catch(()=>{}),r:()=>`${chip('⌕','#7b61ff')}<div class="t"><span class="v">Tìm kiếm</span><span class="l">Windows Search</span></div>`}
];
W.forEach(w=>{if(w.id in CFG.wOn)w.on=CFG.wOn[w.id]});

const ACT={
 win:h=>host('activate',{h:+h}).then(()=>{closePop();setTimeout(()=>pump('wins'),200)}),
 closeWin:h=>host('close',{h:+h}).then(()=>setTimeout(()=>pump('wins'),500)),
 launch:p=>host('launch',{target:p}).catch(e=>toast('Không mở được: '+e.message)),
 pin:p=>{CFG.pins=(CFG.pins||[]).concat(p);saveCfg();tick();closePop()},
 unpin:p=>{CFG.pins=(CFG.pins||[]).filter(x=>x!==p);saveCfg();tick();closePop()},
 openData:s=>openPanel(s||'#secData'),
 retry:k=>{pump(k)},
 media:c=>host('mediaCtl',{cmd:c}).then(()=>setTimeout(()=>pump('media'),400)).catch(e=>toast(e.message)),
 shift:n=>doAction(n),newShift:()=>newShift(),
 todoTog:i=>{const t=CFG.todos[+i];if(t){t.d=t.d?0:1;saveCfg();tick()}},
 todoDel:i=>{CFG.todos.splice(+i,1);saveCfg();tick()}};

/* ---------- hook statusLine c?a Claude Code ---------- */
function refreshChookUi(){const el=$('#st-chook');if(!el)return;const h=DATA.chook,r=DATA.crate;
 el.textContent=!h?'�':!h.exeFound?'? thi?u aurora-statusline.exe (c�i d?t l?i)':h.installed?(r?'? d� c�i � nh?n d? li?u l�c '+hhmm(r.at)+(r.fiveHour?'':' (chua c� rate_limits: ch? c� v?i g�i Pro/Max/Ultra, sau ph?n h?i d?u ti�n)'):'? d� c�i � chua nh?n d? li?u (m? m?t phi�n Claude Code; extension VS Code c� th? kh�ng ch?y statusLine)'):(h.hasOther?'chua c�i � b?n dang c� statusLine ri�ng (s? du?c gi? ch?y ti?p)':'chua c�i')}
async function loadChook(){try{DATA.chook=await host('claudeHook',{action:'status'})}catch(e){DATA.chook=null}refreshChookUi()}
Object.assign(ACT,{chookInstall:()=>{host('claudeHook',{action:'install'}).then(s=>{DATA.chook=s;refreshChookUi();toast('? �� c�i hook Claude Code � d? li?u s? c� sau tin nh?n Claude ti?p theo');closePop()}).catch(e=>toast('L?i: '+e.message))}});
$('#ch-install').onclick=()=>ACT.chookInstall();
$('#ch-remove').onclick=()=>host('claudeHook',{action:'remove'}).then(s=>{DATA.chook=s;DATA.crate=null;refreshChookUi();toast('�� g? hook Claude Code (kh�i ph?c c?u h�nh cu)')}).catch(e=>toast('L?i: '+e.message));
setTimeout(loadChook,1500);setInterval(refreshChookUi,15000);
