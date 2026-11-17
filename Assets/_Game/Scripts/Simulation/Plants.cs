using System;

namespace TinyPlanet.Simulation
{
    public enum PlantStage
    {
        Seed = 0,
        Sprout = 1,
        Mature = 2,
    }

    /// <summary>Tunable numbers for one plant species. Authored as a PlantDefinition asset.</summary>
    public sealed class PlantSpec
    {
        public string Id;
        public int SeedCost;
        public float SeedSeconds;
        public float SproutSeconds;
        public float EnergyPerMinute;
        public float RefundFraction;
        public int DiscoveryReward;

        /// <summary>How much a mature one adds to the planet's life, which raises the planet level.</summary>
        public float Life = 1f;

        /// <summary>Planet level needed before the seed can be bought.</summary>
        public int UnlockLevel = 1;

        /// <summary>Energy the seed costs to unlock. A seed that is free at level one is known from the start.</summary>
        public int UnlockCost;

        public bool StartsUnlocked => UnlockCost <= 0 && UnlockLevel <= 1;

        /// <summary>A vague phrase for this plant in generated codex hints, e.g. "soft moss".</summary>
        public string HintNoun;

        public double SecondsToMature => (double)SeedSeconds + SproutSeconds;
    }

    /// <summary>
    /// One planted plant. Its stage is always derived from when it was planted; the sky's forecast (rain makes
    /// plants grow faster) fixes when it sprouts and matures the moment it is planted.
    /// </summary>
    public sealed class PlantInstance
    {
        public PlantInstance(PlantSpec spec, int cell, long plantedAtUtcTicks, Sky sky = null)
        {
            Spec = spec;
            Cell = cell;
            PlantedAtUtcTicks = plantedAtUtcTicks;
            if (sky == null)
            {
                SproutAtUtcTicks = plantedAtUtcTicks + (long)(spec.SeedSeconds * (double)TimeSpan.TicksPerSecond);
                MatureAtUtcTicks = plantedAtUtcTicks + (long)(spec.SecondsToMature * TimeSpan.TicksPerSecond);
            }
            else
            {
                SproutAtUtcTicks = sky.AfterGrowth(plantedAtUtcTicks, spec.SeedSeconds);
                MatureAtUtcTicks = sky.AfterGrowth(plantedAtUtcTicks, spec.SecondsToMature);
            }
        }

        public PlantSpec Spec { get; }
        public int Cell { get; }
        public long PlantedAtUtcTicks { get; }

        /// <summary>Last stage reported through events; used to detect stage changes.</summary>
        internal PlantStage ReportedStage;

        public long SproutAtUtcTicks { get; }
        public long MatureAtUtcTicks { get; }

        public PlantStage StageAt(long nowUtcTicks) =>
            nowUtcTicks < SproutAtUtcTicks ? PlantStage.Seed : nowUtcTicks < MatureAtUtcTicks ? PlantStage.Sprout : PlantStage.Mature;

        /// <summary>Seconds until the plant is mature; zero once it is.</summary>
        public double SecondsUntilMature(long nowUtcTicks) => Math.Max(0.0, (MatureAtUtcTicks - nowUtcTicks) / (double)TimeSpan.TicksPerSecond);
    }

    public static class Growth
    {
        public static PlantStage StageAt(PlantSpec spec, long plantedAtUtcTicks, long nowUtcTicks)
        {
            double elapsed = (nowUtcTicks - plantedAtUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (elapsed < spec.SeedSeconds) return PlantStage.Seed;
            if (elapsed < spec.SecondsToMature) return PlantStage.Sprout;
            return PlantStage.Mature;
        }

        /// <summary>Seconds until the plant is mature; zero once it is.</summary>
        public static double SecondsUntilMature(PlantSpec spec, long plantedAtUtcTicks, long nowUtcTicks)
        {
            double elapsed = (nowUtcTicks - plantedAtUtcTicks) / (double)TimeSpan.TicksPerSecond;
            return Math.Max(0.0, spec.SecondsToMature - elapsed);
        }
    }
}
