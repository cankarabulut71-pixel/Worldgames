using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Small celebratory bursts: soil when planting, sparkles when a plant grows, a flash of light for a
    /// discovery, a wave across a waking land and a halo around the planet on a level up. Three particle
    /// systems built in code (sparkles, puffs, rings) that never emit on their own: every particle is
    /// placed by hand.
    /// </summary>
    public sealed class Effects : MonoBehaviour
    {
        private ParticleSystem sparkles;
        private ParticleSystem puffs;
        private ParticleSystem rings;

        public void Init(Material template)
        {
            var additive = Variant(template, "Sparkle", BlendMode.One, BlendMode.One, ring: false);
            var blended = Variant(template, "Puff", BlendMode.One, BlendMode.OneMinusSrcAlpha, ring: false);
            var ring = Variant(template, "Ring", BlendMode.One, BlendMode.One, ring: true);
            // A flat ring on a curved planet dips into the ground at its edge; draw it whole.
            ring.SetFloat("_ZTest", (float)CompareFunction.Always);

            sparkles = System("Sparkles", additive, 800, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f), 0.35f);
            puffs = System("Puffs", blended, 300, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)), 0.5f);
            rings = System("Rings", ring, 24, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f), 0f);
            // Rings are oriented by hand (flat on the ground), not turned towards the camera.
            var ringMain = rings.main;
            ringMain.startRotation3D = true;
            rings.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;
        }

        /// <summary>Soil kicked up by a new seed.</summary>
        public void Planted(Vector3 position, Vector3 normal, Color soil)
        {
            for (int i = 0; i < 14; i++)
            {
                var shade = Color.Lerp(soil, soil * 1.35f, Random.value);
                shade.a = 0.95f;
                Emit(puffs, position + normal * 0.05f, Spray(normal, 0.9f) * Random.Range(0.8f, 1.6f), shade, Random.Range(0.14f, 0.26f), Random.Range(0.5f, 0.8f));
            }
            for (int i = 0; i < 4; i++)
                Emit(sparkles, position + normal * 0.1f, Spray(normal, 0.6f) * Random.Range(0.5f, 1f), new Color(0.7f, 1f, 0.5f, 0.8f), Random.Range(0.05f, 0.09f), Random.Range(0.4f, 0.6f));
        }

        /// <summary>A plant reaching full size.</summary>
        public void Grown(Vector3 position, Vector3 normal, Color color, float height)
        {
            var light = Color.Lerp(color, Color.white, 0.45f);
            for (int i = 0; i < 12; i++)
            {
                var start = position + normal * Random.Range(0.1f, height) + Random.insideUnitSphere * 0.15f;
                Emit(sparkles, start, Spray(normal, 0.5f) * Random.Range(0.4f, 1f), light, Random.Range(0.07f, 0.13f), Random.Range(0.7f, 1.1f));
            }
            Ring(position + normal * 0.15f, normal, new Color(light.r, light.g, light.b, 0.7f), 1.1f, 0.55f);
        }

        /// <summary>The flash for a newly discovered creature: bigger and longer for rarer ones.</summary>
        public void Discovered(Vector3 position, Vector3 normal, Color color, int rarity)
        {
            int count = 30 + rarity * 20;
            var light = Color.Lerp(color, Color.white, 0.3f);
            for (int i = 0; i < count; i++)
            {
                var tint = i % 4 == 0 ? Color.white : light;
                Emit(sparkles, position + normal * 0.3f, Spray(normal, 1.2f) * Random.Range(1f, 2.6f), tint, Random.Range(0.08f, 0.18f), Random.Range(0.8f, 1.5f + rarity * 0.3f));
            }
            Ring(position + normal * 0.2f, normal, light, 3f + rarity, 0.8f);
            Ring(position + normal * 0.2f, normal, new Color(1f, 1f, 1f, 0.6f), 1.8f + rarity * 0.6f, 0.6f);
        }

        /// <summary>A creature being petted.</summary>
        public void Tapped(Vector3 position, Vector3 normal, Color color)
        {
            var light = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 7; i++)
                Emit(sparkles, position + normal * 0.25f, Spray(normal, 0.7f) * Random.Range(0.6f, 1.2f), light, Random.Range(0.06f, 0.1f), Random.Range(0.5f, 0.8f));
        }

        /// <summary>A couple of tiny gold sparkles on a golden creature.</summary>
        public void Glint(Vector3 position, Vector3 up)
        {
            for (int i = 0; i < 2; i++)
                Emit(sparkles, position + Random.insideUnitSphere * 0.15f, (up + Random.insideUnitSphere * 0.6f).normalized * 0.35f,
                    new Color(1f, 0.85f, 0.35f), Random.Range(0.05f, 0.08f), Random.Range(0.4f, 0.7f));
        }

        /// <summary>A short ring where the player tapped the ground.</summary>
        public void Ping(Vector3 position, Vector3 normal, Color color, float size)
        {
            Ring(position + normal * 0.08f, normal, color, size, 0.35f);
        }

        /// <summary>
        /// Sparkles rising across a land as it wakes, spreading out from its middle over <paramref name="seconds"/>.
        /// </summary>
        public void Wave(IReadOnlyList<Vector3> points, Vector3 centre, Color color, float seconds)
        {
            StartCoroutine(RunWave(points, centre, color, seconds));
        }

        /// <summary>A halo of light around the whole planet.</summary>
        public void LevelUp(Vector3 planetCentre, float radius, Color color)
        {
            var light = Color.Lerp(color, Color.white, 0.35f);
            for (int i = 0; i < 160; i++)
            {
                var direction = Random.onUnitSphere;
                Emit(sparkles, planetCentre + direction * radius * Random.Range(1.0f, 1.1f), direction * Random.Range(0.8f, 2.2f),
                    i % 3 == 0 ? Color.white : light, Random.Range(0.1f, 0.22f), Random.Range(1.2f, 2.2f));
            }
            var camera = Camera.main;
            var facing = camera != null ? -camera.transform.forward : Vector3.up;
            Ring(planetCentre, facing, light, radius * 3.6f, 1.2f);
        }

        private IEnumerator RunWave(IReadOnlyList<Vector3> points, Vector3 centre, Color color, float seconds)
        {
            var order = new List<(float angle, Vector3 point)>(points.Count);
            float widest = 1f;
            foreach (var point in points)
            {
                float angle = Vector3.Angle(point, centre);
                order.Add((angle, point));
                widest = Mathf.Max(widest, angle);
            }
            order.Sort((a, b) => a.angle.CompareTo(b.angle));

            var light = Color.Lerp(color, Color.white, 0.4f);
            float time = 0f;
            int next = 0;
            while (next < order.Count)
            {
                time += Time.deltaTime;
                float reached = time / seconds * widest;
                while (next < order.Count && order[next].angle <= reached)
                {
                    var point = order[next].point;
                    var normal = point.normalized;
                    if (next % 2 == 0)
                        Emit(sparkles, point + normal * 0.1f, normal * Random.Range(0.5f, 1.2f) + Random.insideUnitSphere * 0.3f, light, Random.Range(0.08f, 0.15f), Random.Range(0.8f, 1.3f));
                    else
                        Emit(puffs, point + normal * 0.05f, normal * Random.Range(0.2f, 0.5f), new Color(light.r, light.g, light.b, 0.5f), Random.Range(0.25f, 0.4f), 0.8f);
                    next++;
                }
                yield return null;
            }
        }

        private static Vector3 Spray(Vector3 normal, float spread) => (normal + Random.insideUnitSphere * spread).normalized;

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false,
            };
            system.Emit(parameters, 1);
        }

        /// <summary>A ring lying flat across <paramref name="normal"/> (on the ground, or facing the camera).</summary>
        private void Ring(Vector3 position, Vector3 normal, Color color, float size, float life)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startColor = color,
                startSize = size,
                startLifetime = life,
                rotation3D = Quaternion.LookRotation(normal).eulerAngles,
                applyShapeToPosition = false,
            };
            rings.Emit(parameters, 1);
        }

        internal static Material Variant(Material template, string name, BlendMode source, BlendMode destination, bool ring)
        {
            var material = new Material(template) { name = name };
            material.SetFloat("_SrcBlend", (float)source);
            material.SetFloat("_DstBlend", (float)destination);
            material.SetFloat("_Shape", ring ? 1f : 0f);
            material.SetFloat("_RingWidth", 0.1f);
            return material;
        }

        private ParticleSystem System(string name, Material material, int maxParticles, AnimationCurve size, float drag) =>
            Build(transform, name, material, maxParticles, size, drag);

        /// <summary>A particle system that never emits on its own: every particle is placed by hand.</summary>
        internal static ParticleSystem Build(Transform parent, string name, Material material, int maxParticles, AnimationCurve size, float drag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, size);

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            if (drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 100f;
                limit.drag = drag * 4f;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }
    }
}
