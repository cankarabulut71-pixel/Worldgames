using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TinyPlanet.UI
{
    /// <summary>Small helpers for building uGUI in code, shared by the HUD and the codex.</summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.14f, 0.72f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.20f, 0.32f, 1f);
        public static readonly Color SelectedColor = new Color(0.98f, 0.82f, 0.30f, 1f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        /// <summary>A coloured panel with rounded corners; a radius of zero gives a plain rectangle.</summary>
        public static RectTransform Panel(string name, Transform parent, Color color, float radius = 24f)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Round(image, radius);
            return rect;
        }

        /// <summary>Gives an image rounded corners of <paramref name="radius"/> canvas units.</summary>
        public static void Round(Image image, float radius)
        {
            if (radius <= 0f) return;
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = CornerPixels / radius;
        }

        /// <summary>A round image, e.g. a badge or the energy orb.</summary>
        public static Image Circle(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = CircleSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private const int CornerPixels = 32;
        private static Sprite roundedSprite;
        private static Sprite circleSprite;

        /// <summary>A white rounded square, nine-sliced so it stretches to any size.</summary>
        public static Sprite RoundedSprite => roundedSprite != null ? roundedSprite : roundedSprite = MakeRounded();

        /// <summary>A white disc with a smooth edge.</summary>
        public static Sprite CircleSprite => circleSprite != null ? circleSprite : circleSprite = MakeCircle(128);

        private static Sprite MakeRounded()
        {
            const int size = CornerPixels * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Rounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the inner rectangle whose corners are the circle centres.
                    float cx = Mathf.Clamp(x + 0.5f, CornerPixels, size - CornerPixels);
                    float cy = Mathf.Clamp(y + 0.5f, CornerPixels, size - CornerPixels);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(CornerPixels - d + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(CornerPixels, CornerPixels, CornerPixels, CornerPixels);
            return Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite MakeCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static TMP_Text Label(Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var rect = Rect("Label", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // Nunito's lines are taller than the box many labels were sized for; a line that does not fit is
            // hidden outright, so let the text spill a little above and below its rect.
            label.margin = new Vector4(0f, -size * 0.2f, 0f, -size * 0.2f);
            label.raycastTarget = false;
            return label;
        }

        public static Button TextButton(Transform parent, string text, float size, Color color, System.Action onClick)
        {
            var frame = Panel(text, parent, color);
            var label = Label(frame, text, size, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            frame.gameObject.AddComponent<PressFeedback>();
            return button;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        /// <summary>Pins a rect to the top edge of its parent, spanning the full width.</summary>
        public static void TopBand(RectTransform rect, float top, float height)
        {
            Anchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }
    }
}
