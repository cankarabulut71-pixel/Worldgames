using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Decorative woods, reeds and rocks drawn with GPU instancing. Rocks are always there; the woods fill in
    /// around mature plants, so the planet grows greener the more the player plants. Purely visual.
    /// </summary>
    public sealed class GroundCover : MonoBehaviour
    {
        private const int MaxPerCall = 1023;
        private const float GrowSeconds = 0.8f;

        private sealed class Slot
        {
            public int Cell;
            public int Variant;
            public Matrix4x4 Full;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public float Shown;      // 0..1, eased towards Target so new trees grow in
            public float Target;
        }

        private sealed class Batch
        {
            public Mesh Mesh;
            public readonly List<Slot> Slots = new List<Slot>();
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>();
        }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly Dictionary<int, List<Slot>> slotsByCell = new Dictionary<int, List<Slot>>();
        private readonly List<Slot> growing = new List<Slot>();
        private readonly Matrix4x4[] chunk = new Matrix4x4[MaxPerCall];
        private PlanetSimulation simulation;
        private TerrainShape terrain;
        private Material material;
        private RenderParams renderParams;

        public void Init(PlanetSimulation simulation, TerrainShape terrain, IReadOnlyList<BiomeDefinition> biomes, Material material, PlanetLook look)
        {
            this.simulation = simulation;
            this.terrain = terrain;
            this.material = material;
            renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * (terrain.Radius * 3f)),
            };

            var planet = simulation.Planet;
            var grid = planet.Grid;
            float cellAngle = Mathf.Sqrt(4f * Mathf.PI / grid.CellCount);
            var random = new System.Random(planet.Seed ^ 0x6f7e57);

            // Per biome: two cover variants (one batch each) and a rock batch.
            var coverBatches = new Batch[biomes.Count, 2];
            var rockBatches = new Batch[biomes.Count];
            for (int b = 0; b < biomes.Count; b++)
            {
                var biome = biomes[b];
                if (biome == null) continue;
                if (biome.coverShape != CoverShape.None && biome.coverPerCell > 0)
                {
                    Color main = look.Shift(biome.coverColor), accent = look.Shift(biome.coverAccent);
                    coverBatches[b, 0] = NewBatch(CoverMesh(biome.coverShape, main, accent, 0));
                    coverBatches[b, 1] = NewBatch(CoverMesh(biome.coverShape, accent, main, 1));
                }
                if (biome.decorationDensity > 0f && biome.decorationColor.a > 0f)
                    rockBatches[b] = NewBatch(RockMesh(biome.decorationColor));
            }

            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                int b = planet.Cells[cell].Biome;
                var biome = b < biomes.Count ? biomes[b] : null;
                Vector3 centre = grid.Direction(cell);
                if (biome == null) continue;

                int cover = coverBatches[b, 0] != null && !planet.IsWater(cell) ? biome.coverPerCell : 0;
                for (int i = 0; i < cover; i++)
                {
                    // Around the cell centre, leaving the middle free for the planted plant.
                    Vector3 dir = Offset(centre, cellAngle * (0.3f + 0.25f * (float)random.NextDouble()), (float)random.NextDouble() * 360f);
                    int variant = random.Next(2);
                    float scale = 0.75f + (float)random.NextDouble() * 0.6f;
                    float turn = (float)random.NextDouble() * 360f;
                    if (terrain.HeightAt(dir) < 0.03f) continue;
                    var slot = MakeSlot(cell, dir, scale, turn, variant);
                    coverBatches[b, variant].Slots.Add(slot);
                    if (!slotsByCell.TryGetValue(cell, out var list)) slotsByCell[cell] = list = new List<Slot>();
                    list.Add(slot);
                }

                if (rockBatches[b] != null && random.NextDouble() < biome.decorationDensity)
                {
                    Vector3 dir = Offset(centre, cellAngle * 0.5f, (float)random.NextDouble() * 360f);
                    float scale = 0.6f + (float)random.NextDouble() * 1.1f;
                    if (terrain.HeightAt(dir) < 0.02f) continue;
                    var rock = MakeSlot(cell, dir, scale, (float)random.NextDouble() * 360f, 0);
                    rock.Shown = rock.Target = 1f;
                    rockBatches[b].Slots.Add(rock);
                }
            }

            foreach (int cell in slotsByCell.Keys) Refresh(cell, instant: true);
            RebuildMatrices();

            simulation.PlantStageChanged += OnStageChanged;
            simulation.PlantRemoved += OnRemoved;
            simulation.BiomeUnlocked += OnBiomeUnlocked;
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.PlantStageChanged -= OnStageChanged;
            simulation.PlantRemoved -= OnRemoved;
            simulation.BiomeUnlocked -= OnBiomeUnlocked;
        }

        private void Update()
        {
            if (growing.Count > 0)
            {
                float step = Time.deltaTime / GrowSeconds;
                for (int i = growing.Count - 1; i >= 0; i--)
                {
                    var slot = growing[i];
                    slot.Shown = Mathf.MoveTowards(slot.Shown, slot.Target, step);
                    if (Mathf.Approximately(slot.Shown, slot.Target)) growing.RemoveAt(i);
                }
                RebuildMatrices();
            }

            foreach (var batch in batches)
            {
                int total = batch.Matrices.Count;
                for (int start = 0; start < total; start += MaxPerCall)
                {
                    int count = Mathf.Min(MaxPerCall, total - start);
                    batch.Matrices.CopyTo(start, chunk, 0, count);
                    Graphics.RenderMeshInstanced(renderParams, batch.Mesh, 0, chunk, count);
                }
            }
        }

        private void OnStageChanged(PlantInstance plant, PlantStage stage)
        {
            if (stage == PlantStage.Mature) RefreshAround(plant.Cell);
        }

        private void OnRemoved(PlantInstance plant) => RefreshAround(plant.Cell);

        private void OnBiomeUnlocked(string biomeId)
        {
            foreach (int cell in slotsByCell.Keys) Refresh(cell, instant: false);
        }

        private void RefreshAround(int cell)
        {
            foreach (int near in simulation.Planet.Grid.CellsWithin(cell, 1)) Refresh(near, instant: false);
        }

        // How many of a cell's cover slots are shown follows how many mature plants grow in and around it.
        private void Refresh(int cell, bool instant)
        {
            if (!slotsByCell.TryGetValue(cell, out var slots)) return;

            int mature = 0;
            if (simulation.IsAwake(cell))
            {
                long now = simulation.NowUtcTicks;
                foreach (int near in simulation.Planet.Grid.CellsWithin(cell, 1))
                {
                    var plant = simulation.PlantAt(near);
                    if (plant != null && plant.StageAt(now) == PlantStage.Mature) mature++;
                }
            }

            float lush = Mathf.Clamp01(mature / 3f);
            int shown = Mathf.RoundToInt(slots.Count * lush);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                float target = i < shown ? 1f : 0f;
                if (Mathf.Approximately(slot.Target, target) && (instant || Mathf.Approximately(slot.Shown, target))) continue;
                slot.Target = target;
                if (instant) slot.Shown = target;
                else if (!growing.Contains(slot)) growing.Add(slot);
            }
            if (!instant && growing.Count > 0) enabled = true;
        }

        private void RebuildMatrices()
        {
            foreach (var batch in batches)
            {
                batch.Matrices.Clear();
                foreach (var slot in batch.Slots)
                {
                    if (slot.Shown <= 0.001f) continue;
                    float t = slot.Shown;
                    float eased = t >= 1f ? 1f : 1f - (1f - t) * (1f - t) * (1f - t);
                    batch.Matrices.Add(eased >= 1f ? slot.Full : Matrix4x4.TRS(slot.Position, slot.Rotation, Vector3.one * (slot.Scale * eased)));
                }
            }
        }

        private Slot MakeSlot(int cell, Vector3 dir, float scale, float turn, int variant)
        {
            // Sink the base a little so it sits in sloped ground rather than floating.
            Vector3 position = dir * (terrain.SurfaceRadius(dir) - 0.02f * scale);
            var rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, turn, 0f);
            return new Slot
            {
                Cell = cell,
                Variant = variant,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                Full = Matrix4x4.TRS(position, rotation, Vector3.one * scale),
            };
        }

        private Batch NewBatch(Mesh mesh)
        {
            var batch = new Batch { Mesh = mesh };
            batches.Add(batch);
            return batch;
        }

        private static Vector3 Offset(Vector3 centre, float angle, float aroundDegrees)
        {
            Vector3 tangent = Vector3.Cross(centre, Mathf.Abs(centre.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            tangent = Quaternion.AngleAxis(aroundDegrees, centre) * tangent;
            return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.Cross(centre, tangent)) * centre;
        }

        // ---------- meshes ----------

        private static readonly Color Bark = new Color(0.42f, 0.29f, 0.18f);

        private static Mesh CoverMesh(CoverShape shape, Color main, Color other, int seed)
        {
            var kit = new MeshKit();
            switch (shape)
            {
                case CoverShape.RoundTrees:
                    kit.Cone(Vector3.zero, 0.16f, 0.035f, 0.025f, 5, Bark);
                    kit.Blob(new Vector3(0f, 0.25f, 0f), new Vector3(0.26f, 0.24f, 0.26f), main, 1, 0.12f, seed);
                    kit.Blob(new Vector3(0.06f, 0.32f, 0.03f), new Vector3(0.15f, 0.14f, 0.15f), Color.Lerp(main, other, 0.5f), 0, 0.1f, seed + 7);
                    break;

                case CoverShape.Pines:
                    kit.Cone(Vector3.zero, 0.1f, 0.03f, 0.022f, 5, Bark);
                    kit.Cone(new Vector3(0f, 0.07f, 0f), 0.22f, 0.13f, 0f, 7, main);
                    kit.Cone(new Vector3(0f, 0.17f, 0f), 0.2f, 0.1f, 0f, 7, Color.Lerp(main, other, 0.4f), 0.4f);
                    kit.Cone(new Vector3(0f, 0.27f, 0f), 0.16f, 0.065f, 0f, 7, other, 0.2f);
                    break;

                case CoverShape.Reeds:
                    var random = new System.Random(seed + 11);
                    for (int i = 0; i < 7; i++)
                    {
                        var foot = new Vector3(((float)random.NextDouble() - 0.5f) * 0.12f, 0f, ((float)random.NextDouble() - 0.5f) * 0.12f);
                        float height = 0.16f + (float)random.NextDouble() * 0.14f;
                        kit.Blade(foot, height, 0.012f, ((float)random.NextDouble() - 0.5f) * 30f, (float)random.NextDouble() * 360f, i % 3 == 0 ? other : main);
                    }
                    break;
            }
            return kit.Build("Cover " + shape + " " + seed);
        }

        private static Mesh RockMesh(Color color)
        {
            var kit = new MeshKit();
            kit.Blob(new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.11f, 0.13f), color, 0, 0.25f, 3);
            kit.Blob(new Vector3(0.07f, 0.015f, 0.04f), new Vector3(0.08f, 0.06f, 0.07f), color * 0.92f, 0, 0.3f, 5);
            return kit.Build("Rock");
        }
    }
}
