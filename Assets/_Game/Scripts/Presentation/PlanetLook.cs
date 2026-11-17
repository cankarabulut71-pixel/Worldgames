using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>How one planet is coloured and shaped on screen, taken from its recipe.</summary>
    public sealed class PlanetLook
    {
        public float HueShift;
        public float Saturation = 1f;
        public float MountainScale = 1f;
        public SkyPalette Palette;

        public static PlanetLook From(PlanetRecipe recipe, GameConfig config)
        {
            var palettes = config.skyPalettes;
            return new PlanetLook
            {
                HueShift = recipe.HueShift,
                Saturation = recipe.Saturation,
                MountainScale = recipe.MountainScale,
                Palette = palettes != null && palettes.Length > 0 ? palettes[Mathf.Clamp(recipe.SkyPalette, 0, palettes.Length - 1)] : default,
            };
        }

        /// <summary>A land or plant colour turned to this planet's palette.</summary>
        public Color Shift(Color color)
        {
            if (Mathf.Approximately(HueShift, 0f) && Mathf.Approximately(Saturation, 1f)) return color;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return Color.HSVToRGB(Mathf.Repeat(h + HueShift, 1f), Mathf.Clamp01(s * Saturation), v);
        }
    }
}
