using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyPlanet.Simulation;

namespace TinyPlanet.Tests
{
    public class PlanetTests
    {
        private static ContentPool Pool()
        {
            var pool = new ContentPool();
            pool.Biomes.Add(new BiomeSpec { Id = "meadow", AllowedPlants = new HashSet<string> { "grass", "flower", "bush", "tree" }, HintNoun = "the hills" });
            pool.Biomes.Add(new BiomeSpec { Id = "wetland", WaterFraction = 0.25f, AllowedPlants = new HashSet<string> { "grass", "flower", "reed", "willow" }, UnlockLevel = 3, UnlockCost = 10 });
            pool.Biomes.Add(new BiomeSpec { Id = "rocky", AllowedPlants = new HashSet<string> { "moss", "pine", "crystal" }, UnlockLevel = 5, UnlockCost = 10 });

            void Plant(string id, int level) => pool.Plants.Add(new PlantSpec
            {
                Id = id, SeedCost = 1, SeedSeconds = 1, SproutSeconds = 1, UnlockLevel = level, UnlockCost = level > 1 ? 10 : 0, HintNoun = id + "s",
            });
            Plant("grass", 1); Plant("flower", 1); Plant("bush", 2); Plant("tree", 2); Plant("reed", 3);
            Plant("willow", 4); Plant("moss", 5); Plant("pine", 6); Plant("crystal", 7);

            void Creature(string id, Rarity rarity, params SpawnCondition[] conditions) => pool.Creatures.Add(new CreatureSpec
            {
                Id = id, Rarity = rarity, Conditions = conditions.ToList(),
            });
            Creature("hopper", Rarity.Common, new PlantsNearby("grass", 3, 1));
            Creature("wing", Rarity.Common, new PlantsNearby("flower", 2, 1));
            Creature("climber", Rarity.Common, new PlantsNearby("tree", 1, 0));
            Creature("skipper", Rarity.Common, new PlantsNearby("reed", 3, 1), new NearWater(1));
            Creature("snail", Rarity.Common, new PlantsNearby("moss", 3, 1), new InBiome("rocky"));
            Creature("mouse", Rarity.Uncommon, new PlantsNearby("bush", 2, 1));
            Creature("bug", Rarity.Uncommon, new PlantsNearby("crystal", 2, 1), new CreatureLivesHere("snail"));
            Creature("moth", Rarity.Rare, new PlantsTouching("willow", "flower", 0), new CreatureLivesHere("wing"));
            return pool;
        }

        private static IEnumerable<string> PlantsUsedBy(CreatureSpec creature)
        {
            foreach (var condition in creature.Conditions)
            {
                switch (condition)
                {
                    case PlantsNearby nearby: yield return nearby.PlantId; break;
                    case PlantsTouching touching: yield return touching.PlantA; yield return touching.PlantB; break;
                }
            }
        }

        [Test]
        public void Recipe_IsTheSameForTheSameSeed()
        {
            var a = PlanetRecipes.Create(Pool(), 99, 1, 500);
            var b = PlanetRecipes.Create(Pool(), 99, 1, 500);

            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.CellCount, b.CellCount);
            Assert.AreEqual(a.SkyPalette, b.SkyPalette);
            CollectionAssert.AreEqual(a.SpeciesIds.ToList(), b.SpeciesIds.ToList());
            CollectionAssert.AreEqual(a.Signatures.Select(s => s.Hint).ToList(), b.Signatures.Select(s => s.Hint).ToList());
        }

