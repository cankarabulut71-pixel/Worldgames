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

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (!File.Exists(ScenePath)) CreateScene(config);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Tiny Planet";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            AssetDatabase.SaveAssets();
            Debug.Log("Tiny Planet: project set up. Open " + ScenePath + " and press Play.");
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
