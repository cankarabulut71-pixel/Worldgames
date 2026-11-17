using System;
using System.Collections.Generic;

namespace TinyPlanet.Simulation
{
    public enum PlantResult
    {
        Ok,
        InvalidCell,
        CellOccupied,
        UnknownPlant,
        NotEnoughEnergy,
        Water,
    }

    public sealed class EconomySettings
    {
        public double StartingEnergy = 20;
        public double MaxOfflineHours = 24;

        /// <summary>How many missed spawn checks per species are replayed after time away.</summary>
        public int MaxCatchUpChecks = 30;
    }

    /// <summary>
    /// The game state and rules for one planet: what is planted where, how it grows, the energy it earns,
    /// which creatures it attracts and what the player has discovered.
    /// Plain C#; presentation listens to the events and never changes state directly.
    /// </summary>
    public sealed class PlanetSimulation
    {
        private readonly IClock clock;
        private readonly EconomySettings economy;
        private readonly Random random;
        private readonly Dictionary<string, PlantSpec> specs = new Dictionary<string, PlantSpec>();
        private readonly List<CreatureSpec> creatureSpecs = new List<CreatureSpec>();
        private readonly Dictionary<string, long> nextCheckUtcTicks = new Dictionary<string, long>();
        private readonly PlantInstance[] occupants;
        private readonly List<PlantInstance> plants = new List<PlantInstance>();
        private readonly List<CreatureInstance> creatures = new List<CreatureInstance>();
        private readonly HashSet<string> discovered = new HashSet<string>();
        private readonly List<int> homeCandidates = new List<int>();
        private long lastAccrualUtcTicks;
        private long evaluationUtcTicks;
        private int nextCreatureId = 1;

        public PlanetSimulation(
            Planet planet,
            IEnumerable<PlantSpec> plantSpecs,
            IEnumerable<CreatureSpec> creatureSpecList,
            EconomySettings economy,
            IClock clock,
            SaveData save = null,
            Random random = null)
        {
            Planet = planet ?? throw new ArgumentNullException(nameof(planet));
            this.economy = economy ?? new EconomySettings();
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? new Random(unchecked(planet.Seed * 31 + Environment.TickCount));

            foreach (var spec in plantSpecs)
                specs[spec.Id] = spec;
            if (creatureSpecList != null)
                creatureSpecs.AddRange(creatureSpecList);

            occupants = new PlantInstance[planet.Grid.CellCount];
            long now = clock.UtcNow.Ticks;
            evaluationUtcTicks = now;

            if (save == null)
            {
                Energy = this.economy.StartingEnergy;
                lastAccrualUtcTicks = now;
                ScheduleChecksFrom(now, now);
                return;
            }

            Energy = save.energy;
            lastAccrualUtcTicks = save.lastAccrualUtcTicks;
            nextCreatureId = Math.Max(1, save.nextCreatureId);

            foreach (var saved in save.plants)
            {
                // Tolerate saves that mention content or cells that no longer exist.
                if (!planet.Grid.IsValid(saved.cell) || occupants[saved.cell] != null || planet.IsWater(saved.cell)) continue;
                if (!specs.TryGetValue(saved.specId ?? string.Empty, out var spec)) continue;

                var plant = new PlantInstance(spec, saved.cell, saved.plantedAtUtcTicks);
                plant.ReportedStage = plant.StageAt(now);
                occupants[saved.cell] = plant;
                plants.Add(plant);
            }

            foreach (var saved in save.creatures)
            {
                if (!planet.Grid.IsValid(saved.homeCell) || !TryGetCreatureSpec(saved.specId, out var spec)) continue;
                var creature = new CreatureInstance(saved.id, spec, saved.homeCell, saved.arrivedAtUtcTicks)
                {
                    UnhappySinceUtcTicks = saved.unhappySinceUtcTicks,
                };
                creatures.Add(creature);
                nextCreatureId = Math.Max(nextCreatureId, saved.id + 1);
            }

            foreach (var id in save.discovered)
            {
                if (!string.IsNullOrEmpty(id)) discovered.Add(id);
            }

            AccrueEnergy(now);

            // Spawn checks missed while the app was closed are replayed (up to a cap) on the first tick.
            ScheduleChecksFrom(Math.Min(save.savedAtUtcTicks > 0 ? save.savedAtUtcTicks : now, now), now);
        }

