using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyPlanet.Data;
using TinyPlanet.Simulation;
using UnityEditor;
using UnityEngine;

namespace TinyPlanet.EditorTools
{
    /// <summary>
    /// Plays the home planet with a simple stand-in player and prints when levels, unlocks and discoveries
    /// happen, so the numbers in the content assets can be tuned against the "about an hour" target.
    /// The stand-in does not know any creature recipe: it buys what it can afford, plants a mix of everything
    /// it has next to what is already growing, and lets the creatures find it.
    /// </summary>
    public static class BalanceReport
    {
        private const double StepSeconds = 2;
        private const double ActEverySeconds = 10;
        private const int PlantsPerAction = 4;

        [MenuItem("Tiny Planet/Balance Report")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run(int runs = 3, double minutes = 90)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameConfig")[0]));
            var pool = new ContentPool();
            foreach (var biome in config.biomes) pool.Biomes.Add(biome.ToSpec());
            foreach (var plant in config.plants) pool.Plants.Add(plant.ToSpec());
            foreach (var creature in config.creatures) pool.Creatures.Add(creature.ToSpec());
            var recipe = PlanetRecipes.Create(pool, config.defaultSeed, 0, config.cellCount);

            var report = new StringBuilder();
            report.Append("Balance report: ").Append(runs).Append(" runs of ").Append(minutes).Append(" min, ")
                .Append(recipe.Plants.Count).Append(" plants, ").Append(recipe.Creatures.Count).AppendLine(" creatures");
            for (int run = 0; run < runs; run++)
                report.AppendLine(Play(config, recipe, run, minutes));
            return report.ToString();
        }

        private static string Play(GameConfig config, PlanetRecipe recipe, int run, double minutes)
        {
            var clock = new StepClock();
            var planet = PlanetGenerator.Generate(config.defaultSeed, recipe.CellCount, recipe.Biomes);
            var economy = new EconomySettings
            {
                StartingEnergy = config.startingEnergy,
                MaxOfflineHours = config.maxOfflineHours,
                MaxCatchUpChecks = config.maxCatchUpChecks,
                LevelLifeThresholds = config.levelLifeThresholds,
            };
            var sim = new PlanetSimulation(planet, recipe.Plants, recipe.Creatures, economy, clock, null, new System.Random(run + 1));
            var random = new System.Random(run * 7 + 3);
            var start = clock.UtcNow;

            var events = new List<string>();
            string Stamp() => (clock.UtcNow - start).TotalMinutes.ToString("0.0");
            sim.LevelChanged += level => events.Add(Stamp() + " Lv" + level);
            sim.BiomeUnlocked += id => events.Add(Stamp() + " +" + id.ToUpperInvariant());
            sim.PlantUnlocked += id => events.Add(Stamp() + " +" + id);
            sim.SpeciesDiscovered += (id, _) =>
            {
                if (sim.TryGetCreatureSpec(id, out var spec)) events.Add(Stamp() + " *" + id + "(" + spec.Rarity.ToString()[0] + ")");
            };

            var energyLog = new StringBuilder();
            double nextAct = 0;
            double nextLog = 10 * 60;
            for (double t = 0; t <= minutes * 60; t += StepSeconds)
            {
                clock.UtcNow = start.AddSeconds(t);
                sim.Tick();
                if (t >= nextAct)
                {
                    Act(sim, random);
                    nextAct += ActEverySeconds;
                }
                if (t >= nextLog)
                {
                    energyLog.Append(" ").Append((int)(t / 60)).Append("m:").Append(Math.Round(sim.Energy))
                        .Append("e/").Append(sim.Plants.Count).Append("p/").Append(Math.Round(Income(sim))).Append("pm");
                    nextLog += 10 * 60;
                }
            }

            int found = recipe.Creatures.Count(c => sim.IsDiscovered(c.Id));
            var missing = recipe.Creatures.Where(c => !sim.IsDiscovered(c.Id)).Select(c => c.Id);
            return "Run " + run + ": " + string.Join(", ", events)
                + "\n   found " + found + "/" + recipe.Creatures.Count + " missing [" + string.Join(" ", missing) + "]"
                + "\n   energy/plants/income:" + energyLog;
        }

        private static double Income(PlanetSimulation sim)
        {
            long now = sim.NowUtcTicks;
            return sim.Plants.Where(p => p.MatureAtUtcTicks <= now).Sum(p => (double)p.Spec.EnergyPerMinute);
        }

