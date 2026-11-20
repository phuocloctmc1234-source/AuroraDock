/* Khay hệ thống: icon THẬT từ khay của Windows (Windows lưu ảnh icon + app đang chạy).
   Thanh nhỏ hiện 3 app nền hoạt động gần nhất + nút ▴ xem tất cả. Bấm icon = mở app, chuột phải = đóng / kết thúc / thư mục / tắt tự khởi động. */
DATA.tray=null;
const TRAYMAX=3,TRAYACT=store.get('trayAct',{});
let TRAYSEL=null,TRAYARM='';
const tkey=a=>String(a.p).toLowerCase();
const trayHidden=()=>CFG.trayHide||(CFG.trayHide=[]);
const trayAll=()=>(DATA.tray||[]).filter(a=>!trayHidden().includes(tkey(a)));
const trayRank=()=>trayAll().slice().sort((a,b)=>(TRAYACT[tkey(b)]||0)-(TRAYACT[tkey(a)]||0)||(b.pro-a.pro)||(b.mb-a.mb));
const trayTop=()=>trayRank().slice(0,TRAYMAX);
const trayImg=a=>`<img src="data:image/png;base64,${a.png}">`;
const trayName=a=>a.tip||appName(a.p);
const trayAgo=a=>{const t=TRAYACT[tkey(a)];if(!t)return 'chưa thấy hoạt động';const m=Math.round((Date.now()-t)/60000);return m<1?'đang hoạt động':m+' phút trước'};
function trayStrip(){const top=trayTop(),more=trayAll().length-top.length;
 return `<div class="bgs">${top.map((a,i)=>`<span class="bgi ${a.cpu>=3?'hot':a.cpu>=0.3?'warm':''}" data-i="${i}" title="${esc(trayName(a))} · ${a.mb} MB · CPU ${a.cpu}% — bấm: mở · chuột phải: tùy chọn">${trayImg(a)}</span>`).join('')}<span class="bgi chev" title="Hiện tất cả app chạy nền${more>0?' (+'+more+')':''}">▴${more>0?`<sup>${more}</sup>`:''}</span></div>`}
function trayMenu(a){const armed=TRAYARM===tkey(a),row=(act,t)=>`<div class="win"><span data-act="trayDo" data-arg="${act}">${t}</span></div>`;
 return `<h4>${esc(trayName(a))}</h4><div class="note">${a.mb} MB · CPU ${a.cpu}% · ${a.n} tiến trình · ${trayAgo(a)}</div>`
  +row('open','▶ Mở / chuyển tới app')+row('close','⏏ Đóng (nhẹ nhàng)')+row('kill',armed?'⚠ Bấm lần nữa để kết thúc hẳn':'✕ Kết thúc (buộc dừng)')
  +row('folder','📂 Mở thư mục chứa app')+row('noauto','⛔ Tắt tự khởi động cùng Windows')+row('hide','👁 Ẩn khỏi danh sách')
  +`<div class="btns">${btn('← Tất cả icon','trayBack')}</div>`}
function trayPop(){
 if(TRAYSEL){const a=trayAll().find(x=>tkey(x)===TRAYSEL);if(a)return trayMenu(a);TRAYSEL=null}
 const L=trayRank();
 return `<h4>App chạy nền</h4><div class="trg">${L.map((a,i)=>`<span class="tri ${a.cpu>=3?'hot':''}" data-act="traySel" data-arg="${i}" title="${esc(trayName(a))} · ${a.mb} MB · CPU ${a.cpu}%">${trayImg(a)}</span>`).join('')||'<div class="note">Không có icon nào</div>'}</div>`
  +`<div class="note">${L.length} app đang chạy nền (sắp theo hoạt động gần nhất). Bấm một icon để xem tùy chọn.</div>`
  +`<div class="btns">${btn('Mở khay Windows','trayFlyout')}${trayHidden().length?btn('Hiện lại '+trayHidden().length+' icon','trayUnhide'):''}</div>`}
function trayOpen(a){if(!a)return;const w=((DATA.wins&&DATA.wins.list)||[]).find(x=>String(x.p||'').toLowerCase()===tkey(a));
 if(w)host('activate',{h:w.h}).catch(()=>{});else host('launch',{target:a.p}).catch(e=>toast('Không mở được: '+e.message))}
async function pTray(){const r=await host('trayIcons',{},20000);let ch=false;
 r.forEach(a=>{const k=tkey(a);if(a.cpu>=0.2){TRAYACT[k]=Date.now();ch=true}else if(!(k in TRAYACT)){TRAYACT[k]=0}});
 DATA.tray=r;if(ch)store.set('trayAct',TRAYACT)}
W.push({id:'tray',g:'Hệ thống',name:'Khay hệ thống (icon nhỏ)',on:true,prov:'tray',ic:'◉',r:trayStrip,p:trayPop,ctxSel:'.bgi[data-i]',
 act:ev=>{const b=ev.target.closest('.bgi[data-i]');if(b){trayOpen(trayTop()[+b.dataset.i]);return}TRAYSEL=null;TRAYARM='';openPop('tray',trayPop,ev.target.closest('.w'))},
 ctx:ev=>{const b=ev.target.closest('.bgi[data-i]'),a=b&&trayTop()[+b.dataset.i];if(!a)return;TRAYSEL=tkey(a);TRAYARM='';closePop();openPop('tray',trayPop,ev.target.closest('.w'))}});
if('tray' in CFG.wOn)W[W.length-1].on=CFG.wOn.tray;
Object.assign(PROV,{tray:{every:15,run:pTray}});
Object.assign(ACT,{
 traySel:i=>{const a=trayRank()[+i];if(a){TRAYSEL=tkey(a);TRAYARM=''}},
 trayBack:()=>{TRAYSEL=null;TRAYARM=''},
 trayHide:i=>{const a=trayRank()[+i];if(!a)return;trayHidden().push(tkey(a));saveCfg()},
 trayUnhide:()=>{CFG.trayHide=[];saveCfg()},
 trayFlyout:()=>{closePop();host('trayFlyout').then(ok=>{if(!ok)toast('Không mở được khay Windows')}).catch(e=>toast('Lỗi: '+e.message))},
 trayDo:act=>{const a=trayAll().find(x=>tkey(x)===TRAYSEL);if(!a)return;
  if(act==='open'){trayOpen(a);closePop();return}
  if(act==='hide'){trayHidden().push(tkey(a));saveCfg();TRAYSEL=null;return}
  if(act==='kill'&&TRAYARM!==tkey(a)){TRAYARM=tkey(a);setTimeout(()=>{if(TRAYARM===tkey(a)){TRAYARM='';tick()}},4000);return}
  TRAYARM='';
  host('trayAct',{path:a.p,act}).then(n=>{
   if(n<0)toast('App đã không còn trong khay');
   else if(act==='close')toast(n?'Đã gửi lệnh đóng '+trayName(a):'App không có cửa sổ để đóng — hãy dùng “Kết thúc”');
   else if(act==='kill')toast(n?'Đã kết thúc '+trayName(a):'Không kết thúc được');
   else if(act==='noauto')toast(n?'Đã tắt tự khởi động của '+trayName(a):'Không thấy mục tự khởi động của người dùng (có thể do hệ thống/quản trị đặt)');
   if(act==='kill'||act==='close'){TRAYSEL=null;setTimeout(()=>pump('tray'),900)}}).catch(e=>toast('Lỗi: '+e.message))}});
