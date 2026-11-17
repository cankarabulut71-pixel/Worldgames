using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyPlanet.Simulation;

namespace TinyPlanet.Tests
{
    public class ProgressionTests
    {
        private const int Cells = 500;

        private static BiomeSpec[] Biomes() => new[]
        {
            new BiomeSpec { Id = "meadow", AllowedPlants = new HashSet<string> { "grass", "tree" } },
            new BiomeSpec { Id = "wetland", WaterFraction = 0.25f, AllowedPlants = new HashSet<string> { "grass", "reed" }, UnlockLevel = 2, UnlockCost = 50 },
            new BiomeSpec { Id = "rocky", AllowedPlants = new HashSet<string> { "moss" }, UnlockLevel = 3, UnlockCost = 100 },
        };

        private static PlantSpec Plant(string id, float life = 1, int unlockLevel = 1, int unlockCost = 0) => new PlantSpec
        {
            Id = id,
            SeedCost = 1,
            SeedSeconds = 5,
            SproutSeconds = 5,
            EnergyPerMinute = 0,
            RefundFraction = 0,
            Life = life,
            UnlockLevel = unlockLevel,
            UnlockCost = unlockCost,
        };

        private static PlantSpec[] Plants() => new[]
        {
            Plant("grass"),
            Plant("tree", life: 5, unlockLevel: 2, unlockCost: 20),
            Plant("reed", unlockLevel: 2, unlockCost: 10),
            Plant("moss", unlockLevel: 3, unlockCost: 10),
        };

        private static PlanetSimulation NewSim(ManualClock clock, double energy = 1000, SaveData save = null, Planet planet = null)
        {
            planet = planet ?? PlanetGenerator.Generate(11, Cells, Biomes());
            var economy = new EconomySettings { StartingEnergy = energy, LevelLifeThresholds = new[] { 5, 20 } };
            return new PlanetSimulation(planet, Plants(), null, economy, clock, save, new System.Random(1));
        }

        private static int LandIn(PlanetSimulation sim, string biomeId, int skip = 0)
        {
            var planet = sim.Planet;
            return Enumerable.Range(0, planet.Cells.Length)
                .Where(c => planet.BiomeOf(c).Id == biomeId && !planet.IsWater(c) && sim.PlantAt(c) == null)
                .Skip(skip).First();
        }

        private static void GrowAll(ManualClock clock, PlanetSimulation sim)
        {
            clock.Advance(11);
            sim.Tick();
        }

        [Test]
        public void Generator_GivesEveryBiomeARegion_AndWaterOnlyWhereAllowed()
        {
            var a = PlanetGenerator.Generate(11, Cells, Biomes());
            var b = PlanetGenerator.Generate(11, Cells, Biomes());

            for (int biome = 0; biome < 3; biome++)
                Assert.That(a.CountCells(biome), Is.GreaterThan(Cells / 8), "biome " + biome);

            for (int i = 0; i < Cells; i++)
            {
                Assert.AreEqual(a.Cells[i].Biome, b.Cells[i].Biome);
                Assert.AreEqual(a.Cells[i].Water, b.Cells[i].Water);
                if (a.Cells[i].Water) Assert.AreEqual("wetland", a.BiomeOf(i).Id);
            }

            int wetland = a.CountCells(1);
            int water = a.Cells.Count(c => c.Water);
            Assert.AreEqual((int)System.Math.Round(wetland * 0.25), water);
        }

        [Test]
        public void Generator_DifferentSeeds_GiveDifferentLayouts()
        {
            var a = PlanetGenerator.Generate(1, Cells, Biomes());
            var b = PlanetGenerator.Generate(2, Cells, Biomes());
            Assert.That(Enumerable.Range(0, Cells).Count(i => a.Cells[i].Biome != b.Cells[i].Biome), Is.GreaterThan(Cells / 10));
        }

        [Test]
        public void NewPlanet_StartsWithFreeBiomeAndSeedsOnly()
        {
            var sim = NewSim(new ManualClock());

            Assert.AreEqual(1, sim.Level);
            Assert.IsTrue(sim.IsBiomeUnlocked("meadow"));
            Assert.IsFalse(sim.IsBiomeUnlocked("wetland"));
            Assert.IsTrue(sim.IsPlantUnlocked("grass"));
            Assert.IsFalse(sim.IsPlantUnlocked("tree"));
        }

