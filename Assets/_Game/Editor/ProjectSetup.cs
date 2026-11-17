using System.IO;
using TinyPlanet.Data;
using TinyPlanet.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.EditorTools
{
    /// <summary>
    /// Creates the starting content, materials and main scene. Safe to run again: existing assets are kept,
    /// so tuning done in the Inspector is not overwritten.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Game";
        private const string ContentFolder = Root + "/Content";
        private const string PlantsFolder = ContentFolder + "/Plants";
        private const string MaterialsFolder = Root + "/Materials";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string ConfigPath = ContentFolder + "/GameConfig.asset";

        [MenuItem("Tiny Planet/Set Up Project")]
        public static void Run()
        {
            Directory.CreateDirectory(PlantsFolder);
            Directory.CreateDirectory(MaterialsFolder);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();

            var config = LoadOrCreate<GameConfig>(ConfigPath, _ => { });

            config.litMaterial = LoadOrCreateMaterial(MaterialsFolder + "/PlaceholderLit.mat", "Universal Render Pipeline/Lit");
            config.unlitMaterial = LoadOrCreateMaterial(MaterialsFolder + "/PlaceholderUnlit.mat", "Universal Render Pipeline/Unlit");

            if (config.plants == null || config.plants.Length == 0)
            {
                config.plants = new[]
                {
                    LoadOrCreate<PlantDefinition>(PlantsFolder + "/Grass.asset", p =>
                    {
                        p.id = "grass"; p.displayName = "Grass"; p.shape = PlantShape.Grass;
                        p.seedCost = 2; p.energyPerMinute = 2f; p.seedSeconds = 8f; p.sproutSeconds = 15f;
                        p.primaryColor = new Color(0.55f, 0.85f, 0.30f); p.secondaryColor = Color.white; p.matureHeight = 0.6f;
                    }),
                    LoadOrCreate<PlantDefinition>(PlantsFolder + "/Flower.asset", p =>
                    {
                        p.id = "flower"; p.displayName = "Flower"; p.shape = PlantShape.Flower;
                        p.seedCost = 10; p.energyPerMinute = 8f; p.seedSeconds = 30f; p.sproutSeconds = 60f;
                        p.primaryColor = new Color(0.25f, 0.62f, 0.25f); p.secondaryColor = new Color(0.98f, 0.45f, 0.70f); p.matureHeight = 0.85f;
                    }),
                    LoadOrCreate<PlantDefinition>(PlantsFolder + "/Tree.asset", p =>
                    {
                        p.id = "tree"; p.displayName = "Tree"; p.shape = PlantShape.Tree;
                        p.seedCost = 40; p.energyPerMinute = 30f; p.seedSeconds = 120f; p.sproutSeconds = 300f;
                        p.primaryColor = new Color(0.13f, 0.50f, 0.22f); p.secondaryColor = new Color(0.22f, 0.62f, 0.28f); p.matureHeight = 1.3f;
                    }),
                };
            }

            AddCodexText(config);
            if (config.creatures == null || config.creatures.Length == 0) config.creatures = CreateCreatures(config);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (!File.Exists(ScenePath)) CreateScene(config);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Tiny Planet";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            AssetDatabase.SaveAssets();
            Debug.Log("Tiny Planet: project set up. Open " + ScenePath + " and press Play.");
        }

        // Plant assets from milestone 1 predate the codex; give them text once, leaving edited text alone.
        private static void AddCodexText(GameConfig config)
        {
            const string defaultHint = "Plant one and let it grow.";
            foreach (var plant in config.plants)
            {
                if (plant == null || (plant.hint != defaultHint && !string.IsNullOrEmpty(plant.description))) continue;
                switch (plant.id)
                {
                    case "grass":
                        plant.hint = "The humblest seed. Try a few side by side.";
                        plant.description = "Soft tufts that spread a cosy carpet. Small things love to hide in it.";
                        break;
                    case "flower":
                        plant.hint = "A splash of colour. Who might it attract?";
                        plant.description = "Bright blossoms full of sweet nectar. Lovely in little clusters.";
                        break;
                    case "tree":
                        plant.hint = "Slow to grow, but worth the wait.";
                        plant.description = "A sturdy old friend. Its branches make a home for many.";
                        break;
                }
                EditorUtility.SetDirty(plant);
            }
        }

        private static CreatureDefinition[] CreateCreatures(GameConfig config)
        {
            const string folder = ContentFolder + "/Creatures";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            PlantDefinition Plant(string id) => System.Array.Find(config.plants, p => p != null && p.id == id);

            SpawnConditionData Nearby(string plant, int count, int radius) => new SpawnConditionData
            {
                kind = ConditionKind.PlantsNearby, plant = Plant(plant), count = count, radius = radius,
            };

            var hopper = LoadOrCreate<CreatureDefinition>(folder + "/Tufthopper.asset", c =>
            {
                c.id = "tufthopper"; c.displayName = "Tufthopper"; c.rarity = Simulation.Rarity.Common;
                c.hint = "Hides in thick tufts.";
                c.description = "A shy, springy nibbler. It only feels safe when grass is all around it.";
                c.discoveryReward = 15;
                c.conditions = new[] { Nearby("grass", 3, 1) };
                c.checkIntervalSeconds = 8f; c.chancePerCheck = 0.35f; c.populationCap = 3; c.leaveAfterSeconds = 90f;
                c.shape = CreatureShape.Hopper; c.primaryColor = new Color(0.86f, 0.72f, 0.52f); c.secondaryColor = new Color(0.98f, 0.95f, 0.90f);
                c.size = 1f; c.moveSpeed = 0.7f; c.wanderRadius = 2;
            });

            var flutter = LoadOrCreate<CreatureDefinition>(folder + "/Petalwing.asset", c =>
            {
                c.id = "petalwing"; c.displayName = "Petalwing"; c.rarity = Simulation.Rarity.Common;
                c.hint = "Never settles for a lonely bloom.";
                c.description = "Drifts from blossom to blossom, sipping nectar. Where there are flowers in company, there are petalwings.";
                c.discoveryReward = 20;
                c.conditions = new[] { Nearby("flower", 2, 1) };
                c.checkIntervalSeconds = 10f; c.chancePerCheck = 0.3f; c.populationCap = 3; c.leaveAfterSeconds = 90f;
                c.shape = CreatureShape.Flutter; c.primaryColor = new Color(1f, 0.62f, 0.25f); c.secondaryColor = new Color(0.25f, 0.18f, 0.15f);
                c.size = 1f; c.moveSpeed = 0.9f; c.wanderRadius = 2;
            });

            var newt = LoadOrCreate<CreatureDefinition>(folder + "/Mudnewt.asset", c =>
            {
                c.id = "mudnewt"; c.displayName = "Mudnewt"; c.rarity = Simulation.Rarity.Common;
                c.hint = "Basks where green meets blue.";
                c.description = "A sleepy little newt that suns itself on grassy banks, never far from a pond.";
                c.discoveryReward = 20;
                c.conditions = new[]
                {
                    new SpawnConditionData { kind = ConditionKind.NearWater, radius = 1 },
                    Nearby("grass", 2, 1),
                };
                c.checkIntervalSeconds = 10f; c.chancePerCheck = 0.3f; c.populationCap = 2; c.leaveAfterSeconds = 90f;
                c.shape = CreatureShape.Crawler; c.primaryColor = new Color(0.40f, 0.68f, 0.45f); c.secondaryColor = new Color(0.98f, 0.80f, 0.30f);
                c.size = 1.1f; c.moveSpeed = 0.35f; c.wanderRadius = 1;
            });

            var climber = LoadOrCreate<CreatureDefinition>(folder + "/Barkbit.asset", c =>
            {
                c.id = "barkbit"; c.displayName = "Barkbit"; c.rarity = Simulation.Rarity.Common;
                c.hint = "Wants a roof of leaves and a soft lawn.";
                c.description = "A busy, bushy-tailed climber. It nests in a tree and forages in the grass below.";
                c.discoveryReward = 25;
                c.conditions = new[] { Nearby("tree", 1, 0), Nearby("grass", 2, 1) };
                c.checkIntervalSeconds = 12f; c.chancePerCheck = 0.3f; c.populationCap = 2; c.leaveAfterSeconds = 120f;
                c.shape = CreatureShape.Climber; c.primaryColor = new Color(0.72f, 0.40f, 0.22f); c.secondaryColor = new Color(0.95f, 0.70f, 0.45f);
                c.size = 1f; c.moveSpeed = 0.8f; c.wanderRadius = 2;
            });

            var rare = LoadOrCreate<CreatureDefinition>(folder + "/Lanternmoth.asset", c =>
            {
                c.id = "lanternmoth"; c.displayName = "Lanternmoth"; c.rarity = Simulation.Rarity.Rare;
                c.hint = "Old wood, a blossom at its side, still water close by... and it hates to fly alone.";
                c.description = "A rare moth that glows like a little lantern. It rests in trees with flowers at their feet, beside water, and only when other wings are about.";
                c.discoveryReward = 120;
                c.conditions = new[]
                {
                    new SpawnConditionData { kind = ConditionKind.PlantsTouching, plant = Plant("tree"), otherPlant = Plant("flower"), radius = 0 },
                    new SpawnConditionData { kind = ConditionKind.NearWater, radius = 2 },
                    new SpawnConditionData { kind = ConditionKind.CreatureLivesHere, creature = flutter },
                };
                c.checkIntervalSeconds = 15f; c.chancePerCheck = 0.25f; c.populationCap = 1; c.leaveAfterSeconds = 180f;
                c.shape = CreatureShape.Glow; c.primaryColor = new Color(0.75f, 0.95f, 1f); c.secondaryColor = new Color(1f, 0.92f, 0.55f);
                c.size = 1.1f; c.moveSpeed = 0.5f; c.wanderRadius = 1;
            });

            return new[] { hopper, flutter, newt, climber, rare };
        }

        private static void CreateScene(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Opening a new scene unloads unused assets, so fetch the config again before referencing it.
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

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
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);

            material = new Material(shader);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> initialise) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            initialise(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
