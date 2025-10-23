using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HistorySim.Game;

public sealed class TileAtlas
{
    private readonly Dictionary<BiomeType, string> _sprites;
    private readonly string _defaultSprite;

    private TileAtlas(Dictionary<BiomeType, string> sprites, string defaultSprite)
    {
        _sprites = sprites;
        _defaultSprite = defaultSprite;
    }

    public string GetSprite(BiomeType biome)
    {
        if (_sprites.TryGetValue(biome, out var sprite))
        {
            return sprite;
        }

        return _defaultSprite;
    }

    public static TileAtlas Empty { get; } = new(new Dictionary<BiomeType, string>(), string.Empty);

    public static async Task<TileAtlas> LoadAsync(HttpClient httpClient, string requestUri)
    {
        using var stream = await httpClient.GetStreamAsync(requestUri);
        var definition = await JsonSerializer.DeserializeAsync<TileAtlasDefinition>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to parse tile atlas definition.");

        if (definition.Biomes is null || definition.Biomes.Count == 0)
        {
            throw new InvalidOperationException("Tile atlas definition must contain at least one biome entry.");
        }

        var map = new Dictionary<BiomeType, string>();
        foreach (var (name, spritePath) in definition.Biomes)
        {
            if (!Enum.TryParse<BiomeType>(name, ignoreCase: true, out var biome))
            {
                continue;
            }

            map[biome] = spritePath;
        }

        var defaultSprite = definition.DefaultSprite ?? map.Values.FirstOrDefault() ?? string.Empty;
        return new TileAtlas(map, defaultSprite);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class TileAtlasDefinition
    {
        public string? DefaultSprite { get; set; }
        public Dictionary<string, string>? Biomes { get; set; }
    }
}