        public Planet Planet { get; }
        public double Energy { get; private set; }
        public IReadOnlyList<PlantInstance> Plants => plants;
        public IReadOnlyList<CreatureInstance> Creatures => creatures;
        public IReadOnlyList<CreatureSpec> CreatureSpecs => creatureSpecs;
        public long NowUtcTicks => clock.UtcNow.Ticks;

        public event Action<PlantInstance> PlantPlanted;
        public event Action<PlantInstance> PlantRemoved;
        public event Action<PlantInstance, PlantStage> PlantStageChanged;
        public event Action<double> EnergyChanged;
        public event Action<CreatureInstance> CreatureArrived;
        public event Action<CreatureInstance> CreatureMoved;
        public event Action<CreatureInstance> CreatureLeft;

        /// <summary>A plant or creature species seen for the first time, and the energy it awarded.</summary>
        public event Action<string, int> SpeciesDiscovered;

        public PlantInstance PlantAt(int cell) => Planet.Grid.IsValid(cell) ? occupants[cell] : null;

        public bool TryGetSpec(string id, out PlantSpec spec) => specs.TryGetValue(id ?? string.Empty, out spec);

        public bool TryGetCreatureSpec(string id, out CreatureSpec spec)
        {
            foreach (var candidate in creatureSpecs)
            {
                if (candidate.Id == id)
                {
                    spec = candidate;
                    return true;
                }
            }
            spec = null;
            return false;
        }

        public bool IsDiscovered(string speciesId) => discovered.Contains(speciesId ?? string.Empty);

        public int CountCreatures(string specId)
        {
            int count = 0;
            foreach (var creature in creatures)
            {
                if (creature.Spec.Id == specId) count++;
            }
            return count;
        }

        public int CountPlants(string specId)
        {
            int count = 0;
            foreach (var plant in plants)
            {
                if (plant.Spec.Id == specId) count++;
            }
            return count;
        }

        public PlantResult TryPlant(int cell, string specId)
        {
            if (!Planet.Grid.IsValid(cell)) return PlantResult.InvalidCell;
            if (!TryGetSpec(specId, out var spec)) return PlantResult.UnknownPlant;
            if (Planet.IsWater(cell)) return PlantResult.Water;
            if (occupants[cell] != null) return PlantResult.CellOccupied;
            if (Energy < spec.SeedCost) return PlantResult.NotEnoughEnergy;

            SetEnergy(Energy - spec.SeedCost);

            var plant = new PlantInstance(spec, cell, clock.UtcNow.Ticks) { ReportedStage = PlantStage.Seed };
            occupants[cell] = plant;
            plants.Add(plant);
            PlantPlanted?.Invoke(plant);
            return PlantResult.Ok;
        }

        /// <summary>Removes the plant in a cell and refunds part of its seed cost.</summary>
        public bool TryRemove(int cell, out int refund)
        {
            refund = 0;
            var plant = PlantAt(cell);
            if (plant == null) return false;

            // Bank what it earned up to now before it disappears.
            AccrueEnergy(clock.UtcNow.Ticks);

            occupants[cell] = null;
            plants.Remove(plant);
            refund = (int)Math.Floor(plant.Spec.SeedCost * (double)plant.Spec.RefundFraction);
            if (refund > 0) SetEnergy(Energy + refund);
            PlantRemoved?.Invoke(plant);
            return true;
        }

        public void AddEnergy(double amount)
        {
            if (amount != 0) SetEnergy(Math.Max(0, Energy + amount));
        }

