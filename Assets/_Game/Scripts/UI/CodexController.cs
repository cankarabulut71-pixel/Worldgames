using System;
using System.Collections.Generic;
using TinyPlanet.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TinyPlanet.UI.UiFactory;

namespace TinyPlanet.UI
{
    /// <summary>One plant or creature as the codex shows it.</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public string Name;
        public string Hint;
        public string Description;
        public bool IsCreature;
        public Rarity Rarity;
    }

    /// <summary>
    /// Full-screen list of every species on this planet. Undiscovered ones show a silhouette and a vague hint;
    /// discovered ones their picture, name and description.
    /// </summary>
    public sealed class CodexController : MonoBehaviour
    {
        // Fully opaque: in linear colour space even a few percent of the bright planet shows through clearly.
        private static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 1f);
        private static readonly Color SilhouetteTint = new Color(0.24f, 0.29f, 0.43f, 1f);
        private static readonly Color CardColor = new Color(0.10f, 0.13f, 0.22f, 1f);

        private sealed class Card
        {
            public CodexEntry Entry;
            public Image Frame;
            public RawImage Icon;
            public TMP_Text Name;
            public TMP_Text Sub;
        }

        private readonly List<Card> cards = new List<Card>();
        private PlanetSimulation simulation;
        private Func<string, bool, Texture> iconFor;
        private RectTransform root;
        private TMP_Text progressLabel;
        private RawImage detailIcon;
        private TMP_Text detailName;
        private TMP_Text detailRarity;
        private TMP_Text detailText;
        private TMP_Text detailStatus;
        private CodexEntry selected;
        private bool iconsLoaded;

        public bool IsOpen => root.gameObject.activeSelf;

        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.45f);
                case Rarity.Rare: return new Color(0.55f, 0.70f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.78f, 0.25f);
                default: return new Color(0.80f, 0.84f, 0.92f);
            }
        }

        public void Init(RectTransform parent, PlanetSimulation simulation, IReadOnlyList<CodexEntry> entries, Func<string, bool, Texture> iconFor)
        {
            this.simulation = simulation;
            this.iconFor = iconFor;

            root = Panel("Codex", parent, Backdrop);
            BuildHeader();
            var content = BuildScroll();
            BuildSection(content, Strings.CodexPlants, entries, creatures: false);
            BuildSection(content, Strings.CodexCreatures, entries, creatures: true);
            BuildDetail();

            if (cards.Count > 0) Select(cards[0].Entry);
            root.gameObject.SetActive(false);
        }

        public (int found, int total) Progress()
        {
            int found = 0;
            foreach (var card in cards)
            {
                if (simulation.IsDiscovered(card.Entry.Id)) found++;
            }
            return (found, cards.Count);
        }

        public void Open()
        {
            // Icons are rendered on first open rather than at start-up, once the camera has settled.
            iconsLoaded = true;
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Close() => root.gameObject.SetActive(false);

        public void Refresh()
        {
            if (!IsOpen) return;

            var (found, total) = Progress();
            int percent = total > 0 ? Mathf.RoundToInt(100f * found / total) : 0;
            progressLabel.text = string.Format(Strings.CodexProgressFormat, found, total, percent);

            foreach (var card in cards)
            {
                bool known = simulation.IsDiscovered(card.Entry.Id);
                card.Icon.texture = iconFor(card.Entry.Id, !known);
                card.Icon.color = known ? Color.white : SilhouetteTint;
                card.Name.text = known ? card.Entry.Name : Strings.Unknown;
                card.Sub.text = card.Entry.IsCreature ? Strings.Rarity(card.Entry.Rarity) : Strings.Plant;
                card.Sub.color = card.Entry.IsCreature ? RarityColor(card.Entry.Rarity) : new Color(1f, 1f, 1f, 0.6f);
                card.Frame.color = card.Entry == selected ? SelectedColor : CardColor;
            }

            ShowDetail();
        }

        private void Select(CodexEntry entry)
        {
            selected = entry;
            Refresh();
        }

        private void ShowDetail()
        {
            if (selected == null) return;
            bool known = simulation.IsDiscovered(selected.Id);

            detailIcon.texture = iconsLoaded ? iconFor(selected.Id, !known) : null;
            detailIcon.color = known ? Color.white : SilhouetteTint;
            detailName.text = known ? selected.Name : Strings.Unknown;
            detailRarity.text = selected.IsCreature ? Strings.Rarity(selected.Rarity) : Strings.Plant;
            detailRarity.color = selected.IsCreature ? RarityColor(selected.Rarity) : new Color(1f, 1f, 1f, 0.6f);
            detailText.text = known && !string.IsNullOrEmpty(selected.Description) ? selected.Description : "\"" + selected.Hint + "\"";
            detailText.fontStyle = known ? FontStyles.Normal : FontStyles.Italic;

            if (!known)
                detailStatus.text = Strings.NotFoundYet;
            else if (selected.IsCreature)
                detailStatus.text = string.Format(Strings.LivingHereFormat, simulation.CountCreatures(selected.Id));
            else
                detailStatus.text = string.Format(Strings.GrowingHereFormat, simulation.CountPlants(selected.Id));
        }

        // ---------- construction ----------

        private void BuildHeader()
        {
            var title = Label(root, Strings.CodexTitle, 64f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            TopBand(title.rectTransform, 30f, 80f);
            title.rectTransform.offsetMin = new Vector2(50f, title.rectTransform.offsetMin.y);

            progressLabel = Label(root, string.Empty, 34f, TextAlignmentOptions.Left);
            progressLabel.color = new Color(1f, 1f, 1f, 0.7f);
            TopBand(progressLabel.rectTransform, 110f, 50f);
            progressLabel.rectTransform.offsetMin = new Vector2(52f, progressLabel.rectTransform.offsetMin.y);

            var close = TextButton(root, Strings.CodexClose, 38f, ButtonColor, Close);
            var rect = (RectTransform)close.transform;
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(-40f, -40f);
            rect.sizeDelta = new Vector2(220f, 100f);
        }

        private RectTransform BuildScroll()
        {
            var area = Rect("Entries", root);
            area.offsetMin = new Vector2(0f, 470f);
            area.offsetMax = new Vector2(0f, -180f);

            var viewport = Rect("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            // An invisible graphic so drags anywhere in the list scroll it.
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var content = Rect("Content", viewport);
            Anchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 10, 40);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            return content;
        }

        private void BuildSection(RectTransform content, string heading, IReadOnlyList<CodexEntry> entries, bool creatures)
        {
            var label = Label(content, heading, 40f, TextAlignmentOptions.BottomLeft);
            label.color = new Color(1f, 1f, 1f, 0.8f);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            var grid = Rect(heading + " Grid", content);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(310f, 330f);
            layout.spacing = new Vector2(25f, 25f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperLeft;

            foreach (var entry in entries)
            {
                if (entry.IsCreature == creatures) cards.Add(BuildCard(grid, entry));
            }
        }

        private Card BuildCard(RectTransform parent, CodexEntry entry)
        {
            var frame = Panel(entry.Id, parent, CardColor);
            var inner = Panel("Inner", frame, CardColor);
            Stretch(inner, 6f);
            inner.GetComponent<Image>().raycastTarget = false;

            var iconRect = Rect("Icon", inner);
            Anchor(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            iconRect.anchoredPosition = new Vector2(0f, -8f);
            iconRect.sizeDelta = new Vector2(220f, 220f);
            var icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;

            var name = Label(inner, string.Empty, 34f, TextAlignmentOptions.Center);
            Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            name.rectTransform.anchoredPosition = new Vector2(0f, 46f);
            name.rectTransform.sizeDelta = new Vector2(-16f, 44f);

            var sub = Label(inner, string.Empty, 26f, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            sub.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            sub.rectTransform.sizeDelta = new Vector2(-16f, 34f);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select(entry));

            return new Card { Entry = entry, Frame = frame.GetComponent<Image>(), Icon = icon, Name = name, Sub = sub };
        }

        private void BuildDetail()
        {
            var panel = Panel("Detail", root, new Color(0.08f, 0.10f, 0.18f, 1f));
            Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            panel.anchoredPosition = new Vector2(0f, 30f);
            panel.sizeDelta = new Vector2(-60f, 420f);

            var iconRect = Rect("Icon", panel);
            Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRect.anchoredPosition = new Vector2(20f, 0f);
            iconRect.sizeDelta = new Vector2(340f, 340f);
            detailIcon = iconRect.gameObject.AddComponent<RawImage>();
            detailIcon.raycastTarget = false;

            var text = Rect("Text", panel);
            text.offsetMin = new Vector2(380f, 30f);
            text.offsetMax = new Vector2(-30f, -30f);

            detailName = Label(text, string.Empty, 54f, TextAlignmentOptions.TopLeft);
            detailName.fontStyle = FontStyles.Bold;
            TopBand(detailName.rectTransform, 0f, 70f);

            detailRarity = Label(text, string.Empty, 32f, TextAlignmentOptions.TopLeft);
            TopBand(detailRarity.rectTransform, 72f, 44f);

            detailText = Label(text, string.Empty, 32f, TextAlignmentOptions.TopLeft);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.overflowMode = TextOverflowModes.Truncate;
            detailText.color = new Color(1f, 1f, 1f, 0.9f);
            TopBand(detailText.rectTransform, 130f, 170f);

            detailStatus = Label(text, string.Empty, 30f, TextAlignmentOptions.BottomLeft);
            detailStatus.color = new Color(1f, 1f, 1f, 0.6f);
            Anchor(detailStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            detailStatus.rectTransform.anchoredPosition = Vector2.zero;
            detailStatus.rectTransform.sizeDelta = new Vector2(0f, 44f);
        }
    }
}
