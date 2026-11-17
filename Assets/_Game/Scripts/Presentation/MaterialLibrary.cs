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
        private readonly Dictionary<Color32, Material> lit = new Dictionary<Color32, Material>();
        private readonly Dictionary<Color32, Material> unlit = new Dictionary<Color32, Material>();

        public MaterialLibrary(Material litTemplate, Material unlitTemplate)
        {
            this.litTemplate = litTemplate;
            this.unlitTemplate = unlitTemplate;
        }

        public Material Lit(Color color) => Get(lit, litTemplate, color);

        public Material Unlit(Color color) => Get(unlit, unlitTemplate, color);

        private static Material Get(Dictionary<Color32, Material> cache, Material template, Color color)
        {
            Color32 key = color;
            if (cache.TryGetValue(key, out var material)) return material;

            material = new Material(template) { name = template.name + " " + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor(BaseColor, color);
            cache[key] = material;
            return material;
        }
    }
}
