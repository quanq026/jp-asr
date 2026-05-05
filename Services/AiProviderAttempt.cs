using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed record AiProviderAttempt(
    AiProvider Provider,
    string Endpoint,
    string Model,
    string ApiKey,
    string Glossary);
