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

            var economy = new EconomySettings
            {
                StartingEnergy = config.startingEnergy,
                MaxOfflineHours = config.maxOfflineHours,
            };
            simulation = new PlanetSimulation(planet, specs, economy, clock, save);

            var materials = new MaterialLibrary(config.litMaterial, config.unlitMaterial);

            planetView = new GameObject("Planet").AddComponent<PlanetView>();
            planetView.Init(simulation, config, materials);

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