        // One visit from the player: buy the cheapest unlock in reach, then plant a few seeds.
        private static void Act(PlanetSimulation sim, System.Random random)
        {
            var unlocks = new List<(int cost, Func<UnlockResult> buy)>();
            foreach (var biome in sim.Planet.Biomes)
                if (!sim.IsBiomeUnlocked(biome.Id) && sim.Level >= biome.UnlockLevel)
                    unlocks.Add((biome.UnlockCost, () => sim.TryUnlockBiome(biome.Id)));
            foreach (var plant in sim.PlantSpecs)
                if (!sim.IsPlantUnlocked(plant.Id) && sim.Level >= plant.UnlockLevel && GrowsOnPlanet(sim, plant.Id))
                    unlocks.Add((plant.UnlockCost, () => sim.TryUnlockPlant(plant.Id)));
            unlocks.Sort((a, b) => a.cost.CompareTo(b.cost));

            double saving = 0;
            foreach (var unlock in unlocks)
            {
                if (unlock.buy() == UnlockResult.Ok) continue;
                saving = unlock.cost; // the next thing worth saving for
                break;
            }

            // While saving, keep most of the energy; a player still plants a little.
            double budget = saving > 0 ? Math.Max(0, sim.Energy - saving) + sim.Energy * 0.25 : sim.Energy;
            var available = sim.PlantSpecs.Where(p => sim.IsPlantUnlocked(p.Id)).ToList();
            for (int i = 0; i < PlantsPerAction; i++)
            {
                // Seeds that take days are planted a couple at a time, as a player would.
                var choices = available.Where(p => p.SeedCost <= budget && CanGrowSomewhere(sim, p.Id, needAwake: true)
                    && (p.SecondsToMature < 3600 || sim.CountPlants(p.Id) < 2)).ToList();
                if (choices.Count == 0)
                {
                    // A full garden: make room for a newer seed by pulling up the plant that earns least.
                    if (!MakeRoom(sim, available.Where(p => p.SeedCost <= budget))) break;
                    continue;
                }
                // Favour what there is least of, so the garden stays mixed.
                var spec = choices.OrderBy(p => sim.CountPlants(p.Id) * (0.7 + random.NextDouble() * 0.6)).First();
                int cell = PickCell(sim, spec.Id, random);
                if (cell < 0 || sim.TryPlant(cell, spec.Id) != PlantResult.Ok) break;
                budget -= spec.SeedCost;
            }
        }

        private static bool GrowsOnPlanet(PlanetSimulation sim, string plantId)
        {
            var planet = sim.Planet;
            for (int cell = 0; cell < planet.Cells.Length; cell++)
                if (!planet.IsWater(cell) && sim.IsAwake(cell) && planet.BiomeOf(cell).Allows(plantId)) return true;
            return false;
        }

        // Only by removing something that earns less: a full garden slowly trades up.
        private static bool MakeRoom(PlanetSimulation sim, IEnumerable<PlantSpec> wanted)
        {
            foreach (var spec in wanted.Where(p => p.SecondsToMature < 3600).OrderByDescending(p => p.EnergyPerMinute))
            {
                var weakest = sim.Plants
                    .Where(p => p.Spec.EnergyPerMinute < spec.EnergyPerMinute && sim.Planet.BiomeOf(p.Cell).Allows(spec.Id))
                    .OrderBy(p => p.Spec.EnergyPerMinute).FirstOrDefault();
                if (weakest != null) return sim.TryRemove(weakest.Cell, out _);
            }
            return false;
        }

        private static bool CanGrowSomewhere(PlanetSimulation sim, string plantId, bool needAwake)
        {
            var planet = sim.Planet;
            for (int cell = 0; cell < planet.Cells.Length; cell++)
            {
                if (planet.IsWater(cell) || sim.PlantAt(cell) != null || !planet.BiomeOf(cell).Allows(plantId)) continue;
                if (!needAwake || sim.IsAwake(cell)) return true;
            }
            return false;
        }

        // Mostly next to something already growing (a player builds out from the garden), sometimes anywhere.
        private static int PickCell(PlanetSimulation sim, string plantId, System.Random random)
        {
            var planet = sim.Planet;
            var free = new List<int>();
            var nextToGarden = new List<int>();
            for (int cell = 0; cell < planet.Cells.Length; cell++)
            {
                if (sim.CanPlant(cell, plantId) != PlantResult.Ok) continue;
                free.Add(cell);
                if (planet.Grid.Neighbours(cell).Any(n => sim.PlantAt(n) != null)) nextToGarden.Add(cell);
            }
            if (free.Count == 0) return -1;
            var from = nextToGarden.Count > 0 && random.NextDouble() < 0.75 ? nextToGarden : free;
            return from[random.Next(from.Count)];
        }

        private sealed class StepClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        }
    }
}
