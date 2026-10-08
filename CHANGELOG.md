# Nhật ký thay đổi

## v1.0.0 — 2026-10-08

Bản phát hành đầu tiên của VniTyping Desktop.

### Gõ tiếng Việt
- Ba kiểu gõ **TELEX**, **VNI**, **VIQR** và chế độ **OFF**.
- Engine v2 lấy âm tiết làm trung tâm (port từ `ukengine.js` của bản web):
  - dấu thanh luôn nằm đúng chỗ dù gõ theo thứ tự nào (`hoafn` → hoàn, `nguyenxe` → nguyễn);
  - `ươ` chỉ cần một phím (`huowng` → hương, `nguoi72` → người);
  - giữ nguyên từ tiếng Anh (`google`, `address`, `windows`);
  - mặc định dấu kiểu mới (hoà, khoẻ, thuỷ), đổi được trong menu.
- Đạt 224/224 ca của bộ test dùng chung với bản web.

### Ứng dụng
- Một file `VniTyping.exe`, không cần cài đặt, không cần quyền admin, không ghi registry, không kết nối mạng.
- Cửa sổ nhỏ luôn nổi trên cùng; nút **Copy** / **Xoá**; dòng trạng thái đếm ký tự.
- 9 giao diện: Sen, Giấy Dó, Mực Đêm, Phố Neon, Hoa Phượng, Anh Đào, Vịnh Hạ Long, Cà Phê Sữa, Tương Phản Cao.
- Nút **–** ẩn xuống khay hệ thống, **✕** thoát hẳn; `Ctrl+Alt+V` gọi cửa sổ lên từ bất kỳ đâu.
- Phím tắt: `Ctrl+Enter` copy, `Ctrl+Shift+Enter` copy rồi xoá, `Ctrl+1..4` đổi kiểu gõ, `Ctrl+Shift` bật/tắt tiếng Việt, `Ctrl+T` luôn nổi trên cùng, `Ctrl+Del` xoá hết, `F1` hướng dẫn.
- Thiết lập lưu trong `VniTyping.ini` cạnh file exe.
- Hỗ trợ màn hình DPI cao, bo góc trên Windows 11, thanh cuộn tối cho giao diện tối.

Yêu cầu: Windows 10 / 11 (.NET Framework 4.8 có sẵn).
