using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>Placeholder visual for one plant, built from primitives. Pooled by PlanetView.</summary>
    public sealed class PlantView : MonoBehaviour
    {
        private const float PopSeconds = 0.35f;
        private const float SproutScale = 0.45f;

        private Transform seed;
        private Transform body;
        private float matureHeight;
        private float popTime = PopSeconds;

        public PlantDefinition Definition { get; private set; }

        public void Build(PlantDefinition definition, GameConfig config, MaterialLibrary materials)
        {
            Definition = definition;
            matureHeight = definition.matureHeight;

            seed = Part(PrimitiveType.Sphere, transform, materials.Lit(config.soilColor),
                new Vector3(0f, 0.03f, 0f), new Vector3(0.24f, 0.13f, 0.24f));

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            // The body is modelled about one unit tall and scaled to the species height.
            var primary = materials.Lit(definition.primaryColor);
            var secondary = materials.Lit(definition.secondaryColor);
            var wood = materials.Lit(config.woodColor);

            switch (definition.shape)
            {
                case PlantShape.Grass:
                    for (int i = 0; i < 3; i++)
                    {
                        var pivot = new GameObject("Blade").transform;
                        pivot.SetParent(body, false);
                        pivot.localRotation = Quaternion.Euler(18f, i * 120f, 0f);
                        Part(PrimitiveType.Cube, pivot, primary, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 1f, 0.05f));
                    }
                    break;

                case PlantShape.Flower:
                    Part(PrimitiveType.Cylinder, body, primary, new Vector3(0f, 0.4f, 0f), new Vector3(0.08f, 0.4f, 0.08f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.88f, 0f), new Vector3(0.46f, 0.34f, 0.46f));
                    break;

                case PlantShape.Tree:
                    Part(PrimitiveType.Cylinder, body, wood, new Vector3(0f, 0.3f, 0f), new Vector3(0.18f, 0.3f, 0.18f));
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.8f, 0f), new Vector3(0.75f, 0.7f, 0.75f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0.18f, 1.08f, 0.1f), new Vector3(0.45f, 0.42f, 0.45f));
                    break;
            }
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

        private static Transform Part(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            return part.transform;
        }
    }
}
