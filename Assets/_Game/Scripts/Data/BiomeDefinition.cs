using System;
using System.Collections.Generic;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Data
{
    public enum CoverShape
    {
        None,
        RoundTrees,
        Pines,
        Reeds,
    }

    /// <summary>One kind of land: where it appears, what grows there, what it costs to wake, and its colours.</summary>
    [CreateAssetMenu(menuName = "Tiny Planet/Biome", fileName = "Biome")]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into save files. Never change it after release.")]
        public string id = "biome";
        public string displayName = "Biome";
        [TextArea] public string description = "";
        [Tooltip("How generated hints name this land, e.g. \"the marsh\".")]
        public string hintNoun = "";

        [Header("Generation")]
        [Tooltip("Relative share of the planet's surface.")]
        [Min(0.05f)] public float weight = 1f;
        [Tooltip("Fraction of its cells that become ponds.")]
        [Range(0f, 0.6f)] public float waterFraction;
        [Range(0f, 1f)] public float baseMoisture = 0.5f;
        [Range(0f, 1f)] public float baseFertility = 0.5f;

        [Header("Plants that grow here")]
        public PlantDefinition[] plants = Array.Empty<PlantDefinition>();

        [Header("Unlock (free at level 1 means awake from the start)")]
        [Min(1)] public int unlockLevel = 1;
        [Min(0)] public int unlockCost;

        [Header("Relief (presentation only)")]
        [Tooltip("Ground height above sea level, in world units.")]
        public float baseHeight = 0.06f;
        [Tooltip("How high its hills or mountains rise above the base height.")]
        [Min(0f)] public float hilliness = 0.08f;

        [Header("Ground cover that grows in as plants mature nearby")]
        public CoverShape coverShape = CoverShape.None;
        public Color coverColor = new Color(0.30f, 0.62f, 0.25f);
        public Color coverAccent = new Color(0.45f, 0.74f, 0.30f);
        [Range(0, 6)] public int coverPerCell = 3;

        [Header("Look")]
        public Color groundColor = new Color(0.36f, 0.66f, 0.30f);
        public Color dotColor = new Color(0.30f, 0.58f, 0.25f);
        [Tooltip("Rocks scattered over this land, or clear for none.")]
        public Color decorationColor = Color.clear;
        [Range(0f, 1f)] public float decorationDensity;

        public BiomeSpec ToSpec()
        {
            var allowed = new HashSet<string>();
            foreach (var plant in plants)
            {
                if (plant != null) allowed.Add(plant.id);
            }

            return new BiomeSpec
            {
                Id = id,
                Weight = weight,
                WaterFraction = waterFraction,
                BaseMoisture = baseMoisture,
                BaseFertility = baseFertility,
                AllowedPlants = allowed,
                UnlockLevel = unlockLevel,
                UnlockCost = unlockCost,
                HintNoun = string.IsNullOrEmpty(hintNoun) ? "the " + displayName.ToLowerInvariant() : hintNoun,
            };
        }
    }
}
