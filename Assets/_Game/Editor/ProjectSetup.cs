using System;
using System.IO;
using TinyPlanet.Data;
using TinyPlanet.Presentation;
using TinyPlanet.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TinyPlanet.EditorTools
{
    /// <summary>
    /// Creates the starting content, materials and main scene. "Set Up Project" is safe to run again: existing
    /// assets are kept, so tuning done in the Inspector is not overwritten. "Reset Content To Defaults" puts
    /// every plant, biome and creature back to the values written here.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Game";
        private const string ContentFolder = Root + "/Content";
        private const string PlantsFolder = ContentFolder + "/Plants";
        private const string BiomesFolder = ContentFolder + "/Biomes";
        private const string CreaturesFolder = ContentFolder + "/Creatures";
        private const string MaterialsFolder = Root + "/Materials";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string ConfigPath = ContentFolder + "/GameConfig.asset";

        private static bool overwrite;

        [MenuItem("Tiny Planet/Set Up Project")]
        public static void Run() => Run(resetContent: false);

        [MenuItem("Tiny Planet/Reset Content To Defaults")]
        public static void ResetContentMenu()
        {
            if (EditorUtility.DisplayDialog("Reset content",
                    "Put every plant, biome and creature, and the level thresholds, back to their default values? Tuning done in the Inspector is lost.",
                    "Reset", "Cancel"))
                Run(resetContent: true);
        }

        public static void Run(bool resetContent)
        {
            overwrite = resetContent;
            try
            {
                Directory.CreateDirectory(PlantsFolder);
                Directory.CreateDirectory(BiomesFolder);
                Directory.CreateDirectory(CreaturesFolder);
                Directory.CreateDirectory(MaterialsFolder);
                Directory.CreateDirectory(Root + "/Scenes");
                AssetDatabase.Refresh();

                var config = Asset<GameConfig>(ConfigPath, _ => { }, alwaysKeep: true);
                config.litMaterial = LoadOrCreateMaterial(MaterialsFolder + "/PlaceholderLit.mat", "Universal Render Pipeline/Lit");
                config.unlitMaterial = LoadOrCreateMaterial(MaterialsFolder + "/PlaceholderUnlit.mat", "Universal Render Pipeline/Unlit");
                config.terrainMaterial = LoadOrCreateMaterial(MaterialsFolder + "/Terrain.mat", "TinyPlanet/Terrain");
                config.terrainMaterial.enableInstancing = true;
                config.waterMaterial = LoadOrCreateMaterial(MaterialsFolder + "/Water.mat", "TinyPlanet/Water");
                config.atmosphereMaterial = LoadOrCreateMaterial(MaterialsFolder + "/Atmosphere.mat", "TinyPlanet/Atmosphere");
                config.skyMaterial = LoadOrCreateMaterial(MaterialsFolder + "/SpaceSky.mat", "TinyPlanet/SpaceSky");

                config.plants = CreatePlants();
                config.biomes = CreateBiomes(config.plants);
                config.creatures = CreateCreatures(config.plants, config.biomes);
                AddHintNouns(config);
                if (overwrite) config.levelLifeThresholds = new[] { 15, 40, 90, 170, 300, 520, 850 };

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();

                if (!File.Exists(ScenePath)) CreateScene();

                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                PlayerSettings.productName = "Tiny Planet";
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

                AssetDatabase.SaveAssets();
                Debug.Log("Tiny Planet: project set up. Open " + ScenePath + " and press Play.");
            }
            finally
            {
                overwrite = false;
            }
        }

        // ---------- plants ----------

        private static PlantDefinition[] CreatePlants()
        {
            PlantDefinition Plant(string file, string id, string name, PlantShape shape, int cost, float energy,
                float seedSeconds, float sproutSeconds, float life, int unlockLevel, int unlockCost, int reward,
                Color primary, Color secondary, float height, string hint, string description) =>
                Asset<PlantDefinition>(PlantsFolder + "/" + file + ".asset", p =>
                {
                    p.id = id; p.displayName = name; p.shape = shape;
                    p.seedCost = cost; p.energyPerMinute = energy; p.refundFraction = 0.5f;
                    p.seedSeconds = seedSeconds; p.sproutSeconds = sproutSeconds;
                    p.life = life; p.unlockLevel = unlockLevel; p.unlockCost = unlockCost; p.discoveryReward = reward;
                    p.primaryColor = primary; p.secondaryColor = secondary; p.matureHeight = height;
                    p.hint = hint; p.description = description;
                });

            return new[]
            {
                // Meadow
                Plant("Grass", "grass", "Grass", PlantShape.Grass, 2, 2f, 8f, 15f, 1f, 1, 0, 5,
                    new Color(0.55f, 0.85f, 0.30f), Color.white, 0.6f,
                    "The humblest seed. Try a few side by side.",
                    "Soft tufts that spread a cosy carpet. Small things love to hide in it."),
                Plant("Flower", "flower", "Flower", PlantShape.Flower, 10, 8f, 30f, 60f, 2f, 1, 0, 5,
                    new Color(0.25f, 0.62f, 0.25f), new Color(0.98f, 0.45f, 0.70f), 0.85f,
                    "A splash of colour. Who might it attract?",
                    "Bright blossoms full of sweet nectar. Lovely in little clusters."),
                Plant("Berrybush", "berrybush", "Berrybush", PlantShape.Bush, 25, 14f, 45f, 120f, 3f, 2, 60, 10,
                    new Color(0.20f, 0.52f, 0.26f), new Color(0.86f, 0.16f, 0.36f), 0.7f,
                    "Sweet things hang from its branches.",
                    "A round, friendly shrub heavy with berries. Something small is always nibbling at it."),
                Plant("Tree", "tree", "Tree", PlantShape.Tree, 40, 30f, 120f, 300f, 5f, 2, 120, 15,
                    new Color(0.13f, 0.50f, 0.22f), new Color(0.22f, 0.62f, 0.28f), 1.3f,
                    "Slow to grow, but worth the wait.",
                    "A sturdy old friend. Its branches make a home for many."),

                // Wetland
                Plant("Reed", "reed", "Reed", PlantShape.Reed, 8, 5f, 20f, 40f, 1f, 3, 40, 10,
                    new Color(0.55f, 0.66f, 0.30f), new Color(0.45f, 0.30f, 0.18f), 0.95f,
                    "Loves wet feet.",
                    "Tall, whispering stalks that sway at the water's edge."),
                Plant("Bogbloom", "bogbloom", "Bogbloom", PlantShape.Flower, 30, 18f, 60f, 120f, 3f, 4, 200, 15,
                    new Color(0.20f, 0.50f, 0.40f), new Color(0.45f, 0.58f, 1f), 0.8f,
                    "A blue blossom of the marsh.",
                    "Cool blue petals that open wide on damp ground. They shimmer faintly at dusk."),
                Plant("Willow", "willow", "Willow", PlantShape.Tree, 90, 55f, 150f, 360f, 6f, 4, 700, 20,
                    new Color(0.45f, 0.70f, 0.35f), new Color(0.62f, 0.80f, 0.45f), 1.4f,
                    "A weeping giant of the wetlands.",
                    "Long, drooping branches that brush the water. A peaceful place to rest."),
                Plant("Moss", "moss", "Moss", PlantShape.Moss, 15, 9f, 30f, 60f, 2f, 5, 600, 15,
                    new Color(0.35f, 0.55f, 0.30f), new Color(0.50f, 0.68f, 0.35f), 0.3f,
                    "Soft and patient. It grows where little else will.",
                    "A springy green cushion. It is happy on bare stone and damp ground alike."),

                // Rocky
                Plant("Pine", "pine", "Pine", PlantShape.Pine, 140, 80f, 180f, 420f, 7f, 6, 3500, 25,
                    new Color(0.10f, 0.38f, 0.25f), new Color(0.15f, 0.46f, 0.30f), 1.6f,
                    "Hardy and evergreen, it climbs the rocks.",
                    "A tall, tough tree that thrives among the stones. It smells of cold air."),
                Plant("Crystalbloom", "crystalbloom", "Crystalbloom", PlantShape.Crystal, 250, 130f, 240f, 600f, 10f, 7, 9000, 40,
                    new Color(0.55f, 0.85f, 0.95f), new Color(0.85f, 0.62f, 1f), 0.9f,
                    "Something gleams deep in the stone.",
                    "A strange bloom of living crystal. Nobody knows quite how it grows."),
            };
        }

        // ---------- biomes ----------

        private static BiomeDefinition[] CreateBiomes(PlantDefinition[] plants)
        {
            PlantDefinition[] Plants(params string[] ids) =>
                Array.ConvertAll(ids, id => Array.Find(plants, p => p != null && p.id == id));

            var meadow = Asset<BiomeDefinition>(BiomesFolder + "/Meadow.asset", b =>
            {
                b.id = "meadow"; b.displayName = "Meadow";
                b.description = "Gentle green hills. Where every planet begins.";
                b.weight = 1.1f; b.waterFraction = 0f; b.baseMoisture = 0.5f; b.baseFertility = 0.6f;
                b.plants = Plants("grass", "flower", "berrybush", "tree");
                b.unlockLevel = 1; b.unlockCost = 0;
                b.groundColor = new Color(0.40f, 0.70f, 0.26f); b.dotColor = new Color(0.30f, 0.58f, 0.25f);
                b.decorationColor = Color.clear; b.decorationDensity = 0f;
                b.baseHeight = 0.07f; b.hilliness = 0.1f;
                b.coverShape = CoverShape.RoundTrees; b.coverPerCell = 4;
                b.coverColor = new Color(0.33f, 0.66f, 0.18f); b.coverAccent = new Color(0.52f, 0.78f, 0.22f);
            });

            var wetland = Asset<BiomeDefinition>(BiomesFolder + "/Wetland.asset", b =>
            {
                b.id = "wetland"; b.displayName = "Wetland";
                b.description = "Ponds, reeds and soft mud. Life loves the water.";
                b.weight = 1f; b.waterFraction = 0.25f; b.baseMoisture = 0.85f; b.baseFertility = 0.5f;
                b.plants = Plants("grass", "flower", "reed", "bogbloom", "willow", "moss");
                b.unlockLevel = 3; b.unlockCost = 400;
                b.groundColor = new Color(0.33f, 0.60f, 0.36f); b.dotColor = new Color(0.22f, 0.45f, 0.36f);
                b.decorationColor = Color.clear; b.decorationDensity = 0f;
                b.baseHeight = 0.05f; b.hilliness = 0.04f;
                b.coverShape = CoverShape.Reeds; b.coverPerCell = 4;
                b.coverColor = new Color(0.45f, 0.66f, 0.28f); b.coverAccent = new Color(0.62f, 0.56f, 0.30f);
            });

            var rocky = Asset<BiomeDefinition>(BiomesFolder + "/Rocky.asset", b =>
            {
                b.id = "rocky"; b.displayName = "Rocky";
                b.description = "Bare stone and thin air. Only the hardiest things grow here.";
                b.weight = 0.9f; b.waterFraction = 0f; b.baseMoisture = 0.2f; b.baseFertility = 0.3f;
                b.plants = Plants("moss", "pine", "crystalbloom");
                b.unlockLevel = 5; b.unlockCost = 3000;
                b.groundColor = new Color(0.50f, 0.51f, 0.53f); b.dotColor = new Color(0.52f, 0.46f, 0.38f);
                b.decorationColor = new Color(0.52f, 0.50f, 0.48f); b.decorationDensity = 0.45f;
                b.baseHeight = 0.14f; b.hilliness = 0.75f;
                b.coverShape = CoverShape.Pines; b.coverPerCell = 3;
                b.coverColor = new Color(0.14f, 0.42f, 0.28f); b.coverAccent = new Color(0.22f, 0.52f, 0.32f);
            });

            return new[] { meadow, wetland, rocky };
        }

        // Phrases used in generated hints for planet-only creatures. Filled in when empty or when resetting.
        private static void AddHintNouns(GameConfig config)
        {
            var plantNouns = new System.Collections.Generic.Dictionary<string, string>
            {
                { "grass", "thick grass" }, { "flower", "a bright blossom" }, { "berrybush", "heavy berries" },
                { "tree", "old wood" }, { "reed", "whispering reeds" }, { "bogbloom", "blue petals" },
                { "willow", "weeping branches" }, { "moss", "soft moss" }, { "pine", "evergreen needles" },
                { "crystalbloom", "gleaming crystal" },
            };
            foreach (var plant in config.plants)
            {
                if (plant == null || !plantNouns.TryGetValue(plant.id, out var noun)) continue;
                if (!overwrite && !string.IsNullOrEmpty(plant.hintNoun)) continue;
                plant.hintNoun = noun;
                EditorUtility.SetDirty(plant);
            }

            var biomeNouns = new System.Collections.Generic.Dictionary<string, string>
            {
                { "meadow", "the green hills" }, { "wetland", "the marsh" }, { "rocky", "the high stones" },
            };
            foreach (var biome in config.biomes)
            {
                if (biome == null || !biomeNouns.TryGetValue(biome.id, out var noun)) continue;
                if (!overwrite && !string.IsNullOrEmpty(biome.hintNoun)) continue;
                biome.hintNoun = noun;
                EditorUtility.SetDirty(biome);
            }
        }

        // ---------- creatures ----------

        private static CreatureDefinition[] CreateCreatures(PlantDefinition[] plants, BiomeDefinition[] biomes)
        {
            PlantDefinition Plant(string id) => Array.Find(plants, p => p != null && p.id == id);
            BiomeDefinition Biome(string id) => Array.Find(biomes, b => b != null && b.id == id);

            SpawnConditionData Nearby(string plant, int count, int radius) => new SpawnConditionData
            {
                kind = ConditionKind.PlantsNearby, plant = Plant(plant), count = count, radius = radius,
            };
            SpawnConditionData Touching(string a, string b, int radius) => new SpawnConditionData
            {
                kind = ConditionKind.PlantsTouching, plant = Plant(a), otherPlant = Plant(b), radius = radius,
            };
            SpawnConditionData Water(int radius) => new SpawnConditionData { kind = ConditionKind.NearWater, radius = radius };
            SpawnConditionData In(string biome) => new SpawnConditionData { kind = ConditionKind.InBiome, biome = Biome(biome) };
            SpawnConditionData Lives(CreatureDefinition creature) => new SpawnConditionData
            {
                kind = ConditionKind.CreatureLivesHere, creature = creature,
            };

            CreatureDefinition Creature(string file, string id, string name, Rarity rarity, string hint, string description,
                Func<SpawnConditionData[]> conditions, float interval, float chance, int cap,
                CreatureShape shape, Color primary, Color secondary, float size, float speed, int wander)
            {
                return Asset<CreatureDefinition>(CreaturesFolder + "/" + file + ".asset", c =>
                {
                    c.id = id; c.displayName = name; c.rarity = rarity;
                    c.hint = hint; c.description = description;
                    c.discoveryReward = rarity == Rarity.Rare ? 150 : rarity == Rarity.Uncommon ? 45 : 20;
                    c.life = rarity == Rarity.Rare ? 25f : rarity == Rarity.Uncommon ? 10f : 5f;
                    c.conditions = conditions();
                    c.checkIntervalSeconds = interval; c.chancePerCheck = chance; c.populationCap = cap;
                    c.leaveAfterSeconds = rarity == Rarity.Common ? 90f : 180f;
                    c.shape = shape; c.primaryColor = primary; c.secondaryColor = secondary;
                    c.size = size; c.moveSpeed = speed; c.wanderRadius = wander;
                });
            }

            var hopper = Creature("Tufthopper", "tufthopper", "Tufthopper", Rarity.Common,
                "Hides in thick tufts.",
                "A shy, springy nibbler. It only feels safe when grass is all around it.",
                () => new[] { Nearby("grass", 3, 1) }, 8f, 0.35f, 3,
                CreatureShape.Hopper, new Color(0.86f, 0.72f, 0.52f), new Color(0.98f, 0.95f, 0.90f), 1f, 0.7f, 2);

            var petalwing = Creature("Petalwing", "petalwing", "Petalwing", Rarity.Common,
                "Never settles for a lonely bloom.",
                "Drifts from blossom to blossom, sipping nectar. Where there are flowers in company, there are petalwings.",
                () => new[] { Nearby("flower", 2, 1) }, 10f, 0.3f, 3,
                CreatureShape.Flutter, new Color(1f, 0.62f, 0.25f), new Color(0.25f, 0.18f, 0.15f), 1f, 0.9f, 2);

            var newt = Creature("Mudnewt", "mudnewt", "Mudnewt", Rarity.Common,
                "Basks where green meets blue.",
                "A sleepy little newt that suns itself on grassy banks, never far from a pond.",
                () => new[] { Water(1), Nearby("grass", 2, 1) }, 10f, 0.3f, 2,
                CreatureShape.Crawler, new Color(0.40f, 0.68f, 0.45f), new Color(0.98f, 0.80f, 0.30f), 1.1f, 0.35f, 1);

            var barkbit = Creature("Barkbit", "barkbit", "Barkbit", Rarity.Common,
                "Wants a roof of leaves and a soft lawn.",
                "A busy, bushy-tailed climber. It nests in a tree and forages in the grass below.",
                () => new[] { Nearby("tree", 1, 0), Nearby("grass", 2, 1) }, 12f, 0.3f, 2,
                CreatureShape.Climber, new Color(0.72f, 0.40f, 0.22f), new Color(0.95f, 0.70f, 0.45f), 1f, 0.8f, 2);

            var skipper = Creature("Reedskipper", "reedskipper", "Reedskipper", Rarity.Common,
                "Skims the still water between tall stalks.",
                "A darting, glassy-winged hunter of the marsh. It never strays far from the reeds.",
                () => new[] { Nearby("reed", 3, 1), Water(1) }, 10f, 0.3f, 3,
                CreatureShape.Flutter, new Color(0.35f, 0.80f, 0.85f), new Color(0.12f, 0.25f, 0.35f), 0.9f, 1.1f, 2);

            var snail = Creature("Mossnail", "mossnail", "Mossnail", Rarity.Common,
                "Slow and steady on a soft green carpet among the stones.",
                "It carries its house on its back and naps on moss between the rocks.",
                () => new[] { Nearby("moss", 3, 1), In("rocky") }, 10f, 0.3f, 3,
                CreatureShape.Shell, new Color(0.82f, 0.56f, 0.30f), new Color(0.78f, 0.82f, 0.62f), 1f, 0.2f, 1);

            var mouse = Creature("Bramblemouse", "bramblemouse", "Bramblemouse", Rarity.Uncommon,
                "Nibbles where berries hang low, and hides where the grass grows thick.",
                "A round little forager with berry-stained whiskers. It needs both a pantry and a hiding place.",
                () => new[] { Nearby("berrybush", 2, 1), Nearby("grass", 2, 1) }, 15f, 0.25f, 2,
                CreatureShape.Hopper, new Color(0.58f, 0.52f, 0.55f), new Color(0.96f, 0.74f, 0.80f), 0.8f, 0.9f, 2);

            var cragtail = Creature("Cragtail", "cragtail", "Cragtail", Rarity.Uncommon,
                "Nests high in the needles, above a soft mossy floor.",
                "A silver-furred climber of the high rocks. It leaps from pine to pine and dozes on moss.",
                () => new[] { Nearby("pine", 1, 0), Nearby("moss", 2, 1) }, 15f, 0.25f, 2,
                CreatureShape.Climber, new Color(0.52f, 0.52f, 0.58f), new Color(0.88f, 0.88f, 0.92f), 1f, 0.9f, 2);

            var wisp = Creature("Bogwisp", "bogwisp", "Bogwisp", Rarity.Uncommon,
                "Flickers where blue blossoms lean against the reeds, close to the water.",
                "A gentle glow that drifts over the marsh at dusk. Some say it is a lost little star.",
                () => new[] { Touching("bogbloom", "reed", 1), Water(1) }, 15f, 0.25f, 2,
                CreatureShape.Glow, new Color(0.55f, 0.75f, 1f), new Color(0.85f, 0.95f, 1f), 0.9f, 0.5f, 1);

            var bug = Creature("Geodebug", "geodebug", "Geodebug", Rarity.Uncommon,
                "Drawn to gleaming crystals... and to slow-moving neighbours.",
                "A beetle with a shell like a split geode. It follows mossnails around, though nobody knows why.",
                () => new[] { Nearby("crystalbloom", 2, 1), Lives(snail) }, 15f, 0.25f, 2,
                CreatureShape.Crawler, new Color(0.42f, 0.30f, 0.62f), new Color(0.70f, 0.95f, 1f), 0.9f, 0.5f, 1);

            var moth = Creature("Lanternmoth", "lanternmoth", "Lanternmoth", Rarity.Rare,
                "Weeping wood, a blossom at its side, still water close by... and it hates to fly alone.",
                "A rare moth that glows like a little lantern. It rests in willows with flowers at their feet, beside water, and only when other wings are about.",
                () => new[] { Touching("willow", "flower", 0), Water(2), Lives(petalwing) }, 15f, 0.25f, 1,
                CreatureShape.Glow, new Color(0.75f, 0.95f, 1f), new Color(1f, 0.92f, 0.55f), 1.1f, 0.5f, 1);

            var starhorn = Creature("Starhorn", "starhorn", "Starhorn", Rarity.Rare,
                "Where three lands meet, it waits for the finest of each to stand together.",
                "A proud, gentle creature with a horn that holds the light of distant stars. It only appears where meadow, marsh and mountain all show their best.",
                () => new[] { Nearby("crystalbloom", 1, 1), Nearby("willow", 1, 2), Nearby("tree", 1, 2) }, 20f, 0.2f, 1,
                CreatureShape.Hopper, new Color(0.96f, 0.93f, 0.82f), new Color(0.98f, 0.80f, 0.35f), 1.5f, 0.6f, 2);

            return new[] { hopper, petalwing, newt, barkbit, skipper, snail, mouse, cragtail, wisp, bug, moth, starhorn };
        }

        // ---------- scene and helpers ----------

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Opening a new scene unloads unused assets, so fetch the config again before referencing it.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            var camera = Object.FindAnyObjectByType<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.spaceColor;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;

            var light = Object.FindAnyObjectByType<Light>();
            light.shadows = LightShadows.None;
            light.intensity = 1.2f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.55f);

            var bootstrap = new GameObject("Game").AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("gameCamera").objectReferenceValue = camera;
            serialized.FindProperty("keyLight").objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);

            material = new Material(shader);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Creates the asset with the default values, or, when resetting, writes them over an existing one.
        private static T Asset<T>(string path, Action<T> initialise, bool alwaysKeep = false) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                if (overwrite && !alwaysKeep)
                {
                    initialise(asset);
                    EditorUtility.SetDirty(asset);
                }
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            initialise(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
