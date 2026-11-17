using System;
using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static TinyPlanet.UI.UiFactory;

namespace TinyPlanet.UI
{
    /// <summary>
    /// The in-game HUD: energy counter, seed tray, toast line and (in development builds) a debug panel.
    /// Built in code while the art is placeholder, so there are no UI prefabs to keep in sync.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private static readonly Color RemoveColor = new Color(0.78f, 0.30f, 0.28f, 1f);

        private sealed class TrayItem
        {
            public string PlantId;
            public int Cost;
            public Image Frame;
            public CanvasGroup Group;
        }

        private readonly List<TrayItem> items = new List<TrayItem>();
        private PlanetSimulation simulation;
        private CanvasScaler scaler;
        private RectTransform safeArea;
        private Rect appliedSafeArea;
        private TMP_Text energyLabel;
        private RectTransform energyPanel;
        private RectTransform energyOrb;
        private double energyTarget;
        private double energyShown = double.NaN;
        private float energyPunch;
        private float energyShake;
        private Func<string, Texture> iconFor;
        private RectTransform toastPill;
        private TMP_Text toastLabel;
        private CanvasGroup toastGroup;
        private float toastTimeLeft;
        private long shownEnergy = long.MinValue;
        private TMP_Text codexLabel;
        private TMP_Text levelLabel;
        private TMP_Text planetLabel;
        private RectTransform levelFill;
        private GameObject upgradesBadge;
        private float levelTimer;
        private Func<bool> anythingAffordable;
        private readonly Queue<(string heading, string name, int reward, Color accent)> banners =
            new Queue<(string heading, string name, int reward, Color accent)>();
        private RectTransform bannerRect;
        private CanvasGroup bannerGroup;
        private Image bannerAccent;
        private TMP_Text bannerHeading;
        private TMP_Text bannerName;
        private TMP_Text bannerReward;
        private float bannerTime = -1f;

        private RectTransform hintHolder;
        private RectTransform hintPill;
        private CanvasGroup hintGroup;
        private TMP_Text hintLabel;
        private string hintText;

        /// <summary>The plant id the player has picked in the tray, or null in remove mode.</summary>
        public string SelectedPlantId { get; private set; }
        public bool RemoveMode { get; private set; }

        /// <summary>Full-screen area inside the notch and home bar, for other screens to build into.</summary>
        public RectTransform SafeArea => safeArea;

        public event Action CodexClicked;
        public event Action UpgradesClicked;
        public event Action PlanetsClicked;
        public event Action DebugNewSeed;
        public event Action DebugUnlockAll;
        public event Action DebugAddEnergy;
        public event Action<double> DebugSkipTime;
        public event Action DebugReset;
        public event Action DebugToggleNight;
        public event Action DebugToggleRain;

        private RectTransform summary;

        public void Init(PlanetSimulation simulation, IReadOnlyList<PlantDefinition> plants, bool showDebug, Func<string, Texture> iconFor = null)
        {
            this.simulation = simulation;
            this.iconFor = iconFor;

            EnsureEventSystem();
            BuildCanvas();
            BuildEnergy();
            BuildToast();
            BuildHint();
            BuildTray(plants);
            BuildCodexButton();
            BuildPlanetButton();
            BuildLevel();
            BuildBanner();
            if (showDebug) BuildDebug();

            RefreshTray();
            simulation.EnergyChanged += OnEnergyChanged;
            simulation.PlantUnlocked += OnPlantUnlocked;
            OnEnergyChanged(simulation.Energy);
            RefreshLevel();
        }

        public void SetPlanetName(string name) => planetLabel.text = name;

        /// <summary>Shakes the energy counter: there is not enough for that.</summary>
        public void ShakeEnergy() => energyShake = 0.45f;

        /// <summary>Lets the upgrades button show a marker when something can be unlocked.</summary>
        public void SetUpgradeCheck(Func<bool> check)
        {
            anythingAffordable = check;
            RefreshLevel();
        }

        /// <summary>Shows only the seeds the player has unlocked, keeping the current pick if it is still there.</summary>
        public void RefreshTray()
        {
            string first = null;
            foreach (var item in items)
            {
                if (item.PlantId == null) continue;
                bool unlocked = simulation.IsPlantUnlocked(item.PlantId);
                item.Group.gameObject.SetActive(unlocked);
                if (unlocked && first == null) first = item.PlantId;
            }
            if (!RemoveMode && (SelectedPlantId == null || !simulation.IsPlantUnlocked(SelectedPlantId)))
                Select(first, remove: false);
        }

