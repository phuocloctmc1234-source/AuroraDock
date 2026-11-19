/**
 * Aurora Bridge — API MẪU cho Aurora Dock (Google Apps Script Web App)
 *
 * ĐÂY LÀ BẢN MẪU, CHƯA ĐƯỢC CHẠY THỬ TRÊN HỆ THỐNG CỦA BẠN.
 * Mình không đọc được app chấm công hiện có, nên phần nguồn dữ liệu (Lịch cả team,
 * lịch break) và phần "ghi nhận chấm công" cần bạn chỉnh cho khớp. Chỗ cần sửa có chữ TODO.
 *
 * Aurora Dock gọi 3 hành động (POST, body là JSON dạng text/plain):
 *   getState     -> trạng thái ca hiện tại trên server
 *   getSchedule  -> giờ vào ca + giờ break hôm nay (đọc LẠI mỗi lần gọi => lịch đổi là có ngay)
 *   act          -> startShift | startBreak | endBreak | endShift | startSleep | endSleep
 *                   có requestId để KHÔNG ghi trùng khi gửi lại (idempotent)
 */

const CONFIG = {
  SHEET_ID: 'DÁN_ID_GOOGLE_SHEET_Ở_ĐÂY',   // TODO: ID file Google Sheet chứa lịch
  TEAM_SHEET: 'Lịch cả team',              // TODO: tên tab. Cột: Ngày | Tên | Giờ vào ca
  BREAK_SHEET: 'Lịch break',               // TODO: tên tab. Cột: Ngày | Tên | Break từ | Break đến
  LOG_SHEET: 'Chấm công (Aurora)',         // tab ghi nhật ký hành động (tự tạo nếu chưa có)
  MY_NAME: 'TÊN_CỦA_BẠN',                  // TODO: đúng như cột "Tên" trong lịch
  TZ: 'Asia/Bangkok'
};

/** Chạy hàm này 1 LẦN trong trình soạn thảo để tạo token bí mật, rồi xem Log (Ctrl+Enter). */
function setup() {
  const token = Utilities.getUuid() + Utilities.getUuid();
  PropertiesService.getScriptProperties().setProperty('AURORA_TOKEN', token);
  Logger.log('TOKEN của bạn (dán vào Aurora Dock, đừng chia sẻ): ' + token);
}

function doGet() { return json_({ ok: true, service: 'aurora-bridge' }); }

function doPost(e) {
  try {
    const req = JSON.parse(e.postData.contents);
    const token = PropertiesService.getScriptProperties().getProperty('AURORA_TOKEN');
    if (!token || req.token !== token) return json_({ error: 'unauthorized' });
    const lock = LockService.getScriptLock();
    lock.waitLock(10000);
    try {
      switch (req.action) {
        case 'getState':    return json_(getState_());
        case 'getSchedule': return json_(getSchedule_());
        case 'act':         return json_(act_(req));
        default:            return json_({ error: 'unknown action' });
      }
    } finally { lock.releaseLock(); }
  } catch (err) {
    return json_({ error: String(err) });
  }
}

/* ---------- trạng thái ca (lưu trong Script Properties) ---------- */
function loadState_() {
  const raw = PropertiesService.getScriptProperties().getProperty('STATE');
  const st = raw ? JSON.parse(raw) : { phase: 'idle', since: 0, brkSince: 0, brkDone: false, day: '' };
  const today = Utilities.formatDate(new Date(), CONFIG.TZ, 'yyyy-MM-dd');
  if (st.phase === 'done' && st.day !== today) { st.phase = 'idle'; st.brkDone = false; }
  return st;
}
function saveState_(st) { PropertiesService.getScriptProperties().setProperty('STATE', JSON.stringify(st)); }
function getState_() { const s = loadState_(); return { phase: s.phase, since: s.since, brkSince: s.brkSince, brkDone: s.brkDone }; }

