using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TinyPlanet.UI.UiFactory;

namespace TinyPlanet.UI
{
    /// <summary>One planet as the planets screen lists it.</summary>
    public sealed class PlanetEntry
    {
        public int Index;
        public string Name;
        public string Traits;
        public int Level;
        public int Found;
        public int Total;
        public Color Ground;
        public Color Sky;
        public bool IsHere;
        public bool Visited;
    }

    /// <summary>
    /// Full-screen list of the player's planets: travel between them, and discover the next one.
    /// </summary>
    public sealed class PlanetsController : MonoBehaviour
    {
        private static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 1f);
        private static readonly Color RowColor = new Color(0.10f, 0.13f, 0.22f, 1f);
        private static readonly Color HereColor = new Color(0.20f, 0.42f, 0.30f, 1f);

        private Func<List<PlanetEntry>> entries;
        private Func<(bool possible, bool levelOk, int cost)> canDiscover;
        private Action discover;
        private Action<int> visit;
        private RectTransform root;
        private RectTransform content;
        private float refreshTimer;

        public bool IsOpen => root.gameObject.activeSelf;

        public void Init(RectTransform parent, Func<List<PlanetEntry>> entries,
            Func<(bool possible, bool levelOk, int cost)> canDiscover, Action discover, Action<int> visit)
        {
            this.entries = entries;
            this.canDiscover = canDiscover;
            this.discover = discover;
            this.visit = visit;

            root = Panel("Planets", parent, Backdrop, 0f);

            var title = Label(root, Strings.PlanetsTitle, 64f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            TopBand(title.rectTransform, 30f, 80f);
            title.rectTransform.offsetMin = new Vector2(50f, title.rectTransform.offsetMin.y);

            var subtitle = Label(root, Strings.PlanetsSubtitle, 32f, TextAlignmentOptions.Left);
            subtitle.color = new Color(1f, 1f, 1f, 0.65f);
            TopBand(subtitle.rectTransform, 110f, 50f);
            subtitle.rectTransform.offsetMin = new Vector2(52f, subtitle.rectTransform.offsetMin.y);

            var close = TextButton(root, Strings.CodexClose, 38f, ButtonColor, Close);
            var rect = (RectTransform)close.transform;
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(-40f, -40f);
            rect.sizeDelta = new Vector2(220f, 100f);

            content = BuildScroll();
            root.gameObject.SetActive(false);
        }

        public void Open()
        {
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            Rebuild();
        }

        public void Close() => root.gameObject.SetActive(false);

        private void Update()
        {
            if (!IsOpen) return;
            // Energy keeps flowing while the screen is open, so the discover button can become available.
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 1f) Rebuild();
        }

        private void Rebuild()
        {
            refreshTimer = 0f;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            foreach (var entry in entries()) PlanetRow(entry);

            var (possible, levelOk, cost) = canDiscover();
            if (cost > 0) DiscoverRow(possible, levelOk, cost);
        }

        private void PlanetRow(PlanetEntry entry)
        {
            var frame = Row(entry.Name);

            var sky = Panel("Sky", frame, entry.Sky, 65f);
            Anchor(sky, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            sky.anchoredPosition = new Vector2(25f, 0f);
            sky.sizeDelta = new Vector2(130f, 130f);
            var ground = Panel("Ground", sky, entry.Ground, 41f);
            Stretch(ground, 24f);

            var text = Rect("Text", frame);
            text.offsetMin = new Vector2(180f, 15f);
            text.offsetMax = new Vector2(-300f, -15f);

            var name = Label(text, entry.Name, 46f, TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            TopBand(name.rectTransform, 0f, 60f);

            var traits = Label(text, entry.Traits, 28f, TextAlignmentOptions.TopLeft);
            traits.color = new Color(1f, 1f, 1f, 0.7f);
            TopBand(traits.rectTransform, 62f, 40f);

            string status = entry.Visited
                ? string.Format(Strings.PlanetStatusFormat, entry.Level, entry.Found, entry.Total)
                : Strings.PlanetUnvisited;
            var stats = Label(text, status, 28f, TextAlignmentOptions.TopLeft);
            stats.color = new Color(1f, 1f, 1f, 0.55f);
            TopBand(stats.rectTransform, 104f, 40f);

            int index = entry.Index;
            var button = TextButton(frame, entry.IsHere ? Strings.PlanetHere : Strings.PlanetVisit, 36f,
                entry.IsHere ? HereColor : SelectedColor, () =>
                {
                    if (!entry.IsHere) visit(index);
                    else Close();
                });
            if (!entry.IsHere) button.GetComponentInChildren<TMP_Text>().color = new Color(0.1f, 0.1f, 0.15f);
            PlaceButton(button);
        }

        private void DiscoverRow(bool possible, bool levelOk, int cost)
        {
            var frame = Row("Discover");
            frame.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.16f, 1f);

            var mark = Panel("Unknown", frame, new Color(0.16f, 0.20f, 0.32f, 1f), 65f);
            Anchor(mark, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            mark.anchoredPosition = new Vector2(25f, 0f);
            mark.sizeDelta = new Vector2(130f, 130f);
            var question = Label(mark, "?", 80f, TextAlignmentOptions.Center);
            question.color = new Color(1f, 1f, 1f, 0.5f);
            Stretch(question.rectTransform);

            var text = Rect("Text", frame);
            text.offsetMin = new Vector2(180f, 15f);
            text.offsetMax = new Vector2(-300f, -15f);

            var name = Label(text, Strings.NewWorld, 46f, TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            TopBand(name.rectTransform, 0f, 60f);

            var detail = Label(text, levelOk ? string.Format(Strings.NewWorldCostFormat, cost.ToString("N0"))
                : string.Format(Strings.NewWorldLevelFormat, LevelNeeded()), 28f, TextAlignmentOptions.TopLeft);
            detail.color = new Color(1f, 1f, 1f, 0.7f);
            detail.textWrappingMode = TextWrappingModes.Normal;
            TopBand(detail.rectTransform, 62f, 80f);

            var button = TextButton(frame, levelOk ? string.Format(Strings.DiscoverFormat, cost.ToString("N0")) : Strings.Locked, 34f,
                possible ? SelectedColor : ButtonColor, () =>
                {
                    if (possible) discover();
                });
            if (possible) button.GetComponentInChildren<TMP_Text>().color = new Color(0.1f, 0.1f, 0.15f);
            else frame.gameObject.AddComponent<CanvasGroup>().alpha = 0.75f;
            PlaceButton(button);
        }

        private int levelNeeded;

        /// <summary>Set by the game so the locked row can say which level is needed.</summary>
        public void SetLevelNeeded(int level) => levelNeeded = level;

        private int LevelNeeded() => levelNeeded;

        private RectTransform Row(string name)
        {
            var frame = Panel(name, content, RowColor);
            frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;
            return frame;
        }

        private static void PlaceButton(Button button)
        {
            var rect = (RectTransform)button.transform;
            Anchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(-25f, 0f);
            rect.sizeDelta = new Vector2(250f, 100f);
        }

        private RectTransform BuildScroll()
        {
            var area = Rect("Entries", root);
            area.offsetMin = new Vector2(0f, 30f);
            area.offsetMax = new Vector2(0f, -180f);

            var viewport = Rect("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var list = Rect("Content", viewport);
            Anchor(list, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;

            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 10, 40);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            return list;
        }
    }
}