        /// <summary>Advances the simulation to the current time of the clock. Safe to call at any interval.</summary>
        public void Tick()
        {
            long now = clock.UtcNow.Ticks;
            AccrueEnergy(now);
            evaluationUtcTicks = now;

            for (int i = 0; i < plants.Count; i++)
            {
                var plant = plants[i];
                var stage = plant.StageAt(now);
                if (stage != plant.ReportedStage)
                {
                    plant.ReportedStage = stage;
                    PlantStageChanged?.Invoke(plant, stage);
                }
                if (stage == PlantStage.Mature) Discover(plant.Spec.Id, plant.Spec.DiscoveryReward);
            }

            UpdateCreatures(now);
        }

        public SaveData ToSave()
        {
            long now = clock.UtcNow.Ticks;
            AccrueEnergy(now);

            var data = new SaveData
            {
                seed = Planet.Seed,
                cellCount = Planet.Grid.CellCount,
                energy = Energy,
                lastAccrualUtcTicks = lastAccrualUtcTicks,
                savedAtUtcTicks = now,
                nextCreatureId = nextCreatureId,
            };
            foreach (var plant in plants)
            {
                data.plants.Add(new PlantSave
                {
                    cell = plant.Cell,
                    specId = plant.Spec.Id,
                    plantedAtUtcTicks = plant.PlantedAtUtcTicks,
                });
            }
            foreach (var creature in creatures)
            {
                data.creatures.Add(new CreatureSave
                {
                    id = creature.Id,
                    specId = creature.Spec.Id,
                    homeCell = creature.HomeCell,
                    arrivedAtUtcTicks = creature.ArrivedAtUtcTicks,
                    unhappySinceUtcTicks = creature.UnhappySinceUtcTicks,
                });
            }
            data.discovered.AddRange(discovered);
            data.discovered.Sort(StringComparer.Ordinal);
            return data;
        }

        /// <summary>True when the cell holds a mature plant of the given species. Used by spawn conditions.</summary>
        internal bool IsMatureAt(int cell, string plantId)
        {
            var plant = occupants[cell];
            return plant != null && plant.Spec.Id == plantId && plant.MatureAtUtcTicks <= evaluationUtcTicks;
        }

        // ---------- creatures ----------

        private void ScheduleChecksFrom(long fromUtcTicks, long now)
        {
            foreach (var spec in creatureSpecs)
                nextCheckUtcTicks[spec.Id] = fromUtcTicks + IntervalTicks(spec);
        }

        private static long IntervalTicks(CreatureSpec spec) =>
            (long)(Math.Max(1.0, spec.CheckIntervalSeconds) * TimeSpan.TicksPerSecond);

        // Each species is checked on its own interval. Checks missed while away are replayed, up to a cap,
        // so a garden left overnight still has visitors in the morning.
        private void UpdateCreatures(long now)
        {
            foreach (var spec in creatureSpecs)
            {
                long interval = IntervalTicks(spec);
                if (!nextCheckUtcTicks.TryGetValue(spec.Id, out long next)) next = now + interval;
                if (next - now > interval) next = now + interval; // the device clock moved backwards

                int checks = 0;
                while (next <= now && checks < economy.MaxCatchUpChecks)
                {
                    RunCheck(spec, next);
                    next += interval;
                    checks++;
                }
                if (next <= now) next = now + interval;
                nextCheckUtcTicks[spec.Id] = next;
            }
        }

        private void RunCheck(CreatureSpec spec, long checkUtcTicks)
        {
            UpdateResidents(spec, checkUtcTicks);

            if (CountCreatures(spec.Id) >= spec.PopulationCap) return;
            if (random.NextDouble() >= spec.ChancePerCheck) return;

            FindHomes(spec, homeCandidates);
            if (homeCandidates.Count == 0) return;

            // Spread a species out: prefer a home that none of its kind already uses.
            int fresh = 0;
            for (int i = 0; i < homeCandidates.Count; i++)
            {
                if (!IsHomeOf(spec, homeCandidates[i])) homeCandidates[fresh++] = homeCandidates[i];
            }
            int choices = fresh > 0 ? fresh : homeCandidates.Count;
            int home = homeCandidates[random.Next(choices)];

            var creature = new CreatureInstance(nextCreatureId++, spec, home, checkUtcTicks);
            creatures.Add(creature);
            CreatureArrived?.Invoke(creature);
            Discover(spec.Id, spec.DiscoveryReward);
        }

