using System.Collections.Generic;

namespace TinyPlanet.Simulation
{
    /// <summary>
    /// One kind of land: how the generator lays it out, which plants grow on it and what it costs to wake.
    /// Authored as a BiomeDefinition asset.
    /// </summary>
    public sealed class BiomeSpec
    {
        public string Id = "meadow";

        /// <summary>Relative share of the surface this biome gets from the generator.</summary>
        public float Weight = 1f;

        /// <summary>Fraction of this biome's cells that become pond water.</summary>
        public float WaterFraction;

        public float BaseMoisture = 0.5f;
        public float BaseFertility = 0.5f;

        /// <summary>Plant ids that may grow here, or null for any plant.</summary>
        public HashSet<string> AllowedPlants;

        /// <summary>Planet level needed before it can be woken.</summary>
        public int UnlockLevel = 1;

        /// <summary>Energy it costs to wake. A biome that is free at level one is awake from the start.</summary>
        public int UnlockCost;

        public bool StartsUnlocked => UnlockCost <= 0 && UnlockLevel <= 1;

        /// <summary>A phrase for this land in generated codex hints, e.g. "the marsh".</summary>
        public string HintNoun;

        public BiomeSpec Copy()
        {
            var copy = (BiomeSpec)MemberwiseClone();
            if (AllowedPlants != null) copy.AllowedPlants = new HashSet<string>(AllowedPlants);
            return copy;
        }

        public bool Allows(string plantId) => AllowedPlants == null || AllowedPlants.Contains(plantId ?? string.Empty);

        /// <summary>A single biome where anything grows, with a few ponds. Used when no biomes are given.</summary>
        public static BiomeSpec Default() => new BiomeSpec { Id = "meadow", WaterFraction = 0.07f };
    }
}
