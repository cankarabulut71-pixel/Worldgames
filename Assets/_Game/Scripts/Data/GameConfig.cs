using UnityEngine;

namespace TinyPlanet.Data
{
    [System.Serializable]
    public struct SkyPalette
    {
        public Color skyCentre;
        public Color skyEdge;
        public Color atmosphere;
        public Color waterShallow;
        public Color waterDeep;

        public SkyPalette(Color skyCentre, Color skyEdge, Color atmosphere, Color waterShallow, Color waterDeep)
        {
            this.skyCentre = skyCentre;
            this.skyEdge = skyEdge;
            this.atmosphere = atmosphere;
            this.waterShallow = waterShallow;
            this.waterDeep = waterDeep;
        }
    }

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

        [Header("Progression")]
        [Tooltip("Life needed to reach level 2, 3, 4 and so on. The planet starts at level 1.")]
        public int[] levelLifeThresholds = { 15, 40, 90, 170, 300, 520, 850 };

        [Header("Planets")]
        [Tooltip("Planet level needed on the current planet before a new one can be discovered.")]
        [Min(1)] public int newPlanetLevel = 6;
        [Tooltip("Energy cost of the 2nd, 3rd, ... planet. Its length limits how many planets there can be.")]
        public int[] newPlanetCosts = { 8000, 60000, 400000, 2000000 };
        [Tooltip("Sky and water colours; the home planet uses the first, others pick from the rest.")]
        public SkyPalette[] skyPalettes =
        {
            new SkyPalette(new Color(0.33f, 0.62f, 0.62f), new Color(0.05f, 0.17f, 0.20f), new Color(0.55f, 0.95f, 1f), new Color(0.42f, 0.88f, 0.80f), new Color(0.07f, 0.46f, 0.56f)),
            new SkyPalette(new Color(0.46f, 0.38f, 0.66f), new Color(0.09f, 0.06f, 0.19f), new Color(0.80f, 0.70f, 1f), new Color(0.55f, 0.75f, 0.95f), new Color(0.18f, 0.25f, 0.55f)),
            new SkyPalette(new Color(0.72f, 0.52f, 0.40f), new Color(0.20f, 0.09f, 0.08f), new Color(1f, 0.80f, 0.60f), new Color(0.45f, 0.85f, 0.75f), new Color(0.10f, 0.40f, 0.45f)),
            new SkyPalette(new Color(0.30f, 0.48f, 0.76f), new Color(0.03f, 0.07f, 0.19f), new Color(0.60f, 0.80f, 1f), new Color(0.40f, 0.75f, 0.95f), new Color(0.06f, 0.28f, 0.58f)),
        };

        [Header("Content")]
        [Tooltip("The first biome is where a new planet starts.")]
        public BiomeDefinition[] biomes;
        public PlantDefinition[] plants;
        public CreatureDefinition[] creatures;
        [Min(0)] public int maxCatchUpChecks = 30;

        [Header("Placeholder look")]
        public Color planetColor = new Color(0.36f, 0.66f, 0.30f);
        public Color cellDotColor = new Color(0.30f, 0.58f, 0.25f);
        public Color spaceColor = new Color(0.03f, 0.04f, 0.10f);
        public Color soilColor = new Color(0.40f, 0.27f, 0.16f);
        public Color woodColor = new Color(0.45f, 0.30f, 0.18f);
        public Color waterColor = new Color(0.24f, 0.55f, 0.85f);
        public Color sandColor = new Color(0.86f, 0.78f, 0.56f);
        public Color snowColor = new Color(0.94f, 0.97f, 0.99f);
        [Tooltip("Height above sea level where snow starts.")]
        public float snowLine = 0.5f;

        [Header("Shaded materials (one shader each, kept as assets so they are included in builds)")]
        public Material terrainMaterial;
        public Material waterMaterial;
        public Material atmosphereMaterial;
        public Material skyMaterial;

        [Tooltip("Land that has not been woken yet.")]
        public Color dormantColor = new Color(0.46f, 0.46f, 0.48f);
        public Color dormantDotColor = new Color(0.40f, 0.40f, 0.42f);
        public Color dormantWaterColor = new Color(0.36f, 0.40f, 0.46f);

        [Header("Materials (kept as assets so their shaders are included in builds)")]
        public Material litMaterial;
        public Material unlitMaterial;
    }
}
