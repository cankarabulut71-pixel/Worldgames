using System;
using UnityEngine;

namespace TinyPlanet.Simulation
{
    /// <summary>
    /// The planet surface as evenly spread cells (a Fibonacci lattice on the unit sphere),
    /// with the nearest neighbours of every cell precomputed.
    /// </summary>
    public sealed class PlanetGrid
    {
        public const int NeighbourCount = 6;

        private readonly Vector3[] directions;
        private readonly int[][] neighbours;

        public PlanetGrid(int cellCount)
        {
            if (cellCount < NeighbourCount + 1)
                throw new ArgumentOutOfRangeException(nameof(cellCount));

            directions = BuildLattice(cellCount);
            neighbours = BuildNeighbours(directions);
        }

        public int CellCount => directions.Length;

        /// <summary>Unit vector from the planet centre to the cell.</summary>
        public Vector3 Direction(int cell) => directions[cell];

        public int[] Neighbours(int cell) => neighbours[cell];

        public bool IsValid(int cell) => cell >= 0 && cell < directions.Length;

        /// <summary>The cell whose centre is closest to the given direction.</summary>
        public int FindNearestCell(Vector3 direction)
        {
            direction.Normalize();
            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < directions.Length; i++)
            {
                float dot = Vector3.Dot(directions[i], direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        private static Vector3[] BuildLattice(int count)
        {
            var result = new Vector3[count];
            double goldenAngle = Math.PI * (3.0 - Math.Sqrt(5.0));
            for (int i = 0; i < count; i++)
            {
                double y = 1.0 - (2.0 * i + 1.0) / count;
                double radius = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                double theta = goldenAngle * i;
                result[i] = new Vector3(
                    (float)(Math.Cos(theta) * radius),
                    (float)y,
                    (float)(Math.Sin(theta) * radius));
            }
            return result;
        }

        private static int[][] BuildNeighbours(Vector3[] dirs)
        {
            int n = dirs.Length;
            var result = new int[n][];
            var bestIndex = new int[NeighbourCount];
            var bestDot = new float[NeighbourCount];

            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < NeighbourCount; k++)
                {
                    bestIndex[k] = -1;
                    bestDot[k] = float.NegativeInfinity;
                }

                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    float dot = Vector3.Dot(dirs[i], dirs[j]);
                    if (dot <= bestDot[NeighbourCount - 1]) continue;

                    // Insert into the sorted top-K list (closest first).
                    int slot = NeighbourCount - 1;
                    while (slot > 0 && dot > bestDot[slot - 1])
                    {
                        bestDot[slot] = bestDot[slot - 1];
                        bestIndex[slot] = bestIndex[slot - 1];
                        slot--;
                    }
                    bestDot[slot] = dot;
                    bestIndex[slot] = j;
                }

                result[i] = (int[])bestIndex.Clone();
            }
            return result;
        }
    }
}