/* ---------- hành động, chống ghi trùng bằng requestId ---------- */
function act_(req) {
  const props = PropertiesService.getScriptProperties();
  const done = JSON.parse(props.getProperty('DONE') || '[]');
  if (done.indexOf(req.requestId) >= 0) return { ok: true, duplicate: true };

  const st = loadState_(), n = req.name, at = Number(req.at) || Date.now();
  const valid =
    (n === 'startShift' && (st.phase === 'idle' || st.phase === 'done')) ||
    (n === 'startBreak' && st.phase === 'working' && !st.brkDone) ||
    (n === 'endBreak'   && st.phase === 'break') ||
    (n === 'endShift'   && st.phase === 'working') ||
    n === 'startSleep' || n === 'endSleep';

  if (valid) {
    if (n === 'startShift') { st.phase = 'working'; st.since = at; st.brkDone = false; st.day = Utilities.formatDate(new Date(at), CONFIG.TZ, 'yyyy-MM-dd'); }
    if (n === 'startBreak') { st.phase = 'break'; st.brkSince = at; }
    if (n === 'endBreak')   { st.phase = 'working'; st.brkDone = true; }
    if (n === 'endShift')   { st.phase = 'done'; }
    saveState_(st);
    applyToExistingApp_(n, at, req);
  }
  done.push(req.requestId);
  props.setProperty('DONE', JSON.stringify(done.slice(-200)));
  return { ok: true, applied: valid };
}

/**
 * TODO: NỐI VÀO APP CHẤM CÔNG HIỆN CÓ CỦA BẠN.
 * Mặc định chỉ ghi 1 dòng nhật ký vào tab LOG_SHEET để bạn kiểm tra.
 * Hãy gọi hàm xử lý "bấm nút" thật của app chấm công ở đây (cùng logic mà nút trên web đang dùng).
 */
function applyToExistingApp_(name, at, req) {
  if (CONFIG.SHEET_ID.indexOf('DÁN') === 0) return;
  const ss = SpreadsheetApp.openById(CONFIG.SHEET_ID);
  const sh = ss.getSheetByName(CONFIG.LOG_SHEET) || ss.insertSheet(CONFIG.LOG_SHEET);
  sh.appendRow([new Date(at), CONFIG.MY_NAME, name, req.requestId]);
}

/* ---------- lịch: đọc lại mỗi lần gọi => cập nhật gần thời gian thực ---------- */
function getSchedule_() {
  const ss = SpreadsheetApp.openById(CONFIG.SHEET_ID);
  const today = Utilities.formatDate(new Date(), CONFIG.TZ, 'yyyy-MM-dd');
  const team = findRow_(ss.getSheetByName(CONFIG.TEAM_SHEET), today);   // [ngày, tên, giờ vào ca]
  const brk  = findRow_(ss.getSheetByName(CONFIG.BREAK_SHEET), today);  // [ngày, tên, từ, đến]
  if (!team) throw new Error('Không thấy lịch ca hôm nay cho ' + CONFIG.MY_NAME);
  if (!brk)  throw new Error('Chưa có lịch break hôm nay (thường cập nhật sau 00:30)');
  const start = hhmm_(team[2]), brkFrom = hhmm_(brk[2]), brkTo = hhmm_(brk[3]);
  const ver = Utilities.base64Encode(Utilities.computeDigest(Utilities.DigestAlgorithm.MD5, [start, brkFrom, brkTo].join('|')));
  return { start: start, brkFrom: brkFrom, brkTo: brkTo, ver: ver, at: Date.now() };
}
// Nếu "Lịch cả team" là Google Calendar: thay getSchedule_ bằng CalendarApp.getCalendarById(...).getEventsForDay(new Date())
function findRow_(sh, today) {
  if (!sh) return null;
  const rows = sh.getDataRange().getValues();
  for (let i = 1; i < rows.length; i++) {
    const d = rows[i][0] instanceof Date ? Utilities.formatDate(rows[i][0], CONFIG.TZ, 'yyyy-MM-dd') : String(rows[i][0]).trim();
    if (d === today && String(rows[i][1]).trim() === CONFIG.MY_NAME) return rows[i];
  }
  return null;
}
function hhmm_(v) {
  if (v instanceof Date) return Utilities.formatDate(v, CONFIG.TZ, 'HH:mm');
  const m = String(v).match(/(\d{1,2})[:h](\d{2})/);
  if (!m) throw new Error('Không đọc được giờ: ' + v);
  return ('0' + m[1]).slice(-2) + ':' + m[2];
}

function json_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}
