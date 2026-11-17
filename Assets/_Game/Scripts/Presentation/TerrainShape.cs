using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// The planet's relief and ground colours, worked out from the cells: each biome has its own height and
    /// hilliness, ponds dip below sea level, and everything is blended smoothly between neighbouring cells.
    /// Presentation only: the simulation never sees heights. Sea level is at <see cref="Radius"/>.
    /// </summary>
    public sealed class TerrainShape
    {
        private const float WaterDepth = 0.2f;

        private readonly Planet planet;
        private readonly float[] cellHeight;
        private readonly Color[] cellColor;
        private readonly float cellAngle;
        private readonly float kernel;
        private readonly Color snowColor;
        private readonly float snowLine;

        public TerrainShape(Planet planet, float radius, IReadOnlyList<BiomeDefinition> biomes, GameConfig config, PlanetLook look)
        {
            this.planet = planet;
            Radius = radius;
            snowColor = config.snowColor;
            snowLine = config.snowLine;

            int count = planet.Grid.CellCount;
            cellAngle = Mathf.Sqrt(4f * Mathf.PI / count);
            float sigma = cellAngle * 0.6f;
            kernel = 1f / (2f * sigma * sigma);

            var random = new System.Random(planet.Seed ^ 0x7e11a1);
            var waves = new Vector4[5];
            for (int i = 0; i < waves.Length; i++)
            {
                var dir = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f).normalized;
                waves[i] = new Vector4(dir.x, dir.y, dir.z, (float)random.NextDouble() * 6.28f);
            }

            cellHeight = new float[count];
            cellColor = new Color[count];
            for (int cell = 0; cell < count; cell++)
            {
                var data = planet.Cells[cell];
                var biome = data.Biome < biomes.Count ? biomes[data.Biome] : null;
                Vector3 dir = planet.Grid.Direction(cell);

                // A few overlapping waves around the sphere give rolling hills; squaring sharpens the peaks.
                float noise = 0f;
                for (int i = 0; i < waves.Length; i++)
                {
                    Vector4 w = waves[i];
                    noise += Mathf.Sin(Vector3.Dot(dir, new Vector3(w.x, w.y, w.z)) * (5f + i * 2.3f) + w.w);
                }
                noise = Mathf.Clamp01(noise / waves.Length * 0.5f + 0.5f);
                float jitter = (float)random.NextDouble();

                float baseHeight = biome != null ? biome.baseHeight : 0.06f;
                float hills = (biome != null ? biome.hilliness : 0.05f) * look.MountainScale;
                cellHeight[cell] = data.Water ? -WaterDepth : baseHeight + hills * (noise * noise * 0.8f + jitter * 0.2f);

                Color ground = look.Shift(biome != null ? biome.groundColor : config.planetColor);
                float shade = 0.94f + jitter * 0.12f;
                cellColor[cell] = data.Water ? config.sandColor : new Color(ground.r * shade, ground.g * shade, ground.b * shade);
            }
        }

        public float Radius { get; }

        /// <summary>Smoothed ground height above sea level (negative under ponds).</summary>
        public float HeightAt(Vector3 direction) => Sample(direction.normalized, out _, out _);

        /// <summary>Distance from the planet centre to the ground, or to the water where there is a pond.</summary>
        public float SurfaceRadius(Vector3 direction) => Radius + Mathf.Max(0f, HeightAt(direction));

        public float CellSurface(int cell) => SurfaceRadius(planet.Grid.Direction(cell));

        /// <summary>Ground colour (with snow on high peaks), the biome of the nearest cell, and the height.</summary>
        public float Sample(Vector3 direction, out Color color, out int biome)
        {
            var grid = planet.Grid;
            int nearest = grid.FindNearestCell(direction);
            biome = planet.Cells[nearest].Biome;

            float weightSum = 0f, height = 0f;
            float r = 0f, g = 0f, b = 0f;
            foreach (int cell in grid.CellsWithin(nearest, 2))
            {
                float distance = (grid.Direction(cell) - direction).sqrMagnitude;
                float weight = Mathf.Exp(-distance * kernel);
                weightSum += weight;
                height += cellHeight[cell] * weight;
                Color c = cellColor[cell];
                r += c.r * weight;
                g += c.g * weight;
                b += c.b * weight;
            }

            height /= weightSum;
            color = new Color(r / weightSum, g / weightSum, b / weightSum);
            float snow = Mathf.SmoothStep(0f, 1f, (height - snowLine) / 0.08f);
            if (snow > 0f) color = Color.Lerp(color, snowColor, snow);
            return height;
        }

        /// <summary>The ground mesh: a subdivided icosphere pushed out to the terrain height, coloured per vertex.</summary>
        public Mesh BuildGround(int subdivisions)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            MeshKit.Icosphere(subdivisions, points, triangles);

            var colors = new Color[points.Count];
            var biomes = new Vector2[points.Count];
            var vertices = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float height = Sample(points[i], out colors[i], out int biome);
                biomes[i] = new Vector2(biome, 0f);
                vertices[i] = points[i] * (Radius + height);
            }

            var mesh = new Mesh { name = "Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(1, biomes);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The sea-level sphere; vertex red holds how deep the ground lies beneath it.</summary>
        public Mesh BuildWater(int subdivisions)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            MeshKit.Icosphere(subdivisions, points, triangles);

            var colors = new Color[points.Count];
            var biomes = new Vector2[points.Count];
            var vertices = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float height = Sample(points[i], out _, out int biome);
                colors[i] = new Color(Mathf.Clamp01(-height / WaterDepth * 1.4f), 0f, 0f, 1f);
                biomes[i] = new Vector2(biome, 0f);
                vertices[i] = points[i] * Radius;
            }

            var mesh = new Mesh { name = "Water" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(points);
            mesh.SetColors(colors);
            mesh.SetUVs(1, biomes);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