        private void OnPlantUnlocked(string plantId)
        {
            RefreshTray();
            Select(plantId, remove: false);
        }

        private void RefreshLevel()
        {
            levelTimer = 0f;
            int level = simulation.Level;
            levelLabel.text = string.Format(Strings.LevelButtonFormat, level);

            int next = simulation.LifeForLevel(level + 1);
            int current = simulation.LifeForLevel(level);
            float fraction = next < 0 ? 1f : (float)((simulation.Life - current) / Math.Max(1, next - current));
            levelFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

            if (upgradesBadge != null) upgradesBadge.SetActive(anythingAffordable != null && anythingAffordable());
        }

        public void ShowToast(string message, float seconds = 1.8f)
        {
            toastLabel.text = message;
            toastTimeLeft = seconds;
            toastGroup.alpha = 1f;
            float width = Mathf.Min(toastLabel.GetPreferredValues(message).x + 80f, 1040f);
            toastPill.sizeDelta = new Vector2(width, toastPill.sizeDelta.y);
        }

        /// <summary>A gentle, lasting nudge above the seed tray; null hides it.</summary>
        public void SetHint(string text)
        {
            if (text == hintText) return;
            hintText = text;
            if (text == null) return; // fades out in Update
            hintLabel.text = text;
            float width = Mathf.Min(hintLabel.GetPreferredValues(text).x + 90f, 1040f);
            hintPill.sizeDelta = new Vector2(width, hintPill.sizeDelta.y);
        }

