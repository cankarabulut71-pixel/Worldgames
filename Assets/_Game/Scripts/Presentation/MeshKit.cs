using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Builds small vertex-coloured low-poly meshes in code: spheres, cones, prisms and blades, merged into one
    /// mesh. TEXCOORD1.x carries a biome index for the terrain shader (99 = always awake).
    /// </summary>
    public sealed class MeshKit
    {
        public const float AlwaysAwake = 99f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> biomes = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public float Biome = AlwaysAwake;

        /// <summary>A low-poly sphere (icosahedron subdivided once) with flat facets.</summary>
        public MeshKit Blob(Vector3 centre, Vector3 size, Color color, int subdivisions = 1, float jitter = 0f, int seed = 0)
        {
            var points = new List<Vector3>();
            var tris = new List<int>();
            Icosphere(subdivisions, points, tris);
            var random = new System.Random(seed);
            for (int i = 0; i < points.Count; i++)
            {
                float j = 1f + ((float)random.NextDouble() - 0.5f) * 2f * jitter;
                points[i] = centre + Vector3.Scale(points[i] * j, size * 0.5f);
            }
            for (int i = 0; i < tris.Count; i += 3) Facet(points[tris[i]], points[tris[i + 1]], points[tris[i + 2]], color, centre);
            return this;
        }

        /// <summary>A cone (or a prism, when the top radius is not zero) standing on its base.</summary>
        public MeshKit Cone(Vector3 basePoint, float height, float bottomRadius, float topRadius, int sides, Color color, float twist = 0f)
        {
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int s = 0; s < sides; s++)
            {
                float angle = (s / (float)sides) * Mathf.PI * 2f + twist;
                var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottom[s] = basePoint + around * bottomRadius;
                top[s] = basePoint + Vector3.up * height + around * topRadius;
            }
            Vector3 apex = basePoint + Vector3.up * height;
            Vector3 inside = basePoint + Vector3.up * (height * 0.4f);
            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                if (topRadius <= 0f) Facet(bottom[s], apex, bottom[n], color, inside);
                else
                {
                    Facet(bottom[s], top[s], top[n], color, inside);
                    Facet(bottom[s], top[n], bottom[n], color, inside);
                    Facet(top[s], apex, top[n], color, inside);
                }
                Facet(bottom[s], bottom[n], basePoint, color, inside);
            }
            return this;
        }

        /// <summary>A thin leaning blade (three-sided spike) for grass and reeds.</summary>
        public MeshKit Blade(Vector3 basePoint, float height, float width, float leanDegrees, float turnDegrees, Color color)
        {
            var rotation = Quaternion.Euler(0f, turnDegrees, 0f) * Quaternion.Euler(leanDegrees, 0f, 0f);
            Vector3 tip = basePoint + rotation * (Vector3.up * height);
            var a = basePoint + rotation * new Vector3(-width, 0f, 0f);
            var b = basePoint + rotation * new Vector3(width, 0f, 0f);
            var c = basePoint + rotation * new Vector3(0f, 0f, width);
            Vector3 inside = (a + b + c) / 3f + (tip - basePoint) * 0.3f;
            Facet(a, tip, b, color, inside);
            Facet(b, tip, c, color, inside);
            Facet(c, tip, a, color, inside);
            return this;
        }

        /// <summary>Where the next shape starts, for <see cref="TransformSince"/>.</summary>
        public int Mark => vertices.Count;

        /// <summary>Moves, turns or scales every vertex added since a mark, for tilted or rotated parts.</summary>
        public MeshKit TransformSince(int mark, Matrix4x4 matrix)
        {
            for (int i = mark; i < vertices.Count; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                normals[i] = matrix.MultiplyVector(normals[i]).normalized;
            }
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(1, biomes);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat-shaded triangle, wound so that it faces away from a point inside the shape.
        private void Facet(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
            {
                var swap = b;
                b = c;
                c = swap;
                normal = -normal;
            }
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                colors.Add(color);
                biomes.Add(new Vector2(Biome, 0f));
                triangles.Add(start + i);
            }
        }

        /// <summary>A unit icosphere with triangles wound for Unity (clockwise seen from outside).</summary>
        public static void Icosphere(int subdivisions, List<Vector3> vertices, List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            foreach (var corner in corners) vertices.Add(corner.normalized);
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var list = new List<int>(faces);

            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                midpoints[key] = index;
                return index;
            }

            for (int level = 0; level < subdivisions; level++)
            {
                var next = new List<int>(list.Count * 4);
                for (int i = 0; i < list.Count; i += 3)
                {
                    int a = list[i], b = list[i + 1], c = list[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                list = next;
            }

            // Wind every triangle so its normal points away from the centre, whatever the table's convention.
            for (int i = 0; i < list.Count; i += 3)
            {
                Vector3 a = vertices[list[i]], b = vertices[list[i + 1]], c = vertices[list[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    int swap = list[i + 1];
                    list[i + 1] = list[i + 2];
                    list[i + 2] = swap;
                }
            }
            triangles.AddRange(list);
        }
    }
}
