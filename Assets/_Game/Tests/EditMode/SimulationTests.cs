using System;
using System.Linq;
using NUnit.Framework;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Tests
{
    public sealed class ManualClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public void Advance(double seconds) => UtcNow = UtcNow.AddSeconds(seconds);
    }

    public class SimulationTests
    {
        private const int Cells = 200;

        private static PlantSpec Grass() => new PlantSpec
        {
            Id = "grass",
            SeedCost = 2,
            SeedSeconds = 10,
            SproutSeconds = 20,
            EnergyPerMinute = 6,
            RefundFraction = 0.5f,
        };

        private static PlantSpec Tree() => new PlantSpec
        {
            Id = "tree",
            SeedCost = 30,
            SeedSeconds = 60,
            SproutSeconds = 120,
            EnergyPerMinute = 30,
            RefundFraction = 0.5f,
        };

        private static PlanetSimulation NewSim(ManualClock clock, double startingEnergy = 100, SaveData save = null)
        {
            var planet = PlanetGenerator.Generate(7, Cells);
            var economy = new EconomySettings { StartingEnergy = startingEnergy, MaxOfflineHours = 24 };
            return new PlanetSimulation(planet, new[] { Grass(), Tree() }, economy, clock, save);
        }

        [Test]
        public void Generator_SameSeed_GivesSamePlanet()
        {
            var a = PlanetGenerator.Generate(42, Cells);
            var b = PlanetGenerator.Generate(42, Cells);

            for (int i = 0; i < Cells; i++)
            {
                Assert.AreEqual(a.Grid.Direction(i), b.Grid.Direction(i));
                Assert.AreEqual(a.Cells[i].Moisture, b.Cells[i].Moisture);
                Assert.AreEqual(a.Cells[i].Fertility, b.Cells[i].Fertility);
            }
        }

        [Test]
        public void Generator_DifferentSeed_GivesDifferentPlanet()
        {
            var a = PlanetGenerator.Generate(1, Cells);
            var b = PlanetGenerator.Generate(2, Cells);

            bool anyDifferent = Enumerable.Range(0, Cells).Any(i => a.Cells[i].Moisture != b.Cells[i].Moisture);
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Grid_CellsAreOnUnitSphere_AndNeighboursAreValid()
        {
            var grid = new PlanetGrid(Cells);
            for (int i = 0; i < grid.CellCount; i++)
            {
                Assert.AreEqual(1f, grid.Direction(i).magnitude, 1e-4f);

                var neighbours = grid.Neighbours(i);
                Assert.AreEqual(PlanetGrid.NeighbourCount, neighbours.Length);
                Assert.IsFalse(neighbours.Contains(i));
                Assert.AreEqual(neighbours.Length, neighbours.Distinct().Count());
                foreach (int n in neighbours)
                    Assert.IsTrue(grid.IsValid(n));
            }
        }

        [Test]
        public void Grid_FindNearestCell_ReturnsTheCellItself()
        {
            var grid = new PlanetGrid(Cells);
            for (int i = 0; i < grid.CellCount; i += 7)
                Assert.AreEqual(i, grid.FindNearestCell(grid.Direction(i) * 3f));
        }

        [Test]
        public void Growth_StageFollowsElapsedTime()
        {
            var spec = Grass();
            long planted = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            long Seconds(double s) => planted + (long)(s * TimeSpan.TicksPerSecond);

            Assert.AreEqual(PlantStage.Seed, Growth.StageAt(spec, planted, Seconds(0)));
            Assert.AreEqual(PlantStage.Seed, Growth.StageAt(spec, planted, Seconds(9.9)));
            Assert.AreEqual(PlantStage.Sprout, Growth.StageAt(spec, planted, Seconds(10)));
            Assert.AreEqual(PlantStage.Sprout, Growth.StageAt(spec, planted, Seconds(29.9)));
            Assert.AreEqual(PlantStage.Mature, Growth.StageAt(spec, planted, Seconds(30)));
            Assert.AreEqual(PlantStage.Mature, Growth.StageAt(spec, planted, Seconds(100000)));
        }

        [Test]
        public void Plant_SpendsEnergy_AndOccupiesCell()
        {
            var sim = NewSim(new ManualClock());

            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(3, "grass"));
            Assert.AreEqual(98, sim.Energy, 1e-9);
            Assert.IsNotNull(sim.PlantAt(3));
            Assert.AreEqual(1, sim.Plants.Count);
        }

        [Test]
        public void Plant_FailsWithoutChangingState()
        {
            var sim = NewSim(new ManualClock(), startingEnergy: 10);
            sim.TryPlant(3, "grass");

            Assert.AreEqual(PlantResult.CellOccupied, sim.TryPlant(3, "grass"));
            Assert.AreEqual(PlantResult.NotEnoughEnergy, sim.TryPlant(4, "tree"));
            Assert.AreEqual(PlantResult.UnknownPlant, sim.TryPlant(4, "cactus"));
            Assert.AreEqual(PlantResult.InvalidCell, sim.TryPlant(-1, "grass"));
            Assert.AreEqual(PlantResult.InvalidCell, sim.TryPlant(Cells, "grass"));

            Assert.AreEqual(8, sim.Energy, 1e-9);
            Assert.AreEqual(1, sim.Plants.Count);
        }

        [Test]
        public void Tick_RaisesStageChangesOnce()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass");

            int sprouted = 0, matured = 0;
            sim.PlantStageChanged += (_, stage) =>
            {
                if (stage == PlantStage.Sprout) sprouted++;
                if (stage == PlantStage.Mature) matured++;
            };

            clock.Advance(5); sim.Tick();
            Assert.AreEqual(0, sprouted);

            clock.Advance(10); sim.Tick(); sim.Tick();
            Assert.AreEqual(1, sprouted);
            Assert.AreEqual(0, matured);

            clock.Advance(100); sim.Tick(); sim.Tick();
            Assert.AreEqual(1, sprouted);
            Assert.AreEqual(1, matured);
        }

        [Test]
        public void Energy_IsEarnedOnlyAfterMaturity()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass"); // 98 left, matures at 30 s, then 6 per minute

            clock.Advance(30); sim.Tick();
            Assert.AreEqual(98, sim.Energy, 1e-6);

            clock.Advance(60); sim.Tick();
            Assert.AreEqual(104, sim.Energy, 1e-6);
        }

        [Test]
        public void Energy_IsTheSame_ForManyTicksOrOneBigJump()
        {
            var clockA = new ManualClock();
            var simA = NewSim(clockA);
            simA.TryPlant(0, "grass");
            simA.TryPlant(1, "tree");
            for (int i = 0; i < 600; i++) { clockA.Advance(1); simA.Tick(); }

            var clockB = new ManualClock();
            var simB = NewSim(clockB);
            simB.TryPlant(0, "grass");
            simB.TryPlant(1, "tree");
            clockB.Advance(600); simB.Tick();

            Assert.AreEqual(simB.Energy, simA.Energy, 1e-6);
        }

        [Test]
        public void Energy_OfflineEarningsAreCapped()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass"); // 98 left
            clock.Advance(30); sim.Tick(); // mature now

            clock.Advance(72 * 3600); sim.Tick(); // three days away, capped to 24 h
            Assert.AreEqual(98 + 6 * 60 * 24, sim.Energy, 1e-3);
        }

        [Test]
        public void Energy_ClockMovingBackwards_EarnsNothingAndRecovers()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass");
            clock.Advance(30); sim.Tick();
            double before = sim.Energy;

            clock.Advance(-3600); sim.Tick();
            Assert.AreEqual(before, sim.Energy, 1e-9);

            clock.Advance(60); sim.Tick();
            Assert.AreEqual(before, sim.Energy, 1e-9, "The plant is not mature again yet at the rewound time.");
        }

        [Test]
        public void Remove_RefundsHalf_AndFreesCell()
        {
            var sim = NewSim(new ManualClock());
            sim.TryPlant(5, "tree"); // 70 left

            Assert.IsTrue(sim.TryRemove(5, out int refund));
            Assert.AreEqual(15, refund);
            Assert.AreEqual(85, sim.Energy, 1e-9);
            Assert.IsNull(sim.PlantAt(5));
            Assert.IsFalse(sim.TryRemove(5, out _));
        }

        [Test]
        public void Save_RoundTrip_RestoresPlantsAndEnergy()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass");
            sim.TryPlant(9, "tree");
            clock.Advance(45); sim.Tick();

            string json = SaveSerializer.ToJson(sim.ToSave());
            var loaded = SaveSerializer.FromJson(json);
            Assert.IsNotNull(loaded);

            var restored = NewSim(clock, save: loaded);
            Assert.AreEqual(sim.Energy, restored.Energy, 1e-6);
            Assert.AreEqual(2, restored.Plants.Count);
            Assert.AreEqual(sim.PlantAt(0).PlantedAtUtcTicks, restored.PlantAt(0).PlantedAtUtcTicks);
            Assert.AreEqual("tree", restored.PlantAt(9).Spec.Id);
        }

        [Test]
        public void Save_LoadingLater_GivesOfflineGrowthAndEnergy()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.TryPlant(0, "grass"); // 98 left
            var save = SaveSerializer.FromJson(SaveSerializer.ToJson(sim.ToSave()));

            clock.Advance(30 + 600); // app closed: matures after 30 s, then earns for 10 minutes
            var restored = NewSim(clock, save: save);

            Assert.AreEqual(PlantStage.Mature, restored.PlantAt(0).StageAt(clock.UtcNow.Ticks));
            Assert.AreEqual(98 + 60, restored.Energy, 1e-6);
        }

        [Test]
        public void Save_UnknownPlantsAndBadCells_AreSkipped()
        {
            var clock = new ManualClock();
            var save = new SaveData { seed = 7, cellCount = Cells, energy = 5, lastAccrualUtcTicks = clock.UtcNow.Ticks };
            save.plants.Add(new PlantSave { cell = 1, specId = "grass", plantedAtUtcTicks = clock.UtcNow.Ticks });
            save.plants.Add(new PlantSave { cell = 1, specId = "tree", plantedAtUtcTicks = clock.UtcNow.Ticks });
            save.plants.Add(new PlantSave { cell = 2, specId = "removed-species", plantedAtUtcTicks = clock.UtcNow.Ticks });
            save.plants.Add(new PlantSave { cell = 99999, specId = "grass", plantedAtUtcTicks = clock.UtcNow.Ticks });

            var sim = NewSim(clock, save: save);
            Assert.AreEqual(1, sim.Plants.Count);
            Assert.AreEqual("grass", sim.PlantAt(1).Spec.Id);
        }

        [Test]
        public void Save_GarbageOrNewerVersion_IsRejected()
        {
            Assert.IsNull(SaveSerializer.FromJson(null));
            Assert.IsNull(SaveSerializer.FromJson(""));
            Assert.IsNull(SaveSerializer.FromJson("not json at all"));
            Assert.IsNull(SaveSerializer.FromJson("{\"version\":" + (SaveData.CurrentVersion + 1) + "}"));
        }
    }
}
