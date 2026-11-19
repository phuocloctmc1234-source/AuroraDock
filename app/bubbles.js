'use strict';
/* Lark: lấy @mention THẬT (qua host) và hiện thành bong bóng chat nổi như chat head của Messenger.
   Bong bóng chỉ xuất hiện khi có @mention chưa xem. Bấm = mở thẻ nội dung, kéo = đổi chỗ và tự hít vào mép màn hình. */
const LKG='linear-gradient(135deg,#00d6b9,#3370ff)';
const LSEEN=new Set(store.get('lark.seen',[]));
const saveSeen=()=>store.set('lark.seen',[...LSEEN].slice(-600));
let lastIds=null,openChat=null;
const larkOn=()=>{const w=W.find(x=>x.id==='lark');return !!(w&&w.on)};
const notifOn=()=>{const w=W.find(x=>x.id==='notif');return !!(w&&w.on)};
/* nguồn bong bóng: (1) @mention Lark qua API, (2) thông báo THẬT của Windows (Messenger/Instagram qua trình duyệt, Lark, Zalo…) */
const larkLive=()=>larkOn()&&have('lark')&&DATA.lark;
const itemsLark=()=>larkLive()?DATA.lark.items.map(i=>Object.assign({src:'lark',appName:'Lark'},i)):[];
const NOTIF_RE=/messenger|instagram|facebook|whatsapp|telegram|zalo|lark|feishu|slack|discord|teams|skype|viber|signal|wechat|gmail|outlook|mail|sms|phone link/i;
const nKey=i=>i.site?'site:'+String(i.site).toLowerCase():'app:'+String(i.aumid).toLowerCase();
const nEnabled=i=>{const k=nKey(i);if(k in CFG.nApps)return !!CFG.nApps[k];return NOTIF_RE.test((i.site||'')+' '+i.app+' '+i.aumid)};
const itemsWin=()=>{if(!notifOn()||!DATA.notif)return [];const lim=Date.now()-3*36e5;
 return DATA.notif.items.filter(i=>i.t>lim&&nEnabled(i)&&!(larkLive()&&i.app==='Lark')).map(i=>({id:i.id,src:'win',chat:'w:'+nKey(i)+'|'+i.title,chatName:i.title,sender:i.title,text:i.text,t:i.t,appName:i.site||i.app,
  url:i.site?'https://'+i.site:'shell:AppsFolder\\'+i.aumid}))};
const allItems=()=>itemsLark().concat(itemsWin()).sort((a,b)=>b.t-a.t);
const unseen=()=>allItems().filter(i=>!LSEEN.has(i.id));
const unseenLark=()=>itemsLark().filter(i=>!LSEEN.has(i.id));
const unseenWin=()=>itemsWin().filter(i=>!LSEEN.has(i.id));
const ago=t=>{const m=Math.round((Date.now()-t)/6e4);return m<1?'vừa xong':m<60?m+' phút trước':m<1440?Math.round(m/60)+' giờ trước':Math.round(m/1440)+' ngày trước'};
const hue=s=>{let h=0;for(const c of String(s))h=(h*31+c.charCodeAt(0))%360;return h};

/* ---------- widget trên dock ---------- */
const larkWidget={id:'lark',g:'Lark',name:'Lark · @mention',on:true,prov:'lark',cfg:'lark',ic:'Lk',sec:'#secLark',
 r:()=>{const n=unseenLark().length,me=DATA.lark.me;return `${chip('Lk',LKG)}<div class="t"><span class="v">${n?n+' @mention':'Không có @mention'}</span><span class="l">Lark · ${esc((me&&me.name)||'đã kết nối')}</span></div>`},
 p:()=>{const all=DATA.lark.items,n=unseenLark().length;
  return popC('Lark · @mention',n?n+' chưa xem':'Không có gì mới',null,all.slice(0,6).map(i=>[(LSEEN.has(i.id)?'✓ ':'● ')+i.sender+' · '+ago(i.t),i.text||'…']),
   `${DATA.lark.note?`<div class="note">${esc(DATA.lark.note)}</div>`:''}${ERR.lark?`<div class="note">Lần cập nhật gần nhất lỗi: ${esc(ERR.lark)}</div>`:''}<div class="btns">${n?btn('Đánh dấu đã xem hết','larkAll'):''}${btn('Cài đặt Lark','openData','#secLark')}</div>`)}};
