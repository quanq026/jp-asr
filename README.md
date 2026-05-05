# Japanese ASR Portable

App Windows portable để chuyển audio/video tiếng Nhật sang chữ Nhật bằng `whisper.cpp`.

## Yêu cầu để chạy

### Cách nhanh: All-in-One

```powershell
.\scripts\setup-aio.ps1
```

Một lệnh duy nhất → cài .NET SDK → tải runtime + model → build portable. Kết quả ở `dist\JapaneseASR-portable\`.

### Cách từng bước

Sau khi clone repo, cần tải thêm các file binary (không có trong git):

### 1. .NET SDK 8 (máy build)

```powershell
.\scripts\install-dotnet-sdk.ps1
```

### 2. Runtime binary + model (~700MB)

```powershell
.\scripts\prepare-runtime.ps1
```

Script này sẽ tải về:

| File | Dung lượng | Mục đích |
|---|---|---|
| `bin\ffmpeg.exe` | ~170MB | Tách audio → WAV mono 16kHz |
| `bin\whisper-cli.exe` | ~0.5MB | Nhận dạng giọng nói (whisper.cpp) |
| `bin\whisper.dll` + `ggml-*.dll` | ~2MB | DLLs cho whisper |
| `models\ggml-small-q5_1.bin` | ~190MB | Model chế độ Cân bằng |
| `models\ggml-medium-q5_0.bin` | ~515MB | Model chế độ Chính xác |

### 3. Build

```powershell
.\scripts\publish-portable.ps1
```

Kết quả: `dist\JapaneseASR-portable\` — copy nguyên folder này sang máy khác, chạy `JapaneseASR.exe` là được.

## Chế độ xử lý

- **Cân bằng:** model `ggml-small-q5_1.bin` (~190MB), whisper dùng 8 thread.
- **Chính xác:** model `ggml-medium-q5_0.bin` (~515MB), chậm hơn đáng kể.

## AI refinement (DeepSeek, Ollama, NVIDIA NIM)

Sau khi Whisper tạo SRT, app gửi SRT lên AI để sửa nhẹ text tiếng Nhật (giữ nguyên timestamp). Kết quả → `.ai.srt`.

### Provider hỗ trợ

| Provider | Endpoint mặc định | Auth |
|---|---|---|
| **DeepSeek** | `https://api.deepseek.com/chat/completions` | Bearer API key |
| **Ollama** | `http://localhost:11434/api/chat` | Không bắt buộc |
| **NVIDIA NIM** | `http://localhost:8000/v1/chat/completions` | Bearer API key |

### Cấu hình nhanh với `config\ai.local.json`

```json
{
    "enabled": true,
    "activeProvider": "DeepSeek",
    "providers": {
        "DeepSeek": {
            "endpoint": "https://api.deepseek.com/chat/completions",
            "model": "deepseek-v4-flash",
            "apiKey": "<your-api-key>"
        }
    }
}
```

Nếu có `fallbackProviders`, app tự thử provider dự phòng khi provider chính lỗi.

### Prompt AI

AI chỉ sửa nhẹ kana/kanji/thuật ngữ/dấu câu/ngắt câu. **Không dịch, không tóm tắt, không thêm ý mới.** Giữ nguyên timestamp SRT.

## Output

| File | Nội dung |
|---|---|
| `.txt` | Plain text đã làm sạch timestamp, dễ đọc |
| `.srt` | SubRip subtitle, giữ timestamp |
| `.vtt` | WebVTT subtitle |
| `.ai.srt` | SRT đã AI sửa text (nếu bật AI) |

## Lưu ý

- Tên file đầu vào có ký tự tiếng Nhật/special chars → app tự sanitize thành ASCII để tương thích với external tool.
- File `.ai.local.json` chứa API key — **KHÔNG commit lên git** (đã có trong `.gitignore`).
