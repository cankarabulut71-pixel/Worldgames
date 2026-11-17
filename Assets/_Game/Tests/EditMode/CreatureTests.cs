using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyPlanet.Simulation;

namespace TinyPlanet.Tests
{
    public static class TestPlanets
    {
        /// <summary>A generated planet with its ponds drained, so tests can plant on any cell.</summary>
        public static Planet Land(int seed, int cells)
        {
            var planet = PlanetGenerator.Generate(seed, cells);
            for (int i = 0; i < planet.Cells.Length; i++) planet.Cells[i].Water = false;
            return planet;
        }
    }

    public class CreatureTests
    {
        private const int Cells = 200;
        private const int Centre = 100;

        private static PlantSpec Plant(string id, int reward = 0) => new PlantSpec
        {
            Id = id,
            SeedCost = 1,
            SeedSeconds = 5,
            SproutSeconds = 5,
            EnergyPerMinute = 0,
            RefundFraction = 0,
            DiscoveryReward = reward,
        };

        private static CreatureSpec Creature(string id, params SpawnCondition[] conditions) => new CreatureSpec
        {
            Id = id,
            CheckIntervalSeconds = 10,
            ChancePerCheck = 1,
            PopulationCap = 1,
            LeaveAfterSeconds = 60,
            DiscoveryReward = 25,
            Conditions = conditions.ToList(),
        };

        private static PlanetSimulation NewSim(ManualClock clock, Planet planet, IEnumerable<CreatureSpec> creatures, SaveData save = null)
        {
            var plants = new[] { Plant("grass", reward: 3), Plant("flower"), Plant("tree") };
            var economy = new EconomySettings { StartingEnergy = 1000, MaxOfflineHours = 24 };
            return new PlanetSimulation(planet, plants, creatures, economy, clock, save, new Random(1));
        }

        private static void PlantRing(PlanetSimulation sim, int centre, string plantId, int count)
        {
            sim.TryPlant(centre, plantId);
            foreach (int cell in sim.Planet.Grid.Neighbours(centre).Take(count - 1))
                sim.TryPlant(cell, plantId);
        }

        private static void Run(ManualClock clock, PlanetSimulation sim, double seconds, double step = 1)
        {
            for (double t = 0; t < seconds; t += step)
            {
                clock.Advance(step);
                sim.Tick();
            }
        }

        [Test]
        public void Generator_MakesSomeWater_TheSameWayEveryTime()
        {
            var a = PlanetGenerator.Generate(5, 500);
            var b = PlanetGenerator.Generate(5, 500);

            int water = a.Cells.Count(c => c.Water);
            Assert.That(water, Is.InRange(20, 50));
            for (int i = 0; i < 500; i++) Assert.AreEqual(a.Cells[i].Water, b.Cells[i].Water);
        }

        [Test]
        public void Planting_OnWater_IsRefused()
        {
            var clock = new ManualClock();
            var planet = PlanetGenerator.Generate(5, 500);
            var sim = NewSim(clock, planet, null);
            int pond = Enumerable.Range(0, 500).First(planet.IsWater);

            Assert.AreEqual(PlantResult.Water, sim.TryPlant(pond, "grass"));
            Assert.AreEqual(0, sim.Plants.Count);
        }

        [Test]
        public void CellsWithin_GrowsWithRadius_AndHasNoDuplicates()
        {
            var grid = new PlanetGrid(Cells);

            CollectionAssert.AreEqual(new[] { Centre }, grid.CellsWithin(Centre, 0));

            var one = grid.CellsWithin(Centre, 1);
            Assert.AreEqual(1 + PlanetGrid.NeighbourCount, one.Count);
            CollectionAssert.IsSubsetOf(grid.Neighbours(Centre), one);

            var two = grid.CellsWithin(Centre, 2);
            Assert.Greater(two.Count, one.Count);
            Assert.AreEqual(two.Count, two.Distinct().Count());
        }