W.splice(Math.max(0,W.findIndex(w=>w.id==='claude'))+1,0,larkWidget);
if('lark' in CFG.wOn)larkWidget.on=CFG.wOn.lark;
Object.assign(PROV,{lark:{every:30,run:pLark}});
Object.assign(ACT,{larkAll:()=>{unseen().forEach(i=>LSEEN.add(i.id));saveSeen();closePop();renderBubbles();tick()}});

async function pLark(){
 const st=await host('larkStatus');DATA.larkStatus=st;refreshLarkUi();
 if(!st.loggedIn){DATA.lark=null;renderBubbles();return}
 try{const r=await host('larkMentions',{hours:24},90000);DATA.lark=r;onLarkData(r)}
 catch(e){if(/CHUA_DANG_NHAP/.test(e.message)){DATA.larkStatus.loggedIn=false;DATA.lark=null;ERR.lark='';refreshLarkUi();renderBubbles();return}throw e}}
function onLarkData(r){
 const ids=new Set(r.items.map(i=>i.id));
 if(lastIds){const fresh=r.items.filter(i=>!lastIds.has(i.id)&&!LSEEN.has(i.id));
  fresh.slice(0,3).forEach(i=>toast('💬 '+i.sender+' @bạn'+(i.chatName?' · '+i.chatName:'')+': '+(i.text||'').slice(0,70)));
  fresh.forEach(i=>NEWCHAT.add(i.chat))}
 lastIds=ids;renderBubbles();tick()}
const NEWCHAT=new Set();

/* ---------- bong bóng ---------- */
const bubbles=$('#bubbles'),bcard=$('#bcard');
function larkGroups(){const m=new Map();unseen().forEach(i=>{let g=m.get(i.chat);if(!g){g={chat:i.chat,chatName:i.chatName,items:[],url:i.url,app:i.appName};m.set(i.chat,g)}g.items.push(i)});
 return [...m.values()].sort((a,b)=>b.items[0].t-a.items[0].t)}
const groupsL=larkGroups;   // KHÔNG đặt tên "groups": trùng với groups() của thanh tác vụ (widgets.js)
let bubSig='';
function placeStack(){const b=CFG.bub||{side:'right',y:Math.round(innerHeight*.38)};CFG.bub=b;
 bubbles.classList.toggle('left',b.side==='left');bubbles.style.top=clamp(b.y,10,innerHeight-160)+'px';bubbles.style.left=b.side==='left'?'10px':'auto';bubbles.style.right=b.side==='left'?'auto':'10px'}
function renderBubbles(){
 const gs=groupsL();
 const sig=gs.map(g=>g.chat+':'+g.items.length+':'+(NEWCHAT.has(g.chat)?1:0)).join('|');
 if(sig===bubSig)return;
 const had=new Set([...bubbles.querySelectorAll('.bubble[data-chat]')].map(e=>e.dataset.chat));bubSig=sig;
 const vis=gs.slice(0,6),extra=gs.length-vis.length;
 bubbles.innerHTML=vis.map(g=>{const h=hue(g.chat),lab=(g.chatName||g.items[0].sender||'?').trim()[0]||'?';
  return `<div class="bubble ${!had.has(g.chat)||NEWCHAT.has(g.chat)?'new':''}" data-chat="${esc(g.chat)}" style="--c1:hsl(${h} 75% 58%);--c2:hsl(${(h+50)%360} 70% 45%)" title="${esc(g.chatName||g.items[0].sender)}"><span>${esc(lab.toUpperCase())}</span>${g.items.length>1?`<i class="bn">${g.items.length}</i>`:''}</div>`}).join('')
  +(extra>0?`<div class="bubble more" data-more="1" title="Còn ${extra} cuộc trò chuyện nữa">+${extra}</div>`:'');
 NEWCHAT.clear();placeStack();
 if(openChat&&!gs.find(g=>g.chat===openChat))closeCard();
 kick(900)}
