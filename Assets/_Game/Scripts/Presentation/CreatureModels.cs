using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>The meshes of one creature species. Wings and tails are separate so they can move.</summary>
    public sealed class CreatureModel
    {
        public Mesh Body;
        public Mesh LeftWing;
        public Mesh RightWing;
        public Mesh Tail;
        public Vector3 TailPivot;
        public bool Flies;
        public float HoverHeight;
        /// <summary>Drawn unlit, so it shines (and blooms) in the dark.</summary>
        public bool Glows;
    }

    /// <summary>
    /// Low-poly, vertex-coloured creature models built in code, in the same faceted style as the plants.
    /// Each model is roughly half a unit tall; the view scales it by the species size. Cached per species.
    /// </summary>
    public static class CreatureModels
    {
        private static readonly Dictionary<CreatureDefinition, CreatureModel> Cache = new Dictionary<CreatureDefinition, CreatureModel>();
        private static readonly Color Dark = new Color(0.07f, 0.07f, 0.09f);
        private static readonly Color Shine = new Color(1f, 1f, 1f);
        private static readonly Color Nose = new Color(0.92f, 0.52f, 0.56f);

        public static CreatureModel For(CreatureDefinition definition)
        {
            if (Cache.TryGetValue(definition, out var model)) return model;

            model = new CreatureModel();
            Color p = definition.primaryColor;
            Color s = definition.secondaryColor;
            bool rare = definition.rarity >= Rarity.Rare;
            var kit = new MeshKit();

            switch (definition.shape)
            {
                case CreatureShape.Hopper:
                    kit.Blob(new Vector3(0f, 0.17f, -0.02f), new Vector3(0.32f, 0.28f, 0.38f), p, 1, 0.06f, 1);
                    kit.Blob(new Vector3(0f, 0.14f, 0.09f), new Vector3(0.2f, 0.17f, 0.14f), s, 1, 0.05f, 2);
                    kit.Blob(new Vector3(0f, 0.34f, 0.15f), new Vector3(0.24f, 0.22f, 0.24f), p, 1, 0.05f, 3);
                    kit.Blob(new Vector3(0f, 0.31f, 0.26f), new Vector3(0.09f, 0.06f, 0.06f), s, 0, 0f, 4);
                    kit.Blob(new Vector3(0f, 0.335f, 0.285f), new Vector3(0.035f, 0.025f, 0.025f), Nose, 0, 0f, 5);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int mark = kit.Mark;
                        kit.Blob(new Vector3(0f, 0.12f, 0f), new Vector3(0.075f, 0.26f, 0.045f), p, 1, 0.04f, 6);
                        kit.Blob(new Vector3(0f, 0.12f, 0.014f), new Vector3(0.04f, 0.18f, 0.03f), s, 0, 0f, 7);
                        kit.TransformSince(mark, Matrix4x4.TRS(new Vector3(side * 0.06f, 0.41f, 0.12f), Quaternion.Euler(-12f, 0f, -side * 16f), Vector3.one));
                        kit.Blob(new Vector3(side * 0.08f, 0.03f, 0.09f), new Vector3(0.07f, 0.05f, 0.1f), s, 0, 0f, 8);
                    }
                    kit.Blob(new Vector3(0f, 0.2f, -0.22f), Vector3.one * 0.11f, s, 1, 0.08f, 9);
                    Eyes(kit, new Vector3(0f, 0.375f, 0.25f), 0.065f, 0.045f);
                    if (rare)
                    {
                        int mark = kit.Mark;
                        kit.Cone(Vector3.zero, 0.17f, 0.032f, 0f, 5, s);
                        kit.TransformSince(mark, Matrix4x4.TRS(new Vector3(0f, 0.43f, 0.2f), Quaternion.Euler(28f, 0f, 0f), Vector3.one));
                    }
                    break;

                case CreatureShape.Flutter:
                    model.Flies = true;
                    model.HoverHeight = 0.75f;
                    kit.Blob(new Vector3(0f, 0f, -0.02f), new Vector3(0.06f, 0.06f, 0.2f), s, 1, 0.05f, 1);
                    kit.Blob(new Vector3(0f, 0.01f, 0.1f), Vector3.one * 0.07f, s, 1, 0f, 2);
                    kit.Blade(new Vector3(0.02f, 0.03f, 0.11f), 0.11f, 0.007f, 50f, 20f, s);
                    kit.Blade(new Vector3(-0.02f, 0.03f, 0.11f), 0.11f, 0.007f, 50f, -20f, s);
                    model.LeftWing = Wing(-1f, p, s, 1f, glowing: false);
                    model.RightWing = Wing(1f, p, s, 1f, glowing: false);
                    break;

                case CreatureShape.Glow:
                    model.Flies = true;
                    model.Glows = true;
                    model.HoverHeight = 1.15f;
                    // Drawn unlit: the colour comes from the material, not these vertex colours.
                    kit.Blob(Vector3.zero, new Vector3(0.08f, 0.08f, 0.22f), s, 1, 0.05f, 1);
                    kit.Blob(new Vector3(0f, 0f, 0.13f), Vector3.one * 0.09f, s, 1, 0f, 2);
                    model.LeftWing = Wing(-1f, p, p, 1.4f, glowing: true);
                    model.RightWing = Wing(1f, p, p, 1.4f, glowing: true);
                    break;

                case CreatureShape.Crawler:
                {
                    kit.Blob(new Vector3(0f, 0.07f, 0f), new Vector3(0.17f, 0.11f, 0.36f), p, 1, 0.05f, 1);
                    kit.Blob(new Vector3(0f, 0.09f, 0.21f), new Vector3(0.15f, 0.1f, 0.15f), p, 1, 0.05f, 2);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i % 2 == 0 ? 0.1f : -0.1f;
                        float z = i < 2 ? 0.1f : -0.1f;
                        kit.Blob(new Vector3(x, 0.03f, z), new Vector3(0.07f, 0.05f, 0.08f), p * 0.85f, 0, 0f, 3 + i);
                    }
                    kit.Blob(new Vector3(0.04f, 0.12f, 0.03f), new Vector3(0.06f, 0.03f, 0.06f), s, 0, 0f, 8);
                    kit.Blob(new Vector3(-0.04f, 0.118f, -0.07f), new Vector3(0.06f, 0.03f, 0.06f), s, 0, 0f, 9);
                    kit.Blob(new Vector3(0.01f, 0.11f, -0.15f), new Vector3(0.045f, 0.025f, 0.045f), s, 0, 0f, 10);
                    Eyes(kit, new Vector3(0f, 0.135f, 0.25f), 0.055f, 0.04f);

                    var tail = new MeshKit();
                    tail.Cone(Vector3.zero, 0.26f, 0.05f, 0f, 6, p);
                    tail.TransformSince(0, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new Vector3(1f, 1f, 0.6f)));
                    model.Tail = tail.Build(definition.displayName + " Tail");
                    model.TailPivot = new Vector3(0f, 0.06f, -0.16f);
                    break;
                }

                case CreatureShape.Climber:
                {
                    kit.Blob(new Vector3(0f, 0.17f, 0f), new Vector3(0.24f, 0.3f, 0.24f), p, 1, 0.05f, 1);
                    kit.Blob(new Vector3(0f, 0.16f, 0.08f), new Vector3(0.15f, 0.2f, 0.1f), s, 1, 0.04f, 2);
                    kit.Blob(new Vector3(0f, 0.37f, 0.06f), new Vector3(0.21f, 0.19f, 0.2f), p, 1, 0.05f, 3);
                    kit.Blob(new Vector3(0f, 0.35f, 0.15f), new Vector3(0.08f, 0.06f, 0.06f), s, 0, 0f, 4);
                    kit.Blob(new Vector3(0f, 0.365f, 0.18f), new Vector3(0.03f, 0.02f, 0.02f), Dark, 0, 0f, 5);
                    kit.Cone(new Vector3(0.06f, 0.43f, 0.04f), 0.09f, 0.04f, 0f, 4, p);
                    kit.Cone(new Vector3(-0.06f, 0.43f, 0.04f), 0.09f, 0.04f, 0f, 4, p);
                    kit.Blob(new Vector3(0.07f, 0.03f, 0.07f), new Vector3(0.07f, 0.05f, 0.09f), p * 0.85f, 0, 0f, 6);
                    kit.Blob(new Vector3(-0.07f, 0.03f, 0.07f), new Vector3(0.07f, 0.05f, 0.09f), p * 0.85f, 0, 0f, 7);
                    Eyes(kit, new Vector3(0f, 0.395f, 0.145f), 0.05f, 0.04f);
                    if (rare)
                    {
                        int mark = kit.Mark;
                        kit.Cone(Vector3.zero, 0.14f, 0.028f, 0f, 5, s);
                        kit.TransformSince(mark, Matrix4x4.TRS(new Vector3(0f, 0.45f, 0.1f), Quaternion.Euler(25f, 0f, 0f), Vector3.one));
                    }

                    var tail = new MeshKit();
                    tail.Blob(new Vector3(0f, 0.08f, -0.05f), new Vector3(0.15f, 0.22f, 0.15f), p, 1, 0.08f, 11);
                    tail.Blob(new Vector3(0f, 0.25f, -0.08f), new Vector3(0.17f, 0.2f, 0.16f), p, 1, 0.08f, 12);
                    tail.Blob(new Vector3(0f, 0.36f, -0.02f), Vector3.one * 0.11f, s, 1, 0.08f, 13);
                    model.Tail = tail.Build(definition.displayName + " Tail");
                    model.TailPivot = new Vector3(0f, 0.12f, -0.12f);
                    break;
                }

                case CreatureShape.Shell:
                    kit.Blob(new Vector3(0f, 0.045f, 0.03f), new Vector3(0.15f, 0.08f, 0.42f), s, 1, 0.04f, 1);
                    kit.Blob(new Vector3(0f, 0.11f, 0.2f), new Vector3(0.11f, 0.13f, 0.11f), s, 1, 0.04f, 2);
                    kit.Blade(new Vector3(0.03f, 0.15f, 0.22f), 0.1f, 0.009f, 20f, 15f, s);
                    kit.Blade(new Vector3(-0.03f, 0.15f, 0.22f), 0.1f, 0.009f, 20f, -15f, s);
                    kit.Blob(new Vector3(0.038f, 0.245f, 0.254f), Vector3.one * 0.035f, Dark, 0, 0f, 3);
                    kit.Blob(new Vector3(-0.038f, 0.245f, 0.254f), Vector3.one * 0.035f, Dark, 0, 0f, 4);
                    kit.Blob(new Vector3(0f, 0.2f, -0.05f), new Vector3(0.3f, 0.3f, 0.26f), p, 1, 0.04f, 5);
                    kit.Blob(new Vector3(0.05f, 0.24f, -0.05f), Vector3.one * 0.19f, Color.Lerp(p, s, 0.45f), 1, 0.04f, 6);
                    kit.Blob(new Vector3(0.085f, 0.27f, -0.05f), Vector3.one * 0.1f, p * 1.1f, 0, 0f, 7);
                    break;
            }

            model.Body = kit.Build(definition.displayName);
            Cache[definition] = model;
            return model;
        }

        // Two dark eyes, each with a tiny highlight so they read as shiny.
        private static void Eyes(MeshKit kit, Vector3 centre, float spacing, float size)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = centre + new Vector3(side * spacing, 0f, 0f);
                kit.Blob(eye, Vector3.one * size, Dark, 0, 0f, 20 + side);
                kit.Blob(eye + new Vector3(side * size * 0.12f, size * 0.22f, size * 0.38f), Vector3.one * (size * 0.35f), Shine, 0, 0f, 30 + side);
            }
        }

        // One side's pair of wings, hinged on the body's centre line so it can flap about the forward axis.
        private static Mesh Wing(float side, Color wing, Color spot, float scale, bool glowing)
        {
            var kit = new MeshKit();
            kit.Blob(new Vector3(side * 0.13f, 0f, 0.05f) * scale, new Vector3(0.24f, 0.02f, 0.17f) * scale, wing, 1, 0.05f, 1);
            kit.Blob(new Vector3(side * 0.1f, 0f, -0.08f) * scale, new Vector3(0.17f, 0.02f, 0.13f) * scale, wing * 0.9f, 1, 0.05f, 2);
            if (!glowing)
                kit.Blob(new Vector3(side * 0.16f, 0.012f, 0.06f) * scale, new Vector3(0.07f, 0.015f, 0.06f) * scale, spot, 0, 0f, 3);
            return kit.Build(side < 0f ? "Left Wing" : "Right Wing");
        }
    }
}