        // Creatures whose home no longer suits them move to another suitable spot if there is one,
        // otherwise they leave once it has been unsuitable for long enough. They never die.
        private void UpdateResidents(CreatureSpec spec, long checkUtcTicks)
        {
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var creature = creatures[i];
                if (creature.Spec != spec) continue;

                if (Suits(spec, creature.HomeCell))
                {
                    creature.UnhappySinceUtcTicks = 0;
                    continue;
                }

                FindHomes(spec, homeCandidates);
                if (homeCandidates.Count > 0)
                {
                    creature.HomeCell = homeCandidates[random.Next(homeCandidates.Count)];
                    creature.UnhappySinceUtcTicks = 0;
                    CreatureMoved?.Invoke(creature);
                    continue;
                }

                if (creature.UnhappySinceUtcTicks == 0)
                {
                    creature.UnhappySinceUtcTicks = checkUtcTicks;
                }
                else if ((checkUtcTicks - creature.UnhappySinceUtcTicks) / (double)TimeSpan.TicksPerSecond >= spec.LeaveAfterSeconds)
                {
                    creatures.RemoveAt(i);
                    CreatureLeft?.Invoke(creature);
                }
            }
        }

        // Candidate homes are cells with a mature plant: habitats are always built from plants.
        private void FindHomes(CreatureSpec spec, List<int> result)
        {
            result.Clear();
            foreach (var plant in plants)
            {
                if (plant.MatureAtUtcTicks <= evaluationUtcTicks && Suits(spec, plant.Cell))
                    result.Add(plant.Cell);
            }
        }

        private bool Suits(CreatureSpec spec, int cell)
        {
            if (Planet.IsWater(cell)) return false;
            foreach (var condition in spec.Conditions)
            {
                if (!condition.IsMet(this, cell)) return false;
            }
            return true;
        }

        private bool IsHomeOf(CreatureSpec spec, int cell)
        {
            foreach (var creature in creatures)
            {
                if (creature.Spec == spec && creature.HomeCell == cell) return true;
            }
            return false;
        }

        private void Discover(string speciesId, int reward)
        {
            if (!discovered.Add(speciesId)) return;
            if (reward > 0) SetEnergy(Energy + reward);
            SpeciesDiscovered?.Invoke(speciesId, reward);
        }

        // ---------- energy ----------

        // Energy is earned by mature plants for the time between the last accrual and now,
        // so it works the same whether that gap was one frame or a night with the app closed.
        private void AccrueEnergy(long now)
        {
            if (now <= lastAccrualUtcTicks)
            {
                // The device clock moved backwards: earn nothing, and start counting from here.
                lastAccrualUtcTicks = now;
                return;
            }

            long maxGap = (long)(economy.MaxOfflineHours * TimeSpan.TicksPerHour);
            long from = Math.Max(lastAccrualUtcTicks, now - maxGap);

            double earned = 0;
            for (int i = 0; i < plants.Count; i++)
            {
                var plant = plants[i];
                long start = Math.Max(from, plant.MatureAtUtcTicks);
                if (now <= start) continue;
                double minutes = (now - start) / (double)TimeSpan.TicksPerMinute;
                earned += plant.Spec.EnergyPerMinute * minutes;
            }

            lastAccrualUtcTicks = now;
            if (earned > 0) SetEnergy(Energy + earned);
        }

        private void SetEnergy(double value)
        {
            Energy = value;
            EnergyChanged?.Invoke(Energy);
        }
    }
}
