using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyPlanet.Simulation;

namespace TinyPlanet.Tests
{
    public class DailyRhythmTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        private static Sky NewSky(double rainChance = 0.25, int seed = 7, double offsetHours = 0) =>
            new Sky(seed, new SkySettings { RainChance = rainChance, RainGrowthMultiplier = 1.5 }, TimeSpan.FromHours(offsetHours));

        private static PlantSpec Grass() => new PlantSpec { Id = "grass", SeedCost = 1, SeedSeconds = 5, SproutSeconds = 5 };

        private static CreatureSpec Creature(string id, params SpawnCondition[] conditions) => new CreatureSpec
        {
            Id = id,
            CheckIntervalSeconds = 1,
            ChancePerCheck = 1,
            PopulationCap = 1,
            LeaveAfterSeconds = 1e6f,
            DiscoveryReward = 10,
            Conditions = conditions.ToList(),
        };

        private static PlanetSimulation NewSim(ManualClock clock, Sky sky, IEnumerable<CreatureSpec> creatures,
            double goldenChance = 0, SaveData save = null)
        {
            var planet = PlanetGenerator.Generate(11, 200, new[] { BiomeSpec.Default() });
            var economy = new EconomySettings { StartingEnergy = 1000, GoldenChance = goldenChance };
            return new PlanetSimulation(planet, new[] { Grass() }, creatures, economy, clock, save, new Random(3), sky);
        }

        private static void PlantGrass(PlanetSimulation sim, int count = 3)
        {
            for (int cell = 0, planted = 0; cell < sim.Planet.Cells.Length && planted < count; cell++)
            {
                if (sim.TryPlant(cell, "grass") == PlantResult.Ok) planted++;
            }
        }

        [Test]
        public void Night_FollowsTheLocalClock()
        {
            var sky = NewSky(offsetHours: 3);
            Assert.IsTrue(sky.IsNight(Noon.AddHours(6).Ticks), "18:00 UTC is 21:00 local");
            Assert.IsFalse(sky.IsNight(Noon.AddHours(-4).Ticks), "08:00 UTC is 11:00 local");
            Assert.AreEqual(1f, sky.Daylight(Noon.Ticks), 1e-4f);
            Assert.AreEqual(0f, sky.Daylight(Noon.AddHours(12).Ticks), 1e-4f);
        }

        [Test]
        public void RainyDays_AreFixedBySeed_AndRoughlyAsCommonAsAsked()
        {
            var a = NewSky(seed: 42);
            var b = NewSky(seed: 42);
            int rainy = 0;
            for (long day = 20000; day < 21000; day++)
            {
                Assert.AreEqual(a.IsRainyDay(day), b.IsRainyDay(day));
                if (a.IsRainyDay(day)) rainy++;
            }
            Assert.That(rainy, Is.InRange(180, 320));
            Assert.IsFalse(Enumerable.Range(0, 1000).All(d => NewSky(seed: 42).IsRainyDay(d) == NewSky(seed: 43).IsRainyDay(d)),
                "different planets get different weather");
        }

        [Test]
        public void Rain_MakesPlantsGrowFaster_AndTheForecastCrossesMidnight()
        {
            var sky = NewSky(rainChance: 0.5);
            long day = Enumerable.Range(20000, 200).First(d => sky.IsRainyDay(d) && !sky.IsRainyDay(d + 1));
            long dayStart = day * TimeSpan.TicksPerDay;

            // Rain all the way: an hour of growing takes 40 minutes.
            Assert.AreEqual(dayStart + TimeSpan.FromMinutes(40).Ticks, sky.AfterGrowth(dayStart, 3600));

            // Started an hour before a dry midnight: the first hour counts 1.5x, the rest at normal speed.
            long lateStart = dayStart + TimeSpan.FromHours(23).Ticks;
            Assert.AreEqual(lateStart + TimeSpan.FromHours(1).Ticks + TimeSpan.FromMinutes(30).Ticks, sky.AfterGrowth(lateStart, 7200));

            var spec = new PlantSpec { Id = "oak", SeedSeconds = 6 * 3600, SproutSeconds = 18 * 3600 };
            var plant = new PlantInstance(spec, 0, dayStart, sky);
            Assert.Less(plant.MatureAtUtcTicks, dayStart + TimeSpan.FromHours(24).Ticks);
            Assert.AreEqual(PlantStage.Seed, plant.StageAt(dayStart + TimeSpan.FromHours(3).Ticks));
            Assert.AreEqual(PlantStage.Sprout, plant.StageAt(dayStart + TimeSpan.FromHours(4.1).Ticks));
            Assert.AreEqual(PlantStage.Mature, plant.StageAt(plant.MatureAtUtcTicks));
        }

