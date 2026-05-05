using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using JapaneseASR.Models;
using JapaneseASR.Services;
using WinForms = System.Windows.Forms;

namespace JapaneseASR;

public partial class MainWindow : Window
{
    private readonly PortablePaths _paths;
    private readonly LogService _logService;
    private readonly AsrJobRunner _jobRunner;
    private AiLocalSettings _aiLocalSettings = AiLocalSettings.Empty;
    private CancellationTokenSource? _jobCancellation;

    public MainWindow()
    {
        InitializeComponent();

        _paths = new PortablePaths(AppContext.BaseDirectory);
        _paths.EnsureWritableDirectories();
        _logService = new LogService(_paths);
        _jobRunner = new AsrJobRunner(
            _paths,
            new ProcessRunner(_logService),
            _logService,
            new AiTranscriptRefiner(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, _logService));

        OutputPathTextBox.Text = _paths.OutputDirectory;
        SetAiControlsEnabled(false);
        LoadAiLocalSettings();
        BalancedModeRadio.Checked += (_, _) => UpdateModeDescription();
        AccurateModeRadio.Checked += (_, _) => UpdateModeDescription();
        AppendStatus("Sẵn sàng. Hãy chọn một file audio/video tiếng Nhật.");
    }

    private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn file audio/video",
            Filter = "Audio/video|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg|Tất cả file|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            InputPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Chọn thư mục xuất TXT/SRT/VTT",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(OutputPathTextBox.Text) ? OutputPathTextBox.Text : _paths.OutputDirectory
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            OutputPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_jobCancellation is not null)
        {
            return;
        }

        var request = new AsrJobRequest(
            InputPathTextBox.Text.Trim(),
            OutputPathTextBox.Text.Trim(),
            GetSelectedMode(),
            BuildAiOptions());

        _jobCancellation = new CancellationTokenSource();
        SetRunningState(isRunning: true);
        StatusTextBox.Clear();
        ProgressBar.IsIndeterminate = true;

        try
        {
            AppendStatus("Bắt đầu xử lý.");
            var result = await _jobRunner.RunAsync(request, AppendStatus, _jobCancellation.Token);
            AppendStatus($"TXT: {result.TextPath}");
            AppendStatus($"SRT: {result.SrtPath}");
            AppendStatus($"VTT: {result.VttPath}");
            if (!string.IsNullOrWhiteSpace(result.AiTextPath))
            {
                AppendStatus($"AI SRT: {result.AiTextPath}");
            }
            AppendStatus($"Log: {result.LogPath}");
            FooterText.Text = "Đã xuất kết quả.";
        }
        catch (OperationCanceledException)
        {
            AppendStatus("Đã hủy xử lý.");
            FooterText.Text = "Đã hủy.";
        }
        catch (UserVisibleException ex)
        {
            AppendStatus(ex.Message);
            AppendStatus($"Log kỹ thuật: {_logService.LogPath}");
            FooterText.Text = "Có lỗi. Xem thông báo trong khung trạng thái.";
        }
        catch (Exception ex)
        {
            await _logService.WriteAsync(ex.ToString());
            AppendStatus("App gặp lỗi ngoài dự kiến. Vui lòng gửi file log cho người hỗ trợ.");
            AppendStatus($"Log kỹ thuật: {_logService.LogPath}");
            FooterText.Text = "Có lỗi ngoài dự kiến.";
        }
        finally
        {
            _jobCancellation.Dispose();
            _jobCancellation = null;
            ProgressBar.IsIndeterminate = false;
            SetRunningState(isRunning: false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _jobCancellation?.Cancel();
        AppendStatus("Đang hủy...");
    }

    private void EnableAiCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        SetAiControlsEnabled(EnableAiCheckBox.IsChecked == true && _jobCancellation is null);
    }

    private void AiProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (GetSelectedAiProvider() == AiProvider.Ollama)
        {
            if (_aiLocalSettings.GetPreset(AiProvider.Ollama) != AiProviderPreset.Empty)
            {
                ApplyAiPreset(AiProvider.Ollama);
            }
            else
            {
                AiEndpointTextBox.Text = "http://localhost:11434/api/chat";
                AiModelTextBox.Text = string.IsNullOrWhiteSpace(AiModelTextBox.Text) || AiModelTextBox.Text.Contains("/", StringComparison.Ordinal)
                    ? "qwen2.5:7b"
                    : AiModelTextBox.Text;
                AiApiKeyPasswordBox.Password = string.Empty;
            }
        }
        else if (GetSelectedAiProvider() == AiProvider.DeepSeek)
        {
            if (_aiLocalSettings.GetPreset(AiProvider.DeepSeek) != AiProviderPreset.Empty)
            {
                ApplyAiPreset(AiProvider.DeepSeek);
            }
            else
            {
                AiEndpointTextBox.Text = "https://api.deepseek.com/chat/completions";
                AiModelTextBox.Text = AiModelTextBox.Text.Contains(":", StringComparison.Ordinal)
                    ? "deepseek-v4-pro"
                    : AiModelTextBox.Text;
                AiApiKeyPasswordBox.Password = string.Empty;
            }
        }
        else
        {
            if (_aiLocalSettings.GetPreset(AiProvider.NvidiaNim) != AiProviderPreset.Empty)
            {
                ApplyAiPreset(AiProvider.NvidiaNim);
            }
            else
            {
                AiEndpointTextBox.Text = "http://localhost:8000/v1/chat/completions";
                AiModelTextBox.Text = AiModelTextBox.Text.Contains(":", StringComparison.Ordinal)
                    ? "meta/llama-3.1-8b-instruct"
                    : AiModelTextBox.Text;
            }
        }
    }

    private void LoadAiLocalSettings()
    {
        try
        {
            var settings = AiLocalSettings.Load(_paths.AiLocalSettingsPath);
            _aiLocalSettings = settings;
            if (string.IsNullOrWhiteSpace(settings.Endpoint) && string.IsNullOrWhiteSpace(settings.Model))
            {
                return;
            }

            SelectAiProvider(settings.Provider);
            EnableAiCheckBox.IsChecked = settings.Enabled;
            ApplyAiPreset(settings.Provider);

            SetAiControlsEnabled(settings.Enabled);
            AppendStatus("Đã nạp cấu hình AI local.");
        }
        catch (Exception ex)
        {
            _ = _logService.WriteAsync($"Cannot load AI local settings: {ex}");
            AppendStatus("Không đọc được config AI local, app vẫn chạy bình thường.");
        }
    }

    private void SelectAiProvider(AiProvider provider)
    {
        foreach (var item in AiProviderComboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), provider.ToString(), StringComparison.Ordinal))
            {
                AiProviderComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ApplyAiPreset(AiProvider provider)
    {
        var preset = _aiLocalSettings.GetPreset(provider);
        if (string.IsNullOrWhiteSpace(preset.Endpoint) && string.IsNullOrWhiteSpace(preset.Model))
        {
            return;
        }

        AiEndpointTextBox.Text = preset.Endpoint;
        AiModelTextBox.Text = preset.Model;
        AiApiKeyPasswordBox.Password = preset.ApiKey;
        if (!string.IsNullOrWhiteSpace(preset.Glossary))
        {
            AiGlossaryTextBox.Text = preset.Glossary;
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            InputPathTextBox.Text = files[0];
            AppendStatus($"Đã chọn file: {files[0]}");
        }
    }

    private TranscriptionMode GetSelectedMode()
    {
        return AccurateModeRadio.IsChecked == true ? TranscriptionMode.Accurate : TranscriptionMode.Balanced;
    }

    private AiRefinementOptions BuildAiOptions()
    {
        if (EnableAiCheckBox.IsChecked != true)
        {
            return AiRefinementOptions.Disabled;
        }

        var primary = GetSelectedAiProvider();
        var attempts = new List<AiProviderAttempt>
        {
            new(
                primary,
                AiEndpointTextBox.Text.Trim(),
                AiModelTextBox.Text.Trim(),
                AiApiKeyPasswordBox.Password.Trim(),
                AiGlossaryTextBox.Text.Trim())
        };

        foreach (var fallbackProvider in _aiLocalSettings.FallbackProviders)
        {
            if (fallbackProvider == primary || attempts.Any(attempt => attempt.Provider == fallbackProvider))
            {
                continue;
            }

            var preset = _aiLocalSettings.GetPreset(fallbackProvider);
            if (string.IsNullOrWhiteSpace(preset.Endpoint) || string.IsNullOrWhiteSpace(preset.Model))
            {
                continue;
            }

            attempts.Add(new AiProviderAttempt(
                fallbackProvider,
                preset.Endpoint,
                preset.Model,
                preset.ApiKey,
                string.IsNullOrWhiteSpace(preset.Glossary) ? AiGlossaryTextBox.Text.Trim() : preset.Glossary));
        }

        return new AiRefinementOptions(true, attempts);
    }

    private AiProvider GetSelectedAiProvider()
    {
        if (AiProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && string.Equals(item.Tag?.ToString(), "NvidiaNim", StringComparison.Ordinal))
        {
            return AiProvider.NvidiaNim;
        }

        if (AiProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item2
            && string.Equals(item2.Tag?.ToString(), "DeepSeek", StringComparison.Ordinal))
        {
            return AiProvider.DeepSeek;
        }

        return AiProvider.Ollama;
    }

    private void UpdateModeDescription()
    {
        if (AccurateModeRadio.IsChecked == true)
        {
            ModeDescriptionText.Text = "Chính xác dùng model medium nên có thể lâu hơn đáng kể trên máy yếu.";
        }
        else
        {
            ModeDescriptionText.Text = "Cân bằng dùng model small để xử lý nhanh hơn trên máy yếu.";
        }
    }

    private void SetRunningState(bool isRunning)
    {
        BrowseInputButton.IsEnabled = !isRunning;
        BrowseOutputButton.IsEnabled = !isRunning;
        BalancedModeRadio.IsEnabled = !isRunning;
        AccurateModeRadio.IsEnabled = !isRunning;
        EnableAiCheckBox.IsEnabled = !isRunning;
        SetAiControlsEnabled(!isRunning && EnableAiCheckBox.IsChecked == true);
        StartButton.IsEnabled = !isRunning;
        CancelButton.IsEnabled = isRunning;
        Cursor = isRunning ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private void SetAiControlsEnabled(bool isEnabled)
    {
        AiProviderComboBox.IsEnabled = isEnabled;
        AiEndpointTextBox.IsEnabled = isEnabled;
        AiModelTextBox.IsEnabled = isEnabled;
        AiApiKeyPasswordBox.IsEnabled = isEnabled;
        AiGlossaryTextBox.IsEnabled = isEnabled;
    }

    private void AppendStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendStatus(message));
            return;
        }

        StatusTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        StatusTextBox.ScrollToEnd();
    }
}
