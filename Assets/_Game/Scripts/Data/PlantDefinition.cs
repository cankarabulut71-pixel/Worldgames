using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Data
{
    public enum PlantShape
    {
        Grass,
        Flower,
        Tree,
    }

    /// <summary>One plant species: its tunable numbers and its placeholder look.</summary>
    [CreateAssetMenu(menuName = "Tiny Planet/Plant", fileName = "Plant")]
    public sealed class PlantDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into save files. Never change it after release.")]
        public string id = "plant";
        public string displayName = "Plant";

        [Header("Economy")]
        [Min(0)] public int seedCost = 1;
        [Min(0)] public float energyPerMinute = 1f;
        [Range(0f, 1f)] public float refundFraction = 0.5f;

        [Header("Growth (seconds)")]
        [Min(0)] public float seedSeconds = 10f;
        [Min(0)] public float sproutSeconds = 20f;

        [Header("Placeholder look")]
        public PlantShape shape = PlantShape.Grass;
        public Color primaryColor = Color.green;
        public Color secondaryColor = Color.white;
        [Min(0.05f)] public float matureHeight = 0.6f;

        public PlantSpec ToSpec() => new PlantSpec
        {
            Id = id,
            SeedCost = seedCost,
            SeedSeconds = seedSeconds,
            SproutSeconds = sproutSeconds,
            EnergyPerMinute = energyPerMinute,
            RefundFraction = refundFraction,
        };
    }
}
