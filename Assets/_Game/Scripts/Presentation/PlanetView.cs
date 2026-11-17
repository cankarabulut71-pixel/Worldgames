using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Draws the planet: relief and ground colours, the sea-level water, the atmosphere glow, drifting clouds
    /// and the growing ground cover, and keeps one PlantView per planted cell in sync with the simulation.
    /// Dormant biomes are drawn grey until they are woken.
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        private const int GroundSubdivisions = 5;
        private const int WaterSubdivisions = 4;
        private const int MaxBiomes = 8;
        private const float WakeSeconds = 2f;
        private static readonly int BiomeWakeId = Shader.PropertyToID("_BiomeWake");

        private readonly Dictionary<int, PlantView> active = new Dictionary<int, PlantView>();
        private readonly Dictionary<string, Stack<PlantView>> pools = new Dictionary<string, Stack<PlantView>>();
        private readonly Dictionary<string, PlantDefinition> definitions = new Dictionary<string, PlantDefinition>();
        private readonly float[] wake = new float[MaxBiomes];
        private readonly float[] wakeTime = new float[MaxBiomes];
        private readonly List<Transform> clouds = new List<Transform>();
        private readonly List<Vector3> cloudAxes = new List<Vector3>();

        private PlanetSimulation simulation;
        private GameConfig config;
        private MaterialLibrary materials;

        public float Radius { get; private set; }
        public TerrainShape Terrain { get; private set; }

        public void Init(PlanetSimulation simulation, GameConfig config, MaterialLibrary materials, IReadOnlyList<BiomeDefinition> biomes, PlanetLook look, float radius)
        {
            this.simulation = simulation;
            this.config = config;
            this.materials = materials;
            Radius = radius;
            Terrain = new TerrainShape(simulation.Planet, Radius, biomes, config, look);

            foreach (var definition in config.plants)
                definitions[definition.id] = definition;

            var planetBiomes = simulation.Planet.Biomes;
            for (int b = 0; b < MaxBiomes; b++)
            {
                wake[b] = b < planetBiomes.Count && !simulation.IsBiomeUnlocked(planetBiomes[b].Id) ? 0f : 1f;
                wakeTime[b] = -1f;
            }
            Shader.SetGlobalFloatArray(BiomeWakeId, wake);

            MeshObject("Ground", Terrain.BuildGround(GroundSubdivisions), config.terrainMaterial);
            var water = new Material(config.waterMaterial);
            water.SetColor("_Shallow", look.Palette.waterShallow);
            water.SetColor("_Deep", look.Palette.waterDeep);
            MeshObject("Water", Terrain.BuildWater(WaterSubdivisions), water);
            var atmosphere = new Material(config.atmosphereMaterial);
            atmosphere.SetColor("_Color", look.Palette.atmosphere);
            BuildAtmosphere(atmosphere);
            BuildClouds();

            var cover = new GameObject("Ground Cover").AddComponent<GroundCover>();
            cover.transform.SetParent(transform, false);
            cover.Init(simulation, Terrain, biomes, config.terrainMaterial, look);

            long now = simulation.NowUtcTicks;
            foreach (var plant in simulation.Plants)
                Spawn(plant, plant.StageAt(now), animate: false);

            simulation.PlantPlanted += OnPlanted;
            simulation.PlantRemoved += OnRemoved;
            simulation.PlantStageChanged += OnStageChanged;
            simulation.BiomeUnlocked += OnBiomeUnlocked;
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.PlantPlanted -= OnPlanted;
            simulation.PlantRemoved -= OnRemoved;
            simulation.PlantStageChanged -= OnStageChanged;
            simulation.BiomeUnlocked -= OnBiomeUnlocked;
        }

        /// <summary>Finds the surface cell under a ray, if the ray hits the planet.</summary>
        public bool TryGetCell(Ray ray, out int cell)
        {
            cell = -1;
            Vector3 origin = ray.origin - transform.position;

            // Hit a sphere at sea level, then refine against the real ground height a couple of times.
            float radius = Radius;
            Vector3 hit = Vector3.zero;
            for (int pass = 0; pass < 3; pass++)
            {
                float b = Vector3.Dot(origin, ray.direction);
                float c = Vector3.Dot(origin, origin) - radius * radius;
                float discriminant = b * b - c;
                if (discriminant < 0f)
                {
                    if (pass == 0) return false;
                    break;
                }
                float distance = -b - Mathf.Sqrt(discriminant);
                if (distance < 0f) return false;
                hit = origin + ray.direction * distance;
                radius = Terrain.SurfaceRadius(hit);
            }

            cell = simulation.Planet.Grid.FindNearestCell(hit);
            return true;
        }

        /// <summary>World position of a cell's centre on the ground.</summary>
        public Vector3 CellPoint(int cell) =>
            transform.TransformPoint(simulation.Planet.Grid.Direction(cell) * Terrain.CellSurface(cell));

        /// <summary>World direction straight up from a cell.</summary>
        public Vector3 CellNormal(int cell) => transform.TransformDirection(simulation.Planet.Grid.Direction(cell));

        /// <summary>Roughly how wide one cell is on the ground, in world units.</summary>
        public float CellSize => Mathf.Sqrt(4f * Mathf.PI / simulation.Planet.Grid.CellCount) * Radius;

        /// <summary>The average direction of a biome's cells: roughly the middle of its region.</summary>
        public Vector3 BiomeCentre(int biome)
        {
            var planet = simulation.Planet;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < planet.Cells.Length; i++)
            {
                if (planet.Cells[i].Biome == biome) sum += planet.Grid.Direction(i);
            }
            return sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector3.forward;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < clouds.Count; i++)
                clouds[i].localRotation = Quaternion.AngleAxis(dt * (1.2f + i * 0.35f), cloudAxes[i]) * clouds[i].localRotation;

            bool changed = false;
            for (int b = 0; b < MaxBiomes; b++)
            {
                if (wakeTime[b] < 0f) continue;
                wakeTime[b] += dt;
                wake[b] = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wakeTime[b] / WakeSeconds));
                if (wakeTime[b] >= WakeSeconds) wakeTime[b] = -1f;
                changed = true;
            }
            if (changed) Shader.SetGlobalFloatArray(BiomeWakeId, wake);
        }

        private void OnBiomeUnlocked(string biomeId)
        {
            var biomes = simulation.Planet.Biomes;
            for (int b = 0; b < biomes.Count && b < MaxBiomes; b++)
            {
                if (biomes[b].Id == biomeId && wake[b] < 1f) wakeTime[b] = 0f;
            }
        }

        private void OnPlanted(PlantInstance plant) => Spawn(plant, PlantStage.Seed, animate: true);

        private void OnStageChanged(PlantInstance plant, PlantStage stage)
        {
            if (active.TryGetValue(plant.Cell, out var view))
                view.Show(stage, animate: true);
        }

        private void OnRemoved(PlantInstance plant)
        {
            if (!active.TryGetValue(plant.Cell, out var view)) return;
            active.Remove(plant.Cell);
            view.gameObject.SetActive(false);
            Pool(plant.Spec.Id).Push(view);
        }

        private void Spawn(PlantInstance plant, PlantStage stage, bool animate)
        {
            if (!definitions.TryGetValue(plant.Spec.Id, out var definition)) return;

            var pool = Pool(definition.id);
            PlantView view;
            if (pool.Count > 0)
            {
                view = pool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject(definition.displayName);
                go.transform.SetParent(transform, false);
                view = go.AddComponent<PlantView>();
                view.Build(definition, config, materials);
            }

            Vector3 direction = simulation.Planet.Grid.Direction(plant.Cell);
            float turn = (plant.Cell * 137.5f) % 360f; // a stable, varied facing per cell
            view.transform.localPosition = direction * (Terrain.CellSurface(plant.Cell) - 0.02f);
            view.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0f, turn, 0f);
            view.Show(stage, animate);
            active[plant.Cell] = view;
        }

        private Stack<PlantView> Pool(string id)
        {
            if (!pools.TryGetValue(id, out var pool))
            {
                pool = new Stack<PlantView>();
                pools[id] = pool;
            }
            return pool;
        }

        private void BuildAtmosphere(Material material)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            MeshKit.Icosphere(3, points, triangles);
            var vertices = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++) vertices[i] = points[i] * (Radius * 1.22f);

            var mesh = new Mesh { name = "Atmosphere" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            MeshObject("Atmosphere", mesh, material);
        }

        // A handful of puffy clouds that slowly circle the planet.
        private void BuildClouds()
        {
            var random = new System.Random(simulation.Planet.Seed ^ 0xc10d);
            var white = new Color(0.97f, 0.99f, 1f);
            for (int c = 0; c < 6; c++)
            {
                var kit = new MeshKit();
                int puffs = 3 + random.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    float x = (p - (puffs - 1) * 0.5f) * 0.32f + ((float)random.NextDouble() - 0.5f) * 0.1f;
                    float size = 0.38f + (float)random.NextDouble() * 0.22f - Mathf.Abs(x) * 0.25f;
                    kit.Blob(new Vector3(x, size * 0.25f, ((float)random.NextDouble() - 0.5f) * 0.2f),
                        new Vector3(size, size * 0.7f, size * 0.85f), white, 1, 0.08f, c * 10 + p);
                }

                var holder = new GameObject("Cloud").transform;
                holder.SetParent(transform, false);
                var direction = new Vector3((float)random.NextDouble() - 0.5f, ((float)random.NextDouble() - 0.5f) * 1.4f, (float)random.NextDouble() - 0.5f).normalized;
                holder.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

                var cloud = MeshObject("Puffs", kit.Build("Cloud"), config.terrainMaterial);
                cloud.transform.SetParent(holder, false);
                cloud.transform.localPosition = Vector3.up * (Radius + 1.05f + (float)random.NextDouble() * 0.3f);
                cloud.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

                clouds.Add(holder);
                cloudAxes.Add(Vector3.Cross(direction, Random.onUnitSphere).normalized);
            }
        }

        private GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
