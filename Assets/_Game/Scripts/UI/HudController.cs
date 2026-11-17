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
        private TMP_Text toastLabel;
        private CanvasGroup toastGroup;
        private float toastTimeLeft;
        private long shownEnergy = long.MinValue;
        private TMP_Text codexLabel;
        private readonly Queue<(string heading, string name, int reward, Color accent)> banners =
            new Queue<(string heading, string name, int reward, Color accent)>();
        private RectTransform bannerRect;
        private CanvasGroup bannerGroup;
        private Image bannerAccent;
        private TMP_Text bannerHeading;
        private TMP_Text bannerName;
        private TMP_Text bannerReward;
        private float bannerTime = -1f;

        /// <summary>The plant id the player has picked in the tray, or null in remove mode.</summary>
        public string SelectedPlantId { get; private set; }
        public bool RemoveMode { get; private set; }

        /// <summary>Full-screen area inside the notch and home bar, for other screens to build into.</summary>
        public RectTransform SafeArea => safeArea;

        public event Action CodexClicked;
        public event Action DebugAddEnergy;
        public event Action<double> DebugSkipTime;
        public event Action DebugReset;

        public void Init(PlanetSimulation simulation, GameConfig config, bool showDebug)
        {
            this.simulation = simulation;

            EnsureEventSystem();
            BuildCanvas();
            BuildEnergy();
            BuildToast();
            BuildTray(config);
            BuildCodexButton();
            BuildBanner();
            if (showDebug) BuildDebug();

            if (config.plants.Length > 0) Select(config.plants[0].id, remove: false);
            simulation.EnergyChanged += OnEnergyChanged;
            OnEnergyChanged(simulation.Energy);
        }

        public void ShowToast(string message, float seconds = 1.8f)
        {
            toastLabel.text = message;
            toastTimeLeft = seconds;
            toastGroup.alpha = 1f;
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
            if (simulation != null) simulation.EnergyChanged -= OnEnergyChanged;
        }

        private void Update()
        {
            if (safeArea != null && Screen.safeArea != appliedSafeArea) ApplySafeArea();

            // Portrait screens scale the HUD by width, landscape ones (the Editor, tablets) by height.
            float match = Screen.width < Screen.height ? 0f : 1f;
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match)) scaler.matchWidthOrHeight = match;

            UpdateBanner();

            if (toastTimeLeft > 0f)
            {
                toastTimeLeft -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimeLeft / 0.3f);
            }
        }

        private void OnEnergyChanged(double energy)
        {
            long whole = (long)Math.Floor(energy);
            if (whole == shownEnergy) return;
            shownEnergy = whole;

            energyLabel.text = string.Format(Strings.EnergyFormat, whole.ToString("N0"));
            foreach (var item in items)
            {
                if (item.PlantId != null)
                    item.Group.alpha = whole >= item.Cost ? 1f : 0.45f;
            }
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
            var panel = Panel("Energy", safeArea, PanelColor);
            Anchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            panel.anchoredPosition = new Vector2(30f, -30f);
            panel.sizeDelta = new Vector2(400f, 100f);

            energyLabel = Label(panel, string.Empty, 46f, TextAlignmentOptions.Center);
            Stretch(energyLabel.rectTransform);
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

            toastLabel = Label(holder, string.Empty, 40f, TextAlignmentOptions.Center);
            toastLabel.raycastTarget = false;
            Stretch(toastLabel.rectTransform);
        }

        private void BuildTray(GameConfig config)
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

            foreach (var plant in config.plants)
            {
                string id = plant.id;
                var item = TrayButton(content, plant.displayName, string.Format(Strings.CostFormat, plant.seedCost),
                    plant.primaryColor, ButtonColor, () => Select(id, remove: false));
                item.PlantId = id;
                item.Cost = plant.seedCost;
                items.Add(item);
            }

            var remove = TrayButton(content, Strings.RemoveTool, Strings.RemoveHint, RemoveColor, RemoveColor,
                () => Select(null, remove: true));
            items.Add(remove);
        }

        private TrayItem TrayButton(RectTransform parent, string title, string subtitle, Color swatch, Color frameColor, Action onClick)
        {
            var frame = Panel(title, parent, frameColor);
            var element = frame.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 200f;
            element.minWidth = 200f;

            var inner = Panel("Inner", frame, ButtonColor);
            Stretch(inner, 8f);
            inner.GetComponent<Image>().raycastTarget = false;

            var chip = Panel("Swatch", inner, swatch);
            Anchor(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            chip.anchoredPosition = new Vector2(0f, -16f);
            chip.sizeDelta = new Vector2(70f, 70f);
            chip.GetComponent<Image>().raycastTarget = false;

            var name = Label(inner, title, 30f, TextAlignmentOptions.Center);
            Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            name.rectTransform.anchoredPosition = new Vector2(0f, 46f);
            name.rectTransform.sizeDelta = new Vector2(0f, 40f);
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

        private void BuildBanner()
        {
            bannerRect = Panel("Discovery Banner", safeArea, new Color(0.05f, 0.07f, 0.14f, 0.92f));
            Anchor(bannerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            bannerRect.anchoredPosition = new Vector2(0f, -400f);
            bannerRect.sizeDelta = new Vector2(840f, 260f);
            bannerRect.GetComponent<Image>().raycastTarget = false;

            bannerGroup = bannerRect.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;

            var accent = Panel("Accent", bannerRect, Color.white);
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
            var panel = Rect("Debug", safeArea);
            Anchor(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            panel.anchoredPosition = new Vector2(-30f, -30f);
            panel.sizeDelta = new Vector2(190f, 4f * 78f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            DebugButton(panel, Strings.DebugAddEnergy, () => DebugAddEnergy?.Invoke());
            DebugButton(panel, Strings.DebugSkipMinute, () => DebugSkipTime?.Invoke(60));
            DebugButton(panel, Strings.DebugSkipHour, () => DebugSkipTime?.Invoke(3600));
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
