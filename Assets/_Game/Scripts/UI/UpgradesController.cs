using System;
using System.Collections.Generic;
using TinyPlanet.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TinyPlanet.UI.UiFactory;

namespace TinyPlanet.UI
{
    /// <summary>One biome or seed as the upgrades screen lists it.</summary>
    public sealed class UpgradeEntry
    {
        public string Id;
        public string Name;
        public string Detail;
        public Color Swatch;
        public bool IsBiome;
    }

    /// <summary>
    /// Full-screen planet upgrades: the planet level and its progress, dormant lands to wake and seeds to buy.
    /// </summary>
    public sealed class UpgradesController : MonoBehaviour
    {
        private static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 1f);
        private static readonly Color RowColor = new Color(0.10f, 0.13f, 0.22f, 1f);
        private static readonly Color DoneColor = new Color(0.20f, 0.42f, 0.30f, 1f);
        private static readonly Color BarBack = new Color(0.12f, 0.15f, 0.25f, 1f);
        private static readonly Color BarFill = new Color(0.45f, 0.85f, 0.45f, 1f);

        private sealed class Row
        {
            public UpgradeEntry Entry;
            public Image Button;
            public TMP_Text ButtonLabel;
            public CanvasGroup Group;
        }

        private readonly List<Row> rows = new List<Row>();
        private PlanetSimulation simulation;
        private RectTransform root;
        private TMP_Text levelLabel;
        private TMP_Text lifeLabel;
        private TMP_Text energyLabel;
        private RectTransform barFill;
        private float refreshTimer;

        public bool IsOpen => root.gameObject.activeSelf;

        public void Init(RectTransform parent, PlanetSimulation simulation, IReadOnlyList<UpgradeEntry> entries)
        {
            this.simulation = simulation;

            root = Panel("Upgrades", parent, Backdrop, 0f);
            BuildHeader();
            var content = BuildScroll();
            BuildSection(content, Strings.UpgradesLands, entries, biomes: true);
            BuildSection(content, Strings.UpgradesSeeds, entries, biomes: false);
            root.gameObject.SetActive(false);
        }

        /// <summary>True when there is something the player could unlock right now.</summary>
        public bool AnythingAffordable()
        {
            foreach (var row in rows)
            {
                if (Check(row.Entry) == UnlockResult.Ok) return true;
            }
            return false;
        }

        public void Open()
        {
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Close() => root.gameObject.SetActive(false);

        private void Update()
        {
            if (!IsOpen) return;
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.25f) Refresh();
        }

        public void Refresh()
        {
            refreshTimer = 0f;
            if (!IsOpen) return;

            int level = simulation.Level;
            levelLabel.text = string.Format(Strings.PlanetLevelFormat, level);
            energyLabel.text = string.Format(Strings.EnergyFormat, Math.Floor(simulation.Energy).ToString("N0"));

            int next = simulation.LifeForLevel(level + 1);
            int current = simulation.LifeForLevel(level);
            double life = simulation.Life;
            if (next < 0)
            {
                lifeLabel.text = string.Format(Strings.LifeMaxFormat, Math.Floor(life));
                SetBar(1f);
            }
            else
            {
                lifeLabel.text = string.Format(Strings.LifeProgressFormat, Math.Floor(life), next, level + 1);
                SetBar((float)((life - current) / Math.Max(1, next - current)));
            }

            foreach (var row in rows)
            {
                var result = Check(row.Entry);
                int cost, needed;
                Requirements(row.Entry, out needed, out cost);
                switch (result)
                {
                    case UnlockResult.AlreadyUnlocked:
                        row.ButtonLabel.text = row.Entry.IsBiome ? Strings.UpgradeAwake : Strings.UpgradeOwned;
                        row.Button.color = DoneColor;
                        row.Group.alpha = 1f;
                        break;
                    case UnlockResult.LevelTooLow:
                        row.ButtonLabel.text = string.Format(Strings.UpgradeNeedsLevelFormat, needed);
                        row.Button.color = ButtonColor;
                        row.Group.alpha = 0.5f;
                        break;
                    default:
                        row.ButtonLabel.text = string.Format(Strings.UpgradeCostFormat, cost.ToString("N0"));
                        row.Button.color = result == UnlockResult.Ok ? SelectedColor : ButtonColor;
                        row.ButtonLabel.color = result == UnlockResult.Ok ? new Color(0.1f, 0.1f, 0.15f) : Color.white;
                        row.Group.alpha = result == UnlockResult.Ok ? 1f : 0.75f;
                        continue;
                }
                row.ButtonLabel.color = Color.white;
            }
        }

        private UnlockResult Check(UpgradeEntry entry) =>
            entry.IsBiome ? simulation.CanUnlockBiome(entry.Id) : simulation.CanUnlockPlant(entry.Id);

