AURORA DOCK 2.0 — dock thay taskbar Windows, dữ liệu THẬT
==========================================================

KHÁC GÌ BẢN 1.0
  - 1.0 là một trang web trong cửa sổ Edge, số liệu do app tự bịa. 2.0 là app Windows thật (AuroraHost.exe).
  - Là AppBar của Windows: chừa vùng làm việc (cửa sổ maximize dừng ngay trên dock), ẩn/tự ẩn taskbar gốc.
  - Hiển thị các cửa sổ đang mở thật (icon lấy từ file .exe), bấm để mở/thu nhỏ, chuột phải để đóng/ghim.
  - Nền kính là blur thật của Windows trên desktop phía sau (không còn desktop giả).
  - Mọi widget lấy dữ liệu thật. Widget chưa có nguồn thì hiện "Kết nối", KHÔNG hiện số giả.

GÓI NÀY CÓ GÌ
  host\            Mã nguồn AuroraHost.exe (C#) + thư viện WebView2. Tự biên dịch khi cài, không cần cài thêm gì.
  app\             Giao diện (HTML/JS), chạy bên trong host
  bridge\Code.gs   API mẫu Google Apps Script để gửi chấm công lên hệ thống của bạn (tuỳ chọn)
  Cai-dat.bat          Cài vào máy (shortcut Desktop + Start menu, tuỳ chọn tự chạy cùng Windows)
  Chay-thu.bat         Chạy thử, GIỮ NGUYÊN taskbar Windows
  Go-cai-dat.bat       Gỡ cài đặt (tự trả taskbar; giữ lại dữ liệu chấm công)
  Khoi-phuc-taskbar.bat  CỨU HỘ: trả lại taskbar nếu có sự cố

YÊU CẦU: Windows 10/11 64-bit + WebView2 Runtime (có sẵn trên Windows 11 và cùng Edge). Không cần quyền Admin.

CÀI ĐẶT
  1. Giải nén thư mục này ra chỗ bất kỳ.
  2. Thử trước: bấm đúp Chay-thu.bat. Dock hiện ở đáy màn hình chính, taskbar Windows vẫn còn.
  3. Cài: bấm đúp Cai-dat.bat (bản cài sẽ ghi đè shortcut "Aurora Dock" cũ dùng Edge).
  4. Mở ⚙ > "Giao diện Windows" > chọn "Tự ẩn" hoặc "Ẩn hẳn" để dock thay taskbar.

THAY GIAO DIỆN WINDOWS — 3 CHẾ ĐỘ TASKBAR
  Giữ nguyên : dock nằm ngay trên taskbar gốc.
  Tự ẩn      : taskbar gốc tự ẩn, rê chuột xuống cạnh màn hình thì hiện (khay hệ thống vẫn dùng được). MẶC ĐỊNH.
  Ẩn hẳn     : taskbar gốc biến mất hoàn toàn. Mất khay hệ thống (icon cạnh đồng hồ). Phím Windows vẫn mở Start.
  Chỉ taskbar của màn hình CHÍNH bị ẩn; dock chỉ chạy trên màn hình chính. Màn hình phụ giữ taskbar của nó.

  AN TOÀN: thoát bằng nút "Thoát & trả lại taskbar" trong ⚙, hoặc phím Ctrl+Alt+Shift+Q.
  Nếu app bị tắt đột ngột (kill, mất điện) khi đang ẩn taskbar: chạy Khoi-phuc-taskbar.bat,
  hoặc cứ mở lại Aurora Dock (nó tự trả taskbar rồi mới áp dụng lại).

DỮ LIỆU THẬT — TỪNG WIDGET LẤY TỪ ĐÂU
  Ứng dụng đang mở  Cửa sổ thật của Windows. Ghim: chuột phải > "Ghim vào dock".
  CPU/RAM, Mạng, Ổ đĩa, Pin    Windows (pin tự ẩn nếu máy không có pin). Ping đo tới 1.1.1.1.
  Screen Time       Cộng dồn thời gian cửa sổ bạn đang dùng (bỏ qua lúc không chạm máy > 2 phút).
                    Chỉ tính từ lúc Aurora Dock chạy.
  Nhạc              Phiên media của Windows (Spotify, trình duyệt...). Tự ẩn khi không có gì phát.
  Downloads/Thùng rác   Thư mục Downloads và Recycle Bin thật.
  Đồng hồ           Giờ máy.
  Claude Code       Đọc log Claude Code trên máy (~/.claude/projects): token của block 5 giờ hiện tại,
                    giờ reset, hôm nay, 7 ngày. KHÔNG cần khóa. Xem "Giới hạn" bên dưới.
                    % HẠN MỨC THẬT (5 giờ + 7 ngày, còn lại, giờ reset): ⚙ > Kết nối dữ liệu > Claude > "Cài hook".
                    Claude Code tự báo số này qua "statusLine" (cách chính thức, không cần đăng nhập/lấy token;
                    Anthropic cấm dùng token gói Pro/Max trong công cụ khác nên Aurora KHÔNG làm vậy).
                    Hook ghi mục statusLine vào ~/.claude/settings.json (sao lưu .aurora-bak, giữ statusLine cũ chạy
                    tiếp, nút "Gỡ hook" khôi phục). Chỉ có với gói Pro/Max/Ultra, sau phản hồi đầu của mỗi phiên.
                    "Ước tính token còn lại" = token đã dùng ÷ % × (100−%).
                    LƯU Ý: extension VS Code KHÔNG gửi dữ liệu này (đã thử: cài hook, chờ >80 giây, không nhận gì).
                    Dùng được khi bạn chạy Claude Code ở TERMINAL (gõ `claude`, kể cả terminal trong VS Code).
                    Nếu cài lại Aurora Dock vào thư mục khác, bấm "Cài hook" lần nữa để cập nhật đường dẫn.
  GitHub            Cần token (⚙ > Kết nối dữ liệu). PR chờ review, issue giao cho bạn, PR của bạn.
  Stripe            Cần Restricted key chỉ quyền ĐỌC Charges. Doanh thu hôm nay (trừ hoàn tiền).
  Lịch              Cần link ICS (Google Calendar: "Địa chỉ bí mật ở định dạng iCal"). Hỗ trợ lặp lại
                    hằng ngày/tuần/tháng/năm, ngoại lệ, múi giờ. Bỏ qua sự kiện cả ngày.
  Thời tiết         Nhập tên thành phố. Nguồn Open-Meteo (miễn phí, không khóa).
  Lark · @mention   Bong bóng chat nổi như Messenger trên Android. Xem mục "LARK" bên dưới.
  Việc cần làm, Pomodoro    Lưu trên máy.
  Khóa/token được mã hóa bằng DPAPI của Windows (chỉ tài khoản Windows của bạn giải mã được), chỉ gửi tới
  đúng máy chủ của dịch vụ (github -> api.github.com, stripe -> api.stripe.com) và giao diện không đọc lại được.

  ĐÃ BỎ vì không có API dùng cá nhân để lấy số thật: ChatGPT/Gemini/Cursor/Grok (mức dùng gói),
  Polar/Shopify/Gumroad/Paddle, Linear/Supabase/Analytics, Lark Họp/Công việc/Phê duyệt. Có API + khóa thì nối thêm được (mỗi nguồn
  là một hàm nhỏ trong app\widgets.js); mình không để số giả thế chỗ.

THÔNG BÁO → BONG BÓNG CHAT (Messenger, Instagram, Lark, Zalo, Telegram…)
  Aurora Dock đọc các thông báo THẬT đang nằm trong Trung tâm thông báo của Windows và hiện tin chưa xem thành
  bong bóng cùng kiểu với bong bóng Lark ở dưới (gộp tin cùng người, bấm = mở thẻ, kéo được, "Đã xem").
  Nguồn là tệp wpndatabase.db của Windows (đọc bản sao, chỉ khi tệp đổi, ~3 ms/lần). Không gửi đi đâu.
   - Chat/app bạn đã TẮT thông báo thì app không gửi toast nên sẽ không hiện bong bóng.
   - Messenger/Instagram chạy trên web: Chrome/Edge gửi toast kèm tên miền (messenger.com, instagram.com),
     Aurora Dock tách riêng theo tên miền. Bấm "Mở trong..." sẽ mở trang đó.
   - Chỉ các app được bật trong ⚙ > Thông báo mới hiện (mặc định bật app nhắn tin quen thuộc; app lạ như
     Windows Security mặc định tắt). Chỉ lấy tin trong 3 giờ qua.
   - YÊU CẦU: Windows phải bật thông báo (Settings > System > Notifications). Máy bạn lúc kiểm tra đang để TẮT
     (Windows báo "DisabledForUser") nên sẽ chưa có gì hiện cho tới khi bạn tự bật. Mình không tự đổi cài đặt đó.
     Mẹo: bật thông báo nhưng với từng app tắt "Hiện banner" và giữ "Hiện trong trung tâm thông báo" để bong
     bóng thay cho banner.
   - Giới hạn: Trung tâm thông báo chỉ giữ tin chưa bị xóa; nếu app tự xóa toast ngay (hoặc bạn xóa nhanh) thì
     có thể lỡ. "Đã xem" trong Aurora Dock không xóa toast khỏi Windows. Định dạng tệp do Microsoft quyết định,
     có thể đổi sau các bản cập nhật Windows.
   - Đã kiểm thử bằng cơ sở dữ liệu giả cùng cấu trúc (toast kiểu Messenger/Instagram qua Chrome, Lark, thông báo
     hệ thống, thêm tin mới lúc đang chạy). CHƯA kiểm thử với toast thật vì thông báo Windows đang tắt.
   - Nếu Lark gửi toast thì đây là cách lấy @mention KHÔNG cần tạo app Lark/quản trị viên duyệt (nhưng chỉ thấy
     tin Lark đã hiện thành toast, không phân biệt được @mention với tin thường). Kết nối Lark API (bên dưới) chính
     xác hơn; khi đã kết nối API thì toast Lark tự bị ẩn để khỏi trùng.

HIỆU NĂNG
  Đo trên máy 8GB đang dùng ~90% RAM: Aurora Dock chiếm khoảng 33-48 MB RAM vật lý riêng và ~0,2-0,4% CPU lúc rảnh;
  luồng giao diện của host phản hồi ~1 ms. Đã giảm: gộp GPU vào tiến trình chính + 1 tiến trình render (bộ nhớ cam kết
  ~411 -> ~214 MB, bớt 1 tiến trình), cửa sổ Shift Desk dùng chung môi trường WebView và tự "ngủ" khi không đọc,
  lưu tạm danh sách card mạng, giãn chu kỳ quét. Lag khi mở tab trình duyệt chủ yếu do máy thiếu RAM (VS Code, Chrome,
  Lark, Claude chiếm phần lớn), nên giảm tải ở các app đó hoặc nâng RAM sẽ hiệu quả hơn.

LARK · @MENTION THÀNH BONG BÓNG CHAT
  Mỗi cuộc trò chuyện có @mention bạn chưa xem hiện thành một bong bóng tròn (chữ cái đầu, số tin, dấu @)
  ở mép màn hình. Bấm = mở thẻ nội dung (người gửi, giờ, tin) + nút "Mở trong Lark" / "Đã xem".
  Kéo bong bóng để đổi chỗ, thả ra thì tự hít vào mép trái/phải. Có @mention mới: bong bóng lắc + thông báo.
  Dock có thêm widget "Lark · @mention" (số chưa xem, danh sách, "Đánh dấu đã xem hết").

  Cách kết nối (làm một lần, cần tài khoản Lark của bạn):
   1. Vào open.larksuite.com/app (hoặc open.feishu.cn/app) > Create custom app.
   2. Security settings > Redirect URLs: thêm đúng  http://127.0.0.1:47653/lark/callback
      (⚙ > Lark có sẵn ô để chép địa chỉ này).
   3. Permissions: bật quyền cho user token: search:message, im:message:readonly, offline_access.
      Muốn hiện tên nhóm / tên người gửi thì bật thêm im:chat:readonly, contact:user.base:readonly
      rồi thêm vào ô Scopes trong ⚙ (mặc định chỉ xin 3 quyền đầu; xin quyền chưa bật thì Lark từ chối).
   4. Create version + Publish. Công ty bạn có thể yêu cầu QUẢN TRỊ VIÊN LARK DUYỆT trước khi dùng được.
   5. ⚙ > Lark: chọn Lark/Feishu, dán App ID + App Secret, bấm "Đăng nhập Lark", cho phép trên trang Lark.
  App ID/Secret và token được mã hóa DPAPI trong host, giao diện không đọc lại được. Token tự làm mới;
  nếu không mở app hơn ~7 ngày thì phải đăng nhập lại (Lark chỉ cho refresh token sống 7 ngày).
  Cách lấy @mention: ưu tiên API tìm tin nhắn theo người được @; nếu Lark từ chối thì tự quét tin nhắn
  gần đây của các chat bạn tham gia (tối đa 40 chat) và lọc tin có @bạn. Chu kỳ 30 giây, tin trong 24 giờ qua.
  CHƯA KIỂM CHỨNG với tài khoản Lark thật (mình không có App ID/Secret của bạn): xem mục GIỚI HẠN.

CHẤM CÔNG
  - Mọi lần bấm (Bắt đầu ca, break, kết thúc ca, giờ ngủ) được ghi vào file THẬT:
      %APPDATA%\AuroraDock\timesheet.jsonl     (mỗi dòng một sự kiện, có id chống ghi trùng)
    Widget "Ca làm" tính giờ đã làm hôm nay / 7 ngày từ file này. Trạng thái ca cũng suy ra từ file,
    nên mở lại app hay khởi động lại máy vẫn đúng.
  - Giờ vào ca / giờ break: nhập tay trong ⚙ (để trống = chỉ bấm tay, không tự động),
    hoặc lấy từ server nếu nối bridge.
  - Gửi lên hệ thống chấm công của bạn (tuỳ chọn): xem bridge\Code.gs, điền URL + token trong ⚙ > Chấm công.
    Khi đó mỗi thao tác ghi vào file trên máy TRƯỚC, rồi gửi lên server; mất mạng thì xếp hàng và thử lại
    (2s, 5s, 15s, 30s, 1p, 2p), gửi lại không bị ghi trùng.
  - Tự động (bật mặc định, cần có giờ vào ca): bắt đầu ca trước giờ N phút, kết thúc break trước giờ quy định.
    Luôn có đếm ngược + nút Hủy, đọc lại lịch ngay trước khi bấm, quá giờ > 60 phút thì không tự bấm.
  - Giờ ngủ tự động TẮT mặc định; bật ở ⚙ > Giờ ngủ tự động.
  - App chấm công công ty (Shift Desk): xem mục SHIFT DESK ngay dưới. Widget "Ca làm" cục bộ ở trên sẽ tự tắt
    khi bạn dán URL Shift Desk (để không ghi trùng hai nơi); bật lại ở ⚙ > Widget nếu muốn dùng riêng.

SHIFT DESK · BREAK (hệ thống break của công ty)
  Shift Desk là web app Apps Script chỉ có giao diện (không có API), đăng nhập bằng Google công ty.
  Cách Aurora Dock làm việc với nó:
   - Mở trang trong một cửa sổ WebView RIÊNG, ẨN, có hồ sơ tách biệt. Bạn đăng nhập MỘT lần (⚙ > Shift Desk >
     "Mở Shift Desk (đăng nhập)", xong thì đóng cửa sổ đó). Phiên đăng nhập lưu mã hóa trên máy bạn (kiểu như
     Chrome lưu), mật khẩu không đi đâu khác và không được lưu bởi Aurora Dock.
   - Cứ 30 giây (10 giây khi gần giờ nghỉ / sắp hết nghỉ) đọc lại trang: trạng thái (ĐANG LÀM VIỆC / ĐẾN GIỜ NGHỈ /
     ĐANG NGHỈ), giờ nghỉ của bạn (giờ VN, lấy từ chính trang, khớp "Lịch cả team"), đã nghỉ bao nhiêu phút,
     "Đã nghỉ từ ..", "Kế hoạch kết thúc ..". Giờ trên trang là giờ VN: máy bạn phải ở múi giờ VN (UTC+7),
     nếu không widget sẽ cảnh báo.
   - Widget "Shift Desk · Break" trên dock: đếm giờ nghỉ / đang nghỉ + nút "Bắt đầu nghỉ" / "Kết thúc nghỉ"
     (chỉ hiện khi nút đó đang có trên Shift Desk). Mỗi lần bấm đều ĐỌC LẠI trang trước, nếu nút không còn
     (trạng thái đã đổi) thì từ chối, không bấm nhầm.
   - TỰ KẾT THÚC NGHỈ (⚙ > Shift Desk, TẮT mặc định): khi đang nghỉ và còn <= N phút (mặc định 1) tới
     "Kế hoạch kết thúc", dock đếm ngược 10 giây (chỉnh ở ⚙ > Ca làm & Break) kèm nút "Làm ngay" / "Hủy",
     đọc lại trang lần nữa trước khi bấm, và bỏ qua nếu dữ liệu cũ hơn 90 giây hoặc đã quá giờ > 60 phút.
   - "Bắt đầu nghỉ" luôn do bạn bấm. Aurora Dock không tự bắt đầu nghỉ.
  Nếu trang đổi bố cục khiến app không nhận ra: ⚙ > Shift Desk > "Sao chép chẩn đoán" rồi gửi mình chỉnh
  (bản sao chép đã cắt bỏ bảng lịch có tên đồng nghiệp).
  HÃY HỎI IT/QUẢN LÝ trước khi bật tự động: bấm nút chấm công thay bạn ảnh hưởng dữ liệu công.

GIỚI HẠN — NÓI THẲNG
  Claude: % trên vòng tròn KHÔNG phải hạn mức thật của gói. Anthropic không công bố API cho việc đó.
    Vòng tròn so với "kỷ lục cũ" (block 5 giờ nhiều token nhất trước đây) hoặc số bạn tự đặt.
    Chỉ tính Claude Code trên máy này, không gồm claude.ai / app / máy khác.
  Mất khay hệ thống ở chế độ "Ẩn hẳn" (Wi-Fi, âm lượng, icon nền). Dùng "Tự ẩn" nếu cần.
  Start menu, thông báo, Win+nút tắt vẫn là của Windows (dock có nút Start gửi phím Windows).
  Kính: blur thật của Windows (DWM). Viền dock bo góc bằng vùng cắt nên hơi răng cưa so với Windows 11 gốc.
  Chỉ màn hình chính. Chưa kiểm thử: màn hình có DPI khác 100% hoặc 2 màn hình khác DPI.
  Lark: đã kiểm thử đăng nhập OAuth tới Lark thật bằng App ID giả (URL authorize đúng, cổng nhận mã chặn
    state sai, mã sai thì hiện đúng lỗi Lark trả về) và giao diện bong bóng bằng chuột thật. CHƯA chạy được
    với tài khoản thật nên chưa chắc: (1) API tìm tin nhắn có nhận bộ lọc "@ai" như mình giả định không,
    (2) token người dùng có đọc được chi tiết tin nhắn không, (3) quyền nào công ty bạn cần quản trị viên duyệt.
    Nếu có lỗi, thông báo của Lark hiện ở ⚙ > Lark và trong popover widget để sửa nhanh. Link "Mở trong Lark"
    dùng applink.larksuite.com/client/chat/open (mở đúng cuộc trò chuyện, chưa chắc nhảy tới đúng tin).
  Shift Desk: đã kiểm thử cơ chế đọc/bấm trên MỘT BẢN GIẢ mình dựng theo ảnh chụp của bạn (2 domain khác nhau
    như iframe của Apps Script): đọc chữ + nút trong iframe, bấm đúng nút, từ chối bấm khi nút không còn,
    đếm ngược tự động và nút Hủy. Với link thật mới kiểm được tới trang đăng nhập Google (hiển thị bình thường,
    app nhận ra "cần đăng nhập"). CHƯA thấy trang thật sau đăng nhập, nên: (1) bố cục chữ/nút thật có đúng như
    ảnh không (mình dò theo chữ "Bắt đầu nghỉ", "Kết thúc nghỉ", "Kế hoạch kết thúc", "Giờ nghỉ của bạn"...),
    (2) Google có chặn bước nhập mật khẩu trong WebView không, (3) phiên đăng nhập sống bao lâu trước khi bị
    hỏi lại (Google Workspace có thể bắt đăng nhập lại định kỳ) — đều chỉ biết được khi bạn chạy thật.
  Chưa kiểm thử với dữ liệu thật: GitHub/Stripe (mới thử cơ chế khóa bằng token giả: GitHub trả 401 đúng,
    khóa bị chặn khi gửi sai máy chủ), lịch ICS thật (mới thử bằng dữ liệu mẫu), nhạc, pin (máy bàn không có).
  Đã kiểm thử trên Windows 11 thật: biên dịch, đọc CPU/RAM/mạng/ổ đĩa/cửa sổ/log Claude/Downloads/Recycle Bin,
    kính + vùng cắt chuột, AppBar chừa vùng làm việc, ẩn/tự ẩn/khôi phục taskbar (kể cả khi bị kill),
    ghi/đọc/chống trùng timesheet, gõ chữ trong Cài đặt, thời tiết thật.

LƯU Ý CHÍNH SÁCH
  Tự động chấm công ảnh hưởng dữ liệu công/lương. Hãy chắc chắn công ty cho phép trước khi bật.

ĐỂ NỐI VÀO APP CHẤM CÔNG CỦA BẠN, MÌNH CẦN BIẾT
  Nó là gì (web trên trình duyệt nào, Lark Attendance, Google Sheet/Form, phần mềm khác)? Có API hoặc
  link webhook không? Nếu chỉ có giao diện web đăng nhập bằng tài khoản công ty thì cách duy nhất là
  điều khiển trình duyệt, dễ vỡ và có thể vi phạm quy định, nên cần hỏi IT trước.

CẬP NHẬT TỰ ĐỘNG, BONG BÓNG CHAT, LARK (2.0+)
  - Cập nhật: app kiểm tra GitHub (nhánh main) mỗi 30 phút, báo "Có bản cập nhật"; bấm Cập nhật để tải, biên dịch thử, rồi cài (không cài nếu bản mới lỗi biên dịch).
  - Bong bóng chat: tin Lark (popup riêng của Lark, @mention) và thông báo Windows hiện thành bong bóng; bấm để xem + trả lời nhanh.
    Mở đúng cuộc chat Lark: ưu tiên API (cần đăng nhập Lark + quyền search:user / im:chat:readonly), nếu không được thì tự thao tác tìm kiếm trong Lark.
  - Không làm phiền / Ẩn nội dung tin: ⚙ > Thông báo.
  - Khóa máy hoặc vắng mặt > 10 phút: app tạm dừng lấy dữ liệu nền (tự chạy lại khi bạn quay lại).
  - Kiểm tra trước khi commit: scripts\check.ps1 (biên dịch host + kiểm tra cú pháp JS).
  - Nhật ký: %APPDATA%\AuroraDock\host.log (tự xoay vòng, giữ file .old).
