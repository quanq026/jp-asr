namespace JapaneseASR.Services;

public sealed record AiProviderPreset(string Endpoint, string Model, string ApiKey, string Glossary)
{
    public static AiProviderPreset Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);
}
