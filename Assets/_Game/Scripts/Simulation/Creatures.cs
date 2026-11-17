using System;
using System.Collections.Generic;

namespace TinyPlanet.Simulation
{
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Legendary = 3,
    }

    /// <summary>
    /// One creature species and the spawn rule that attracts it. Authored as a CreatureDefinition asset.
    /// </summary>
    public sealed class CreatureSpec
    {
        public string Id;
        public Rarity Rarity;
        public float CheckIntervalSeconds = 10f;
        public float ChancePerCheck = 0.3f;
        public int PopulationCap = 3;
        public float LeaveAfterSeconds = 90f;
        public int DiscoveryReward = 10;

        /// <summary>How much one living here adds to the planet's life, which raises the planet level.</summary>
        public float Life = 3f;

        /// <summary>All must hold around one cell (the creature's home) for the creature to come or stay.</summary>
        public List<SpawnCondition> Conditions = new List<SpawnCondition>();
    }

    /// <summary>A creature living on the planet. Where it wanders is up to the view; the simulation only knows its home.</summary>
    public sealed class CreatureInstance
    {
        public CreatureInstance(int id, CreatureSpec spec, int homeCell, long arrivedAtUtcTicks)
        {
            Id = id;
            Spec = spec;
            HomeCell = homeCell;
            ArrivedAtUtcTicks = arrivedAtUtcTicks;
        }

        public int Id { get; }
        public CreatureSpec Spec { get; }
        public int HomeCell { get; internal set; }
        public long ArrivedAtUtcTicks { get; }

        /// <summary>When its habitat stopped suiting it, or zero while it is happy.</summary>
        public long UnhappySinceUtcTicks { get; internal set; }
    }

    /// <summary>
    /// One small requirement of a spawn rule, checked around a candidate home cell.
    /// Rules combine several of these; add a new subclass only for a genuinely new kind of requirement.
    /// Only mature plants count.
    /// </summary>
    public abstract class SpawnCondition
    {
        internal abstract bool IsMet(PlanetSimulation sim, int home);
    }

    /// <summary>At least <c>MinCount</c> mature plants of one species within <c>Radius</c> steps of home.</summary>
    public sealed class PlantsNearby : SpawnCondition
    {
        public readonly string PlantId;
        public readonly int MinCount;
        public readonly int Radius;

        public PlantsNearby(string plantId, int minCount, int radius)
        {
            PlantId = plantId;
            MinCount = Math.Max(1, minCount);
            Radius = Math.Max(0, radius);
        }

        internal override bool IsMet(PlanetSimulation sim, int home)
        {
            int found = 0;
            foreach (int cell in sim.Planet.Grid.CellsWithin(home, Radius))
            {
                if (sim.IsMatureAt(cell, PlantId) && ++found >= MinCount) return true;
            }
            return false;
        }
    }

    /// <summary>A water cell within <c>Radius</c> steps of home.</summary>
    public sealed class NearWater : SpawnCondition
    {
        public readonly int Radius;

        public NearWater(int radius) => Radius = Math.Max(0, radius);

        internal override bool IsMet(PlanetSimulation sim, int home)
        {
            foreach (int cell in sim.Planet.Grid.CellsWithin(home, Radius))
            {
                if (sim.Planet.IsWater(cell)) return true;
            }
            return false;
        }
    }

    /// <summary>Within <c>Radius</c> steps of home, a mature plant A stands right next to a mature plant B.</summary>
    public sealed class PlantsTouching : SpawnCondition
    {
        public readonly string PlantA;
        public readonly string PlantB;
        public readonly int Radius;

        public PlantsTouching(string plantA, string plantB, int radius)
        {
            PlantA = plantA;
            PlantB = plantB;
            Radius = Math.Max(0, radius);
        }

        internal override bool IsMet(PlanetSimulation sim, int home)
        {
            var grid = sim.Planet.Grid;
            foreach (int cell in grid.CellsWithin(home, Radius))
            {
                if (!sim.IsMatureAt(cell, PlantA)) continue;
                foreach (int next in grid.Neighbours(cell))
                {
                    if (sim.IsMatureAt(next, PlantB)) return true;
                }
            }
            return false;
        }
    }

    /// <summary>At least one creature of another species already lives on the planet.</summary>
    public sealed class CreatureLivesHere : SpawnCondition
    {
        public readonly string CreatureId;

        public CreatureLivesHere(string creatureId) => CreatureId = creatureId;

        internal override bool IsMet(PlanetSimulation sim, int home) => sim.CountCreatures(CreatureId) > 0;
    }

    /// <summary>Home is in one particular biome.</summary>
    public sealed class InBiome : SpawnCondition
    {
        public readonly string BiomeId;

        public InBiome(string biomeId) => BiomeId = biomeId;

        internal override bool IsMet(PlanetSimulation sim, int home) => sim.Planet.BiomeOf(home).Id == BiomeId;
    }
}
