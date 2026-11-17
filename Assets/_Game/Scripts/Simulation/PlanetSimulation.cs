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
    }

    public sealed class EconomySettings
    {
        public double StartingEnergy = 20;
        public double MaxOfflineHours = 24;
    }

    /// <summary>
    /// The game state and rules for one planet: what is planted where, how it grows, and the energy it earns.
    /// Plain C#; presentation listens to the events and never changes state directly.
    /// </summary>
    public sealed class PlanetSimulation
    {
        private readonly IClock clock;
        private readonly EconomySettings economy;
        private readonly Dictionary<string, PlantSpec> specs = new Dictionary<string, PlantSpec>();
        private readonly PlantInstance[] occupants;
        private readonly List<PlantInstance> plants = new List<PlantInstance>();
        private long lastAccrualUtcTicks;

        public PlanetSimulation(Planet planet, IEnumerable<PlantSpec> plantSpecs, EconomySettings economy, IClock clock, SaveData save = null)
        {
            Planet = planet ?? throw new ArgumentNullException(nameof(planet));
            this.economy = economy ?? new EconomySettings();
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

            foreach (var spec in plantSpecs)
                specs[spec.Id] = spec;

            occupants = new PlantInstance[planet.Grid.CellCount];
            long now = clock.UtcNow.Ticks;

            if (save == null)
            {
                Energy = this.economy.StartingEnergy;
                lastAccrualUtcTicks = now;
                return;
            }

            Energy = save.energy;
            lastAccrualUtcTicks = save.lastAccrualUtcTicks;
            foreach (var saved in save.plants)
            {
                // Tolerate saves that mention content or cells that no longer exist.
                if (!planet.Grid.IsValid(saved.cell) || occupants[saved.cell] != null) continue;
                if (!specs.TryGetValue(saved.specId ?? string.Empty, out var spec)) continue;

                var plant = new PlantInstance(spec, saved.cell, saved.plantedAtUtcTicks);
                plant.ReportedStage = plant.StageAt(now);
                occupants[saved.cell] = plant;
                plants.Add(plant);
            }

            AccrueEnergy(now);
        }

        public Planet Planet { get; }
        public double Energy { get; private set; }
        public IReadOnlyList<PlantInstance> Plants => plants;
        public long NowUtcTicks => clock.UtcNow.Ticks;

        public event Action<PlantInstance> PlantPlanted;
        public event Action<PlantInstance> PlantRemoved;
        public event Action<PlantInstance, PlantStage> PlantStageChanged;
        public event Action<double> EnergyChanged;

        public PlantInstance PlantAt(int cell) => Planet.Grid.IsValid(cell) ? occupants[cell] : null;

        public bool TryGetSpec(string id, out PlantSpec spec) => specs.TryGetValue(id ?? string.Empty, out spec);

        public PlantResult TryPlant(int cell, string specId)
        {
            if (!Planet.Grid.IsValid(cell)) return PlantResult.InvalidCell;
            if (!TryGetSpec(specId, out var spec)) return PlantResult.UnknownPlant;
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

            for (int i = 0; i < plants.Count; i++)
            {
                var plant = plants[i];
                var stage = plant.StageAt(now);
                if (stage == plant.ReportedStage) continue;
                plant.ReportedStage = stage;
                PlantStageChanged?.Invoke(plant, stage);
            }
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
            return data;
        }

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