        private void UpdateHint()
        {
            float target = hintText != null ? 1f : 0f;
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, target, Time.unscaledDeltaTime * 2.5f);
            // A slow bob, so it reads as a hint rather than part of the HUD.
            hintHolder.anchoredPosition = new Vector2(0f, 360f + Mathf.Sin(Time.unscaledTime * 2.2f) * 6f);
        }

        /// <summary>
        /// The "while you were away" card: one line per thing that happened, each with an optional icon.
        /// Closes on its button (or a tap on the dimmed backdrop).
        /// </summary>
        public void ShowSummary(string title, IReadOnlyList<(string text, Texture icon, Color tint)> lines, Action closed = null)
        {
            if (summary != null) Destroy(summary.gameObject);

            var backdrop = Panel("Away Summary", safeArea, new Color(0.02f, 0.03f, 0.07f, 0.55f), 0f);
            Stretch(backdrop);
            summary = backdrop;
            void Close()
            {
                if (summary == backdrop) summary = null;
                Destroy(backdrop.gameObject);
                closed?.Invoke();
            }
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(Close);

            const float lineHeight = 96f;
            float height = 150f + lines.Count * lineHeight + 150f;
            var card = Panel("Card", backdrop, new Color(0.07f, 0.09f, 0.17f, 0.97f), 44f);
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.sizeDelta = new Vector2(860f, height);
            card.anchoredPosition = new Vector2(0f, 60f);

            var heading = Label(card, title, 52f, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            heading.color = SelectedColor;
            TopBand(heading.rectTransform, 40f, 70f);

            for (int i = 0; i < lines.Count; i++)
            {
                var (text, icon, tint) = lines[i];
                var row = Rect("Line", card);
                TopBand(row, 140f + i * lineHeight, lineHeight);
                row.offsetMin = new Vector2(60f, row.offsetMin.y);
                row.offsetMax = new Vector2(-60f, row.offsetMax.y);

                float textLeft = 0f;
                if (icon != null)
                {
                    var glow = Circle("Glow", row, new Color(tint.r, tint.g, tint.b, 0.25f));
                    Anchor(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                    glow.rectTransform.sizeDelta = new Vector2(80f, 80f);
                    glow.raycastTarget = false;
                    var picture = Rect("Icon", row);
                    Anchor(picture, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                    picture.anchoredPosition = new Vector2(-6f, 0f);
                    picture.sizeDelta = new Vector2(92f, 92f);
                    var image = picture.gameObject.AddComponent<RawImage>();
                    image.texture = icon;
                    image.raycastTarget = false;
                    textLeft = 110f;
                }
                var label = Label(row, text, 40f, TextAlignmentOptions.Left);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            }

            var ok = TextButton(card, Strings.AwayOk, 42f, SelectedColor, Close);
            var okRect = (RectTransform)ok.transform;
            Anchor(okRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            okRect.anchoredPosition = new Vector2(0f, 40f);
            okRect.sizeDelta = new Vector2(320f, 96f);
            ok.GetComponentInChildren<TMP_Text>().color = new Color(0.16f, 0.12f, 0.04f);

            backdrop.SetAsLastSibling();
        }

        public void SetCodexProgress(int found, int total)
        {
            codexLabel.text = string.Format(Strings.CodexButtonFormat, found, total);
        }

        /// <summary>The celebration for a first discovery. Several in a row are shown one after another.</summary>
        public void ShowDiscovery(string heading, string name, int reward, Color accent)
        {
            banners.Enqueue((heading, name, reward, accent));
            if (bannerTime < 0f) NextBanner();
        }

        private void NextBanner()
        {
            if (banners.Count == 0)
            {
                bannerTime = -1f;
                bannerGroup.alpha = 0f;
                return;
            }

            var (heading, name, reward, accent) = banners.Dequeue();
            bannerHeading.text = heading;
            bannerHeading.color = accent;
            bannerName.text = name;
            bannerReward.text = reward > 0 ? string.Format(Strings.RewardFormat, reward) : string.Empty;
            bannerAccent.color = accent;
            bannerRect.SetAsLastSibling();
            bannerTime = 0f;
        }

        private void UpdateBanner()
        {
            if (bannerTime < 0f) return;
            bannerTime += Time.unscaledDeltaTime;

            const float popIn = 0.35f, hold = 2.6f, fade = 0.4f;
            float t = bannerTime;
            if (t < popIn)
            {
                float u = t / popIn - 1f;
                float eased = 1f + 3.2f * u * u * u + 2.2f * u * u;
                bannerRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
                bannerGroup.alpha = Mathf.Clamp01(t / (popIn * 0.5f));
            }
            else
            {
                bannerRect.localScale = Vector3.one;
                bannerGroup.alpha = 1f - Mathf.Clamp01((t - popIn - hold) / fade);
            }

            if (t >= popIn + hold + fade) NextBanner();
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.EnergyChanged -= OnEnergyChanged;
            simulation.PlantUnlocked -= OnPlantUnlocked;
        }

        private void Update()
        {
            if (safeArea != null && Screen.safeArea != appliedSafeArea) ApplySafeArea();

            // Portrait screens scale the HUD by width, landscape ones (the Editor, tablets) by height.
            float match = Screen.width < Screen.height ? 0f : 1f;
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match)) scaler.matchWidthOrHeight = match;

            UpdateBanner();
            UpdateEnergy();
            UpdateHint();

            levelTimer += Time.unscaledDeltaTime;
            if (levelTimer >= 0.5f) RefreshLevel();

            if (toastTimeLeft > 0f)
            {
                toastTimeLeft -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimeLeft / 0.3f);
            }
        }

        private void OnEnergyChanged(double energy)
        {
            // A sudden gain (a reward) makes the counter jump and roll up; steady income just ticks.
            if (!double.IsNaN(energyShown) && energy - energyTarget >= 5.0) energyPunch = 1f;
            energyTarget = energy;
            if (double.IsNaN(energyShown)) energyShown = energy;

            long whole = (long)Math.Floor(energy);
            if (whole == shownEnergy) return;
            shownEnergy = whole;

            foreach (var item in items)
            {
                if (item.PlantId != null)
                    item.Group.alpha = whole >= item.Cost ? 1f : 0.45f;
            }
        }

        // Rolls the counter towards the real value, and plays the punch and shake.
        private void UpdateEnergy()
        {
            if (double.IsNaN(energyShown)) return;
            float dt = Time.unscaledDeltaTime;
            double gap = energyTarget - energyShown;
            if (Math.Abs(gap) < 0.5) energyShown = energyTarget;
            else energyShown += gap * (1.0 - Math.Exp(-dt * 6.0));

            // The orb says "energy", so the counter is just the number.
            string text = ((long)Math.Floor(energyShown)).ToString("N0");
            if (energyLabel.text != text) energyLabel.text = text;

            energyPunch = Mathf.Max(0f, energyPunch - dt * 2.5f);
            energyOrb.localScale = Vector3.one * (1f + Mathf.Sin(energyPunch * Mathf.PI) * 0.35f + Mathf.Sin(Time.unscaledTime * 2f) * 0.04f);

            float shake = 0f;
            if (energyShake > 0f)
            {
                energyShake = Mathf.Max(0f, energyShake - dt);
                shake = Mathf.Sin(energyShake * 60f) * 14f * (energyShake / 0.45f);
            }
            energyPanel.anchoredPosition = new Vector2(30f + shake, -30f);
            energyLabel.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), energyShake / 0.45f);
        }

        private void Select(string plantId, bool remove)
        {
            SelectedPlantId = remove ? null : plantId;
            RemoveMode = remove;
            foreach (var item in items)
            {
                bool selected = remove ? item.PlantId == null : item.PlantId == plantId;
                item.Frame.color = selected ? SelectedColor : (item.PlantId == null ? RemoveColor : ButtonColor);
            }
        }

        // ---------- construction ----------

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            safeArea = Rect("Safe Area", transform);
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            safeArea.anchorMin = new Vector2(appliedSafeArea.xMin / Screen.width, appliedSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(appliedSafeArea.xMax / Screen.width, appliedSafeArea.yMax / Screen.height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        private void BuildEnergy()
        {
            var panel = Panel("Energy", safeArea, PanelColor, 50f);
            Anchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            panel.anchoredPosition = new Vector2(30f, -30f);
            panel.sizeDelta = new Vector2(400f, 100f);
            energyPanel = panel;

            // A little glowing orb: the energy the planet makes.
            var halo = Circle("Orb Glow", panel, new Color(1f, 0.85f, 0.35f, 0.25f));
            Anchor(halo.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
            halo.rectTransform.anchoredPosition = new Vector2(52f, 0f);
            halo.rectTransform.sizeDelta = new Vector2(78f, 78f);
            var orb = Circle("Orb", halo.rectTransform, new Color(1f, 0.84f, 0.32f, 1f));
            Stretch(orb.rectTransform, 15f);
            var shine = Circle("Shine", orb.rectTransform, new Color(1f, 0.98f, 0.85f, 0.9f));
            Anchor(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            shine.rectTransform.anchoredPosition = new Vector2(-7f, 7f);
            shine.rectTransform.sizeDelta = new Vector2(16f, 16f);
            energyOrb = halo.rectTransform;

            energyLabel = Label(panel, string.Empty, 50f, TextAlignmentOptions.Center);
            Stretch(energyLabel.rectTransform);
            energyLabel.rectTransform.offsetMin = new Vector2(90f, 0f);
            energyLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
            energyLabel.fontStyle = FontStyles.Bold;
        }

        private void BuildToast()
        {
            var holder = Rect("Toast", safeArea);
            Anchor(holder, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            holder.anchoredPosition = new Vector2(0f, 270f);
            holder.sizeDelta = new Vector2(0f, 80f);

            toastGroup = holder.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            toastPill = Panel("Pill", holder, new Color(0.05f, 0.07f, 0.14f, 0.8f), 40f);
            Anchor(toastPill, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            toastPill.sizeDelta = new Vector2(600f, 0f);
            toastPill.anchoredPosition = Vector2.zero;
            toastPill.GetComponent<Image>().raycastTarget = false;

            toastLabel = Label(holder, string.Empty, 40f, TextAlignmentOptions.Center);
            toastLabel.raycastTarget = false;
            Stretch(toastLabel.rectTransform);
        }

        private void BuildHint()
        {
            hintHolder = Rect("Hint", safeArea);
            Anchor(hintHolder, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            hintHolder.anchoredPosition = new Vector2(0f, 360f);
            hintHolder.sizeDelta = new Vector2(0f, 84f);

            hintGroup = hintHolder.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintGroup.blocksRaycasts = false;
            hintGroup.interactable = false;

            hintPill = Panel("Pill", hintHolder, new Color(0.98f, 0.92f, 0.62f, 0.95f), 42f);
            Anchor(hintPill, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            hintPill.sizeDelta = new Vector2(600f, 0f);
            hintPill.anchoredPosition = Vector2.zero;
            hintPill.GetComponent<Image>().raycastTarget = false;

            hintLabel = Label(hintHolder, string.Empty, 40f, TextAlignmentOptions.Center);
            hintLabel.color = new Color(0.22f, 0.17f, 0.08f);
            hintLabel.raycastTarget = false;
            Stretch(hintLabel.rectTransform);
        }

        private void BuildTray(IReadOnlyList<PlantDefinition> plants)
        {
            var tray = Panel("Seed Tray", safeArea, PanelColor);
            Anchor(tray, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            tray.anchoredPosition = Vector2.zero;
            tray.sizeDelta = new Vector2(0f, 240f);

            var viewport = Rect("Viewport", tray);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = tray.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            foreach (var plant in plants)
            {
                string id = plant.id;
                var item = TrayButton(content, id, plant.displayName, string.Format(Strings.CostFormat, plant.seedCost),
                    plant.primaryColor, ButtonColor, () => Select(id, remove: false));
                item.PlantId = id;
                item.Cost = plant.seedCost;
                items.Add(item);
            }

            var remove = TrayButton(content, null, Strings.RemoveTool, Strings.RemoveHint, RemoveColor, RemoveColor,
                () => Select(null, remove: true));
            items.Add(remove);
        }

        private TrayItem TrayButton(RectTransform parent, string plantId, string title, string subtitle, Color swatch, Color frameColor, Action onClick)
        {
            var frame = Panel(title, parent, frameColor);
            var element = frame.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 200f;
            element.minWidth = 200f;

            var inner = Panel("Inner", frame, ButtonColor, 18f);
            Stretch(inner, 8f);
            inner.GetComponent<Image>().raycastTarget = false;

            var icon = plantId != null ? iconFor?.Invoke(plantId) : null;
            if (icon != null)
            {
                var glow = Circle("Glow", inner, new Color(swatch.r, swatch.g, swatch.b, 0.22f));
                Anchor(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                glow.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                glow.rectTransform.sizeDelta = new Vector2(88f, 88f);

                var picture = Rect("Icon", inner);
                Anchor(picture, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                picture.anchoredPosition = new Vector2(0f, 0f);
                picture.sizeDelta = new Vector2(100f, 100f);
                var image = picture.gameObject.AddComponent<RawImage>();
                image.texture = icon;
                image.raycastTarget = false;
            }
            else
            {
                var chip = Panel("Swatch", inner, swatch, 35f);
                Anchor(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                chip.anchoredPosition = new Vector2(0f, -16f);
                chip.sizeDelta = new Vector2(70f, 70f);
                chip.GetComponent<Image>().raycastTarget = false;
            }

            var name = Label(inner, title, 30f, TextAlignmentOptions.Center);
            Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            name.rectTransform.anchoredPosition = new Vector2(0f, 44f);
            name.rectTransform.sizeDelta = new Vector2(0f, 46f);
            name.raycastTarget = false;

            var sub = Label(inner, subtitle, 26f, TextAlignmentOptions.Center);
            sub.color = new Color(1f, 1f, 1f, 0.75f);
            Anchor(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            sub.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            sub.rectTransform.sizeDelta = new Vector2(0f, 36f);
            sub.raycastTarget = false;

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            frame.gameObject.AddComponent<PressFeedback>();

            return new TrayItem
            {
                Frame = frame.GetComponent<Image>(),
                Group = frame.gameObject.AddComponent<CanvasGroup>(),
            };
        }

        private void BuildCodexButton()
        {
            var button = TextButton(safeArea, string.Empty, 38f, PanelColor, () => CodexClicked?.Invoke());
            var rect = (RectTransform)button.transform;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(450f, -30f);
            rect.sizeDelta = new Vector2(250f, 100f);
            codexLabel = button.GetComponentInChildren<TMP_Text>();
        }

        // Top right: the current planet's name, which opens the planets screen.
        private void BuildPlanetButton()
        {
            var button = TextButton(safeArea, string.Empty, 38f, PanelColor, () => PlanetsClicked?.Invoke());
            var rect = (RectTransform)button.transform;
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = new Vector2(-30f, -30f);
            rect.sizeDelta = new Vector2(330f, 100f);
            planetLabel = button.GetComponentInChildren<TMP_Text>();
        }

        // Second row under the energy: the planet level with its progress, and the upgrades button.
        private void BuildLevel()
        {
            var levelButton = TextButton(safeArea, string.Empty, 34f, PanelColor, () => UpgradesClicked?.Invoke());
            var rect = (RectTransform)levelButton.transform;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(30f, -145f);
            rect.sizeDelta = new Vector2(400f, 90f);
            levelLabel = levelButton.GetComponentInChildren<TMP_Text>();
            levelLabel.rectTransform.offsetMin = new Vector2(0f, 14f);

            var bar = Panel("Level Bar", rect, new Color(0.12f, 0.15f, 0.25f, 1f), 6f);
            Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            bar.anchoredPosition = new Vector2(0f, 10f);
            bar.sizeDelta = new Vector2(-40f, 12f);
            bar.GetComponent<Image>().raycastTarget = false;
            levelFill = Panel("Fill", bar, new Color(0.45f, 0.85f, 0.45f, 1f), 6f);
            levelFill.anchorMin = Vector2.zero;
            levelFill.anchorMax = new Vector2(0f, 1f);
            levelFill.offsetMin = Vector2.zero;
            levelFill.offsetMax = Vector2.zero;
            levelFill.GetComponent<Image>().raycastTarget = false;

            var upgrades = TextButton(safeArea, Strings.UpgradesButton, 38f, PanelColor, () => UpgradesClicked?.Invoke());
            var upgradesRect = (RectTransform)upgrades.transform;
            Anchor(upgradesRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            upgradesRect.anchoredPosition = new Vector2(450f, -145f);
            upgradesRect.sizeDelta = new Vector2(250f, 90f);

            var badge = Panel("Badge", upgradesRect, SelectedColor, 15f);
            Anchor(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            badge.anchoredPosition = new Vector2(-8f, -8f);
            badge.sizeDelta = new Vector2(30f, 30f);
            badge.GetComponent<Image>().raycastTarget = false;
            upgradesBadge = badge.gameObject;
            upgradesBadge.SetActive(false);
        }

        private void BuildBanner()
        {
            bannerRect = Panel("Discovery Banner", safeArea, new Color(0.05f, 0.07f, 0.14f, 0.92f), 40f);
            Anchor(bannerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            bannerRect.anchoredPosition = new Vector2(0f, -400f);
            bannerRect.sizeDelta = new Vector2(840f, 260f);
            bannerRect.GetComponent<Image>().raycastTarget = false;

            bannerGroup = bannerRect.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;

            var accent = Panel("Accent", bannerRect, Color.white, 5f);
            TopBand(accent, 0f, 10f);
            bannerAccent = accent.GetComponent<Image>();
            bannerAccent.raycastTarget = false;

            bannerHeading = Label(bannerRect, string.Empty, 38f, TextAlignmentOptions.Center);
            TopBand(bannerHeading.rectTransform, 30f, 50f);
            bannerName = Label(bannerRect, string.Empty, 72f, TextAlignmentOptions.Center);
            bannerName.fontStyle = FontStyles.Bold;
            TopBand(bannerName.rectTransform, 85f, 90f);
            bannerReward = Label(bannerRect, string.Empty, 38f, TextAlignmentOptions.Center);
            bannerReward.color = SelectedColor;
            TopBand(bannerReward.rectTransform, 185f, 50f);
        }

        private void BuildDebug()
        {
            // Out of the way of the top bar and the discovery banner.
            var panel = Rect("Debug", safeArea);
            Anchor(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            panel.anchoredPosition = new Vector2(-20f, 120f);
            panel.sizeDelta = new Vector2(190f, 8f * 78f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            DebugButton(panel, Strings.DebugAddEnergy, () => DebugAddEnergy?.Invoke());
            DebugButton(panel, Strings.DebugSkipMinute, () => DebugSkipTime?.Invoke(60));
            DebugButton(panel, Strings.DebugSkipHour, () => DebugSkipTime?.Invoke(3600));
            DebugButton(panel, Strings.DebugUnlockAll, () => DebugUnlockAll?.Invoke());
            DebugButton(panel, Strings.DebugNewSeed, () => DebugNewSeed?.Invoke());
            DebugButton(panel, Strings.DebugNight, () => DebugToggleNight?.Invoke());
            DebugButton(panel, Strings.DebugRain, () => DebugToggleRain?.Invoke());
            DebugButton(panel, Strings.DebugReset, () => DebugReset?.Invoke());
        }

        private static void DebugButton(RectTransform parent, string text, Action onClick)
        {
            var frame = Panel(text, parent, PanelColor);
            var label = Label(frame, text, 30f, TextAlignmentOptions.Center);
            label.raycastTarget = false;
            Stretch(label.rectTransform);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
        }
    }
}
