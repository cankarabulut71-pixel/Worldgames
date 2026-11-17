using TinyPlanet.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Shows the sky's state: the planet darkens at night and fireflies drift over the garden; on rainy days
    /// the light turns grey and rain falls towards the planet. Changes ease in, so a forced debug switch fades
    /// rather than snaps.
    /// </summary>
    public sealed class SkyView : MonoBehaviour
    {
        private static readonly Color DayKey = new Color(1f, 0.95f, 0.86f);
        private static readonly Color NightKey = new Color(0.58f, 0.66f, 1f);
        private static readonly Color RainKey = new Color(0.82f, 0.86f, 0.92f);
        private static readonly Color DayAmbient = new Color(0.36f, 0.42f, 0.48f);
        private static readonly Color NightAmbient = new Color(0.13f, 0.16f, 0.30f);
        private static readonly Color RainAmbient = new Color(0.33f, 0.36f, 0.41f);

        private PlanetSimulation simulation;
        private PlanetView view;
        private Camera viewCamera;
        private Light keyLight;
        private Material skyMaterial;
        private Color dayCentre;
        private Color dayEdge;
        private ColorAdjustments grading;
        private AudioDirector audioDirector;
        private float radius;

        private ParticleSystem fireflies;
        private ParticleSystem rain;
        private float fireflyDue;
        private float rainDue;
        private float shownDaylight = -1f;
        private float shownRain = -1f;

        /// <summary>0 in full day, 1 in full night, as currently shown.</summary>
        public float Night => 1f - Mathf.Max(0f, shownDaylight);

        public void Init(PlanetSimulation simulation, PlanetView view, Camera viewCamera, Light keyLight, Material skyMaterial,
            ColorAdjustments grading, AudioDirector audioDirector, Material particleTemplate, float radius)
        {
            this.simulation = simulation;
            this.view = view;
            this.viewCamera = viewCamera;
            this.keyLight = keyLight;
            this.skyMaterial = skyMaterial;
            this.grading = grading;
            this.audioDirector = audioDirector;
            this.radius = radius;
            if (skyMaterial != null)
            {
                dayCentre = skyMaterial.GetColor("_Centre");
                dayEdge = skyMaterial.GetColor("_Edge");
            }

            var glow = Effects.Variant(particleTemplate, "Firefly", BlendMode.One, BlendMode.One, ring: false);
            fireflies = Effects.Build(transform, "Fireflies", glow, 400,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.7f)), 0f);
            var noise = fireflies.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.25f;
            var fireflyFade = new Gradient();
            fireflyFade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            var fireflyColour = fireflies.colorOverLifetime;
            fireflyColour.color = new ParticleSystem.MinMaxGradient(fireflyFade);

            var drop = Effects.Variant(particleTemplate, "Raindrop", BlendMode.One, BlendMode.OneMinusSrcAlpha, ring: false);
            rain = Effects.Build(transform, "Rain", drop, 1500, AnimationCurve.Constant(0f, 1f, 1f), 0f);
            var rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.velocityScale = 0.016f;
            rainRenderer.lengthScale = 1.2f;
            var dropFade = new Gradient();
            dropFade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 1f) });
            var rainColour = rain.colorOverLifetime;
            rainColour.color = new ParticleSystem.MinMaxGradient(dropFade);
        }

        private void Update()
        {
            if (simulation == null) return;
            long now = simulation.NowUtcTicks;
            float daylight = simulation.Sky.Daylight(now);
            float wet = simulation.Sky.IsRaining(now) ? 1f : 0f;
            float dt = Time.unscaledDeltaTime;
            shownDaylight = shownDaylight < 0f ? daylight : Mathf.MoveTowards(shownDaylight, daylight, dt * 0.5f);
            shownRain = shownRain < 0f ? wet : Mathf.MoveTowards(shownRain, wet, dt * 0.4f);
            float night = 1f - shownDaylight;

            ApplyLight(night, shownRain);
            if (audioDirector != null) audioDirector.SetWeather(night, shownRain > 0.5f);

            fireflyDue += dt * 18f * Mathf.SmoothStep(0f, 1f, night) * (1f - 0.7f * shownRain);
            while (fireflyDue >= 1f)
            {
                fireflyDue -= 1f;
                EmitFirefly();
            }

            rainDue += dt * 280f * shownRain;
            while (rainDue >= 1f)
            {
                rainDue -= 1f;
                EmitDrop();
            }
        }

        private void ApplyLight(float night, float wet)
        {
            if (keyLight != null)
            {
                var colour = Color.Lerp(DayKey, NightKey, night);
                keyLight.color = Color.Lerp(colour, RainKey, wet * 0.6f);
                keyLight.intensity = Mathf.Lerp(1.05f, 0.38f, night) * Mathf.Lerp(1f, 0.72f, wet);
            }

            var ambient = Color.Lerp(DayAmbient, NightAmbient, night);
            RenderSettings.ambientLight = Color.Lerp(ambient, RainAmbient * Mathf.Lerp(1f, 0.45f, night), wet * 0.6f);

            if (skyMaterial != null)
            {
                skyMaterial.SetColor("_Centre", Shade(dayCentre, night, wet, 0.22f));
                skyMaterial.SetColor("_Edge", Shade(dayEdge, night, wet, 0.35f));
            }
            if (grading != null) grading.postExposure.value = Mathf.Lerp(-0.1f, -0.4f, night) - wet * 0.08f;
        }

        // Night keeps a little of the day's hue under deep blue; rain washes colour towards grey.
        private static Color Shade(Color day, float night, float wet, float keep)
        {
            var dark = day * keep + new Color(0.02f, 0.03f, 0.09f);
            var colour = Color.Lerp(day, dark, night);
            float grey = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
            return Color.Lerp(colour, new Color(grey, grey, grey * 1.05f) * 0.85f, wet * 0.55f);
        }

        // A firefly near a plant on the side the player is looking at.
        private void EmitFirefly()
        {
            var plants = simulation.Plants;
            var facing = viewCamera.transform.position.normalized;
            int cell = -1;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int candidate = plants.Count > 0
                    ? plants[Random.Range(0, plants.Count)].Cell
                    : Random.Range(0, simulation.Planet.Grid.CellCount);
                if (simulation.Planet.IsWater(candidate)) continue;
                if (Vector3.Dot(simulation.Planet.Grid.Direction(candidate), facing) < 0.3f) continue;
                cell = candidate;
                break;
            }
            if (cell < 0) return;

            var normal = view.CellNormal(cell);
            var position = view.CellPoint(cell) + normal * Random.Range(0.25f, 1.1f)
                + Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * 0.4f;
            var colour = Random.value < 0.7f ? new Color(1f, 0.9f, 0.45f) : new Color(0.75f, 1f, 0.5f);
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Random.insideUnitSphere * 0.08f,
                startColor = colour,
                startSize = Random.Range(0.07f, 0.12f),
                startLifetime = Random.Range(2.5f, 4.5f),
                applyShapeToPosition = false,
            };
            fireflies.Emit(parameters, 1);
        }

        // A short streak falling onto the face of the planet the camera sees, never past its edge.
        private void EmitDrop()
        {
            var facing = viewCamera.transform.position.normalized;
            var direction = (facing + Random.insideUnitSphere * 0.65f).normalized;
            if (Vector3.Dot(direction, facing) < 0.65f) return;
            const float speed = 10f;
            float start = radius * 1.6f;
            var parameters = new ParticleSystem.EmitParams
            {
                position = direction * start,
                velocity = -direction * speed,
                startColor = new Color(0.78f, 0.86f, 1f, 0.38f),
                startSize = Random.Range(0.025f, 0.035f),
                startLifetime = (start - radius * 1.02f) / speed,
                applyShapeToPosition = false,
            };
            rain.Emit(parameters, 1);
        }
    }
}
