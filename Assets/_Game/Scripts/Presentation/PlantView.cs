using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// One planted plant: a low-poly, vertex-coloured model built in code (one shared mesh per species),
    /// shown as a seed mound, a sprout or the grown plant. Pooled by PlanetView.
    /// </summary>
    public sealed class PlantView : MonoBehaviour
    {
        private const float PopSeconds = 0.35f;
        private const float SproutScale = 0.45f;

        private static readonly Dictionary<PlantDefinition, Mesh> Meshes = new Dictionary<PlantDefinition, Mesh>();
        private static Mesh seedMesh;

        private Transform seed;
        private Transform body;
        private float matureHeight;
        private float popTime = PopSeconds;

        public PlantDefinition Definition { get; private set; }

        public void Build(PlantDefinition definition, GameConfig config, MaterialLibrary materials)
        {
            Definition = definition;
            matureHeight = definition.matureHeight;

            if (seedMesh == null)
            {
                var kit = new MeshKit();
                kit.Blob(new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.1f, 0.26f), config.soilColor, 1, 0.15f, 1);
                kit.Blob(new Vector3(0f, 0.07f, 0f), new Vector3(0.07f, 0.06f, 0.07f), new Color(0.55f, 0.80f, 0.35f), 0, 0.1f, 2);
                seedMesh = kit.Build("Seed");
            }
            seed = Part("Seed", seedMesh, config.terrainMaterial);

            if (!Meshes.TryGetValue(definition, out var mesh))
            {
                mesh = BuildMesh(definition, config);
                Meshes[definition] = mesh;
            }
            body = Part("Body", mesh, config.terrainMaterial);
        }

        public void Show(PlantStage stage, bool animate)
        {
            bool isSeed = stage == PlantStage.Seed;
            seed.gameObject.SetActive(isSeed);
            body.gameObject.SetActive(!isSeed);
            body.localScale = Vector3.one * (matureHeight * (stage == PlantStage.Sprout ? SproutScale : 1f));

            if (animate)
            {
                popTime = 0f;
                transform.localScale = Vector3.one * 0.3f;
                enabled = true;
            }
            else
            {
                popTime = PopSeconds;
                transform.localScale = Vector3.one;
                enabled = false;
            }
        }

        private void Update()
        {
            popTime += Time.deltaTime;
            float t = Mathf.Clamp01(popTime / PopSeconds);

            // Ease out with a little overshoot, so planting and growing feel springy.
            const float overshoot = 2.2f;
            float u = t - 1f;
            float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, eased);

            if (t >= 1f)
            {
                transform.localScale = Vector3.one;
                enabled = false;
            }
        }

        private Transform Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        // Each model is about one unit tall; the body is scaled to the species height.
        private static Mesh BuildMesh(PlantDefinition definition, GameConfig config)
        {
            Color primary = definition.primaryColor;
            Color secondary = definition.secondaryColor;
            Color wood = config.woodColor;
            var kit = new MeshKit();
            var random = new System.Random(definition.id.GetHashCode());
            float R() => (float)random.NextDouble();

            switch (definition.shape)
            {
                case PlantShape.Grass:
                    for (int i = 0; i < 9; i++)
                    {
                        var foot = new Vector3((R() - 0.5f) * 0.3f, 0f, (R() - 0.5f) * 0.3f);
                        var shade = Color.Lerp(primary, primary * 0.75f, R());
                        shade.a = 1f;
                        kit.Blade(foot, 0.6f + R() * 0.45f, 0.035f, 10f + R() * 25f, R() * 360f, shade);
                    }
                    break;

                case PlantShape.Flower:
                    kit.Cone(Vector3.zero, 0.75f, 0.035f, 0.025f, 5, primary);
                    kit.Blade(new Vector3(0f, 0.15f, 0f), 0.3f, 0.06f, 55f, 30f, primary);
                    kit.Blade(new Vector3(0f, 0.25f, 0f), 0.26f, 0.05f, 55f, 210f, primary);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 6f;
                        var petal = new Vector3(Mathf.Cos(angle) * 0.15f, 0.8f, Mathf.Sin(angle) * 0.15f);
                        kit.Blob(petal, new Vector3(0.2f, 0.08f, 0.2f), secondary, 0, 0.1f, i);
                    }
                    kit.Blob(new Vector3(0f, 0.83f, 0f), new Vector3(0.14f, 0.1f, 0.14f), new Color(1f, 0.85f, 0.3f), 0, 0f, 9);
                    break;

                case PlantShape.Tree:
                    kit.Cone(Vector3.zero, 0.5f, 0.1f, 0.07f, 6, wood);
                    kit.Blob(new Vector3(0f, 0.72f, 0f), new Vector3(0.8f, 0.62f, 0.8f), primary, 1, 0.12f, 1);
                    kit.Blob(new Vector3(0.2f, 0.95f, 0.08f), new Vector3(0.5f, 0.45f, 0.5f), secondary, 1, 0.12f, 2);
                    kit.Blob(new Vector3(-0.22f, 0.86f, -0.12f), new Vector3(0.45f, 0.4f, 0.45f), Color.Lerp(primary, secondary, 0.5f), 1, 0.12f, 3);
                    break;

                case PlantShape.Bush:
                    kit.Blob(new Vector3(0f, 0.32f, 0f), new Vector3(0.85f, 0.62f, 0.85f), primary, 1, 0.12f, 1);
                    kit.Blob(new Vector3(0.22f, 0.5f, -0.1f), new Vector3(0.5f, 0.42f, 0.5f), primary * 1.12f, 1, 0.12f, 2);
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = i * 2.4f;
                        float y = 0.2f + R() * 0.45f;
                        float r = 0.4f - Mathf.Abs(y - 0.4f) * 0.4f;
                        kit.Blob(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r), Vector3.one * 0.12f, secondary, 0, 0f, i);
                    }
                    break;

                case PlantShape.Reed:
                    for (int i = 0; i < 6; i++)
                    {
                        var foot = new Vector3((R() - 0.5f) * 0.25f, 0f, (R() - 0.5f) * 0.25f);
                        float height = 0.7f + R() * 0.35f;
                        kit.Blade(foot, height, 0.03f, R() * 12f, R() * 360f, primary);
                        if (i % 2 == 0)
                            kit.Cone(foot + Vector3.up * (height * 0.72f), 0.18f, 0.045f, 0.04f, 5, secondary);
                    }
                    break;

                case PlantShape.Pine:
                    kit.Cone(Vector3.zero, 0.3f, 0.08f, 0.06f, 6, wood);
                    kit.Cone(new Vector3(0f, 0.2f, 0f), 0.55f, 0.42f, 0f, 8, primary);
                    kit.Cone(new Vector3(0f, 0.48f, 0f), 0.48f, 0.32f, 0f, 8, secondary, 0.3f);
                    kit.Cone(new Vector3(0f, 0.75f, 0f), 0.42f, 0.22f, 0f, 8, primary * 1.1f, 0.15f);
                    break;

                case PlantShape.Moss:
                    for (int i = 0; i < 6; i++)
                    {
                        var at = new Vector3((R() - 0.5f) * 0.55f, 0.05f, (R() - 0.5f) * 0.55f);
                        float size = 0.25f + R() * 0.2f;
                        kit.Blob(at, new Vector3(size, size * 0.6f, size), i % 2 == 0 ? primary : secondary, 1, 0.15f, i);
                    }
                    break;

                case PlantShape.Crystal:
                    for (int i = 0; i < 6; i++)
                    {
                        int mark = kit.Mark;
                        float height = i == 0 ? 0.95f : 0.4f + R() * 0.4f;
                        float width = i == 0 ? 0.12f : 0.06f + R() * 0.05f;
                        kit.Cone(Vector3.zero, height * 0.8f, width, width, 6, i % 2 == 0 ? primary : secondary);
                        kit.Cone(Vector3.up * height * 0.8f, height * 0.2f, width, 0f, 6, i % 2 == 0 ? primary : secondary);
                        if (i > 0)
                        {
                            var tilt = Quaternion.Euler(0f, R() * 360f, 0f) * Quaternion.Euler(25f + R() * 20f, 0f, 0f);
                            kit.TransformSince(mark, Matrix4x4.TRS(new Vector3((R() - 0.5f) * 0.12f, 0f, (R() - 0.5f) * 0.12f), tilt, Vector3.one));
                        }
                    }
                    break;
            }
            return kit.Build(definition.displayName);
        }
    }
}