        [Test]
        public void Planting_RespectsDormantLand_LockedSeeds_AndBiomeRules()
        {
            var sim = NewSim(new ManualClock());

            Assert.AreEqual(PlantResult.Dormant, sim.TryPlant(LandIn(sim, "wetland"), "grass"));
            Assert.AreEqual(PlantResult.Locked, sim.TryPlant(LandIn(sim, "meadow"), "tree"));

            sim.DebugUnlockAll();
            Assert.AreEqual(PlantResult.WrongBiome, sim.TryPlant(LandIn(sim, "meadow"), "moss"));
            Assert.AreEqual(PlantResult.WrongBiome, sim.TryPlant(LandIn(sim, "rocky"), "grass"));
            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(LandIn(sim, "rocky"), "moss"));
            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(LandIn(sim, "wetland"), "reed"));
        }

        [Test]
        public void Unlocking_NeedsTheLevel_ThenCostsEnergy()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock, energy: 60);
            string unlocked = null;
            sim.BiomeUnlocked += id => unlocked = id;

            Assert.AreEqual(UnlockResult.LevelTooLow, sim.TryUnlockBiome("wetland"));
            Assert.AreEqual(UnlockResult.Unknown, sim.TryUnlockBiome("desert"));

            for (int i = 0; i < 5; i++) sim.TryPlant(LandIn(sim, "meadow"), "grass");
            GrowAll(clock, sim);
            Assert.AreEqual(2, sim.Level);

            Assert.AreEqual(UnlockResult.Ok, sim.TryUnlockBiome("wetland"));
            Assert.AreEqual("wetland", unlocked);
            Assert.AreEqual(60 - 5 - 50, sim.Energy, 1e-6);
            Assert.AreEqual(UnlockResult.AlreadyUnlocked, sim.TryUnlockBiome("wetland"));
            Assert.AreEqual(UnlockResult.NotEnoughEnergy, sim.TryUnlockPlant("tree"));
        }

        [Test]
        public void Level_RisesWithLife_AndNeverFalls()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            var levels = new List<int>();
            sim.LevelChanged += levels.Add;

            var cells = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                int cell = LandIn(sim, "meadow");
                sim.TryPlant(cell, "grass");
                cells.Add(cell);
            }
            Assert.AreEqual(0, sim.Life, 1e-6, "seeds are not life yet");
            GrowAll(clock, sim);

            Assert.AreEqual(20, sim.Life, 1e-6);
            Assert.AreEqual(3, sim.Level);
            CollectionAssert.AreEqual(new[] { 2, 3 }, levels);
            Assert.AreEqual(sim.MaxLevel, sim.Level);
            Assert.AreEqual(-1, sim.LifeForLevel(4));

            foreach (int cell in cells) sim.TryRemove(cell, out _);
            sim.Tick();
            Assert.AreEqual(3, sim.Level);
        }

        [Test]
        public void SaveAndLoad_KeepsLevelAndUnlocks()
        {
            var clock = new ManualClock();
            var sim = NewSim(clock);
            sim.DebugUnlockAll();
            int reedCell = LandIn(sim, "wetland");
            sim.TryPlant(reedCell, "reed");

            var json = SaveSerializer.ToJson(sim.ToSave());
            var loaded = NewSim(clock, save: SaveSerializer.FromJson(json));

            Assert.AreEqual(3, loaded.Level);
            Assert.IsTrue(loaded.IsBiomeUnlocked("rocky"));
            Assert.IsTrue(loaded.IsPlantUnlocked("moss"));
            Assert.AreEqual("reed", loaded.PlantAt(reedCell).Spec.Id);
        }

        [Test]
        public void OldSave_KeepsItsSeeds_AndRefundsPlantsOnSleepingLand()
        {
            var clock = new ManualClock();
            var fresh = NewSim(clock);
            int meadowCell = LandIn(fresh, "meadow");
            int wetlandCell = LandIn(fresh, "wetland");

            // A milestone 2 save: no unlock lists, a tree in the meadow and grass on what is now dormant wetland.
            string json = "{\"version\":2,\"seed\":11,\"cellCount\":500,\"energy\":10,\"plants\":[" +
                          "{\"cell\":" + meadowCell + ",\"specId\":\"tree\",\"plantedAtUtcTicks\":1}," +
                          "{\"cell\":" + wetlandCell + ",\"specId\":\"grass\",\"plantedAtUtcTicks\":1}]}";
            var save = SaveSerializer.FromJson(json);
            Assert.AreEqual(SaveData.CurrentVersion, save.version);

            var loaded = NewSim(clock, save: save);
            Assert.IsTrue(loaded.IsPlantUnlocked("tree"));
            Assert.AreEqual("tree", loaded.PlantAt(meadowCell).Spec.Id);
            Assert.IsNull(loaded.PlantAt(wetlandCell));
            Assert.AreEqual(10 + 1, loaded.Energy, 1e-6, "the grass seed was given back");
        }

        [Test]
        public void InBiome_OnlyMatchesHomesInThatBiome()
        {
            var clock = new ManualClock();
            var planet = PlanetGenerator.Generate(11, Cells, Biomes());
            var snail = new CreatureSpec
            {
                Id = "snail", CheckIntervalSeconds = 5, ChancePerCheck = 1, PopulationCap = 1, LeaveAfterSeconds = 60,
                Conditions = new List<SpawnCondition> { new InBiome("rocky") },
            };
            var economy = new EconomySettings { StartingEnergy = 1000 };
            var sim = new PlanetSimulation(planet, Plants(), new[] { snail }, economy, clock, null, new System.Random(1));
            sim.DebugUnlockAll();

            sim.TryPlant(LandIn(sim, "meadow"), "grass");
            for (int t = 0; t < 30; t++)
            {
                clock.Advance(1);
                sim.Tick();
            }
            Assert.AreEqual(0, sim.Creatures.Count);

            int rock = LandIn(sim, "rocky");
            sim.TryPlant(rock, "moss");
            for (int t = 0; t < 30; t++)
            {
                clock.Advance(1);
                sim.Tick();
            }
            Assert.AreEqual(1, sim.Creatures.Count);
            Assert.AreEqual(rock, sim.Creatures[0].HomeCell);
        }
    }
}
