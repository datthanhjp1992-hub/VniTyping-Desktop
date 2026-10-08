# VniTyping Desktop

Bộ gõ tiếng Việt **portable** cho Windows. Chỉ một file `VniTyping.exe` khoảng 100 KB: tải về là chạy, không cần cài đặt, không cần quyền admin, không ghi registry, không kết nối mạng.

Dành cho máy công ty, máy khách hàng hay máy dùng chung không cài được bộ gõ: mở app, gõ tiếng Việt trong cửa sổ nhỏ luôn nổi trên cùng, bấm **Copy** rồi dán sang Outlook, Teams, Excel…

Đây là bản desktop của [VniTyping](https://github.com/datthanhjp1992-hub/VniTyping) (bản web). Hai bản dùng chung một engine và một bộ test.

---

## Tải về

- **Bản phát hành:** trang **Releases** của repo này.
- **Bản mới nhất từ `main`:** tab **Actions** → lần chạy **Build** mới nhất → mục *Artifacts* → `VniTyping`.

Yêu cầu: Windows 10 hoặc 11 (đã có sẵn .NET Framework 4.8).

> Lần đầu chạy, Windows SmartScreen có thể cảnh báo vì file chưa được ký số. Bấm **More info → Run anyway**.

---

## Cách dùng

| Thao tác | Cách làm |
|---|---|
| Gõ tiếng Việt | Gõ trong ô, chọn TELEX / VNI / VIQR / OFF ở hàng trên |
| Copy toàn bộ | Nút **Copy** hoặc `Ctrl+Enter` |
| Copy rồi xoá trắng | `Ctrl+Shift+Enter` |
| Xoá hết | Nút **Xoá** hoặc `Ctrl+Del` |
| Đổi kiểu gõ | `Ctrl+1` TELEX · `Ctrl+2` VNI · `Ctrl+3` VIQR · `Ctrl+4` OFF |
| Bật/tắt nhanh tiếng Việt | `Ctrl+Shift` (nhấn rồi thả) |
| Luôn nổi trên cùng | Nút ghim hoặc `Ctrl+T` |
| Đổi cỡ chữ | `Ctrl` + cuộn chuột, `Ctrl+0` về mặc định |
| Ẩn xuống khay hệ thống | Nút **–** hoặc `Esc` |
| Gọi cửa sổ lên từ bất kỳ đâu | `Ctrl+Alt+V` |
| Thoát hẳn | Nút **✕** |
| Bảng phím tắt & cách gõ | `F1` |

Menu **⋯** có thêm: kiểu dấu mới (hoà / hòa), giữ nguyên từ tiếng Anh, gõ `ươ` bằng một phím `w`, **9 giao diện** (Sen, Giấy Dó, Mực Đêm, Phố Neon, Hoa Phượng, Anh Đào, Vịnh Hạ Long, Cà Phê Sữa, Tương Phản Cao) và cỡ chữ.

Thiết lập được lưu trong `VniTyping.ini` cạnh file exe. Nếu thư mục đó không cho ghi, app dùng `%APPDATA%\VniTyping\VniTyping.ini`. Xoá file ini để về mặc định.

---

## Engine

`src/Engine/` là bản port 1:1 của `ukengine.js` v2 sang C#:

- tính lại cả từ sau mỗi phím, nên dấu thanh luôn nằm đúng chỗ: `hoafn` → hoàn, `nguyenxe` → nguyễn;
- `ươ` chỉ cần một phím: `huowng` → hương, `nguoi72` → người;
- giữ nguyên từ tiếng Anh: `google`, `address`, `windows`;
- đọc lại từ đứng trước con trỏ trước mỗi phím, nên Undo, dán, click chuột không làm xoá nhầm chữ.

Thiết kế chi tiết xem [`tech.md`](https://github.com/datthanhjp1992-hub/VniTyping/blob/main/tech.md) của repo VniTyping.

---

## Build

Không cần Visual Studio. Trên bất kỳ máy Windows nào:

```bat
build.bat
```

Script dùng `csc.exe` có sẵn trong `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`, chạy bộ test engine trước rồi mới build. Kết quả nằm ở `dist\VniTyping.exe`.

Vì `csc.exe` này chỉ hỗ trợ **C# 5**, mã nguồn không dùng cú pháp mới hơn (`$"..."`, `?.`, `=>` cho thuộc tính…).

GitHub Actions (`.github/workflows/build.yml`) chạy đúng `build.bat` trên máy Windows mỗi lần push.

### Phát hành bản mới

1. Ghi thay đổi vào `CHANGELOG.md` và tăng số phiên bản trong `src/Program.cs`, commit lên `main`.
2. Vào tab **Actions** → **Build** → **Run workflow**, nhập `release_tag` (ví dụ `v1.0.1`) rồi bấm chạy.
3. Workflow build, chạy test, tạo tag và đưa `VniTyping.exe` lên trang **Releases**.

Push một tag `v*` từ máy cũng cho kết quả tương tự.

### Bộ test dùng chung

`tests/cases.json` được **chép** từ repo VniTyping (224 ca). Khi sửa engine:

1. sửa và thêm ca test ở repo VniTyping (`node tests/run.js`);
2. port thay đổi sang `src/Engine/`;
3. chép lại `tests/cases.json` sang đây và chạy `build.bat`.

---

## Cấu trúc

```
├── src/
│   ├── Engine/            # engine gõ tiếng Việt (C# thuần, không phụ thuộc UI)
│   ├── UI/                # cửa sổ chính, 9 giao diện, control tự vẽ, khay hệ thống, F1
│   ├── Settings.cs        # đọc/ghi VniTyping.ini
│   ├── NativeMethods.cs   # Win32: phím tắt toàn cục, bo góc Windows 11, thanh cuộn tối
│   └── Program.cs         # chỉ cho chạy một bản
├── tests/                 # cases.json + TestRunner.cs
├── assets/                # icon, manifest (DPI, quyền người dùng thường)
├── docs/ui-design.html    # bản thiết kế UI/UX đã duyệt (mở bằng trình duyệt, gõ thử được)
├── build.bat
└── COPYING                # GPL v2
```

---

## Bản quyền và giấy phép

```
Copyright © 2026 Nguyễn Thành Đạt
Engine kế thừa ý tưởng và bảng dữ liệu từ UniKey 3.62,
Copyright © 1998–2002 Phạm Kim Long.
```

Phát hành theo **GNU General Public License phiên bản 2**, hoặc (tuỳ bạn chọn) bất kỳ phiên bản nào mới hơn. Toàn văn: [COPYING](COPYING).