function bubbleOf(chat){return [...bubbles.querySelectorAll('.bubble[data-chat]')].find(e=>e.dataset.chat===chat)}
function openCardFor(chat){const g=groupsL().find(x=>x.chat===chat),el=bubbleOf(chat);if(!g||!el)return;
 openChat=chat;
 bcard.innerHTML=`<div class="bh"><b>${esc(g.chatName||g.items[0].sender)}</b><span>${g.items.length>1?g.items.length+' tin · ':''}${esc(g.app||'')}</span></div>`
  +g.items.slice(0,4).map(i=>`<div class="bm"><small><span>${esc(i.sender)}</span><span>${ago(i.t)}</span></small><p>${esc(i.text||'…')}</p></div>`).join('')
  +(g.items.length>4?`<div class="note" style="font-size:11px;opacity:.6">và ${g.items.length-4} tin nữa…</div>`:'')
  +`<div class="bf"><button class="btn" data-bact="open">Mở trong Lark</button><button class="btn" data-bact="seen">Đã xem</button></div>`;
 const r=el.getBoundingClientRect(),left=bubbles.classList.contains('left');
 bcard.style.left=(left?r.right+12:Math.max(10,r.left-12-310))+'px';
 bcard.classList.add('show');
 const h=bcard.offsetHeight;bcard.style.top=clamp(r.top+r.height/2-h/2,10,innerHeight-h-90)+'px';kick(700)}
function closeCard(){openChat=null;bcard.classList.remove('show');kick(500)}
function markGroupSeen(chat){const g=groupsL().find(x=>x.chat===chat);if(!g)return;g.items.forEach(i=>LSEEN.add(i.id));saveSeen();
 const el=bubbleOf(chat);closeCard();if(el){el.classList.add('out');setTimeout(()=>{renderBubbles();tick()},230)}else{renderBubbles();tick()}}
bcard.addEventListener('click',e=>{const b=e.target.closest('[data-bact]');if(!b||!openChat)return;const chat=openChat,g=groupsL().find(x=>x.chat===chat);
 if(b.dataset.bact==='open'&&g)host('launch',{target:g.url}).catch(err=>toast('Không mở được Lark: '+err.message));
 markGroupSeen(chat)});
document.addEventListener('pointerdown',e=>{if(!e.target.closest('.bubble')&&!e.target.closest('#bcard'))closeCard()},true);

/* kéo bong bóng: đi theo con trỏ, thả ra thì hít vào mép trái/phải gần nhất */
let drag=null;
bubbles.addEventListener('pointerdown',e=>{const b=e.target.closest('.bubble');if(!b)return;const r=bubbles.getBoundingClientRect();
 drag={b,sx:e.clientX,sy:e.clientY,ox:e.clientX-r.left,oy:e.clientY-r.top,moved:false};b.setPointerCapture(e.pointerId)});
bubbles.addEventListener('pointermove',e=>{if(!drag)return;
 if(!drag.moved&&Math.hypot(e.clientX-drag.sx,e.clientY-drag.sy)>6){drag.moved=true;window.DRAG=true;closeCard();bubbles.classList.remove('snap')}
 if(drag.moved){bubbles.style.left=clamp(e.clientX-drag.ox,0,innerWidth-70)+'px';bubbles.style.right='auto';bubbles.style.top=clamp(e.clientY-drag.oy,10,innerHeight-160)+'px';kick(300)}});
