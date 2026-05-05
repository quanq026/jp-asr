using JapaneseASR.Models;
using JapaneseASR.Services;
using System.Net;

var tests = new (string Name, Action Body)[]
{
    ("Normalize joins Whisper text into one readable paragraph", TestNormalizeText),
    ("ResolveUniquePrefix avoids overwriting existing outputs", TestUniqueOutputPrefix),
    ("ResolveUniquePrefix handles invalid or empty input names", TestInvalidInputName),
    ("AI request builder creates Ollama native chat payload", TestBuildsOllamaPayload),
    ("AI response parser reads OpenAI compatible content", TestParsesOpenAiCompatibleResponse),
    ("AI response parser reads Ollama native content", TestParsesOllamaResponse),
    ("AI local settings loader reads NVIDIA NIM config", TestLoadsAiLocalSettings),
    ("AI local settings loader reads provider presets", TestLoadsAiProviderPresets),
    ("AI transcript chunker splits long text at punctuation", TestChunksLongTranscript),
    ("AI local settings loader reads fallback providers", TestLoadsAiFallbackProviders),
    ("AI refiner uses primary provider when it succeeds", TestAiRefinerUsesPrimaryProvider),
    ("AI refiner falls back per chunk on eligible provider failure", TestAiRefinerFallsBackPerChunk),
    ("AI refiner does not fallback on malformed provider response", TestAiRefinerDoesNotFallbackOnMalformedResponse),
    ("OutputNameResolver sanitizes non-ASCII and problematic chars for external tool safety", TestSanitizeOutputPrefix),
    ("DeepSeek uses OpenAI-compatible request format", TestDeepSeekRequestFormat),
    ("DeepSeek response parsed as OpenAI-compatible", TestDeepSeekResponseParsing),
    ("AI system prompt includes SRT format instruction", TestSrtSystemPrompt)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static void TestNormalizeText()
{
    var input = """
    [00:00:00.000 --> 00:00:02.000]  こんにちは 。
    [00:00:02.000 --> 00:00:04.000]  日本語 の テスト です

    """;

    var actual = TextPostProcessor.Normalize(input);
    AssertEqual($"こんにちは。日本語 の テスト です{Environment.NewLine}", actual);
}

static void TestUniqueOutputPrefix()
{
    var directory = CreateTempDirectory();
    try
    {
        File.WriteAllText(Path.Combine(directory, "video.srt"), "exists");
        var prefix = OutputNameResolver.ResolveUniquePrefix(directory, @"C:\input\video.mp4");
        AssertEqual(Path.Combine(directory, "video-1"), prefix);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestInvalidInputName()
{
    var directory = CreateTempDirectory();
    try
    {
        var prefix = OutputNameResolver.ResolveUniquePrefix(directory, "   .mp4");
        AssertEqual(Path.Combine(directory, "ket-qua"), prefix);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestBuildsOllamaPayload()
{
    var request = AiTranscriptRequestFactory.Create(
        AiProvider.Ollama,
        "qwen2.5:7b",
        "今日はJLPTのN4について話します。",
        "JLPT, N4, 自動詞, 他動詞");

    AssertContains("\"model\":\"qwen2.5:7b\"", request.Json);
    AssertContains("\"stream\":false", request.Json);
    AssertContains("Không dịch", request.Json);
    AssertContains("自動詞", request.Json);
}

static void TestParsesOpenAiCompatibleResponse()
{
    var json = """
    {"choices":[{"message":{"content":"修正済みテキスト"}}]}
    """;

    var actual = AiTranscriptResponseParser.Parse(AiProvider.NvidiaNim, json);
    AssertEqual("修正済みテキスト", actual);
}

static void TestParsesOllamaResponse()
{
    var json = """
    {"message":{"role":"assistant","content":"修正済みテキスト"},"done":true}
    """;

    var actual = AiTranscriptResponseParser.Parse(AiProvider.Ollama, json);
    AssertEqual("修正済みテキスト", actual);
}

static void TestLoadsAiLocalSettings()
{
    var directory = CreateTempDirectory();
    try
    {
        var path = Path.Combine(directory, "ai.local.json");
        File.WriteAllText(path, """
        {
          "enabled": true,
          "provider": "NvidiaNim",
          "endpoint": "https://integrate.api.nvidia.com/v1/chat/completions",
          "model": "meta/llama-3.1-8b-instruct",
          "apiKey": "secret-key",
          "glossary": "JLPT, 自動詞"
        }
        """);

        var settings = AiLocalSettings.Load(path);
        AssertEqual("True", settings.Enabled.ToString());
        AssertEqual(AiProvider.NvidiaNim.ToString(), settings.Provider.ToString());
        AssertEqual("https://integrate.api.nvidia.com/v1/chat/completions", settings.Endpoint);
        AssertEqual("meta/llama-3.1-8b-instruct", settings.Model);
        AssertEqual("secret-key", settings.ApiKey);
        AssertEqual("JLPT, 自動詞", settings.Glossary);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestLoadsAiProviderPresets()
{
    var directory = CreateTempDirectory();
    try
    {
        var path = Path.Combine(directory, "ai.local.json");
        File.WriteAllText(path, """
        {
          "enabled": true,
          "activeProvider": "Ollama",
          "providers": {
            "Ollama": {
              "endpoint": "https://ollama.com/api/chat",
              "model": "gpt-oss:20b",
              "apiKey": "ollama-key"
            },
            "NvidiaNim": {
              "endpoint": "https://integrate.api.nvidia.com/v1/chat/completions",
              "model": "meta/llama-3.1-8b-instruct",
              "apiKey": "nim-key"
            }
          }
        }
        """);

        var settings = AiLocalSettings.Load(path);
        AssertEqual(AiProvider.Ollama.ToString(), settings.Provider.ToString());
        AssertEqual("https://ollama.com/api/chat", settings.Endpoint);
        AssertEqual("gpt-oss:20b", settings.Model);
        AssertEqual("ollama-key", settings.ApiKey);

        var nim = settings.GetPreset(AiProvider.NvidiaNim);
        AssertEqual("https://integrate.api.nvidia.com/v1/chat/completions", nim.Endpoint);
        AssertEqual("meta/llama-3.1-8b-instruct", nim.Model);
        AssertEqual("nim-key", nim.ApiKey);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestChunksLongTranscript()
{
    var text = "これは最初の文です。これは二番目の文です。これは三番目の長い文です。";
    var chunks = AiTranscriptChunker.Split(text, 18).ToArray();
    AssertEqual("3", chunks.Length.ToString());
    AssertEqual("これは最初の文です。", chunks[0]);
    AssertEqual("これは二番目の文です。", chunks[1]);
    AssertEqual("これは三番目の長い文です。", chunks[2]);
}

static void TestLoadsAiFallbackProviders()
{
    var directory = CreateTempDirectory();
    try
    {
        var path = Path.Combine(directory, "ai.local.json");
        File.WriteAllText(path, """
        {
          "enabled": true,
          "activeProvider": "Ollama",
          "fallbackProviders": ["NvidiaNim"],
          "providers": {
            "Ollama": {
              "endpoint": "https://ollama.com/api/chat",
              "model": "gpt-oss:20b",
              "apiKey": "ollama-key"
            },
            "NvidiaNim": {
              "endpoint": "https://integrate.api.nvidia.com/v1/chat/completions",
              "model": "meta/llama-3.1-8b-instruct",
              "apiKey": "nim-key"
            }
          }
        }
        """);

        var settings = AiLocalSettings.Load(path);
        AssertEqual("1", settings.FallbackProviders.Count.ToString());
        AssertEqual(AiProvider.NvidiaNim.ToString(), settings.FallbackProviders[0].ToString());
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestAiRefinerUsesPrimaryProvider()
{
    var handler = new FakeHttpHandler(request =>
    {
        AssertContains("ollama.com", request.RequestUri?.Host ?? string.Empty);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"message":{"content":"primary-ok"},"done":true}""")
        };
    });
    var refiner = CreateRefiner(handler);
    var options = CreateRotateOptions();

    var actual = refiner.RefineAsync(options, "chunk one.", CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual($"primary-ok{Environment.NewLine}", actual);
    AssertEqual("1", handler.Requests.Count.ToString());
}

static void TestAiRefinerFallsBackPerChunk()
{
    var handler = new FakeHttpHandler(request =>
    {
        if (request.RequestUri?.Host == "ollama.com")
        {
            return new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("quota")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"content":"fallback-ok"}}]}""")
        };
    });
    var refiner = CreateRefiner(handler);
    var options = CreateRotateOptions();

    var actual = refiner.RefineAsync(options, "chunk one.", CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual($"fallback-ok{Environment.NewLine}", actual);
    AssertEqual("2", handler.Requests.Count.ToString());
    AssertContains("ollama.com", handler.Requests[0].RequestUri?.Host ?? string.Empty);
    AssertContains("integrate.api.nvidia.com", handler.Requests[1].RequestUri?.Host ?? string.Empty);
}

static void TestSanitizeOutputPrefix()
{
    // Japanese + brackets + parentheses in filename - all should be sanitized
    var directory = CreateTempDirectory();
    try
    {
        var prefix = OutputNameResolver.ResolveUniquePrefix(
            directory,
            @"C:\Users\Administrator\Downloads\会話のアドバイス_Get better slowly (Japanese Podcast for Listening Practice) [C9VabhxOPbA] (1).mp3");

        var fileName = Path.GetFileName(prefix);
        // Must NOT contain Japanese characters
        AssertDoesNotContain("会話", prefix);
        AssertDoesNotContain("アドバイス", prefix);
        // Must NOT contain brackets
        AssertDoesNotContain("[", prefix);
        AssertDoesNotContain("]", prefix);
        // Must NOT contain parentheses
        AssertDoesNotContain("(", prefix);
        AssertDoesNotContain(")", prefix);
        // Must preserve ASCII meaningful parts
        AssertContains("Get_better_slowly", prefix);
        AssertContains("Japanese_Podcast", prefix);
        AssertContains("C9VabhxOPbA", prefix);
        // Should not start with underscore from stripped prefix chars
        AssertTrue(!fileName.StartsWith('_'), "Should not start with underscore");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestDeepSeekRequestFormat()
{
    var request = AiTranscriptRequestFactory.Create(
        AiProvider.DeepSeek,
        "deepseek-v4-pro",
        "こんにちは。日本語のテストです。",
        "JLPT, N4");

    // DeepSeek uses OpenAI-compatible format (no Ollama-specific fields)
    AssertContains("\"model\":\"deepseek-v4-pro\"", request.Json);
    AssertContains("\"temperature\"", request.Json);
    AssertContains("\"messages\"", request.Json);
    AssertContains("Không dịch", request.Json);
    AssertContains("JLPT, N4", request.Json);
    // Must NOT contain Ollama-specific "stream" or "options" fields
    AssertDoesNotContain("\"stream\"", request.Json);
    AssertDoesNotContain("\"options\"", request.Json);
}

static void TestDeepSeekResponseParsing()
{
    var json = """
    {"choices":[{"message":{"content":"修正済みテキスト"}}]}
    """;

    var actual = AiTranscriptResponseParser.Parse(AiProvider.DeepSeek, json);
    AssertEqual("修正済みテキスト", actual);
}

static void TestSrtSystemPrompt()
{
    var request = AiTranscriptRequestFactory.Create(
        AiProvider.DeepSeek,
        "deepseek-v4-flash",
        "1\r\n00:00:01,000 --> 00:00:03,000\r\nこんにちは\r\n",
        "JLPT");

    // Must contain SRT-specific instructions (not present in old prompt)
    AssertContains("SRT", request.Json);
    AssertContains("timestamp", request.Json);
    AssertContains("số thứ tự", request.Json);
    // Must still include glossary and key rules
    AssertContains("JLPT", request.Json);
    AssertContains("Không dịch", request.Json);
}

static void TestAiRefinerDoesNotFallbackOnMalformedResponse()
{
    var handler = new FakeHttpHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"bad":true}""")
    });
    var refiner = CreateRefiner(handler);
    var options = CreateRotateOptions();

    try
    {
        _ = refiner.RefineAsync(options, "chunk one.", CancellationToken.None).GetAwaiter().GetResult();
        throw new InvalidOperationException("Expected malformed response to fail.");
    }
    catch (UserVisibleException)
    {
        AssertEqual("1", handler.Requests.Count.ToString());
    }
}

static AiTranscriptRefiner CreateRefiner(FakeHttpHandler handler)
{
    var directory = CreateTempDirectory();
    var paths = new PortablePaths(directory);
    paths.EnsureWritableDirectories();
    return new AiTranscriptRefiner(new HttpClient(handler), new LogService(paths));
}

static AiRefinementOptions CreateRotateOptions()
{
    return new AiRefinementOptions(
        true,
        [
            new AiProviderAttempt(
                AiProvider.Ollama,
                "https://ollama.com/api/chat",
                "gpt-oss:20b",
                "ollama-key",
                "JLPT"),
            new AiProviderAttempt(
                AiProvider.NvidiaNim,
                "https://integrate.api.nvidia.com/v1/chat/completions",
                "meta/llama-3.1-8b-instruct",
                "nim-key",
                "JLPT")
        ]);
}

static string CreateTempDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "JapaneseASR.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected: <{expected}> Actual: <{actual}>");
    }
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected to contain: <{expected}> Actual: <{actual}>");
    }
}

static void AssertDoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected NOT to contain: <{unexpected}> Actual: <{actual}>");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected true but was false: {message}");
    }
}

internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_handler(request));
    }
}
