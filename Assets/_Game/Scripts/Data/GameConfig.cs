using UnityEngine;

namespace TinyPlanet.Data
{
    /// <summary>Top-level tuning and content list. One asset, referenced by the bootstrap in the scene.</summary>
    [CreateAssetMenu(menuName = "Tiny Planet/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Planet")]
        public int defaultSeed = 12345;
        [Range(100, 800)] public int cellCount = 500;
        [Min(1f)] public float planetRadius = 5f;

        [Header("Economy")]
        [Min(0)] public float startingEnergy = 20f;
        [Min(0)] public float maxOfflineHours = 24f;

        [Header("Content")]
        public PlantDefinition[] plants;

        [Header("Placeholder look")]
        public Color planetColor = new Color(0.36f, 0.66f, 0.30f);
        public Color cellDotColor = new Color(0.30f, 0.58f, 0.25f);
        public Color spaceColor = new Color(0.03f, 0.04f, 0.10f);
        public Color soilColor = new Color(0.40f, 0.27f, 0.16f);
        public Color woodColor = new Color(0.45f, 0.30f, 0.18f);

        [Header("Materials (kept as assets so their shaders are included in builds)")]
        public Material litMaterial;
        public Material unlitMaterial;
    }
}
