using System;
using System.Collections.Generic;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Data
{
    public enum CreatureShape
    {
        Hopper,
        Flutter,
        Crawler,
        Climber,
        Glow,
        Shell,
    }

    public enum ConditionKind
    {
        [Tooltip("At least Count mature Plant within Radius steps of home.")]
        PlantsNearby,
        [Tooltip("A water cell within Radius steps of home.")]
        NearWater,
        [Tooltip("Within Radius steps of home, a mature Plant stands next to a mature Other Plant.")]
        PlantsTouching,
        [Tooltip("At least one Creature already lives on the planet.")]
        CreatureLivesHere,
        [Tooltip("Home is in Biome.")]
        InBiome,
    }

    /// <summary>One requirement of a spawn rule, editable in the Inspector. Fields unused by the kind are ignored.</summary>
    [Serializable]
    public sealed class SpawnConditionData
    {
        public ConditionKind kind;
        public PlantDefinition plant;
        public PlantDefinition otherPlant;
        public CreatureDefinition creature;
        public BiomeDefinition biome;
        [Min(1)] public int count = 1;
        [Min(0)] public int radius = 1;

        public SpawnCondition ToCondition()
        {
            switch (kind)
            {
                case ConditionKind.PlantsNearby: return new PlantsNearby(plant != null ? plant.id : null, count, radius);
                case ConditionKind.NearWater: return new NearWater(radius);
                case ConditionKind.PlantsTouching:
                    return new PlantsTouching(plant != null ? plant.id : null, otherPlant != null ? otherPlant.id : null, radius);
                case ConditionKind.CreatureLivesHere: return new CreatureLivesHere(creature != null ? creature.id : null);
                case ConditionKind.InBiome: return new InBiome(biome != null ? biome.id : null);
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>One creature species: its spawn rule, codex text and placeholder look.</summary>
    [CreateAssetMenu(menuName = "Tiny Planet/Creature", fileName = "Creature")]
    public sealed class CreatureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into save files. Never change it after release.")]
        public string id = "creature";
        public string displayName = "Creature";
        public Rarity rarity = Rarity.Common;

        [Header("Codex")]
        [Tooltip("Shown while undiscovered. Vague on purpose: never spell out the recipe.")]
        public string hint = "Seems to like something on this planet.";
        [Tooltip("Shown once discovered.")]
        [TextArea] public string description = "";
        [Min(0)] public int discoveryReward = 20;
        [Tooltip("How much one living here adds to the planet's life, which raises the planet level.")]
        [Min(0)] public float life = 5f;

        [Header("Spawn rule")]
        public SpawnConditionData[] conditions = Array.Empty<SpawnConditionData>();
        [Min(1f)] public float checkIntervalSeconds = 10f;
        [Range(0f, 1f)] public float chancePerCheck = 0.3f;
        [Min(1)] public int populationCap = 3;
        [Tooltip("How long it stays after its habitat stops suiting it.")]
        [Min(0f)] public float leaveAfterSeconds = 90f;

        [Header("Placeholder look")]
        public CreatureShape shape = CreatureShape.Hopper;
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.grey;
        [Min(0.1f)] public float size = 1f;
        [Min(0f)] public float moveSpeed = 0.6f;
        [Tooltip("How far, in cells, it wanders from home.")]
        [Min(0)] public int wanderRadius = 2;

        public CreatureSpec ToSpec()
        {
            var list = new List<SpawnCondition>();
            foreach (var condition in conditions)
            {
                if (condition != null) list.Add(condition.ToCondition());
            }

            return new CreatureSpec
            {
                Id = id,
                Rarity = rarity,
                CheckIntervalSeconds = checkIntervalSeconds,
                ChancePerCheck = chancePerCheck,
                PopulationCap = populationCap,
                LeaveAfterSeconds = leaveAfterSeconds,
                DiscoveryReward = discoveryReward,
                Life = life,
                Conditions = list,
            };
        }
    }
}