bubbles.addEventListener('pointerup',e=>{if(!drag)return;const d=drag;drag=null;
 if(d.moved){window.DRAG=false;const left=e.clientX<innerWidth/2;CFG.bub={side:left?'left':'right',y:parseInt(bubbles.style.top)||0};saveCfg();bubbles.classList.add('snap');placeStack();setTimeout(()=>bubbles.classList.remove('snap'),400);kick(700);return}
 if(d.b.dataset.more){const w=track.querySelector('[data-id="lark"]');if(w)w.click();return}
 const chat=d.b.dataset.chat;if(openChat===chat)closeCard();else openCardFor(chat)});
bubbles.addEventListener('pointercancel',()=>{drag=null;window.DRAG=false;kick(300)});
addEventListener('resize',()=>{placeStack();kick(500)});

/* ---------- cài đặt Lark ---------- */
let lkDomain='lark';
function refreshLarkUi(){const s=DATA.larkStatus;if(!s)return;lkDomain=s.domain;
 document.querySelectorAll('#segLk button').forEach(x=>x.classList.toggle('on',x.dataset.v===lkDomain));
 const id=$('#lk-id'),sc=$('#lk-scopes'),sec=$('#lk-secret');
 if(document.activeElement!==id&&!id.value)id.value=s.appId||'';if(document.activeElement!==sc&&!sc.value)sc.value=s.scopes||'';
 sec.placeholder=s.hasSecret?'App Secret (đã lưu — để trống nếu không đổi)':'App Secret';$('#lk-redirect').value=s.redirect||'';
 $('#st-lark').textContent=s.loggedIn?'✓ '+(s.name||'đã đăng nhập'):s.waiting?'… đang chờ bạn đăng nhập trong trình duyệt':s.configured?'đã lưu cấu hình, chưa đăng nhập':'chưa cấu hình';
 if(s.error)$('#st-lark').textContent+=' · ✗ '+s.error;
 const w=W.find(x=>x.id==='lark');if(w)tick()}
$('#segLk').onclick=e=>{const b=e.target.closest('button');if(!b)return;lkDomain=b.dataset.v;document.querySelectorAll('#segLk button').forEach(x=>x.classList.toggle('on',x===b))};
$('#lk-redirect').onclick=e=>{e.target.select();try{navigator.clipboard.writeText(e.target.value);toast('Đã chép địa chỉ Redirect URL')}catch(_){}};
async function larkSave(){await host('larkConfig',{domain:lkDomain,appId:$('#lk-id').value,appSecret:$('#lk-secret').value,scopes:$('#lk-scopes').value});$('#lk-secret').value='';
 DATA.larkStatus=await host('larkStatus');refreshLarkUi()}
$('#lk-save').onclick=async()=>{try{await larkSave();toast('✓ Đã lưu cấu hình Lark (mã hóa)')}catch(e){toast('Lỗi: '+e.message)}};
$('#lk-login').onclick=async()=>{try{await larkSave();await host('larkLogin',{});toast('Đã mở trình duyệt — hãy đăng nhập và cho phép trên trang Lark');
 for(let i=0;i<150;i++){await sleep(2000);const s=await host('larkStatus');DATA.larkStatus=s;refreshLarkUi();if(!s.waiting)break}
 if(DATA.larkStatus.loggedIn){setWidgetOn('lark',true);toast('✓ Đã kết nối Lark: '+DATA.larkStatus.name);pump('lark')}else if(DATA.larkStatus.error)toast('Lark: '+DATA.larkStatus.error)}catch(e){toast('Lỗi: '+e.message)}};
