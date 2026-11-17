using System;
using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.GameInput;
using TinyPlanet.Simulation;
using TinyPlanet.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Composition root: builds the simulation from the save file (or a fresh planet), creates the views,
    /// and wires input, HUD and saving together. The only object that needs to be placed in the scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float TickInterval = 0.25f;

        /// <summary>Time away after which coming back shows the "while you were away" card instead of a toast.</summary>
        private const double AwaySummarySeconds = 10 * 60;

        private Sky planetSky;
        private SkyView skyView;
        private Material skyMaterial;
        private ColorAdjustments grading;
        private const float AutosaveInterval = 30f;

        [SerializeField] private GameConfig config;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Light keyLight;

        private readonly Dictionary<string, PlantDefinition> definitions = new Dictionary<string, PlantDefinition>();
        private readonly Dictionary<string, CreatureDefinition> creatureDefinitions = new Dictionary<string, CreatureDefinition>();
        private readonly List<BiomeDefinition> biomeDefinitions = new List<BiomeDefinition>();
        private readonly List<PlantDefinition> planetPlants = new List<PlantDefinition>();
        private readonly List<CreatureDefinition> planetCreatures = new List<CreatureDefinition>();
        private readonly Dictionary<int, PlanetRecipe> recipes = new Dictionary<int, PlanetRecipe>();
        private GameSave game;
        private int activeIndex;
        private ContentPool pool;
        private PlanetRecipe recipe;
        private PlanetLook look;
        private PlanetsController planets;
        private readonly Dictionary<string, Texture> icons = new Dictionary<string, Texture>();
        private readonly Dictionary<string, Texture> silhouettes = new Dictionary<string, Texture>();
        private MaterialLibrary materials;
        private CreatureLayer creatureLayer;
        private CodexController codex;
        private UpgradesController upgrades;
        private OffsetClock clock;
        private PlanetSimulation simulation;
        private PlanetView planetView;
        private OrbitCamera orbit;
        private HudController hud;
        private Effects effects;
        private AudioDirector audioDirector;
        private PlacementIndicator marker;
        private InputReader input;
        private float radius;
        private float tickTimer;
        private float autosaveTimer;
        private bool savingEnabled;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            game = SaveStore.Load();
            bool freshGame = game == null;
            if (freshGame)
            {
                game = new GameSave();
                game.planets.Add(new SaveData { seed = config.defaultSeed, cellCount = config.cellCount, started = false });
            }
            var save = game.Active;
            activeIndex = game.planets.IndexOf(save);
            clock = new OffsetClock(new SystemClock(), game.debugClockOffsetSeconds);

            pool = BuildPool();
            recipe = RecipeFor(activeIndex);
            look = PlanetLook.From(recipe, config);

            // A saved planet keeps its own cell count, so plant positions stay valid if the config changes.
            int cellCount = save.started && save.cellCount > PlanetGrid.NeighbourCount ? save.cellCount : recipe.CellCount;
            save.cellCount = cellCount;
            var planet = PlanetGenerator.Generate(save.seed, cellCount, recipe.Biomes);

            foreach (var plant in config.plants) definitions[plant.id] = plant;
            foreach (var creature in config.creatures ?? Array.Empty<CreatureDefinition>())
            {
                if (creature != null) creatureDefinitions[creature.id] = creature;
            }
            foreach (var signature in recipe.Signatures)
                creatureDefinitions[signature.Spec.Id] = SignatureDefinition(signature);
            foreach (var plant in recipe.Plants)
            {
                if (definitions.TryGetValue(plant.Id, out var definition)) planetPlants.Add(definition);
            }
            foreach (var creature in recipe.Creatures)
            {
                if (creatureDefinitions.TryGetValue(creature.Id, out var definition)) planetCreatures.Add(definition);
            }

            var economy = new EconomySettings
            {
                StartingEnergy = config.startingEnergy,
                MaxOfflineHours = config.maxOfflineHours,
                MaxCatchUpChecks = config.maxCatchUpChecks,
                LevelLifeThresholds = config.levelLifeThresholds ?? Array.Empty<int>(),
                GoldenChance = config.goldenChance,
            };
            // Night and day follow the device's own clock; each planet has its own rainy days.
            planetSky = new Sky(save.seed, new SkySettings
            {
                NightStartHour = config.nightStartHour,
                DayStartHour = config.dayStartHour,
                RainChance = config.rainChance,
                RainGrowthMultiplier = config.rainGrowthMultiplier,
            }, TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow));
            simulation = new PlanetSimulation(planet, recipe.Plants, recipe.Creatures, economy, clock, save, null, planetSky);

            materials = new MaterialLibrary(config.litMaterial, config.unlitMaterial, config.terrainMaterial);

            radius = config.planetRadius * recipe.RadiusScale;
            planetView = new GameObject("Planet").AddComponent<PlanetView>();
            planetView.Init(simulation, config, materials, biomeDefinitions, look, radius);

            creatureLayer = new GameObject("Creatures").AddComponent<CreatureLayer>();
            creatureLayer.transform.SetParent(planetView.transform, false);
            creatureLayer.Init(simulation, planetCreatures, materials, planetView.Terrain);

            SetUpLook();

            effects = new GameObject("Effects").AddComponent<Effects>();
            effects.Init(config.particleMaterial);
            marker = new GameObject("Placement Marker").AddComponent<PlacementIndicator>();
            marker.Init(config.particleMaterial, planetView.CellSize * 1.3f);
            audioDirector = new GameObject("Audio").AddComponent<AudioDirector>();
            audioDirector.Init(AmbienceInView);
            orbit = gameCamera.gameObject.AddComponent<OrbitCamera>();
            orbit.Init(gameCamera, radius);
            creatureLayer.Effects = effects;
            skyView = new GameObject("Sky").AddComponent<SkyView>();
            skyView.Init(simulation, planetView, gameCamera, keyLight, skyMaterial, grading, audioDirector, config.particleMaterial, radius);
            // A new planet opens on its starting land.
            if (!save.started) orbit.LookAt(planetView.BiomeCentre(0), instant: true);

            if (keyLight != null)
            {
                // The light travels with the camera, so the side the player looks at is always lit.
                keyLight.transform.SetParent(gameCamera.transform, false);
                keyLight.transform.localPosition = Vector3.zero;
                keyLight.transform.localRotation = Quaternion.Euler(32f, -42f, 0f);
            }

            hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Init(simulation, planetPlants, showDebug: Debug.isDebugBuild, iconFor: id => IconFor(id, false));
            hud.SetPlanetName(recipe.Name);
            hud.DebugAddEnergy += () => simulation.AddEnergy(100);
            hud.DebugSkipTime += SkipTime;
            hud.DebugReset += ResetProgress;
            hud.DebugUnlockAll += simulation.DebugUnlockAll;
            hud.DebugNewSeed += RerollPlanet;
            // First press flips night or rain away from what the clock says; the second follows the clock again.
            hud.DebugToggleNight += () => planetSky.DebugNight =
                planetSky.DebugNight.HasValue ? (bool?)null : !planetSky.IsNight(simulation.NowUtcTicks);
            hud.DebugToggleRain += () => planetSky.DebugRain =
                planetSky.DebugRain.HasValue ? (bool?)null : !planetSky.IsRaining(simulation.NowUtcTicks);

            codex = hud.gameObject.AddComponent<CodexController>();
            codex.Init(hud.SafeArea, simulation, BuildCodexEntries(), IconFor, OverallProgress);
            hud.CodexClicked += codex.Open;
            UpdateCodexProgress();
            simulation.SpeciesDiscovered += OnSpeciesDiscovered;
            simulation.GoldenDiscovered += OnGoldenDiscovered;
            simulation.CreatureArrived += _ => codex.Refresh();
            simulation.CreatureLeft += _ => codex.Refresh();

            upgrades = hud.gameObject.AddComponent<UpgradesController>();
            upgrades.Init(hud.SafeArea, simulation, BuildUpgradeEntries());
            hud.UpgradesClicked += upgrades.Open;
            hud.SetUpgradeCheck(upgrades.AnythingAffordable);
            simulation.LevelChanged += OnLevelChanged;
            simulation.BiomeUnlocked += OnBiomeUnlocked;
            simulation.PlantUnlocked += OnPlantUnlocked;

            planets = hud.gameObject.AddComponent<PlanetsController>();
            planets.Init(hud.SafeArea, BuildPlanetEntries, CanDiscoverPlanet, DiscoverPlanet, VisitPlanet);
            planets.SetLevelNeeded(config.newPlanetLevel);
            hud.PlanetsClicked += planets.Open;

            simulation.PlantPlanted += OnPlantPlanted;
            simulation.PlantStageChanged += OnPlantStageChanged;
            PressFeedback.Clicked += OnUiClicked;

            input = gameObject.AddComponent<InputReader>();
            input.Dragged += orbit.Drag;
            input.DragEnded += orbit.EndDrag;
            input.Zoomed += orbit.Zoom;
            input.Tapped += OnTapped;

            savingEnabled = true;

            if (save.started)
            {
                WelcomeBack(save);
            }
            else if (activeIndex > 0)
            {
                hud.ShowDiscovery(Strings.NewPlanetHeading, recipe.Name, 0, look.Palette.atmosphere);
            }
            save.started = true;
        }

        private void Update()
        {
            // The sky glows behind the planet, wherever the camera has turned.
            Shader.SetGlobalVector(SkyCentreId, gameCamera.transform.forward);

            UpdateMarker();

            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer = 0f;
                simulation.Tick();
                hud.SetHint(CurrentHint());
            }

            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveInterval) Save();
        }

        private static readonly int SkyCentreId = Shader.PropertyToID("_SkyCentre");

        // Two hints for a brand-new player on the home planet, each gone as soon as it has been followed.
        private string CurrentHint()
        {
            if (activeIndex != 0) return null;
            foreach (var creature in simulation.CreatureSpecs)
            {
                if (simulation.IsDiscovered(creature.Id)) return null;
            }
            return simulation.Plants.Count == 0 ? Strings.HintPlant : Strings.HintAttract;
        }

        // Sky, light and post-processing: the mood of the scene.
        private void SetUpLook()
        {
            if (config.skyMaterial != null)
            {
                var sky = new Material(config.skyMaterial);
                skyMaterial = sky;
                sky.SetColor("_Centre", look.Palette.skyCentre);
                sky.SetColor("_Edge", look.Palette.skyEdge);
                RenderSettings.skybox = sky;
                gameCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.backgroundColor = config.spaceColor;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.42f, 0.48f);
            if (keyLight != null)
            {
                keyLight.color = new Color(1f, 0.95f, 0.86f);
                keyLight.intensity = 1.05f;
                keyLight.shadows = LightShadows.None;
            }

            var cameraData = gameCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            grading = profile.Add<ColorAdjustments>(true);
            grading.saturation.Override(14f);
            grading.contrast.Override(8f);
            grading.postExposure.Override(-0.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.5f);

            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
            else simulation?.Tick();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy() => PressFeedback.Clicked -= OnUiClicked;

        private void OnTapped(Vector2 screenPosition)
        {
            // Creatures first: they are small and stand on top of cells the player might otherwise plant on.
            float pickRadius = 45f * Mathf.Max(1f, Screen.dpi / 160f);
            if (creatureLayer.TryPick(gameCamera, screenPosition, pickRadius, out var creature))
            {
                creature.React();
                var definition = creature.Definition;
                effects.Tapped(creature.BodyCentre, creature.transform.up, definition.primaryColor);
                audioDirector.Creature((int)definition.shape, definition.size);
                Haptics.Medium();
                hud.ShowToast(string.Format(Strings.CreatureTapFormat, definition.displayName, Strings.Rarity(definition.rarity)));
                return;
            }

            if (!planetView.TryGetCell(gameCamera.ScreenPointToRay(screenPosition), out int cell)) return;

            var existing = simulation.PlantAt(cell);

            if (hud.RemoveMode)
            {
                if (existing == null)
                {
                    hud.ShowToast(Strings.NothingToRemove);
                    return;
                }
                string name = DisplayName(existing.Spec.Id);
                if (simulation.TryRemove(cell, out int refund))
                {
                    hud.ShowToast(string.Format(Strings.RemovedFormat, name, refund));
                    effects.Planted(planetView.CellPoint(cell), planetView.CellNormal(cell), config.soilColor);
                    audioDirector.Plant();
                    Haptics.Light();
                }
                return;
            }

            if (existing != null)
            {
                DescribePlant(existing);
                effects.Ping(planetView.CellPoint(cell), planetView.CellNormal(cell), new Color(1f, 1f, 1f, 0.8f), planetView.CellSize * 1.6f);
                audioDirector.Click();
                return;
            }

            var result = simulation.TryPlant(cell, hud.SelectedPlantId);
            if (result != PlantResult.Ok)
            {
                effects.Ping(planetView.CellPoint(cell), planetView.CellNormal(cell), MarkerNo, planetView.CellSize * 1.4f);
                audioDirector.Deny();
                if (result == PlantResult.NotEnoughEnergy) hud.ShakeEnergy();
            }
            string land = BiomeName(simulation.Planet.Cells[cell].Biome);
            switch (result)
            {
                case PlantResult.NotEnoughEnergy: hud.ShowToast(Strings.NotEnoughEnergy); break;
                case PlantResult.Water: hud.ShowToast(Strings.WaterHere); break;
                case PlantResult.Dormant: hud.ShowToast(string.Format(Strings.DormantFormat, land.ToLowerInvariant()), 2.5f); break;
                case PlantResult.Locked: hud.ShowToast(Strings.LockedSeed); break;
                case PlantResult.WrongBiome:
                    hud.ShowToast(string.Format(Strings.WrongBiomeFormat, DisplayName(hud.SelectedPlantId), land.ToLowerInvariant()), 2.5f);
                    break;
            }
        }

        private static readonly Color LevelAccent = new Color(0.45f, 0.85f, 0.45f);

        private void OnLevelChanged(int level)
        {
            hud.ShowDiscovery(Strings.LevelUpHeading, string.Format(Strings.LevelUpFormat, level), 0, LevelAccent);
            upgrades.Refresh();
            effects.LevelUp(planetView.transform.position, radius, LevelAccent);
            audioDirector.LevelUp();
            Haptics.Strong();
        }

        private void OnBiomeUnlocked(string biomeId)
        {
            int index = biomeDefinitions.FindIndex(b => b.id == biomeId);
            if (index < 0) return;
            var biome = biomeDefinitions[index];
            hud.ShowDiscovery(Strings.LandAwakeHeading, biome.displayName, 0, biome.groundColor);
            // Show the player the land waking up.
            upgrades.Close();
            orbit.LookAt(planetView.BiomeCentre(index), instant: false);

            var points = new List<Vector3>();
            var cells = simulation.Planet.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Biome == index && !simulation.Planet.IsWater(i)) points.Add(planetView.CellPoint(i));
            }
            effects.Wave(points, planetView.BiomeCentre(index), look.Shift(biome.groundColor), 2f);
            audioDirector.Unlock();
            Haptics.Strong();
        }

        private void OnPlantUnlocked(string plantId)
        {
            audioDirector.Unlock();
            Haptics.Medium();
            if (definitions.TryGetValue(plantId, out var plant))
                hud.ShowDiscovery(Strings.SeedUnlockedHeading, plant.displayName, 0, plant.secondaryColor == Color.white ? plant.primaryColor : plant.secondaryColor);
        }

        private string BiomeName(int index) =>
            index >= 0 && index < biomeDefinitions.Count ? biomeDefinitions[index].displayName : "land";

        // The lands a plant grows in, as text: "Meadow, Wetland".
        private string WhereItGrows(PlantDefinition plant)
        {
            var names = new List<string>();
            for (int i = 0; i < biomeDefinitions.Count && i < recipe.Biomes.Count; i++)
            {
                if (recipe.Biomes[i].Allows(plant.id)) names.Add(biomeDefinitions[i].displayName);
            }
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        private List<UpgradeEntry> BuildUpgradeEntries()
        {
            var entries = new List<UpgradeEntry>();
            foreach (var biome in biomeDefinitions)
            {
                entries.Add(new UpgradeEntry
                {
                    Id = biome.id, Name = biome.displayName, Detail = biome.description, Swatch = biome.groundColor, IsBiome = true,
                });
            }
            foreach (var plant in planetPlants)
            {
                entries.Add(new UpgradeEntry
                {
                    Id = plant.id,
                    Name = plant.displayName,
                    Detail = string.Format(Strings.SeedDetailFormat, WhereItGrows(plant) ?? "-", plant.energyPerMinute.ToString("0.#")),
                    Swatch = plant.secondaryColor == Color.white ? plant.primaryColor : plant.secondaryColor,
                });
            }
            return entries;
        }

        private void OnSpeciesDiscovered(string id, int reward)
        {
            if (creatureDefinitions.TryGetValue(id, out var creature))
            {
                string heading = creature.rarity >= Rarity.Rare
                    ? string.Format(Strings.NewRareCreatureFormat, Strings.Rarity(creature.rarity).ToLowerInvariant())
                    : Strings.NewCreature;
                hud.ShowDiscovery(heading, creature.displayName, reward, CodexController.RarityColor(creature.rarity));
                audioDirector.Discover((int)creature.rarity);
                Haptics.Strong();

                // Flash where it arrived, and turn the planet so the player sees it.
                CreatureInstance newest = null;
                foreach (var living in simulation.Creatures)
                {
                    if (living.Spec.Id == id) newest = living;
                }
                if (newest != null)
                {
                    int cell = newest.HomeCell;
                    effects.Discovered(planetView.CellPoint(cell), planetView.CellNormal(cell), CodexController.RarityColor(creature.rarity), (int)creature.rarity);
                    orbit.LookAt(simulation.Planet.Grid.Direction(cell), instant: false);
                }
            }
            else if (definitions.TryGetValue(id, out var plant))
            {
                hud.ShowDiscovery(Strings.NewPlant, plant.displayName, reward, plant.primaryColor);
                audioDirector.Discover(0);
                Haptics.Strong();
            }
            UpdateCodexProgress();
            codex.Refresh();
        }

        private void OnGoldenDiscovered(string id, int reward)
        {
            if (!creatureDefinitions.TryGetValue(id, out var creature)) return;
            hud.ShowDiscovery(Strings.NewGolden, GoldenLook.Of(creature).displayName, reward, GoldenLook.Gold);
            audioDirector.Discover(2);
            Haptics.Strong();

            CreatureInstance newest = null;
            foreach (var living in simulation.Creatures)
            {
                if (living.Spec.Id == id && living.Golden) newest = living;
            }
            if (newest != null)
            {
                int cell = newest.HomeCell;
                effects.Discovered(planetView.CellPoint(cell), planetView.CellNormal(cell), GoldenLook.Gold, 2);
                orbit.LookAt(simulation.Planet.Grid.Direction(cell), instant: false);
            }
            codex.Refresh();
        }

        // ---------- coming back ----------

        // Catches up on the time away right now, so everything that happened can be summed up: after a short
        // break a toast with the energy earned, after a longer one a card listing who arrived and what grew.
        private void WelcomeBack(SaveData save)
        {
            long savedAt = save.savedAtUtcTicks;
            double energyBefore = save.energy;
            var arrivals = new List<CreatureInstance>();
            Action<CreatureInstance> collect = arrivals.Add;
            simulation.CreatureArrived += collect;
            simulation.Tick();
            simulation.CreatureArrived -= collect;

            long now = simulation.NowUtcTicks;
            long gained = (long)Math.Floor(simulation.Energy - energyBefore);
            double away = savedAt > 0 ? (now - savedAt) / (double)TimeSpan.TicksPerSecond : 0;
            if (away < AwaySummarySeconds)
            {
                if (gained >= 1) hud.ShowToast(string.Format(Strings.WelcomeBackFormat, gained.ToString("N0")), 3f);
                return;
            }

            // Only those still here: a night visitor may have gone home by morning.
            arrivals.RemoveAll(creature => !ContainsCreature(creature));
            int grown = 0;
            foreach (var plant in simulation.Plants)
            {
                if (plant.MatureAtUtcTicks > savedAt && plant.MatureAtUtcTicks <= now) grown++;
            }

            var lines = new List<(string text, Texture icon, Color tint)>();
            if (gained >= 1) lines.Add((string.Format(Strings.AwayEnergyFormat, gained.ToString("N0")), null, Color.white));
            if (grown > 0) lines.Add((grown == 1 ? Strings.AwayGrownOne : string.Format(Strings.AwayGrownFormat, grown), null, Color.white));

            var groups = new List<(string id, bool golden, int count)>();
            foreach (var creature in arrivals)
            {
                int index = groups.FindIndex(g => g.id == creature.Spec.Id && g.golden == creature.Golden);
                if (index < 0) groups.Add((creature.Spec.Id, creature.Golden, 1));
                else groups[index] = (groups[index].id, groups[index].golden, groups[index].count + 1);
            }
            // Golden and rare visitors first; the card has room for a handful.
            groups.Sort((a, b) => (b.golden ? 10 : 0) + (int)RarityOf(b.id) - ((a.golden ? 10 : 0) + (int)RarityOf(a.id)));
            for (int i = 0; i < groups.Count && i < 4; i++)
            {
                var (id, golden, count) = groups[i];
                if (!creatureDefinitions.TryGetValue(id, out var definition)) continue;
                string name = golden ? GoldenLook.Of(definition).displayName : definition.displayName;
                string text = count > 1 ? string.Format(Strings.AwayArrivedManyFormat, name, count) : string.Format(Strings.AwayArrivedFormat, name);
                lines.Add((text, IconFor(id, false), golden ? GoldenLook.Gold : CodexController.RarityColor(definition.rarity)));
            }
            if (simulation.Sky.IsRaining(now)) lines.Add((Strings.AwayRaining, null, Color.white));
            if (lines.Count == 0) lines.Add((Strings.AwayNothing, null, Color.white));

            hud.ShowSummary(Strings.AwayTitle, lines);

            // Turn to the newest arrival, so the first thing the player sees is who came.
            if (arrivals.Count > 0)
                orbit.LookAt(simulation.Planet.Grid.Direction(arrivals[arrivals.Count - 1].HomeCell), instant: false);
        }

        private bool ContainsCreature(CreatureInstance creature)
        {
            foreach (var living in simulation.Creatures)
            {
                if (living == creature) return true;
            }
            return false;
        }

        private Rarity RarityOf(string creatureId) =>
            creatureDefinitions.TryGetValue(creatureId, out var definition) ? definition.rarity : Rarity.Common;

        private void UpdateCodexProgress()
        {
            var (found, total) = codex.Progress();
            hud.SetCodexProgress(found, total);
        }

        // ---------- feel: effects, sound, marker ----------

        private static readonly Color MarkerOk = new Color(0.75f, 1f, 0.6f, 0.9f);
        private static readonly Color MarkerLook = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color MarkerPoor = new Color(1f, 0.78f, 0.35f, 0.85f);
        private static readonly Color MarkerNo = new Color(1f, 0.45f, 0.4f, 0.6f);
        private static readonly Color MarkerRemove = new Color(1f, 0.4f, 0.35f, 0.9f);

        private void OnPlantPlanted(PlantInstance plant)
        {
            effects.Planted(planetView.CellPoint(plant.Cell), planetView.CellNormal(plant.Cell), config.soilColor);
            audioDirector.Plant();
            Haptics.Light();
        }

        private void OnPlantStageChanged(PlantInstance plant, PlantStage stage)
        {
            if (stage != PlantStage.Mature || !definitions.TryGetValue(plant.Spec.Id, out var definition)) return;
            var color = definition.secondaryColor == Color.white ? definition.primaryColor : definition.secondaryColor;
            effects.Grown(planetView.CellPoint(plant.Cell), planetView.CellNormal(plant.Cell), color, definition.matureHeight);
            audioDirector.Grow();
        }

        private void OnUiClicked()
        {
            audioDirector.Click();
            Haptics.Light();
        }

        // The ring under the pointer, coloured by what a tap would do there.
        private void UpdateMarker()
        {
            if (!input.TryGetPointer(out var point, out bool pressed)
                || !planetView.TryGetCell(gameCamera.ScreenPointToRay(point), out int cell))
            {
                marker.Hide();
                return;
            }

            Color color;
            var existing = simulation.PlantAt(cell);
            if (hud.RemoveMode) color = existing != null ? MarkerRemove : MarkerNo;
            else if (existing != null) color = MarkerLook;
            else
            {
                switch (simulation.CanPlant(cell, hud.SelectedPlantId))
                {
                    case PlantResult.Ok: color = MarkerOk; break;
                    case PlantResult.NotEnoughEnergy: color = MarkerPoor; break;
                    default: color = MarkerNo; break;
                }
            }
            marker.Show(planetView.CellPoint(cell), planetView.CellNormal(cell), color, pressed);
        }

        // Which ambience fits the land in the middle of the view.
        private Ambience AmbienceInView()
        {
            var planet = simulation.Planet;
            int cell = planet.Grid.FindNearestCell(gameCamera.transform.position - planetView.transform.position);
            if (!simulation.IsAwake(cell)) return Ambience.Wind;
            int biome = planet.Cells[cell].Biome;
            if (biome < 0 || biome >= biomeDefinitions.Count) return Ambience.Birds;
            switch (biomeDefinitions[biome].coverShape)
            {
                case CoverShape.Reeds: return Ambience.Marsh;
                case CoverShape.RoundTrees: return Ambience.Birds;
                default: return Ambience.Wind;
            }
        }

        // ---------- planets ----------

        private ContentPool BuildPool()
        {
            var result = new ContentPool();
            foreach (var biome in config.biomes ?? Array.Empty<BiomeDefinition>())
            {
                if (biome == null) continue;
                biomeDefinitions.Add(biome);
                result.Biomes.Add(biome.ToSpec());
            }
            foreach (var plant in config.plants) result.Plants.Add(plant.ToSpec());
            foreach (var creature in config.creatures ?? Array.Empty<CreatureDefinition>())
            {
                if (creature != null) result.Creatures.Add(creature.ToSpec());
            }
            return result;
        }

        private PlanetRecipe RecipeFor(int index)
        {
            if (recipes.TryGetValue(index, out var cached)) return cached;
            var made = PlanetRecipes.Create(pool, game.planets[index].seed, index, config.cellCount);
            recipes[index] = made;
            return made;
        }

        // A planet-only creature, dressed in colours from its recipe.
        private CreatureDefinition SignatureDefinition(SignatureCreature signature)
        {
            var definition = ScriptableObject.CreateInstance<CreatureDefinition>();
            definition.name = signature.Name;
            definition.id = signature.Spec.Id;
            definition.displayName = signature.Name;
            definition.rarity = signature.Spec.Rarity;
            definition.hint = signature.Hint;
            definition.description = signature.Description;
            definition.discoveryReward = signature.Spec.DiscoveryReward;
            definition.shape = (CreatureShape)Mathf.Clamp(signature.Shape, 0, Enum.GetValues(typeof(CreatureShape)).Length - 1);
            definition.primaryColor = Color.HSVToRGB(signature.Hue, 0.55f, 0.95f);
            definition.secondaryColor = Color.HSVToRGB(Mathf.Repeat(signature.Hue + 0.45f, 1f), 0.45f, 1f);
            definition.size = signature.Size;
            definition.moveSpeed = 0.6f;
            definition.wanderRadius = 2;
            return definition;
        }

        /// <summary>Codex progress over every planet: species found anywhere out of all species on all planets.</summary>
        private (int found, int total) OverallProgress()
        {
            var all = new HashSet<string>();
            var found = new HashSet<string>();
            for (int i = 0; i < game.planets.Count; i++)
            {
                var planetRecipe = RecipeFor(i);
                foreach (var id in planetRecipe.SpeciesIds)
                {
                    all.Add(id);
                    bool discovered = i == activeIndex ? simulation.IsDiscovered(id) : game.planets[i].discovered.Contains(id);
                    if (discovered) found.Add(id);
                }
            }
            return (found.Count, all.Count);
        }

        private List<PlanetEntry> BuildPlanetEntries()
        {
            var entries = new List<PlanetEntry>();
            for (int i = 0; i < game.planets.Count; i++)
            {
                var planetRecipe = RecipeFor(i);
                var data = game.planets[i];
                int found = 0, total = 0;
                foreach (var id in planetRecipe.SpeciesIds)
                {
                    total++;
                    if (i == activeIndex ? simulation.IsDiscovered(id) : data.discovered.Contains(id)) found++;
                }
                var planetLook = PlanetLook.From(planetRecipe, config);
                entries.Add(new PlanetEntry
                {
                    Index = i,
                    Name = planetRecipe.Name,
                    Traits = Strings.PlanetTraits(planetRecipe),
                    Level = i == activeIndex ? simulation.Level : data.planetLevel,
                    Found = found,
                    Total = total,
                    Ground = planetLook.Shift(biomeDefinitions.Count > 0 ? biomeDefinitions[0].groundColor : config.planetColor),
                    Sky = planetLook.Palette.skyCentre,
                    IsHere = i == activeIndex,
                    Visited = data.started,
                });
            }
            return entries;
        }

        /// <summary>Whether another planet can be discovered from here, and what it costs.</summary>
        private (bool possible, bool levelOk, int cost) CanDiscoverPlanet()
        {
            int next = game.planets.Count - 1;
            var costs = config.newPlanetCosts ?? Array.Empty<int>();
            if (next >= costs.Length) return (false, false, 0);
            int cost = costs[next];
            bool levelOk = simulation.Level >= config.newPlanetLevel;
            return (levelOk && simulation.Energy >= cost, levelOk, cost);
        }

        private void DiscoverPlanet()
        {
            var (possible, _, cost) = CanDiscoverPlanet();
            if (!possible) return;
            simulation.AddEnergy(-cost);
            AddPlanetAndVisit(NewSeed());
        }

        private void AddPlanetAndVisit(int seed)
        {
            int index = game.planets.Count;
            game.planets.Add(new SaveData { seed = seed, started = false });
            game.planets[index].cellCount = RecipeFor(index).CellCount;
            VisitPlanet(index);
        }

        private void VisitPlanet(int index)
        {
            if (index < 0 || index >= game.planets.Count) return;
            Save();
            game.activePlanet = index;
            SaveStore.Write(game);
            savingEnabled = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Development builds: replace this planet with a brand new one from a random seed.
        private void RerollPlanet()
        {
            int seed = NewSeed();
            game.planets[activeIndex] = new SaveData { seed = seed, started = false };
            recipes.Remove(activeIndex);
            game.planets[activeIndex].cellCount = RecipeFor(activeIndex).CellCount;
            SaveStore.Write(game);
            savingEnabled = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static int NewSeed() => new System.Random().Next(1, int.MaxValue);

        private List<CodexEntry> BuildCodexEntries()
        {
            var entries = new List<CodexEntry>();
            foreach (var plant in planetPlants)
            {
                entries.Add(new CodexEntry
                {
                    Id = plant.id, Name = plant.displayName, Hint = plant.hint, Description = plant.description,
                    Where = WhereItGrows(plant),
                });
            }
            foreach (var creature in planetCreatures)
            {
                entries.Add(new CodexEntry
                {
                    Id = creature.id, Name = creature.displayName, Hint = creature.hint, Description = creature.description,
                    IsCreature = true, Rarity = creature.rarity,
                });
            }
            return entries;
        }

        private Texture IconFor(string id, bool silhouette)
        {
            if (silhouette)
            {
                if (silhouettes.TryGetValue(id, out var shape)) return shape;
                if (!(IconFor(id, false) is RenderTexture full)) return null;
                shape = IconRenderer.Silhouette(full);
                silhouettes[id] = shape;
                return shape;
            }

            if (icons.TryGetValue(id, out var icon)) return icon;

            GameObject model = null;
            if (definitions.TryGetValue(id, out var plant))
            {
                model = new GameObject(plant.displayName);
                var view = model.AddComponent<PlantView>();
                view.Build(plant, config, materials);
                view.Show(PlantStage.Mature, animate: false);
            }
            else if (creatureDefinitions.TryGetValue(id, out var creature))
            {
                model = new GameObject(creature.displayName);
                var view = model.AddComponent<CreatureView>();
                view.Build(creature, materials);
                view.PoseForIcon();
            }
            if (model == null) return null;

            icon = IconRenderer.Render(model, gameCamera.transform.rotation);
            icons[id] = icon;
            return icon;
        }

        private void DescribePlant(PlantInstance plant)
        {
            long now = simulation.NowUtcTicks;
            string name = DisplayName(plant.Spec.Id);
            if (plant.StageAt(now) == PlantStage.Mature)
            {
                hud.ShowToast(string.Format(Strings.MatureFormat, name, plant.Spec.EnergyPerMinute.ToString("0.#")));
            }
            else
            {
                double left = plant.SecondsUntilMature(now);
                hud.ShowToast(string.Format(Strings.GrowingFormat, name, Strings.Duration(left)));
            }
        }

        private string DisplayName(string plantId) =>
            definitions.TryGetValue(plantId, out var definition) ? definition.displayName : plantId;

        private void SkipTime(double seconds)
        {
            clock.OffsetSeconds += seconds;
            simulation.Tick();
        }

        private void ResetProgress()
        {
            savingEnabled = false;
            SaveStore.Delete();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Save()
        {
            autosaveTimer = 0f;
            if (!savingEnabled || simulation == null) return;

            game.planets[activeIndex] = simulation.ToSave();
            game.debugClockOffsetSeconds = clock.OffsetSeconds;
            SaveStore.Write(game);
        }
    }
}
