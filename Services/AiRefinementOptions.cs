using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed record AiRefinementOptions(
    bool Enabled,
    IReadOnlyList<AiProviderAttempt> Attempts)
{
    public AiRefinementOptions(
        bool enabled,
        AiProvider provider,
        string endpoint,
        string model,
        string apiKey,
        string glossary)
        : this(enabled, [new AiProviderAttempt(provider, endpoint, model, apiKey, glossary)])
    {
    }

    public static AiRefinementOptions Disabled { get; } = new(
        false,
        []);
}
