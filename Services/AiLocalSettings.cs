using System.IO;
using System.Text.Json;
using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed record AiLocalSettings(
    bool Enabled,
    AiProvider Provider,
    string Endpoint,
    string Model,
    string ApiKey,
    string Glossary,
    IReadOnlyList<AiProvider> FallbackProviders,
    IReadOnlyDictionary<AiProvider, AiProviderPreset>? Presets = null)
{
    public static AiLocalSettings Empty { get; } = new(
        false,
        AiProvider.Ollama,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        []);

    public static AiLocalSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<AiLocalSettingsDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dto is null)
        {
            return Empty;
        }

        var providerText = string.IsNullOrWhiteSpace(dto.ActiveProvider) ? dto.Provider : dto.ActiveProvider;
        var provider = Enum.TryParse<AiProvider>(providerText, ignoreCase: true, out var parsedProvider)
            ? parsedProvider
            : AiProvider.Ollama;

        var presets = BuildPresets(dto);
        var fallbackProviders = BuildFallbackProviders(dto);
        if (presets.TryGetValue(provider, out var activePreset))
        {
            return new AiLocalSettings(
                dto.Enabled,
                provider,
                activePreset.Endpoint,
                activePreset.Model,
                activePreset.ApiKey,
                string.IsNullOrWhiteSpace(activePreset.Glossary) ? dto.Glossary ?? string.Empty : activePreset.Glossary,
                fallbackProviders,
                presets);
        }

        return new AiLocalSettings(
            dto.Enabled,
            provider,
            dto.Endpoint ?? string.Empty,
            dto.Model ?? string.Empty,
            dto.ApiKey ?? string.Empty,
            dto.Glossary ?? string.Empty,
            fallbackProviders,
            presets);
    }

    public AiProviderPreset GetPreset(AiProvider provider)
    {
        if (Presets is not null && Presets.TryGetValue(provider, out var preset))
        {
            return preset;
        }

        if (provider == Provider)
        {
            return new AiProviderPreset(Endpoint, Model, ApiKey, Glossary);
        }

        return AiProviderPreset.Empty;
    }

    private static Dictionary<AiProvider, AiProviderPreset> BuildPresets(AiLocalSettingsDto dto)
    {
        var presets = new Dictionary<AiProvider, AiProviderPreset>();
        if (dto.Providers is null)
        {
            return presets;
        }

        foreach (var item in dto.Providers)
        {
            if (!Enum.TryParse<AiProvider>(item.Key, ignoreCase: true, out var provider) || item.Value is null)
            {
                continue;
            }

            presets[provider] = new AiProviderPreset(
                item.Value.Endpoint ?? string.Empty,
                item.Value.Model ?? string.Empty,
                item.Value.ApiKey ?? string.Empty,
                item.Value.Glossary ?? string.Empty);
        }

        return presets;
    }

    private static IReadOnlyList<AiProvider> BuildFallbackProviders(AiLocalSettingsDto dto)
    {
        if (dto.FallbackProviders is null || dto.FallbackProviders.Count == 0)
        {
            return [];
        }

        var providers = new List<AiProvider>();
        foreach (var value in dto.FallbackProviders)
        {
            if (Enum.TryParse<AiProvider>(value, ignoreCase: true, out var provider))
            {
                providers.Add(provider);
            }
        }

        return providers;
    }

    private sealed class AiLocalSettingsDto
    {
        public bool Enabled { get; set; }
        public string? Provider { get; set; }
        public string? ActiveProvider { get; set; }
        public string? Endpoint { get; set; }
        public string? Model { get; set; }
        public string? ApiKey { get; set; }
        public string? Glossary { get; set; }
        public List<string>? FallbackProviders { get; set; }
        public Dictionary<string, AiProviderPresetDto?>? Providers { get; set; }
    }

    private sealed class AiProviderPresetDto
    {
        public string? Endpoint { get; set; }
        public string? Model { get; set; }
        public string? ApiKey { get; set; }
        public string? Glossary { get; set; }
    }
}
