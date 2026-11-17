using System;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// One creature on the planet, drawn with its low-poly model. Wanders between cells near its home,
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
            var model = CreatureModels.For(definition);
            flies = model.Flies;
            hoverHeight = model.HoverHeight;

            // Glowing creatures are unlit, in an over-bright colour so the bloom picks them up.
            Material bodyMaterial = model.Glows ? materials.Unlit(definition.secondaryColor * 1.5f) : materials.Terrain;
            Material wingMaterial = model.Glows ? materials.Unlit(definition.primaryColor * 1.6f) : materials.Terrain;

            body = Part("Body", transform, model.Body, bodyMaterial, Vector3.zero);
            if (model.LeftWing != null)
            {
                leftWing = Part("Left Wing", body, model.LeftWing, wingMaterial, Vector3.zero);
                rightWing = Part("Right Wing", body, model.RightWing, wingMaterial, Vector3.zero);
            }
            if (model.Tail != null) tail = Part("Tail", body, model.Tail, materials.Terrain, model.TailPivot);
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

            if (tail != null)
            {
                tail.localRotation = Definition.shape == CreatureShape.Crawler
                    ? Quaternion.Euler(0f, Mathf.Sin(clock * 6f) * 20f, 0f)
                    : Quaternion.Euler(Mathf.Sin(clock * 3.1f) * 8f, 0f, Mathf.Sin(clock * 2.3f) * 6f);
            }

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

        private static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }
    }
}
