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

        public double SecondsToMature => (double)SeedSeconds + SproutSeconds;
    }

    /// <summary>One planted plant. Its stage is always derived from when it was planted.</summary>
    public sealed class PlantInstance
    {
        public PlantInstance(PlantSpec spec, int cell, long plantedAtUtcTicks)
        {
            Spec = spec;
            Cell = cell;
            PlantedAtUtcTicks = plantedAtUtcTicks;
        }

        public PlantSpec Spec { get; }
        public int Cell { get; }
        public long PlantedAtUtcTicks { get; }

        /// <summary>Last stage reported through events; used to detect stage changes.</summary>
        internal PlantStage ReportedStage;

        public PlantStage StageAt(long nowUtcTicks) => Growth.StageAt(Spec, PlantedAtUtcTicks, nowUtcTicks);

        public long MatureAtUtcTicks => PlantedAtUtcTicks + (long)(Spec.SecondsToMature * TimeSpan.TicksPerSecond);
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
