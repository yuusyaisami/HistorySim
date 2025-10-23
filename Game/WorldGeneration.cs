using System;
using System.Collections.Generic;
using System.Linq;

namespace HistorySim.Game;

public sealed class WorldGenerator
{
    public GameWorld Generate(WorldGenerationSettings settings)
    {
        if (settings.Width <= 0 || settings.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "World dimensions must be positive.");
        }

        var seed = settings.Seed != 0 ? settings.Seed : Random.Shared.Next();
        settings = settings with { Seed = seed };

        var elevationNoise = new FractalNoise(seed + 11, settings.ElevationNoise);
        var temperatureNoise = new FractalNoise(seed + 37, settings.TemperatureNoise);
        var moistureNoise = new FractalNoise(seed + 53, settings.MoistureNoise);

        var tiles = new WorldTile[settings.Width, settings.Height];

        for (var y = 0; y < settings.Height; y++)
        {
            for (var x = 0; x < settings.Width; x++)
            {
                var position = new GridPosition(x, y);
                var nx = (double)x / settings.Width;
                var ny = (double)y / settings.Height;

                var elevation = elevationNoise.Sample(nx, ny);
                var isLand = elevation >= settings.SeaLevel;

                var latitude = 1.0 - Math.Abs((ny * 2.0) - 1.0); // 1 at equator, 0 at poles

                var temperature = temperatureNoise.Sample(nx, ny);
                temperature = Clamp01(temperature * 0.65 + latitude * 0.35);

                var moisture = moistureNoise.Sample(nx, ny);
                moisture = Clamp01(moisture);

                var biome = DetermineBiome(isLand, elevation, temperature, moisture, settings.SeaLevel);

                tiles[x, y] = new WorldTile(position, elevation, temperature, moisture, isLand, biome);
            }
        }

        var map = new WorldMap(tiles);
        return new GameWorld(settings, map);
    }

    private static BiomeType DetermineBiome(bool isLand, double elevation, double temperature, double moisture, double seaLevel)
    {
        if (!isLand)
        {
            var delta = seaLevel - elevation;
            return delta > 0.12 ? BiomeType.DeepOcean : delta > 0.04 ? BiomeType.Ocean : BiomeType.Coast;
        }

        if (temperature < 0.28)
        {
            return BiomeType.Snow;
        }

        if (temperature > 0.68)
        {
            return moisture < 0.35 ? BiomeType.Desert : BiomeType.Jungle;
        }

        if (moisture > 0.72)
        {
            return BiomeType.Jungle;
        }

        if (moisture < 0.28)
        {
            return BiomeType.Desert;
        }

        return BiomeType.Plains;
    }

    private static double Clamp01(double value)
        => Math.Clamp(value, 0.0, 1.0);
}

public sealed record WorldGenerationSettings
{
    public int Width { get; init; } = 96;
    public int Height { get; init; } = 54;
    public int Seed { get; init; } = Environment.TickCount;
    public double SeaLevel { get; init; } = 0.52;
    public NoiseSettings ElevationNoise { get; init; } = NoiseSettings.ElevationDefault;
    public NoiseSettings TemperatureNoise { get; init; } = NoiseSettings.TemperatureDefault;
    public NoiseSettings MoistureNoise { get; init; } = NoiseSettings.MoistureDefault;

    public static WorldGenerationSettings Default => new();
}

public readonly record struct NoiseSettings(double Frequency, int Octaves, double Persistence, double Lacunarity)
{
    public static NoiseSettings ElevationDefault { get; } = new(1.35, 5, 0.48, 2.15);
    public static NoiseSettings TemperatureDefault { get; } = new(0.9, 4, 0.55, 2.0);
    public static NoiseSettings MoistureDefault { get; } = new(0.8, 4, 0.58, 2.2);
}

public sealed class GameWorld
{
    internal GameWorld(WorldGenerationSettings settings, WorldMap map)
    {
        Settings = settings;
        Map = map;
        Entities = new List<GameEntity>(capacity: 8);
    }

    public WorldGenerationSettings Settings { get; }
    public WorldMap Map { get; }
    public IList<GameEntity> Entities { get; }
}

public abstract class GameEntity
{
    protected GameEntity()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }
}

public sealed class WorldMap
{
    private readonly WorldTile[,] _tiles;

    internal WorldMap(WorldTile[,] tiles)
    {
        _tiles = tiles;
        Width = tiles.GetLength(0);
        Height = tiles.GetLength(1);
    }

    public int Width { get; }
    public int Height { get; }

    public WorldTile this[int x, int y] => _tiles[x, y];

    public WorldTile this[GridPosition position] => _tiles[position.X, position.Y];

    public IEnumerable<WorldTile> Tiles => _tiles.Cast<WorldTile>();
}

public readonly record struct WorldTile(GridPosition Position, double Elevation, double Temperature, double Moisture, bool IsLand, BiomeType Biome);

public readonly record struct GridPosition(int X, int Y)
{
    public static GridPosition operator +(GridPosition left, GridPosition right)
        => new(left.X + right.X, left.Y + right.Y);
}

public enum BiomeType
{
    DeepOcean,
    Ocean,
    Coast,
    Plains,
    Desert,
    Jungle,
    Snow
}

internal sealed class FractalNoise
{
    private readonly int _seed;
    private readonly NoiseSettings _settings;

    public FractalNoise(int seed, NoiseSettings settings)
    {
        _seed = seed;
        _settings = settings;
    }

    public double Sample(double x, double y)
    {
        double total = 0;
        double amplitude = 1;
        double frequency = _settings.Frequency;
        double max = 0;

        for (var octave = 0; octave < _settings.Octaves; octave++)
        {
            total += ValueNoise(_seed + octave * 17, x * frequency, y * frequency) * amplitude;
            max += amplitude;
            amplitude *= _settings.Persistence;
            frequency *= _settings.Lacunarity;
        }

        if (max <= 0)
        {
            return 0;
        }

        return Math.Clamp(total / max, 0.0, 1.0);
    }

    private static double ValueNoise(int seed, double x, double y)
    {
        var xi = (int)Math.Floor(x);
        var yi = (int)Math.Floor(y);

        var xf = x - xi;
        var yf = y - yi;

        var v00 = RandomGradient(seed, xi, yi);
        var v10 = RandomGradient(seed, xi + 1, yi);
        var v01 = RandomGradient(seed, xi, yi + 1);
        var v11 = RandomGradient(seed, xi + 1, yi + 1);

        var i1 = Lerp(v00, v10, Smooth(xf));
        var i2 = Lerp(v01, v11, Smooth(xf));
        return Lerp(i1, i2, Smooth(yf));
    }

    private static double RandomGradient(int seed, int x, int y)
    {
        unchecked
        {
            var h = (uint)seed;
            h ^= 374761393u * (uint)x;
            h ^= 668265263u * (uint)y;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;

    private static double Smooth(double t)
        => t * t * (3 - 2 * t);
}
