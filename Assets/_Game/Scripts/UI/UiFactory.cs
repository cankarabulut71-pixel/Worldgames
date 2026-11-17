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

        public static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
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
            // The default font has no ellipsis character, so long text is cut rather than marked.
            label.overflowMode = TextOverflowModes.Truncate;
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
