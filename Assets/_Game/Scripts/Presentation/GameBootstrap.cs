using System;
using System.Collections.Generic;
using TinyPlanet.Data;
using TinyPlanet.GameInput;
using TinyPlanet.Simulation;
using TinyPlanet.UI;
using UnityEngine;
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
        private const float AutosaveInterval = 30f;

        [SerializeField] private GameConfig config;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Light keyLight;

        private readonly Dictionary<string, PlantDefinition> definitions = new Dictionary<string, PlantDefinition>();
        private readonly Dictionary<string, CreatureDefinition> creatureDefinitions = new Dictionary<string, CreatureDefinition>();
        private readonly Dictionary<string, Texture> icons = new Dictionary<string, Texture>();
        private readonly Dictionary<string, Texture> silhouettes = new Dictionary<string, Texture>();
        private MaterialLibrary materials;
        private CreatureLayer creatureLayer;
        private CodexController codex;
        private OffsetClock clock;
        private PlanetSimulation simulation;
        private PlanetView planetView;
        private OrbitCamera orbit;
        private HudController hud;
        private float tickTimer;
        private float autosaveTimer;
        private bool savingEnabled;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            var save = SaveStore.Load();
            clock = new OffsetClock(new SystemClock(), save != null ? save.debugClockOffsetSeconds : 0);

            // A saved planet keeps its own seed and cell count, so plant positions stay valid if the config changes.
            int seed = save != null ? save.seed : config.defaultSeed;
            int cellCount = save != null && save.cellCount > PlanetGrid.NeighbourCount ? save.cellCount : config.cellCount;
            var planet = PlanetGenerator.Generate(seed, cellCount);

            var specs = new List<PlantSpec>();
            foreach (var plant in config.plants)
            {
                definitions[plant.id] = plant;
                specs.Add(plant.ToSpec());
            }

            var creatureSpecs = new List<CreatureSpec>();
            foreach (var creature in config.creatures ?? Array.Empty<CreatureDefinition>())
            {
                if (creature == null) continue;
                creatureDefinitions[creature.id] = creature;
                creatureSpecs.Add(creature.ToSpec());
            }

            var economy = new EconomySettings
            {
                StartingEnergy = config.startingEnergy,
                MaxOfflineHours = config.maxOfflineHours,
                MaxCatchUpChecks = config.maxCatchUpChecks,
            };
            simulation = new PlanetSimulation(planet, specs, creatureSpecs, economy, clock, save);

            materials = new MaterialLibrary(config.litMaterial, config.unlitMaterial);

            planetView = new GameObject("Planet").AddComponent<PlanetView>();
            planetView.Init(simulation, config, materials);

            creatureLayer = new GameObject("Creatures").AddComponent<CreatureLayer>();
            creatureLayer.transform.SetParent(planetView.transform, false);
            creatureLayer.Init(simulation, config, materials);

            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = config.spaceColor;
            orbit = gameCamera.gameObject.AddComponent<OrbitCamera>();
            orbit.Init(gameCamera, config.planetRadius);

            if (keyLight != null)
            {
                // The light travels with the camera, so the side the player looks at is always lit.
                keyLight.transform.SetParent(gameCamera.transform, false);
                keyLight.transform.localPosition = Vector3.zero;
                keyLight.transform.localRotation = Quaternion.Euler(25f, -25f, 0f);
            }

            hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Init(simulation, config, showDebug: Debug.isDebugBuild);
            hud.DebugAddEnergy += () => simulation.AddEnergy(100);
            hud.DebugSkipTime += SkipTime;
            hud.DebugReset += ResetProgress;

            codex = hud.gameObject.AddComponent<CodexController>();
            codex.Init(hud.SafeArea, simulation, BuildCodexEntries(), IconFor);
            hud.CodexClicked += codex.Open;
            UpdateCodexProgress();
            simulation.SpeciesDiscovered += OnSpeciesDiscovered;
            simulation.CreatureArrived += _ => codex.Refresh();
            simulation.CreatureLeft += _ => codex.Refresh();

            var input = gameObject.AddComponent<InputReader>();
            input.Dragged += orbit.Drag;
            input.DragEnded += orbit.EndDrag;
            input.Zoomed += orbit.Zoom;
            input.Tapped += OnTapped;

            savingEnabled = true;

            if (save != null)
            {
                long gained = (long)Math.Floor(simulation.Energy - save.energy);
                if (gained >= 1) hud.ShowToast(string.Format(Strings.WelcomeBackFormat, gained.ToString("N0")), 3f);
            }
        }

        private void Update()
        {
            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer = 0f;
                simulation.Tick();
            }

            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveInterval) Save();
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

        private void OnTapped(Vector2 screenPosition)
        {
            // Creatures first: they are small and stand on top of cells the player might otherwise plant on.
            float pickRadius = 45f * Mathf.Max(1f, Screen.dpi / 160f);
            if (creatureLayer.TryPick(gameCamera, screenPosition, pickRadius, out var creature))
            {
                creature.React();
                var definition = creature.Definition;
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
                    hud.ShowToast(string.Format(Strings.RemovedFormat, name, refund));
                return;
            }

            if (existing != null)
            {
                DescribePlant(existing);
                return;
            }

            var result = simulation.TryPlant(cell, hud.SelectedPlantId);
            if (result == PlantResult.NotEnoughEnergy) hud.ShowToast(Strings.NotEnoughEnergy);
            else if (result == PlantResult.Water) hud.ShowToast(Strings.WaterHere);
        }

        private void OnSpeciesDiscovered(string id, int reward)
        {
            if (creatureDefinitions.TryGetValue(id, out var creature))
            {
                string heading = creature.rarity >= Rarity.Rare
                    ? string.Format(Strings.NewRareCreatureFormat, Strings.Rarity(creature.rarity).ToLowerInvariant())
                    : Strings.NewCreature;
                hud.ShowDiscovery(heading, creature.displayName, reward, CodexController.RarityColor(creature.rarity));
            }
            else if (definitions.TryGetValue(id, out var plant))
            {
                hud.ShowDiscovery(Strings.NewPlant, plant.displayName, reward, plant.primaryColor);
            }
            UpdateCodexProgress();
            codex.Refresh();
        }

        private void UpdateCodexProgress()
        {
            var (found, total) = codex.Progress();
            hud.SetCodexProgress(found, total);
        }

        private List<CodexEntry> BuildCodexEntries()
        {
            var entries = new List<CodexEntry>();
            foreach (var plant in config.plants)
            {
                entries.Add(new CodexEntry
                {
                    Id = plant.id, Name = plant.displayName, Hint = plant.hint, Description = plant.description,
                });
            }
            foreach (var creature in config.creatures ?? Array.Empty<CreatureDefinition>())
            {
                if (creature == null) continue;
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
                double left = Growth.SecondsUntilMature(plant.Spec, plant.PlantedAtUtcTicks, now);
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

            var data = simulation.ToSave();
            data.debugClockOffsetSeconds = clock.OffsetSeconds;
            SaveStore.Write(data);
        }
    }
}
