using System.Collections.Generic;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>Hands out one shared material per colour, so placeholder visuals batch well.</summary>
    public sealed class MaterialLibrary
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Material litTemplate;
        private readonly Material unlitTemplate;
        private readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> unlit = new Dictionary<Color, Material>();

        public MaterialLibrary(Material litTemplate, Material unlitTemplate, Material terrain)
        {
            Terrain = terrain;
            this.litTemplate = litTemplate;
            this.unlitTemplate = unlitTemplate;
        }

        /// <summary>The shared vertex-coloured material used by the planet, plants and creatures.</summary>
        public Material Terrain { get; }

        public Material Lit(Color color) => Get(lit, litTemplate, color);

        public Material Unlit(Color color) => Get(unlit, unlitTemplate, color);

        private static Material Get(Dictionary<Color, Material> cache, Material template, Color color)
        {
            Color key = color;
            if (cache.TryGetValue(key, out var material)) return material;

            material = new Material(template) { name = template.name + " " + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor(BaseColor, color);
            cache[key] = material;
            return material;
        }
    }
}