/* ---------- Thông báo Windows → bong bóng ---------- */
let lastNIds=null;
const nTitle=i=>(i.appName||'')+' · '+i.sender;
const notifWidget={id:'notif',g:'Thông báo',name:'Thông báo → bong bóng',on:true,prov:'notif',
 r:()=>{const n=DATA.notif;if(!n.enabled&&!n.items.length)return `${chip('🔕',GREY)}<div class="t"><span class="v">Thông báo tắt</span><span class="l">Bật trong Windows</span></div>`;
  const c=unseenWin().length;return `${chip('🔔','linear-gradient(135deg,#ff6a88,#ff99ac)')}<div class="t"><span class="v">${c?c+' tin mới':'Không tin mới'}</span><span class="l">Thông báo → bong bóng</span></div>`},
 p:()=>{const n=DATA.notif,list=itemsWin().slice(0,6),c=unseenWin().length;
  return popC('Thông báo → bong bóng',c?c+' tin chưa xem':'Không có tin mới',null,list.map(i=>[(LSEEN.has(i.id)?'✓ ':'● ')+nTitle(i)+' · '+ago(i.t),i.text||'…']),
   `${n.enabled?'':'<div class="note">⚠ Thông báo Windows đang TẮT nên Windows không gửi toast nào, không có gì để hiện. Bật lại rồi tắt “banner” từng app (giữ “Hiện trong trung tâm thông báo”) để bong bóng thay cho banner.</div>'}${n.ok===false?`<div class="note">Lỗi đọc thông báo: ${esc(n.error||'')}</div>`:''}<div class="btns">${c?btn('Đánh dấu đã xem hết','larkAll'):''}${btn('Chọn ứng dụng','openData','#secNotif')}${n.enabled?'':btn('Mở cài đặt thông báo','launch','ms-settings:notifications')}</div>`)}};
W.splice(Math.max(0,W.findIndex(w=>w.id==='lark')),0,notifWidget);
if('notif' in CFG.wOn)notifWidget.on=CFG.wOn.notif;
Object.assign(PROV,{notif:{every:3,run:async()=>{const r=await host('notifs');DATA.notif=r;onNotifData(r)}}});
function onNotifData(r){
 r.items.forEach(i=>{const k=nKey(i);if(!CFG.nSeen[k]){CFG.nSeen[k]=i.site||i.app;saveCfg()}});
 const cur=itemsWin(),fresh=lastNIds?cur.filter(i=>!lastNIds.has(i.id)&&!LSEEN.has(i.id)):[];
 fresh.slice(0,3).forEach(i=>toast('💬 '+nTitle(i)+': '+(i.text||'').slice(0,70)));fresh.forEach(i=>NEWCHAT.add(i.chat));
 lastNIds=new Set(cur.map(i=>i.id));renderBubbles();refreshNotifUi();tick()}
function refreshNotifUi(){const el=$('#nlist');if(!el)return;const n=DATA.notif;
 $('#st-notif').textContent=!n?'…':n.ok===false?'✗ '+(n.error||'lỗi'):(n.enabled?'✓ Windows đang gửi thông báo':'⚠ Thông báo Windows đang TẮT')+' · '+n.items.length+' thông báo trong trung tâm';
 const keys=Object.keys(CFG.nSeen).sort();
 const sig=keys.join(',')+JSON.stringify(CFG.nApps);if(el._sig===sig)return;el._sig=sig;
 el.innerHTML=keys.length?keys.map(k=>{const on=(k in CFG.nApps)?CFG.nApps[k]:NOTIF_RE.test(k+' '+CFG.nSeen[k]);return `<div class="tg"><span>${esc(CFG.nSeen[k])}<small>${esc(k.startsWith('site:')?'trang web (qua trình duyệt)':'ứng dụng')}</small></span><div class="sw ${on?'on':''}" data-nk="${esc(k)}"></div></div>`}).join(''):'<div class="mini2">Chưa thấy thông báo nào. Khi có app nào gửi thông báo, nó sẽ hiện ở đây để bạn chọn.</div>'}
$('#nlist').onclick=e=>{const s=e.target.closest('.sw');if(!s)return;const k=s.dataset.nk,cur=s.classList.contains('on');CFG.nApps[k]=!cur;saveCfg();$('#nlist')._sig='';refreshNotifUi();renderBubbles();tick()};
$('#lk-logout').onclick=async()=>{try{await host('larkLogout');DATA.larkStatus=await host('larkStatus');DATA.lark=null;lastIds=null;refreshLarkUi();renderBubbles();tick();toast('Đã đăng xuất Lark')}catch(e){toast('Lỗi: '+e.message)}};
placeStack();
