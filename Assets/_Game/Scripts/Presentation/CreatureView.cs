using System;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Placeholder visual for one creature, built from primitives. Wanders between cells near its home,
    /// following the sphere with its up vector along the surface normal. Pooled by CreatureLayer.
    /// </summary>
    public sealed class CreatureView : MonoBehaviour
    {
        private const float PopSeconds = 0.4f;
        private const float LeaveSeconds = 0.6f;
        private const float ReactSeconds = 0.7f;

        private Transform body;
        private Transform leftWing;
        private Transform rightWing;
        private Transform tail;
        private float hoverHeight;
        private bool flies;

        private Planet planet;
        private TerrainShape terrain;
        private int home;
        private Vector3 position;     // unit direction from the planet centre
        private Vector3 target;
        private Vector3 facing = Vector3.forward;
        private bool moving;
        private float pauseLeft;
        private float clock;
        private float popTime = PopSeconds;
        private float reactTime = ReactSeconds;
        private float leaveTime = -1f;
        private Action<CreatureView> onGone;
        private System.Random random;

        public CreatureDefinition Definition { get; private set; }
        public int CreatureId { get; private set; }

        /// <summary>World position of the middle of the body, for tap picking.</summary>
        public Vector3 BodyCentre => body.position + transform.up * (0.2f * Definition.size);

        public void Build(CreatureDefinition definition, MaterialLibrary materials)
        {
            Definition = definition;
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            var primary = materials.Lit(definition.primaryColor);
            var secondary = materials.Lit(definition.secondaryColor);
            var eye = materials.Lit(new Color(0.08f, 0.08f, 0.1f));

            switch (definition.shape)
            {
                case CreatureShape.Hopper:
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.16f, 0f), new Vector3(0.30f, 0.26f, 0.36f));
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.31f, 0.16f), Vector3.one * 0.22f);
                    Part(PrimitiveType.Cube, body, secondary, new Vector3(0.055f, 0.47f, 0.12f), new Vector3(0.05f, 0.2f, 0.035f));
                    Part(PrimitiveType.Cube, body, secondary, new Vector3(-0.055f, 0.47f, 0.12f), new Vector3(0.05f, 0.2f, 0.035f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.19f, -0.19f), Vector3.one * 0.11f);
                    Eyes(eye, new Vector3(0f, 0.34f, 0.26f), 0.05f);
                    break;

                case CreatureShape.Flutter:
                    flies = true;
                    hoverHeight = 0.75f;
                    Part(PrimitiveType.Capsule, body, secondary, Vector3.zero, new Vector3(0.06f, 0.1f, 0.06f), Quaternion.Euler(90f, 0f, 0f));
                    leftWing = Wing(body, primary, -1f, new Vector3(0.24f, 0.015f, 0.2f));
                    rightWing = Wing(body, primary, 1f, new Vector3(0.24f, 0.015f, 0.2f));
                    break;

                case CreatureShape.Crawler:
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.07f, 0f), new Vector3(0.16f, 0.11f, 0.36f));
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.09f, 0.21f), new Vector3(0.15f, 0.1f, 0.15f));
                    tail = Part(PrimitiveType.Cube, body, primary, new Vector3(0f, 0.05f, -0.27f), new Vector3(0.06f, 0.04f, 0.22f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0.04f, 0.12f, 0.03f), Vector3.one * 0.06f);
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(-0.04f, 0.12f, -0.07f), Vector3.one * 0.06f);
                    Eyes(eye, new Vector3(0f, 0.13f, 0.25f), 0.06f);
                    break;

                case CreatureShape.Climber:
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.17f, 0f), new Vector3(0.24f, 0.3f, 0.24f));
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.37f, 0.06f), Vector3.one * 0.2f);
                    Part(PrimitiveType.Cube, body, primary, new Vector3(0.06f, 0.48f, 0.04f), new Vector3(0.05f, 0.07f, 0.03f));
                    Part(PrimitiveType.Cube, body, primary, new Vector3(-0.06f, 0.48f, 0.04f), new Vector3(0.05f, 0.07f, 0.03f));
                    tail = Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.3f, -0.17f), new Vector3(0.15f, 0.38f, 0.15f), Quaternion.Euler(-20f, 0f, 0f));
                    Eyes(eye, new Vector3(0f, 0.4f, 0.15f), 0.05f);
                    break;

                case CreatureShape.Shell:
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.05f, 0.02f), new Vector3(0.16f, 0.09f, 0.40f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.11f, 0.13f, 0.11f));
                    Part(PrimitiveType.Sphere, body, primary, new Vector3(0f, 0.2f, -0.04f), new Vector3(0.3f, 0.3f, 0.26f));
                    Part(PrimitiveType.Sphere, body, secondary, new Vector3(0f, 0.27f, -0.05f), new Vector3(0.14f, 0.14f, 0.12f));
                    Part(PrimitiveType.Cube, body, secondary, new Vector3(0.035f, 0.22f, 0.24f), new Vector3(0.02f, 0.1f, 0.02f), Quaternion.Euler(20f, 0f, 0f));
                    Part(PrimitiveType.Cube, body, secondary, new Vector3(-0.035f, 0.22f, 0.24f), new Vector3(0.02f, 0.1f, 0.02f), Quaternion.Euler(20f, 0f, 0f));
                    Eyes(eye, new Vector3(0f, 0.27f, 0.26f), 0.035f);
                    break;

                case CreatureShape.Glow:
                    flies = true;
                    hoverHeight = 1.15f;
                    var glow = materials.Unlit(definition.primaryColor);
                    var core = materials.Unlit(definition.secondaryColor);
                    Part(PrimitiveType.Capsule, body, core, Vector3.zero, new Vector3(0.07f, 0.12f, 0.07f), Quaternion.Euler(90f, 0f, 0f));
                    Part(PrimitiveType.Sphere, body, core, new Vector3(0f, 0f, 0.13f), Vector3.one * 0.08f);
                    leftWing = Wing(body, glow, -1f, new Vector3(0.34f, 0.015f, 0.28f));
                    rightWing = Wing(body, glow, 1f, new Vector3(0.34f, 0.015f, 0.28f));
                    break;
            }
        }

        /// <summary>Starts life on the planet: at home, popping in if it has just arrived.</summary>
        public void Place(CreatureInstance creature, Planet planet, TerrainShape terrain, bool animate)
        {
            CreatureId = creature.Id;
            this.planet = planet;
            this.terrain = terrain;
            home = creature.HomeCell;
            random = new System.Random(creature.Id * 7919 + planet.Seed);

            position = Jitter(planet.Grid.Direction(home));
            target = position;
            moving = false;
            pauseLeft = (float)random.NextDouble() * 2f;
            clock = (float)random.NextDouble() * 10f;
            facing = AnyTangent(position);
            leaveTime = -1f;
            onGone = null;
            reactTime = ReactSeconds;
            popTime = animate ? 0f : PopSeconds;

            ApplyTransform(animate ? 0f : 1f);
            enabled = true;
        }

        /// <summary>A static pose for codex icons: no wandering, no animation.</summary>
        public void PoseForIcon()
        {
            enabled = false;
            transform.localScale = Vector3.one * Definition.size;
            body.localPosition = Vector3.up * hoverHeight * 0.15f;
            if (flies)
            {
                // Nose up, so the camera sees the top of the wings rather than their edges.
                body.localRotation = Quaternion.Euler(-50f, 0f, 0f);
                leftWing.localRotation = Quaternion.Euler(0f, 0f, 20f);
                rightWing.localRotation = Quaternion.Euler(0f, 0f, -20f);
            }
        }

        public void SetHome(int cell)
        {
            home = cell;
            pauseLeft = 0f; // head over now
        }

        public void React() => reactTime = 0f;

        /// <summary>Shrinks away, then reports back so the view can be pooled.</summary>
        public void Leave(Action<CreatureView> gone)
        {
            onGone = gone;
            leaveTime = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;

            if (leaveTime >= 0f)
            {
                leaveTime += dt;
                float k = 1f - Mathf.Clamp01(leaveTime / LeaveSeconds);
                ApplyTransform(k * k);
                if (leaveTime >= LeaveSeconds)
                {
                    var callback = onGone;
                    onGone = null;
                    leaveTime = -1f;
                    callback?.Invoke(this);
                }
                return;
            }

            Wander(dt);

            float scale = 1f;
            if (popTime < PopSeconds)
            {
                popTime += dt;
                float t = Mathf.Clamp01(popTime / PopSeconds);
                const float overshoot = 2.2f;
                float u = t - 1f;
                scale = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            }
            ApplyTransform(scale);
        }

        private void Wander(float dt)
        {
            if (!moving)
            {
                pauseLeft -= dt;
                if (pauseLeft > 0f) return;
                target = PickTarget();
                moving = true;
            }

            float step = Definition.moveSpeed / terrain.Radius * dt;
            position = Vector3.RotateTowards(position, target, step, 0f).normalized;

            Vector3 toward = target - position;
            Vector3 tangent = toward - Vector3.Dot(toward, position) * position;
            if (tangent.sqrMagnitude > 1e-8f) facing = tangent.normalized;

            if (Vector3.Angle(position, target) < 0.3f)
            {
                moving = false;
                pauseLeft = 0.8f + (float)random.NextDouble() * 2.5f;
            }
        }

        private Vector3 PickTarget()
        {
            var cells = planet.Grid.CellsWithin(home, Definition.wanderRadius);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int cell = cells[random.Next(cells.Count)];
                if (flies || !planet.IsWater(cell)) return Jitter(planet.Grid.Direction(cell));
            }
            return Jitter(planet.Grid.Direction(home));
        }

        // A point a little off the cell centre, so creatures stand beside plants rather than inside them.
        private Vector3 Jitter(Vector3 direction)
        {
            Vector3 offset = new Vector3(
                (float)random.NextDouble() - 0.5f,
                (float)random.NextDouble() - 0.5f,
                (float)random.NextDouble() - 0.5f);
            offset -= Vector3.Dot(offset, direction) * direction;
            float cellAngle = Mathf.Sqrt(4f * Mathf.PI / planet.Grid.CellCount);
            return (direction + offset.normalized * cellAngle * 0.35f).normalized;
        }

        private void ApplyTransform(float scale)
        {
            transform.localPosition = position * terrain.SurfaceRadius(position);
            transform.localRotation = Quaternion.LookRotation(facing, position);
            transform.localScale = Vector3.one * (Definition.size * Mathf.Max(0.001f, scale));

            float height = hoverHeight;
            float spin = 0f;

            if (flies)
            {
                height += Mathf.Sin(clock * 2.3f) * 0.08f;
                float flap = Mathf.Sin(clock * (Definition.shape == CreatureShape.Glow ? 7f : 16f)) * 55f;
                leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
                rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            else if (moving)
            {
                if (Definition.shape == CreatureShape.Crawler)
                    spin = Mathf.Sin(clock * 12f) * 12f;
                else
                    height += Mathf.Abs(Mathf.Sin(clock * 9f)) * 0.12f;
            }

            if (tail != null && Definition.shape == CreatureShape.Crawler)
                tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(clock * 6f) * 20f, 0f);

            if (reactTime < ReactSeconds)
            {
                reactTime += Time.deltaTime;
                float t = Mathf.Clamp01(reactTime / ReactSeconds);
                height += Mathf.Sin(t * Mathf.PI) * 0.45f;
                spin += t * 360f;
            }

            body.localPosition = Vector3.up * height;
            body.localRotation = Quaternion.Euler(0f, spin, 0f);
        }

        private static Vector3 AnyTangent(Vector3 normal)
        {
            Vector3 side = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(normal, side).normalized;
        }

        private void Eyes(Material material, Vector3 centre, float spacing)
        {
            Part(PrimitiveType.Sphere, body, material, centre + new Vector3(spacing, 0f, 0f), Vector3.one * 0.04f);
            Part(PrimitiveType.Sphere, body, material, centre - new Vector3(spacing, 0f, 0f), Vector3.one * 0.04f);
        }

        private static Transform Wing(Transform parent, Material material, float side, Vector3 size)
        {
            var pivot = new GameObject(side < 0f ? "Left Wing" : "Right Wing").transform;
            pivot.SetParent(parent, false);
            Part(PrimitiveType.Cube, pivot, material, new Vector3(side * size.x * 0.5f, 0f, 0f), size);
            return pivot;
        }

        private static Transform Part(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale) =>
            Part(type, parent, material, position, scale, Quaternion.identity);

        private static Transform Part(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var part = GameObject.CreatePrimitive(type);
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            return part.transform;
        }
    }
}
