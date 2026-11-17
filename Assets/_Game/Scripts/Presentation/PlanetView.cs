using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>Draws the planet and keeps one PlantView per planted cell in sync with the simulation.</summary>
    public sealed class PlanetView : MonoBehaviour
    {
        private readonly Dictionary<int, PlantView> active = new Dictionary<int, PlantView>();
        private readonly Dictionary<string, Stack<PlantView>> pools = new Dictionary<string, Stack<PlantView>>();
        private readonly Dictionary<string, PlantDefinition> definitions = new Dictionary<string, PlantDefinition>();

        private PlanetSimulation simulation;
        private GameConfig config;
        private MaterialLibrary materials;

        public float Radius { get; private set; }

        public void Init(PlanetSimulation simulation, GameConfig config, MaterialLibrary materials)
        {
            this.simulation = simulation;
            this.config = config;
            this.materials = materials;
            Radius = config.planetRadius;

            foreach (var definition in config.plants)
                definitions[definition.id] = definition;

            BuildSurface();
            BuildCellDots();
            BuildWater();

            long now = simulation.NowUtcTicks;
            foreach (var plant in simulation.Plants)
                Spawn(plant, plant.StageAt(now), animate: false);

            simulation.PlantPlanted += OnPlanted;
            simulation.PlantRemoved += OnRemoved;
            simulation.PlantStageChanged += OnStageChanged;
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.PlantPlanted -= OnPlanted;
            simulation.PlantRemoved -= OnRemoved;
            simulation.PlantStageChanged -= OnStageChanged;
        }

        /// <summary>Finds the surface cell under a ray, if the ray hits the planet.</summary>
        public bool TryGetCell(Ray ray, out int cell)
        {
            cell = -1;
            Vector3 origin = ray.origin - transform.position;
            float b = Vector3.Dot(origin, ray.direction);
            float c = Vector3.Dot(origin, origin) - Radius * Radius;
            float discriminant = b * b - c;
            if (discriminant < 0f) return false;

            float distance = -b - Mathf.Sqrt(discriminant);
            if (distance < 0f) return false;

            cell = simulation.Planet.Grid.FindNearestCell(origin + ray.direction * distance);
            return true;
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
            view.transform.localPosition = direction * Radius;
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

        private void BuildSurface()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Surface";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * (Radius * 2f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = materials.Lit(config.planetColor);
        }

        // One small marker per cell, all in a single mesh, so the player can see where plants will land.
        private void BuildCellDots()
        {
            var planet = simulation.Planet;
            var grid = planet.Grid;
            const float halfSize = 0.07f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                if (planet.IsWater(cell)) continue;
                int i = vertices.Count / 4;
                vertices.AddRange(new Vector3[4]);
                triangles.AddRange(new int[6]);

                Vector3 normal = grid.Direction(cell);
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Vector3 centre = normal * (Radius + 0.012f);

                int v = i * 4;
                vertices[v] = centre + (-tangent - bitangent) * halfSize;
                vertices[v + 1] = centre + (-tangent + bitangent) * halfSize;
                vertices[v + 2] = centre + (tangent + bitangent) * halfSize;
                vertices[v + 3] = centre + (tangent - bitangent) * halfSize;

                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "Cell Dots" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var dots = new GameObject("Cell Dots");
            dots.transform.SetParent(transform, false);
            dots.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = dots.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Unlit(config.cellDotColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Each water cell gets a flat disc a bit wider than the cell, so neighbouring discs merge into ponds.
        private void BuildWater()
        {
            var planet = simulation.Planet;
            var grid = planet.Grid;
            const int segments = 12;
            // Wide enough to close the gaps where three discs meet, short of the neighbouring land cells' centres.
            float discRadius = Radius * Mathf.Sqrt(4f * Mathf.PI / grid.CellCount) * 0.85f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                if (!planet.IsWater(cell)) continue;

                Vector3 normal = grid.Direction(cell);
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent);

                int centre = vertices.Count;
                vertices.Add(normal * (Radius + 0.015f));
                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    Vector3 rim = normal * Radius + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * discRadius;
                    vertices.Add(rim.normalized * (Radius + 0.015f));
                }
                for (int s = 0; s < segments; s++)
                {
                    triangles.Add(centre);
                    triangles.Add(centre + 1 + s);
                    triangles.Add(centre + 1 + (s + 1) % segments);
                }
            }
            if (vertices.Count == 0) return;

            var mesh = new Mesh { name = "Water" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var water = new GameObject("Water");
            water.transform.SetParent(transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Unlit(config.waterColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
