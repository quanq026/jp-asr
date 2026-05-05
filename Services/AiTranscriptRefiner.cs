using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed class AiTranscriptRefiner
{
    private readonly HttpClient _httpClient;
    private readonly LogService _logService;

    public AiTranscriptRefiner(HttpClient httpClient, LogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;
    }

    public async Task<string> RefineAsync(
        AiRefinementOptions options,
        string transcript,
        CancellationToken cancellationToken,
        Action<string>? onStatus = null)
    {
        Validate(options);
        var chunks = AiTranscriptChunker.Split(transcript, 2000);
        var refinedChunks = new List<string>(chunks.Count);

        await _logService.WriteAsync($"AI chunks={chunks.Count}", cancellationToken);
        for (var index = 0; index < chunks.Count; index++)
        {
            var refinedChunk = await RefineChunkWithFallbackAsync(options, chunks[index], index + 1, cancellationToken, onStatus);
            refinedChunks.Add(refinedChunk);
        }

        return string.Join(string.Empty, refinedChunks.Select(chunk => chunk.Trim())) + Environment.NewLine;
    }

    private async Task<string> RefineChunkWithFallbackAsync(
        AiRefinementOptions options,
        string transcript,
        int chunkNumber,
        CancellationToken cancellationToken,
        Action<string>? onStatus)
    {
        Exception? lastError = null;
        for (var attemptIndex = 0; attemptIndex < options.Attempts.Count; attemptIndex++)
        {
            var attempt = options.Attempts[attemptIndex];
            try
            {
                var refined = await RefineChunkAsync(attempt, transcript, chunkNumber, cancellationToken);
                if (attemptIndex > 0)
                {
                    await _logService.WriteAsync($"AI chunk={chunkNumber} provider={attempt.Provider} status=fallback-success", cancellationToken);
                    onStatus?.Invoke("Ollama lỗi, đã dùng NVIDIA NIM cho một phần transcript.");
                }

                return refined;
            }
            catch (AiFallbackException ex) when (attemptIndex + 1 < options.Attempts.Count)
            {
                lastError = ex;
                var next = options.Attempts[attemptIndex + 1].Provider;
                await _logService.WriteAsync($"AI chunk={chunkNumber} provider={attempt.Provider} status=failed fallback={next} reason={ex.Reason}", CancellationToken.None);
            }
        }

        throw new UserVisibleException("AI API xử lý thất bại. Kiểm tra endpoint, model hoặc API key.");
    }

    private async Task<string> RefineChunkAsync(
        AiProviderAttempt options,
        string transcript,
        int chunkNumber,
        CancellationToken cancellationToken)
    {
        Validate(options);
        var requestBody = AiTranscriptRequestFactory.Create(
            options.Provider,
            options.Model.Trim(),
            transcript,
            options.Glossary.Trim());

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint.Trim());
        request.Content = new StringContent(requestBody.Json, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        }

        await _logService.WriteAsync($"AI chunk={chunkNumber} provider={options.Provider} status=post endpoint={options.Endpoint} model={options.Model}", cancellationToken);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new AiFallbackException("timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiFallbackException("network", ex);
        }

        using (response)
        {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await _logService.WriteAsync($"AI ERROR {(int)response.StatusCode}: {responseBody}", CancellationToken.None);
            if (IsFallbackStatus(response.StatusCode))
            {
                throw new AiFallbackException($"http-{(int)response.StatusCode}");
            }

            throw new UserVisibleException("AI API xử lý thất bại. Kiểm tra endpoint, model hoặc API key.");
        }

        var refined = AiTranscriptResponseParser.Parse(options.Provider, responseBody);
        if (string.IsNullOrWhiteSpace(refined))
        {
            throw new UserVisibleException("AI API trả về nội dung trống.");
        }

        return refined;
        }
    }

    private static void Validate(AiRefinementOptions options)
    {
        if (options.Attempts.Count == 0)
        {
            throw new UserVisibleException("Chưa có provider AI hợp lệ.");
        }

        foreach (var attempt in options.Attempts)
        {
            Validate(attempt);
        }
    }

    private static void Validate(AiProviderAttempt options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint)
            || !Uri.TryCreate(options.Endpoint.Trim(), UriKind.Absolute, out _))
        {
            throw new UserVisibleException("Endpoint AI không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new UserVisibleException("Vui lòng nhập tên model AI.");
        }
    }

    private static bool IsFallbackStatus(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 401 or 402 or 403 or 408 or 429 || code >= 500;
    }

    private sealed class AiFallbackException : Exception
    {
        public AiFallbackException(string reason, Exception? innerException = null)
            : base("AI provider tạm thời lỗi, đang thử provider khác.", innerException)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }
}
