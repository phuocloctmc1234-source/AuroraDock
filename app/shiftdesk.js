'use strict';
/* Shift Desk (hệ thống chấm công/break của công ty): đọc trạng thái trên trang thật qua host và bấm đúng nút
   “Bắt đầu nghỉ” / “Kết thúc nghỉ”. Giờ nghỉ lấy từ chính trang (giờ VN, khớp “Lịch cả team”).
   Tự kết thúc nghỉ: TẮT mặc định, có đếm ngược + nút Hủy, đọc lại trang ngay trước khi bấm. */
const T2=s=>{const m=String(s||'').match(/(\d{1,2}):(\d{2})/);return m?pad(+m[1])+':'+m[2]:''};
const RE_RANGE=/(\d{1,2}:\d{2})\s*[–\-—]\s*(\d{1,2}:\d{2})/;
function parseSd(r){
 const t=r.text||'',btns=(r.buttons||[]).map(x=>String(x).trim()),lines=t.split(/\n+/).map(s=>s.trim()).filter(Boolean);
 const o={at:r.at||Date.now(),raw:t,buttons:btns,found:!!r.found,diag:r.diag||[],topUrl:String(r.url||'').replace(/[?#].*$/,'').slice(0,90)};
 o.canStart=btns.some(b=>/^bắt đầu nghỉ$/i.test(b));o.canEnd=btns.some(b=>/^kết thúc nghỉ$/i.test(b));
 const i0=Math.max(0,lines.findIndex(l=>/break của tôi/i.test(l)));
 o.status=(lines.slice(i0,i0+8).find(l=>/^\p{Lu}[\p{Lu}\s]{3,30}$/u.test(l))||'');
 let m=t.match(/Ca\s+(C\d)\s+(\d{1,2}:\d{2})\s*[–\-—]\s*(\d{1,2}:\d{2})/);o.shift=m?{code:m[1],from:T2(m[2]),to:T2(m[3])}:null;
 m=t.match(/Giờ nghỉ của bạn[^\n]*\n+\s*(\d{1,2}:\d{2})\s*[–\-—]\s*(\d{1,2}:\d{2})/i)||t.match(/Giờ nghỉ của bạn[\s\S]{0,60}?(\d{1,2}:\d{2})\s*[–\-—]\s*(\d{1,2}:\d{2})/i);o.win=m?{from:T2(m[1]),to:T2(m[2])}:null;
 if(!o.win){m=t.match(/Đợt nghỉ tiếp theo\s*(\d{1,2}:\d{2})\s*[–\-—]\s*(\d{1,2}:\d{2})/i);if(m)o.win={from:T2(m[1]),to:T2(m[2])}}
 m=t.match(/(?:Đã|Đang) nghỉ từ\s*(\d{1,2}:\d{2})/i);o.since=m?T2(m[1]):'';
 m=t.match(/Kế hoạch kết thúc\s*(\d{1,2}:\d{2})/i);o.planEnd=m?T2(m[1]):'';
 m=t.match(/Đã nghỉ hôm nay\s*(\d+)\s*\/\s*(\d+)\s*phút/i);o.used=m?{min:+m[1],max:+m[2]}:null;
 m=t.match(/Bắt đầu ca:\s*(\d{1,2}:\d{2})\s*([^\n(]*)/i);o.started=m?{at:T2(m[1]),note:(m[2]||'').trim()}:null;
 o.state=(o.canEnd||/ĐANG NGHỈ/.test(o.status))?'break':(o.canStart||/ĐẾN GIỜ NGHỈ/.test(o.status))?'due':/ĐANG LÀM VIỆC/.test(o.status)?'work':/HẾT CA|KẾT THÚC CA|XONG CA/.test(o.status)?'done':'unknown';
 if(o.state==='break'&&!o.planEnd&&o.win)o.planEnd=o.win.to;
 o.tzWarn=new Date().getTimezoneOffset()!==-420;      // giờ trên trang là giờ VN
 return o}
const sdSinceTs=s=>{const n=Date.now();let t=occ(s,n);if(t>n+6e4)t-=864e5;return t};
const sdEndTs=o=>o.planEnd?occAfter(o.planEnd,o.since?sdSinceTs(o.since):Date.now()-36e5):null;
const sdNear=()=>{const o=DATA.sd;if(!o)return false;if(o.state==='due')return true;if(o.state==='break'){const e=sdEndTs(o);return e&&e-Date.now()<300000}return false};

async function sdRefresh(maxAge){
 const r=await host('sdRead',{url:CFG.sdUrl,maxAge:maxAge==null?50:maxAge},90000);
 if(r.needLogin){DATA.sd=null;throw new Error('Cần đăng nhập Shift Desk (⚙ > Shift Desk > Mở Shift Desk)')}
 const o=parseSd(r);if(!r.found)o.state='unknown';DATA.sd=o;refreshSdUi();return o}
const pSd=()=>sdRefresh(sdNear()?8:50);
Object.assign(PROV,{sd:{every:()=>sdNear()?10:30,run:pSd,cfg:'sd'}});

async function sdDo(what,opt){opt=opt||{};const lab=what==='endBreak'?'Kết thúc nghỉ':'Bắt đầu nghỉ';
 try{await sdRefresh(0);const o=DATA.sd;
  if(what==='endBreak'&&!o.canEnd)throw new Error('Shift Desk không ở trạng thái đang nghỉ');
  if(what==='startBreak'&&!o.canStart)throw new Error('Shift Desk chưa cho bắt đầu nghỉ');
  await host('sdClick',{url:CFG.sdUrl,what},60000);
  log('sd','Shift Desk: '+lab+(opt.auto?' (tự động'+(opt.note?' · '+opt.note:'')+')':' (thủ công)'),'ok');toast((opt.auto?'🤖 ':'')+'Shift Desk: '+lab);
  setTimeout(()=>sdRefresh(0).then(tick).catch(()=>{}),2500);return true}
 catch(e){log('sd','Shift Desk: '+lab+' THẤT BẠI — '+e.message,'err');toast('⚠ '+lab+' thất bại: '+e.message);return false}}

/* tự kết thúc nghỉ: chỉ khi bật, dữ liệu mới (<90s), đang nghỉ, đã gần giờ kết thúc kế hoạch */
function sdDue(){const o=DATA.sd;if(!CFG.sdAuto||!o||o.state!=='break'||!o.planEnd||!have('sd'))return null;
 if(Date.now()-o.at>90000)return null;
 const end=sdEndTs(o),n=Date.now();if(n<end-CFG.sdLead*6e4)return null;if(n>end+60*6e4)return {late:true};
 return {key:'sd-end-'+dayKeyOf(sdSinceTs(o.since||o.planEnd))+'-'+(o.since||''),over:n-end}}

/* ---------- widget ---------- */
const SDG='linear-gradient(135deg,#34c58a,#12a0b8)',SDO='linear-gradient(135deg,#ffb020,#ff7a59)';
const sdDot=()=>{const o=DATA.sd,age=o?Date.now()-o.at:1e9;return `<i class="sd ${age<120000?'ok':'off'}"></i>`};
const sdWidget={id:'sd',g:'Chấm công & Giấc ngủ',name:'Shift Desk · Break',on:false,prov:'sd',cfg:'sd',ic:'☕',sec:'#secSd',errAct:['Mở Shift Desk','sdLogin'],
 r:()=>{const o=DATA.sd,now=Date.now(),d=sdDot(),win=o.win?o.win.from+'–'+o.win.to:'';
  if(o.state==='break'){const e=sdEndTs(o),left=e?hmShort((e-now)/1000):'';
   return `${chip('☕',SDO)}<div class="t"><span class="v">${dur(now-(o.since?sdSinceTs(o.since):now))}</span><span class="l">Nghỉ · ${o.planEnd?'kết thúc '+o.planEnd+(CFG.sdAuto?' (tự động '+hhmm(e-CFG.sdLead*6e4)+')':''):'đang nghỉ'}${left&&e>now?' · còn '+left:''}</span></div>${d}`}
  if(o.state==='due'){const st=o.win?nextAt(o.win.from):0,pre=st&&st-now<864e5/2&&occNear(o.win.from)>now;
   return `${chip('☕',SDO)}<div class="t"><span class="v">${pre?dur(occNear(o.win.from)-now):'Đến giờ nghỉ'}</span><span class="l">${pre?'đến giờ nghỉ '+o.win.from:'Bấm để bắt đầu · '+win}</span></div>${d}`}
  if(o.state==='work'){const st=o.win?nextAt(o.win.from):0;return `${chip('⏱',SDG)}<div class="t"><span class="v">Đang làm</span><span class="l">${win?'nghỉ '+win+(st?' · sau '+hmShort((st-now)/1000):''):'Shift Desk'}</span></div>${d}`}
  if(o.state==='done')return `${chip('✔','#34c58a')}<div class="t"><span class="v">Hết ca</span><span class="l">Shift Desk</span></div>${d}`;
  return `${chip('?',GREY)}<div class="t"><span class="v">Không nhận ra trang</span><span class="l">Shift Desk · xem ⚙</span></div>${d}`},
 p:()=>{const o=DATA.sd,now=Date.now(),st={work:'Đang làm việc',due:'Đến giờ nghỉ',break:'Đang nghỉ',done:'Hết ca',unknown:'Không nhận ra trang'}[o.state];
  const rows=[['Trạng thái',st],['Ca',o.shift?o.shift.code+' '+o.shift.from+'–'+o.shift.to:'—'],['Giờ nghỉ của bạn',o.win?o.win.from+' – '+o.win.to:'—'],
   ['Đã nghỉ hôm nay',o.used?o.used.min+' / '+o.used.max+' phút':'—'],['Bắt đầu ca',o.started?o.started.at+(o.started.note?' · '+o.started.note:''):'—'],['Cập nhật',hhmm(o.at)]];
  if(o.state==='break'&&o.planEnd)rows.splice(2,0,['Kế hoạch kết thúc',o.planEnd+(CFG.sdAuto?' · tự động '+hhmm(sdEndTs(o)-CFG.sdLead*6e4):' · tự động đang TẮT')]);
  return popC('Shift Desk · Break',st,null,rows,
   `${o.tzWarn?'<div class="note">⚠ Máy bạn không ở múi giờ VN (UTC+7) nhưng giờ trên Shift Desk là giờ VN — giờ tự động sẽ lệch.</div>':''}${o.state==='unknown'?`<div class="note">Không nhận ra trang. Đang ở: ${esc(o.topUrl)}<br>${o.diag.map(esc).join('<br>')||'không đọc được khung nào'}<br>Mở ⚙ > Shift Desk > “Mở Shift Desk” để xem cửa sổ, hoặc “Sao chép chẩn đoán” rồi gửi mình.</div>`:''}`
   +`<div class="btns">${o.canStart?btn('☕ Bắt đầu nghỉ','sdStart'):''}${o.canEnd?btn('Kết thúc nghỉ','sdEnd'):''}${btn('Làm mới','sdRefresh')}${btn('Mở Shift Desk','sdLogin')}</div>`)}};
W.splice(Math.max(0,W.findIndex(w=>w.id==='wk'))+1,0,sdWidget);
if('sd' in CFG.wOn)sdWidget.on=CFG.wOn.sd;
Object.assign(ACT,{
 sdStart:()=>{closePop();sdDo('startBreak')},sdEnd:()=>{closePop();sdDo('endBreak')},
 sdRefresh:()=>{sdRefresh(0).then(()=>tick()).catch(e=>{ERR.sd=e.message;toast(e.message);tick()})},
 sdLogin:()=>host('sdLogin',{url:CFG.sdUrl},60000).then(()=>toast('Đã mở cửa sổ Shift Desk — đăng nhập xong thì đóng cửa sổ đó (app vẫn giữ phiên)')).catch(e=>toast('Lỗi: '+e.message))});

/* ---------- cài đặt ---------- */
function sdDiag(){const o=DATA.sd;if(!o)return 'Chưa có dữ liệu Shift Desk'+(ERR.sd?' — lỗi: '+ERR.sd:'');
 const t=o.raw||'',i=t.indexOf('Lịch cả team'),copy=Object.assign({},o);delete copy.raw;
 return JSON.stringify({parsed:copy,diag:o.diag,topUrl:o.topUrl,textBeforeTeamTable:i>0?t.slice(0,i):t.slice(0,900)},null,1)}   // cố ý cắt bỏ bảng lịch (có tên đồng nghiệp)
function refreshSdUi(){const el=$('#st-sd');if(!el)return;const o=DATA.sd;
 el.textContent=!CFG.sdUrl?'chưa nhập URL':ERR.sd&&!o?'✗ '+ERR.sd:o?(o.state==='unknown'?'✗ không nhận ra trang':'✓ '+({work:'đang làm việc',due:'đến giờ nghỉ',break:'đang nghỉ',done:'hết ca'}[o.state])+' · '+hhmm(o.at)):'…'}
$('#sd-login').onclick=()=>{if(!CFG.sdUrl){toast('Hãy dán URL Shift Desk trước');return}ACT.sdLogin()};
$('#sd-hide').onclick=()=>host('sdHide').catch(()=>{});
$('#sd-read').onclick=()=>{if(!CFG.sdUrl){toast('Hãy dán URL Shift Desk trước');return}$('#st-sd').textContent='… đang đọc';sdRefresh(0).then(()=>{tick();toast('Đã đọc Shift Desk')}).catch(e=>{ERR.sd=e.message;refreshSdUi();toast(e.message)})};
$('#sd-diag').onclick=()=>{try{navigator.clipboard.writeText(sdDiag()).then(()=>toast('Đã sao chép chẩn đoán (không gồm tên đồng nghiệp)'),()=>toast('Không sao chép được'))}catch(e){toast('Không sao chép được')}};
