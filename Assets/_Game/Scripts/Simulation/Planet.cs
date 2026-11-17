namespace TinyPlanet.Simulation
{
    public enum BiomeType
    {
        Meadow = 0,
    }

    /// <summary>Static, generated properties of one surface cell.</summary>
    public struct CellData
    {
        public BiomeType Biome;
        public float Moisture;
        public float Fertility;

        /// <summary>Pond water: nothing can be planted here, but some creatures like to live nearby.</summary>
        public bool Water;
    }

    /// <summary>A generated planet: fully determined by its seed and cell count.</summary>
    public sealed class Planet
    {
        public Planet(int seed, PlanetGrid grid, CellData[] cells)
        {
            Seed = seed;
            Grid = grid;
            Cells = cells;
        }

        public int Seed { get; }
        public PlanetGrid Grid { get; }
        public CellData[] Cells { get; }

        public bool IsWater(int cell) => Cells[cell].Water;
    }
}
