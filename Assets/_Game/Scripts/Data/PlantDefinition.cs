using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Data
{
    public enum PlantShape
    {
        Grass,
        Flower,
        Tree,
        Bush,
        Reed,
        Pine,
        Moss,
        Crystal,
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

        [Header("Progression")]
        [Tooltip("How much a mature one adds to the planet's life, which raises the planet level.")]
        [Min(0)] public float life = 1f;
        [Tooltip("Planet level needed before the seed can be bought. Free at level 1 means it is in the tray from the start.")]
        [Min(1)] public int unlockLevel = 1;
        [Min(0)] public int unlockCost;

        [Header("Growth (seconds)")]
        [Min(0)] public float seedSeconds = 10f;
        [Min(0)] public float sproutSeconds = 20f;

        [Header("Codex")]
        [Tooltip("Shown until the first one of these grows up.")]
        public string hint = "Plant one and let it grow.";
        [TextArea] public string description = "";
        [Min(0)] public int discoveryReward = 5;
        [Tooltip("A vague phrase used in generated hints for planet-only creatures, e.g. \"soft moss\".")]
        public string hintNoun = "";

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
            DiscoveryReward = discoveryReward,
            Life = life,
            UnlockLevel = unlockLevel,
            UnlockCost = unlockCost,
            HintNoun = string.IsNullOrEmpty(hintNoun) ? displayName.ToLowerInvariant() : hintNoun,
        };
    }
}