        [Test]
        public void Creature_ArrivesOnlyOnceItsPlantsAreMature()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 3, 1));
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });
            CreatureInstance arrived = null;
            sim.CreatureArrived += c => arrived = c;

            PlantRing(sim, Centre, "grass", 3);
            Run(clock, sim, 9); // still sprouts
            Assert.IsNull(arrived);

            Run(clock, sim, 12);
            Assert.IsNotNull(arrived);
            Assert.AreEqual("hopper", arrived.Spec.Id);
            Assert.That(sim.Planet.Grid.CellsWithin(Centre, 1), Has.Member(arrived.HomeCell));
        }

        [Test]
        public void Creature_StaysAway_WhenConditionsAreNotMet()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 3, 1));
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });

            PlantRing(sim, Centre, "grass", 2);
            sim.TryPlant(10, "grass"); // far away, does not count
            Run(clock, sim, 120);

            Assert.AreEqual(0, sim.Creatures.Count);
            Assert.IsFalse(sim.IsDiscovered("hopper"));
        }

        [Test]
        public void PopulationCap_IsRespected()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            hopper.PopulationCap = 2;
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });

            for (int i = 0; i < 20; i++) sim.TryPlant(i * 9, "grass");
            Run(clock, sim, 300);

            Assert.AreEqual(2, sim.CountCreatures("hopper"));
            Assert.AreNotEqual(sim.Creatures[0].HomeCell, sim.Creatures[1].HomeCell);
        }

        [Test]
        public void RareRecipe_NeedsTouchingPlants_WaterAndAnotherCreature()
        {
            var clock = new ManualClock();
            var planet = TestPlanets.Land(3, Cells);
            int pond = planet.Grid.Neighbours(Centre)[0];
            planet.Cells[pond].Water = true;

            var butterfly = Creature("butterfly", new PlantsNearby("flower", 1, 0));
            var moth = Creature("moth",
                new PlantsTouching("tree", "flower", 0),
                new NearWater(1),
                new CreatureLivesHere("butterfly"));
            var sim = NewSim(clock, planet, new[] { butterfly, moth });

            // Tree beside water, but nothing touching it yet.
            sim.TryPlant(Centre, "tree");
            Run(clock, sim, 60);
            Assert.AreEqual(0, sim.CountCreatures("moth"));

            // A flower touching the tree also brings the butterfly, and then the moth can come.
            int flowerCell = planet.Grid.Neighbours(Centre).First(c => c != pond);
            sim.TryPlant(flowerCell, "flower");
            Run(clock, sim, 60);

            Assert.AreEqual(1, sim.CountCreatures("butterfly"));
            Assert.AreEqual(1, sim.CountCreatures("moth"));
            Assert.AreEqual(Centre, sim.Creatures.First(c => c.Spec.Id == "moth").HomeCell);
        }

        [Test]
        public void Creature_Leaves_AfterItsHabitatIsGoneLongEnough()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });
            bool left = false;
            sim.CreatureLeft += _ => left = true;

            sim.TryPlant(Centre, "grass");
            Run(clock, sim, 30);
            Assert.AreEqual(1, sim.Creatures.Count);

            sim.TryRemove(Centre, out _);
            Run(clock, sim, 40);
            Assert.AreEqual(1, sim.Creatures.Count, "should linger for a while first");

            Run(clock, sim, 40);
            Assert.IsTrue(left);
            Assert.AreEqual(0, sim.Creatures.Count);
            Assert.IsTrue(sim.IsDiscovered("hopper"), "discoveries are permanent");
        }

        [Test]
        public void Creature_MovesHome_WhenAnotherSuitableSpotExists()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });

            sim.TryPlant(Centre, "grass");
            Run(clock, sim, 30);
            int home = sim.Creatures[0].HomeCell;

            sim.TryPlant(10, "grass");
            Run(clock, sim, 15);
            sim.TryRemove(home, out _);
            Run(clock, sim, 15);

            Assert.AreEqual(1, sim.Creatures.Count);
            Assert.AreEqual(10, sim.Creatures[0].HomeCell);
        }

        [Test]
        public void Discovery_RewardsEnergyOnce_ForPlantsAndCreatures()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            hopper.PopulationCap = 3;
            var sim = NewSim(clock, TestPlanets.Land(3, Cells), new[] { hopper });
            var found = new List<string>();
            sim.SpeciesDiscovered += (id, reward) => found.Add(id + ":" + reward);

            sim.TryPlant(Centre, "grass");
            sim.TryPlant(10, "grass");
            sim.TryPlant(20, "grass");
            double afterPlanting = sim.Energy;
            Run(clock, sim, 120);

            Assert.AreEqual(3, sim.CountCreatures("hopper"));
            CollectionAssert.AreEqual(new[] { "grass:3", "hopper:25" }, found);
            Assert.AreEqual(afterPlanting + 3 + 25, sim.Energy, 1e-6);
        }

        [Test]
        public void Save_RoundTrip_KeepsCreaturesAndDiscoveries()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            var planet = TestPlanets.Land(3, Cells);
            var sim = NewSim(clock, planet, new[] { hopper });
            sim.TryPlant(Centre, "grass");
            Run(clock, sim, 30);

            var json = SaveSerializer.ToJson(sim.ToSave());
            var loaded = NewSim(clock, planet, new[] { hopper }, SaveSerializer.FromJson(json));

            Assert.AreEqual(1, loaded.Creatures.Count);
            Assert.AreEqual(sim.Creatures[0].Id, loaded.Creatures[0].Id);
            Assert.AreEqual(sim.Creatures[0].HomeCell, loaded.Creatures[0].HomeCell);
            Assert.IsTrue(loaded.IsDiscovered("hopper"));
            Assert.IsTrue(loaded.IsDiscovered("grass"));
        }

        [Test]
        public void Offline_CreaturesArriveWhileTheAppWasClosed()
        {
            var clock = new ManualClock();
            var hopper = Creature("hopper", new PlantsNearby("grass", 1, 0));
            var planet = TestPlanets.Land(3, Cells);
            var sim = NewSim(clock, planet, new[] { hopper });
            sim.TryPlant(Centre, "grass");
            var save = SaveSerializer.FromJson(SaveSerializer.ToJson(sim.ToSave()));

            clock.Advance(3600);
            var loaded = NewSim(clock, planet, new[] { hopper }, save);
            Assert.AreEqual(0, loaded.Creatures.Count);
            loaded.Tick();

            Assert.AreEqual(1, loaded.Creatures.Count);
            Assert.IsTrue(loaded.IsDiscovered("hopper"));
        }

        [Test]
        public void VersionOneSave_StillLoads()
        {
            const string json = "{\"version\":1,\"seed\":3,\"cellCount\":200,\"energy\":42.0,\"lastAccrualUtcTicks\":0,\"savedAtUtcTicks\":0,\"plants\":[{\"cell\":5,\"specId\":\"grass\",\"plantedAtUtcTicks\":0}]}";
            var save = SaveSerializer.FromJson(json);

            Assert.IsNotNull(save);
            Assert.IsNotNull(save.creatures);
            Assert.IsNotNull(save.discovered);

            var sim = NewSim(new ManualClock(), TestPlanets.Land(3, Cells), null, save);
            Assert.AreEqual(1, sim.Plants.Count);
        }
    }
}
