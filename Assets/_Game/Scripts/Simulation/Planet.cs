using System.Collections.Generic;

namespace TinyPlanet.Simulation
{
    /// <summary>Static, generated properties of one surface cell.</summary>
    public struct CellData
    {
        /// <summary>Index into <see cref="Planet.Biomes"/>.</summary>
        public int Biome;
        public float Moisture;
        public float Fertility;

        /// <summary>Pond water: nothing can be planted here, but some creatures like to live nearby.</summary>
        public bool Water;
    }

    /// <summary>A generated planet: fully determined by its seed, cell count and biome list.</summary>
    public sealed class Planet
    {
        public Planet(int seed, PlanetGrid grid, CellData[] cells, IReadOnlyList<BiomeSpec> biomes)
        {
            Seed = seed;
            Grid = grid;
            Cells = cells;
            Biomes = biomes;
        }

        public int Seed { get; }
        public PlanetGrid Grid { get; }
        public CellData[] Cells { get; }
        public IReadOnlyList<BiomeSpec> Biomes { get; }

        public bool IsWater(int cell) => Cells[cell].Water;

        public BiomeSpec BiomeOf(int cell) => Biomes[Cells[cell].Biome];

        public int CountCells(int biome)
        {
            int count = 0;
            foreach (var cell in Cells)
            {
                if (cell.Biome == biome) count++;
            }
            return count;
        }
    }
}
