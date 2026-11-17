using System;
using UnityEngine;

namespace TinyPlanet.Simulation
{
    /// <summary>Builds a planet from a seed. The same seed and cell count always give the same planet.</summary>
    public static class PlanetGenerator
    {
        private const int FieldLobes = 4;

        public static Planet Generate(int seed, int cellCount)
        {
            var grid = new PlanetGrid(cellCount);
            var random = new System.Random(seed);

            var moistureField = RandomField(random);
            var fertilityField = RandomField(random);

            var cells = new CellData[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                Vector3 dir = grid.Direction(i);
                cells[i] = new CellData
                {
                    Biome = BiomeType.Meadow,
                    Moisture = Sample(moistureField, dir),
                    Fertility = Sample(fertilityField, dir),
                };
            }

            return new Planet(seed, grid, cells);
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