        private void Requirements(UpgradeEntry entry, out int level, out int cost)
        {
            level = 1;
            cost = 0;
            if (entry.IsBiome)
            {
                if (simulation.TryGetBiome(entry.Id, out var biome))
                {
                    level = biome.UnlockLevel;
                    cost = biome.UnlockCost;
                }
            }
            else if (simulation.TryGetSpec(entry.Id, out var plant))
            {
                level = plant.UnlockLevel;
                cost = plant.UnlockCost;
            }
        }

        private void Buy(UpgradeEntry entry)
        {
            if (entry.IsBiome) simulation.TryUnlockBiome(entry.Id);
            else simulation.TryUnlockPlant(entry.Id);
            Refresh();
        }

        private void SetBar(float fraction)
        {
            barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        // ---------- construction ----------

        private void BuildHeader()
        {
            var title = Label(root, Strings.UpgradesTitle, 64f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            TopBand(title.rectTransform, 30f, 80f);
            title.rectTransform.offsetMin = new Vector2(50f, title.rectTransform.offsetMin.y);

            energyLabel = Label(root, string.Empty, 34f, TextAlignmentOptions.Left);
            energyLabel.color = SelectedColor;
            TopBand(energyLabel.rectTransform, 110f, 50f);
            energyLabel.rectTransform.offsetMin = new Vector2(52f, energyLabel.rectTransform.offsetMin.y);

            var close = TextButton(root, Strings.CodexClose, 38f, ButtonColor, Close);
            var rect = (RectTransform)close.transform;
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(-40f, -40f);
            rect.sizeDelta = new Vector2(220f, 100f);

            var card = Panel("Level", root, RowColor);
            TopBand(card, 190f, 220f);
            card.offsetMin = new Vector2(40f, card.offsetMin.y);
            card.offsetMax = new Vector2(-40f, card.offsetMax.y);

            levelLabel = Label(card, string.Empty, 56f, TextAlignmentOptions.Left);
            levelLabel.fontStyle = FontStyles.Bold;
            TopBand(levelLabel.rectTransform, 20f, 70f);
            levelLabel.rectTransform.offsetMin = new Vector2(30f, levelLabel.rectTransform.offsetMin.y);

            var bar = Panel("Bar", card, BarBack, 18f);
            TopBand(bar, 100f, 36f);
            bar.offsetMin = new Vector2(30f, bar.offsetMin.y);
            bar.offsetMax = new Vector2(-30f, bar.offsetMax.y);
            barFill = Panel("Fill", bar, BarFill, 18f);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;

            lifeLabel = Label(card, string.Empty, 32f, TextAlignmentOptions.Left);
            lifeLabel.color = new Color(1f, 1f, 1f, 0.75f);
            TopBand(lifeLabel.rectTransform, 150f, 50f);
            lifeLabel.rectTransform.offsetMin = new Vector2(30f, lifeLabel.rectTransform.offsetMin.y);
        }

        private RectTransform BuildScroll()
        {
            var area = Rect("Entries", root);
            area.offsetMin = new Vector2(0f, 30f);
            area.offsetMax = new Vector2(0f, -430f);

            var viewport = Rect("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
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

        private void BuildSection(RectTransform content, string heading, IReadOnlyList<UpgradeEntry> entries, bool biomes)
        {
            var label = Label(content, heading, 40f, TextAlignmentOptions.BottomLeft);
            label.color = new Color(1f, 1f, 1f, 0.8f);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            foreach (var entry in entries)
            {
                if (entry.IsBiome == biomes) rows.Add(BuildRow(content, entry));
            }
        }

        private Row BuildRow(RectTransform parent, UpgradeEntry entry)
        {
            var frame = Panel(entry.Id, parent, RowColor);
            frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
            var group = frame.gameObject.AddComponent<CanvasGroup>();

            var swatch = Panel("Swatch", frame, entry.Swatch);
            Anchor(swatch, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            swatch.anchoredPosition = new Vector2(25f, 0f);
            swatch.sizeDelta = new Vector2(100f, 100f);

            var text = Rect("Text", frame);
            text.offsetMin = new Vector2(150f, 15f);
            text.offsetMax = new Vector2(-320f, -15f);

            var name = Label(text, entry.Name, 42f, TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            TopBand(name.rectTransform, 0f, 56f);

            var detail = Label(text, entry.Detail, 26f, TextAlignmentOptions.TopLeft);
            detail.color = new Color(1f, 1f, 1f, 0.65f);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.lineSpacing = -10f;
            TopBand(detail.rectTransform, 54f, 70f);

            var button = TextButton(frame, string.Empty, 34f, ButtonColor, () => Buy(entry));
            var rect = (RectTransform)button.transform;
            Anchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(-25f, 0f);
            rect.sizeDelta = new Vector2(270f, 100f);

            return new Row
            {
                Entry = entry,
                Button = button.GetComponent<Image>(),
                ButtonLabel = button.GetComponentInChildren<TMP_Text>(),
                Group = group,
            };
        }
    }
}
