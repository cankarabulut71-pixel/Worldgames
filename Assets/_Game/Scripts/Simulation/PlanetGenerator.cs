using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyPlanet.Simulation
{
    /// <summary>
    /// Builds a planet from a seed. The same seed, cell count and biome list always give the same planet.
    /// </summary>
    public static class PlanetGenerator
    {
        private const int FieldLobes = 4;
        private const int PondSize = 8;

        // How much noise bends the borders between biome regions, so they are not straight lines.
        private const float BorderWarp = 0.45f;

        /// <summary>A planet with a single biome where anything grows.</summary>
        public static Planet Generate(int seed, int cellCount) =>
            Generate(seed, cellCount, new[] { BiomeSpec.Default() });

        public static Planet Generate(int seed, int cellCount, IReadOnlyList<BiomeSpec> biomes)
        {
            if (biomes == null || biomes.Count == 0) biomes = new[] { BiomeSpec.Default() };

            var grid = new PlanetGrid(cellCount);
            var random = new System.Random(seed);

            var moistureField = RandomField(random);
            var fertilityField = RandomField(random);
            var centres = SpreadDirections(random, biomes.Count);
            var warps = new Vector4[biomes.Count][];
            for (int b = 0; b < biomes.Count; b++) warps[b] = RandomField(random);

            var cells = new CellData[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                Vector3 dir = grid.Direction(i);
                int biome = ClosestRegion(dir, biomes, centres, warps);
                var spec = biomes[biome];
                cells[i] = new CellData
                {
                    Biome = biome,
                    Moisture = Mathf.Clamp01(spec.BaseMoisture + (Sample(moistureField, dir) - 0.5f) * 0.6f),
                    Fertility = Mathf.Clamp01(spec.BaseFertility + (Sample(fertilityField, dir) - 0.5f) * 0.6f),
                };
            }

            var planet = new Planet(seed, grid, cells, biomes);
            for (int b = 0; b < biomes.Count; b++) DigPonds(planet, b, random);
            return planet;
        }

        // Each biome owns the cells nearest its centre; per-biome noise makes the borders wander.
        private static int ClosestRegion(Vector3 dir, IReadOnlyList<BiomeSpec> biomes, Vector3[] centres, Vector4[][] warps)
        {
            int best = 0;
            float bestScore = float.NegativeInfinity;
            for (int b = 0; b < biomes.Count; b++)
            {
                float score = Vector3.Dot(dir, centres[b])
                              + (Sample(warps[b], dir) - 0.5f) * 2f * BorderWarp
                              + 0.3f * Mathf.Log(Mathf.Max(0.05f, biomes[b].Weight));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = b;
                }
            }
            return best;
        }

        // Ponds of about PondSize cells, grown out from scattered starting cells inside the biome.
        private static void DigPonds(Planet planet, int biome, System.Random random)
        {
            var cells = planet.Cells;
            var grid = planet.Grid;

            var land = new List<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Biome == biome) land.Add(i);
            }

            int target = (int)Math.Round(land.Count * planet.Biomes[biome].WaterFraction);
            if (target <= 0) return;

            int dug = 0;
            int pondLeft = 0;
            var frontier = new List<int>();
            while (dug < target)
            {
                if (pondLeft <= 0 || frontier.Count == 0)
                {
                    int start = PickPondStart(planet, biome, land, random);
                    if (start < 0) return;
                    frontier.Clear();
                    frontier.Add(start);
                    pondLeft = PondSize;
                }

                int pick = random.Next(frontier.Count);
                int cell = frontier[pick];
                frontier.RemoveAt(pick);
                if (cells[cell].Water || cells[cell].Biome != biome) continue;

                cells[cell].Water = true;
                cells[cell].Moisture = 1f;
                dug++;
                pondLeft--;
                foreach (int next in grid.Neighbours(cell))
                {
                    if (!cells[next].Water && cells[next].Biome == biome) frontier.Add(next);
                }
            }
        }

        // Of a few random dry cells, the one farthest from existing water, so ponds spread across the biome.
        private static int PickPondStart(Planet planet, int biome, List<int> land, System.Random random)
        {
            int best = -1;
            int bestDistance = -1;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int cell = land[random.Next(land.Count)];
                if (planet.Cells[cell].Water) continue;

                int distance = 0;
                while (distance < 3 && !HasWaterWithin(planet, cell, distance)) distance++;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }
            if (best >= 0) return best;

            foreach (int cell in land)
            {
                if (!planet.Cells[cell].Water) return cell;
            }
            return -1;
        }

        private static bool HasWaterWithin(Planet planet, int cell, int radius)
        {
            foreach (int near in planet.Grid.CellsWithin(cell, radius))
            {
                if (planet.Cells[near].Water) return true;
            }
            return false;
        }

        // Region centres as far apart as a few random tries allow, so every biome gets a fair share.
        private static Vector3[] SpreadDirections(System.Random random, int count)
        {
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 best = RandomDirection(random);
                float bestClosest = float.PositiveInfinity;
                for (int attempt = 0; attempt < 24 && i > 0; attempt++)
                {
                    Vector3 candidate = attempt == 0 ? best : RandomDirection(random);
                    float closest = float.NegativeInfinity;
                    for (int j = 0; j < i; j++) closest = Mathf.Max(closest, Vector3.Dot(candidate, result[j]));
                    if (closest < bestClosest)
                    {
                        bestClosest = closest;
                        best = candidate;
                    }
                }
                result[i] = best;
            }
            return result;
        }

        // A smooth scalar field on the sphere: a few random directions, each pulling the value up or down.
        private static Vector4[] RandomField(System.Random random)
        {
            var lobes = new Vector4[FieldLobes];
            for (int i = 0; i < FieldLobes; i++)
            {
                Vector3 dir = RandomDirection(random);
                float weight = (float)(random.NextDouble() * 2.0 - 1.0) * 0.35f;
                lobes[i] = new Vector4(dir.x, dir.y, dir.z, weight);
            }
            return lobes;
        }

        private static float Sample(Vector4[] lobes, Vector3 dir)
        {
            float value = 0.5f;
            for (int i = 0; i < lobes.Length; i++)
            {
                Vector4 lobe = lobes[i];
                value += lobe.w * (dir.x * lobe.x + dir.y * lobe.y + dir.z * lobe.z);
            }
            return Mathf.Clamp01(value);
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            double y = random.NextDouble() * 2.0 - 1.0;
            double theta = random.NextDouble() * Math.PI * 2.0;
            double radius = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            return new Vector3((float)(Math.Cos(theta) * radius), (float)y, (float)(Math.Sin(theta) * radius));
        }
    }
}