        [Test]
        public void HomePlanet_HasEverySpecies_AndNoUnusualTraits()
        {
            var pool = Pool();
            var home = PlanetRecipes.Create(pool, 12345, 0, 500);

            Assert.IsTrue(home.IsHome);
            Assert.AreEqual(500, home.CellCount);
            Assert.AreEqual(1f, home.MountainScale);
            Assert.AreEqual(0, home.SkyPalette);
            Assert.AreEqual(pool.Plants.Count, home.Plants.Count);
            foreach (var creature in pool.Creatures) Assert.That(home.Creatures, Has.Member(creature));
            Assert.AreEqual(1, home.Signatures.Count);

            // Same layout as a planet generated straight from the pool's biomes.
            var direct = PlanetGenerator.Generate(12345, 500, pool.Biomes);
            var viaRecipe = PlanetGenerator.Generate(12345, 500, home.Biomes);
            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual(direct.Cells[i].Biome, viaRecipe.Cells[i].Biome);
                Assert.AreEqual(direct.Cells[i].Water, viaRecipe.Cells[i].Water);
            }
        }

        [Test]
        public void NewPlanets_DifferFromEachOther_AndStayPlayable()
        {
            var pool = Pool();
            var recipes = Enumerable.Range(1, 40).Select(seed => PlanetRecipes.Create(pool, seed * 7919, 1, 500)).ToList();

            // Different seeds give different worlds.
            Assert.That(recipes.Select(r => r.Name).Distinct().Count(), Is.GreaterThan(30));
            Assert.That(recipes.Select(r => string.Join(",", r.SpeciesIds.Where(id => !id.StartsWith("sig-")))).Distinct().Count(), Is.GreaterThan(5));
            Assert.That(recipes.Select(r => r.Size).Distinct().Count(), Is.EqualTo(3));

            foreach (var recipe in recipes)
            {
                var plants = new HashSet<string>(recipe.Plants.Select(p => p.Id));
                var creatures = new HashSet<string>(recipe.Creatures.Select(c => c.Id));

                // The starting seeds are always there, and every land has at least two plants.
                Assert.That(plants, Has.Member("grass").And.Member("flower"));
                foreach (var biome in recipe.Biomes)
                    Assert.That(recipe.Plants.Count(p => biome.Allows(p.Id)), Is.GreaterThanOrEqualTo(2), recipe.Name + " " + biome.Id);

                Assert.That(recipe.Creatures.Count(c => c.Rarity == Rarity.Common), Is.GreaterThanOrEqualTo(4), recipe.Name);
                Assert.That(recipe.Signatures.Count, Is.InRange(1, 2), recipe.Name);

                // Every creature, signature ones included, only asks for things this planet has.
                foreach (var creature in recipe.Creatures)
                {
                    foreach (var plant in PlantsUsedBy(creature)) Assert.That(plants, Has.Member(plant), recipe.Name + " " + creature.Id);
                    foreach (var lives in creature.Conditions.OfType<CreatureLivesHere>()) Assert.That(creatures, Has.Member(lives.CreatureId));
                }
            }
        }

        [Test]
        public void SignatureCreature_ComesWhenItsRecipeIsPlanted()
        {
            // A seed whose rare signature needs only plants (water and companions are checked elsewhere).
            var pool = Pool();
            var recipe = Enumerable.Range(1, 300).Select(seed => PlanetRecipes.Create(pool, seed, 1, 500))
                .First(r => r.Signatures[0].Spec.Conditions.Count == 3);
            var rare = recipe.Signatures[0];
            var touching = rare.Spec.Conditions.OfType<PlantsTouching>().Single();
            var nearby = rare.Spec.Conditions.OfType<PlantsNearby>().Single();
            var land = rare.Spec.Conditions.OfType<InBiome>().Single().BiomeId;

            var clock = new ManualClock();
            var planet = PlanetGenerator.Generate(recipe.Seed, recipe.CellCount, recipe.Biomes);
            var economy = new EconomySettings { StartingEnergy = 100000 };
            // Only the signature, so nothing else competes for the checks.
            var sim = new PlanetSimulation(planet, recipe.Plants, new[] { rare.Spec }.Concat(recipe.Creatures.Where(c => c != rare.Spec)),
                economy, clock, null, new System.Random(2));
            sim.DebugUnlockAll();

            // Find a dry cell in the right land whose neighbours are also dry and in that land, and plant the recipe.
            var grid = planet.Grid;
            int home = Enumerable.Range(0, grid.CellCount).First(c =>
                planet.BiomeOf(c).Id == land && !planet.IsWater(c) &&
                grid.Neighbours(c).Count(n => planet.BiomeOf(n).Id == land && !planet.IsWater(n)) >= 4);
            var around = grid.Neighbours(home).Where(n => planet.BiomeOf(n).Id == land && !planet.IsWater(n)).ToList();
            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(home, touching.PlantA));
            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(around[0], touching.PlantB));
            if (nearby.PlantId != touching.PlantA) sim.TryPlant(around[1], nearby.PlantId);
            sim.TryPlant(around[2], nearby.PlantId);

            for (int t = 0; t < 200 && !sim.IsDiscovered(rare.Spec.Id); t++)
            {
                clock.Advance(20);
                sim.Tick();
            }

            Assert.IsTrue(sim.IsDiscovered(rare.Spec.Id), recipe.Name + ": " + rare.Hint);
        }

        [Test]
        public void GameSave_RoundTrip_KeepsEveryPlanet()
        {
            var game = new GameSave { activePlanet = 1 };
            game.planets.Add(new SaveData { seed = 1, cellCount = 500, energy = 50, planetLevel = 4 });
            game.planets.Add(new SaveData { seed = 2, cellCount = 400, started = false });

            var loaded = SaveSerializer.GameFromJson(SaveSerializer.ToJson(game));

            Assert.AreEqual(2, loaded.planets.Count);
            Assert.AreEqual(1, loaded.activePlanet);
            Assert.AreEqual(4, loaded.planets[0].planetLevel);
            Assert.IsTrue(loaded.planets[0].started);
            Assert.IsFalse(loaded.planets[1].started);
            Assert.AreSame(loaded.planets[1], loaded.Active);
        }

        [Test]
        public void OldSingleSave_BecomesTheHomePlanet()
        {
            string json = "{\"version\":3,\"seed\":12345,\"cellCount\":500,\"energy\":42,\"planetLevel\":3,\"debugClockOffsetSeconds\":60}";
            var game = SaveSerializer.GameFromJson(json);

            Assert.AreEqual(1, game.planets.Count);
            Assert.AreEqual(0, game.activePlanet);
            Assert.AreEqual(12345, game.Active.seed);
            Assert.AreEqual(42, game.Active.energy, 1e-9);
            Assert.IsTrue(game.Active.started);
            Assert.AreEqual(60, game.debugClockOffsetSeconds, 1e-9);
            Assert.IsNull(SaveSerializer.GameFromJson("{\"version\":99}"));
        }

        [Test]
        public void CanPlant_PredictsTryPlant_WithoutChangingAnything()
        {
            var clock = new ManualClock();
            var planet = PlanetGenerator.Generate(5, 300);
            var plants = new[] { new PlantSpec { Id = "grass", SeedCost = 10, SeedSeconds = 1, SproutSeconds = 1 } };
            var sim = new PlanetSimulation(planet, plants, null, new EconomySettings { StartingEnergy = 15 }, clock, null);
            var land = Enumerable.Range(0, planet.Grid.CellCount).Where(c => !planet.IsWater(c)).Take(2).ToList();

            Assert.AreEqual(PlantResult.Ok, sim.CanPlant(land[0], "grass"));
            Assert.AreEqual(15, sim.Energy, 1e-9);
            Assert.AreEqual(0, sim.Plants.Count);

            Assert.AreEqual(PlantResult.Ok, sim.TryPlant(land[0], "grass"));
            Assert.AreEqual(PlantResult.CellOccupied, sim.CanPlant(land[0], "grass"));
            Assert.AreEqual(PlantResult.NotEnoughEnergy, sim.CanPlant(land[1], "grass"));
            Assert.AreEqual(PlantResult.UnknownPlant, sim.CanPlant(land[1], "nothing"));
        }

        [Test]
        public void UnvisitedPlanet_StartsFresh()
        {
            var clock = new ManualClock();
            var planet = PlanetGenerator.Generate(3, 300);
            var plants = new[] { new PlantSpec { Id = "grass", SeedCost = 1, SeedSeconds = 1, SproutSeconds = 1 } };
            var stub = new SaveData { seed = 3, cellCount = 300, started = false, energy = 0 };
            var sim = new PlanetSimulation(planet, plants, null, new EconomySettings { StartingEnergy = 20 }, clock, stub);
            Assert.AreEqual(20, sim.Energy, 1e-9);
        }
    }
}
