# Japanese ASR Portable

App Windows portable để chuyển audio/video tiếng Nhật sang chữ Nhật bằng `whisper.cpp`.

## Mục tiêu

- Khách hàng mở `JapaneseASR.exe` trong một thư mục và chạy ngay.
- Không cần cài Python, .NET runtime, FFmpeg, Whisper hay model trên máy khách.
- V1 xử lý một file/lần và xuất `TXT`, `SRT`, `VTT`.

## Chế độ

- **Cân bằng:** dùng model `small` quantized. Script mặc định tải `ggml-small-q5_1.bin` vì upstream hiện không có `ggml-small-q5_0.bin`.
- **Chính xác:** dùng `ggml-medium-q5_0.bin`, có thể chậm hơn đáng kể trên máy yếu.

## Chuẩn bị máy build

Máy build cần .NET SDK 8. Nếu chưa có SDK, chạy:

```powershell
.\scripts\install-dotnet-sdk.ps1
```

Sau đó mở terminal mới hoặc dùng đường dẫn `.dotnet\dotnet.exe` trong workspace.

## Chuẩn bị runtime portable

Tải `ffmpeg.exe`, `whisper-cli.exe`, và model vào đúng thư mục:

```powershell
.\scripts\prepare-runtime.ps1
```

Script tạo/cập nhật:

```text
bin\ffmpeg.exe
bin\whisper-cli.exe
models\ggml-small-q5_1.bin
models\ggml-medium-q5_0.bin
```

## Build bản portable

```powershell
.\scripts\publish-portable.ps1
```

Kết quả nằm ở:

```text
dist\JapaneseASR-portable\
```

Copy nguyên thư mục này sang máy khách.

## Ghi chú vận hành

- File TXT là bản đọc liền, đã làm sạch khoảng trắng nhẹ.
- SRT/VTT giữ timestamp để đối chiếu audio.
- Nếu app báo thiếu file runtime/model, cần chạy lại script chuẩn bị runtime hoặc dùng lại bản đóng gói đầy đủ.

## AI sửa TXT với Ollama hoặc NVIDIA NIM

Mặc định app không gọi AI. Nếu muốn hậu xử lý transcript:

1. Tick `Dùng AI sửa nhẹ TXT sau khi nhận dạng`.
2. Chọn provider:
   - `Ollama`: mặc định `http://localhost:11434/api/chat`.
   - `NVIDIA NIM`: mặc định `http://localhost:8000/v1/chat/completions`.
3. Nhập đúng model đang chạy.
4. Nhập API key nếu endpoint yêu cầu.

AI không ghi đè TXT gốc. App tạo thêm:

```text
<input-name>.ai.txt
```

Quy tắc prompt của app: không dịch, không tóm tắt, không thêm ý mới, chỉ sửa nhẹ kana/kanji/thuật ngữ/dấu câu/ngắt câu. Glossary mặc định ưu tiên các thuật ngữ JLPT và ngữ pháp Nhật như `自動詞`, `他動詞`, `尊敬語`, `謙譲語`.

Nếu `config\ai.local.json` có `fallbackProviders`, app sẽ tự thử provider dự phòng theo từng chunk khi provider chính gặp lỗi quota/server/timeout/network. Cấu hình khuyến nghị:

```json
{
  "enabled": true,
  "activeProvider": "Ollama",
  "fallbackProviders": ["NvidiaNim"]
}
```