        [Test]
        public void NightCreature_OnlyArrivesAfterDark()
        {
            var clock = new ManualClock { UtcNow = Noon };
            var sim = NewSim(clock, NewSky(rainChance: 0), new[] { Creature("moth", new AtNight(), new PlantsNearby("grass", 1, 0)) });
            PlantGrass(sim);
            clock.Advance(20);
            sim.Tick();
            clock.Advance(20);
            sim.Tick();
            Assert.AreEqual(0, sim.CountCreatures("moth"));

            clock.UtcNow = Noon.AddHours(10); // 22:00
            sim.Tick();
            Assert.AreEqual(1, sim.CountCreatures("moth"));
        }

        [Test]
        public void RainCreature_OnlyArrivesInRain()
        {
            var clock = new ManualClock { UtcNow = Noon };
            var sky = NewSky(rainChance: 0);
            var sim = NewSim(clock, sky, new[] { Creature("frog", new InRain(), new PlantsNearby("grass", 1, 0)) });
            PlantGrass(sim);
            clock.Advance(20);
            sim.Tick();
            Assert.AreEqual(0, sim.CountCreatures("frog"));

            sky.DebugRain = true;
            clock.Advance(5);
            sim.Tick();
            Assert.AreEqual(1, sim.CountCreatures("frog"));
        }

        [Test]
        public void TimeAway_ReplaysChecksAcrossTheWholeAbsence_SoNightVisitorsCome()
        {
            var clock = new ManualClock { UtcNow = Noon };
            var creatures = new[] { Creature("moth", new AtNight(), new PlantsNearby("grass", 1, 0)) };
            var sim = NewSim(clock, NewSky(rainChance: 0), creatures);
            PlantGrass(sim);
            clock.Advance(20);
            sim.Tick();
            var save = SaveSerializer.FromJson(SaveSerializer.ToJson(sim.ToSave()));

            // Back at noon the next day: the replayed checks are spread over the night in between.
            clock.UtcNow = Noon.AddHours(24);
            var back = NewSim(clock, NewSky(rainChance: 0), creatures, save: save);
            back.Tick();
            Assert.AreEqual(1, back.CountCreatures("moth"));
            var arrived = new DateTime(back.Creatures[0].ArrivedAtUtcTicks, DateTimeKind.Utc);
            Assert.That(arrived.Hour >= 20 || arrived.Hour < 6, "arrived at " + arrived);
        }

        [Test]
        public void GoldenVariant_PaysTwice_AndIsRemembered()
        {
            var clock = new ManualClock { UtcNow = Noon };
            var creatures = new[] { Creature("hopper", new PlantsNearby("grass", 1, 0)) };
            var sim = NewSim(clock, NewSky(rainChance: 0), creatures, goldenChance: 1);
            var golden = new List<(string, int)>();
            sim.GoldenDiscovered += (id, reward) => golden.Add((id, reward));
            PlantGrass(sim);
            clock.Advance(20);
            sim.Tick();

            Assert.AreEqual(1, sim.CountCreatures("hopper"));
            Assert.IsTrue(sim.Creatures[0].Golden);
            Assert.IsTrue(sim.IsGoldenFound("hopper"));
            CollectionAssert.AreEqual(new[] { ("hopper", 20) }, golden);

            var save = SaveSerializer.FromJson(SaveSerializer.ToJson(sim.ToSave()));
            var restored = NewSim(clock, NewSky(rainChance: 0), creatures, save: save);
            Assert.IsTrue(restored.IsGoldenFound("hopper"));
            Assert.IsTrue(restored.Creatures[0].Golden);
        }

        [Test]
        public void OldSaves_LoadWithoutGoldenData()
        {
            var save = SaveSerializer.FromJson("{\"version\":3,\"seed\":11,\"energy\":5}");
            Assert.IsNotNull(save);
            Assert.IsNotNull(save.goldenFound);
            Assert.AreEqual(0, save.goldenFound.Count);
        }
    }
}
