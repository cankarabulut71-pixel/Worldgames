using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyPlanet.Simulation
{
    /// <summary>All species and lands the game knows about; each planet uses part of it.</summary>
    public sealed class ContentPool
    {
        public List<BiomeSpec> Biomes = new List<BiomeSpec>();
        public List<PlantSpec> Plants = new List<PlantSpec>();
        public List<CreatureSpec> Creatures = new List<CreatureSpec>();
    }

    public enum PlanetSize { Small, Medium, Large }
    public enum PlanetWater { Dry, Normal, Watery }
    public enum PlanetRelief { Gentle, Normal, Craggy }

    /// <summary>A creature that exists only on one planet: its spawn rule is built from that planet's species.</summary>
    public sealed class SignatureCreature
    {
        public CreatureSpec Spec;
        public string Name;
        public string Hint;
        public string Description;

        /// <summary>Index into the presentation's creature shapes.</summary>
        public int Shape;
        public float Hue;
        public float Size;
    }

    /// <summary>
    /// Everything a seed decides about one planet: its name and character, which species live there, and its
    /// signature rare creatures. The same seed and index always give the same recipe.
    /// </summary>
    public sealed class PlanetRecipe
    {
        public int Seed;
        public int Index;
        public string Name;
        public bool IsHome;

        public int CellCount;
        public float RadiusScale = 1f;
        public PlanetSize Size = PlanetSize.Medium;
        public PlanetWater Water = PlanetWater.Normal;
        public PlanetRelief Relief = PlanetRelief.Normal;

        /// <summary>Presentation: how much higher or flatter mountains are drawn.</summary>
        public float MountainScale = 1f;
        public float HueShift;
        public float Saturation = 1f;
        public int SkyPalette;

        public List<BiomeSpec> Biomes = new List<BiomeSpec>();
        public List<PlantSpec> Plants = new List<PlantSpec>();

        /// <summary>Every creature on the planet, signature creatures included.</summary>
        public List<CreatureSpec> Creatures = new List<CreatureSpec>();
        public List<SignatureCreature> Signatures = new List<SignatureCreature>();

        public IEnumerable<string> SpeciesIds => Plants.Select(p => p.Id).Concat(Creatures.Select(c => c.Id));
    }

    public static class PlanetRecipes
    {
        private const float PlantChance = 0.75f;
        private const float CreatureChance = 0.8f;
        private const float RareChance = 0.6f;
        private const int MinCommonCreatures = 5;
        public const int SkyPaletteCount = 4;

        private static readonly string[] PlanetStarts = { "Ve", "Ta", "Or", "Mi", "Sel", "Ka", "Lu", "Ny", "Ar", "Pe", "Zo", "Il" };
        private static readonly string[] PlanetMiddles = { "lo", "ra", "mi", "the", "va", "no", "ri", "sa", "" };
        private static readonly string[] PlanetEnds = { "ria", "nis", "dor", "ra", "lune", "ka", "mere", "thos", "via" };
        private static readonly string[] CreatureStarts = { "Lu", "Mi", "Ze", "Ko", "Fa", "Ny", "Sa", "Vo", "Pe", "Ri", "Ta", "Bo", "Glim", "Fen" };
        private static readonly string[] CreatureMiddles = { "ra", "li", "mo", "ve", "nu", "sha", "ki", "lo", "" };
        private static readonly string[] CreatureEnds = { "wisp", "fin", "horn", "tail", "moth", "bun", "drake", "pip", "glow", "whisk", "back", "song" };
        private const int ShapeCount = 6;

        /// <param name="index">0 is the home planet: every species, no unusual traits.</param>
        /// <param name="homeCellCount">Cell count of an ordinary, medium planet.</param>
        public static PlanetRecipe Create(ContentPool pool, int seed, int index, int homeCellCount)
        {
            var random = new Random(unchecked(seed * 486187739 + 0x5157));
            var recipe = new PlanetRecipe
            {
                Seed = seed,
                Index = index,
                IsHome = index == 0,
                CellCount = homeCellCount,
                Name = Word(random, PlanetStarts, PlanetMiddles, PlanetEnds),
            };

            float waterScale = 1f;
            var biomeWeights = pool.Biomes.Select(_ => 1f).ToArray();
            if (!recipe.IsHome)
            {
                recipe.Size = (PlanetSize)random.Next(3);
                recipe.Water = (PlanetWater)random.Next(3);
                recipe.Relief = (PlanetRelief)random.Next(3);
                recipe.CellCount = recipe.Size == PlanetSize.Small ? homeCellCount * 4 / 5
                    : recipe.Size == PlanetSize.Large ? homeCellCount * 6 / 5 : homeCellCount;
                recipe.RadiusScale = (float)Math.Sqrt(recipe.CellCount / (double)homeCellCount);
                waterScale = recipe.Water == PlanetWater.Dry ? 0.55f : recipe.Water == PlanetWater.Watery ? 1.7f : 1f;
                recipe.MountainScale = recipe.Relief == PlanetRelief.Gentle ? 0.55f : recipe.Relief == PlanetRelief.Craggy ? 1.45f : 1f;
                recipe.HueShift = ((float)random.NextDouble() - 0.5f) * 0.14f;
                recipe.Saturation = 0.85f + (float)random.NextDouble() * 0.35f;
                recipe.SkyPalette = 1 + random.Next(SkyPaletteCount - 1);
                for (int i = 0; i < biomeWeights.Length; i++) biomeWeights[i] = 0.7f + (float)random.NextDouble() * 0.6f;
            }

            ChoosePlants(pool, recipe, random);
            ChooseCreatures(pool, recipe, random);

            var plantIds = new HashSet<string>(recipe.Plants.Select(p => p.Id));
            for (int i = 0; i < pool.Biomes.Count; i++)
            {
                var biome = pool.Biomes[i].Copy();
                biome.Weight *= biomeWeights[i];
                biome.WaterFraction = Math.Min(0.6f, biome.WaterFraction * waterScale);
                if (biome.AllowedPlants != null) biome.AllowedPlants.IntersectWith(plantIds);
                recipe.Biomes.Add(biome);
            }

            AddSignatures(recipe, random);
            return recipe;
        }

        // Seeds free from the start are always there; others are a matter of luck, but every land keeps
        // at least two plants so it is always worth waking.
        private static void ChoosePlants(ContentPool pool, PlanetRecipe recipe, Random random)
        {
            var chosen = new HashSet<string>();
            foreach (var plant in pool.Plants)
            {
                if (recipe.IsHome || plant.StartsUnlocked || random.NextDouble() < PlantChance) chosen.Add(plant.Id);
            }

            foreach (var biome in pool.Biomes)
            {
                var allowed = pool.Plants.Where(p => biome.Allows(p.Id)).ToList();
                while (allowed.Count(p => chosen.Contains(p.Id)) < Math.Min(2, allowed.Count))
                {
                    var missing = allowed.Where(p => !chosen.Contains(p.Id)).ToList();
                    chosen.Add(missing[random.Next(missing.Count)].Id);
                }
            }

            recipe.Plants.AddRange(pool.Plants.Where(p => chosen.Contains(p.Id)));
        }

        // A creature can only live on a planet that has everything its rule asks for.
        private static void ChooseCreatures(ContentPool pool, PlanetRecipe recipe, Random random)
        {
            var plants = new HashSet<string>(recipe.Plants.Select(p => p.Id));
            var biomes = new HashSet<string>(pool.Biomes.Select(b => b.Id));
            var chosen = new List<CreatureSpec>();
            var skipped = new List<CreatureSpec>();

            foreach (var creature in pool.Creatures)
            {
                double roll = random.NextDouble();
                bool possible = Possible(creature, plants, biomes, chosen);
                double chance = creature.Rarity >= Rarity.Rare ? RareChance : CreatureChance;
                if (possible && (recipe.IsHome || roll < chance)) chosen.Add(creature);
                else if (creature.Rarity == Rarity.Common) skipped.Add(creature);
            }

            // Keep enough easy finds that a new planet never feels empty, bringing in the plants they need.
            while (chosen.Count(c => c.Rarity == Rarity.Common) < MinCommonCreatures && skipped.Count > 0)
            {
                var add = skipped[random.Next(skipped.Count)];
                skipped.Remove(add);
                foreach (var plant in PlantIdsUsedBy(add)) plants.Add(plant);
                if (Possible(add, plants, biomes, chosen)) chosen.Add(add);
            }

            // Keep the pool's order, so codex and tray listings stay familiar from planet to planet.
            recipe.Plants.Clear();
            recipe.Plants.AddRange(pool.Plants.Where(p => plants.Contains(p.Id)));
            recipe.Creatures.AddRange(pool.Creatures.Where(chosen.Contains));
        }

        private static IEnumerable<string> PlantIdsUsedBy(CreatureSpec creature)
        {
            foreach (var condition in creature.Conditions)
            {
                if (condition is PlantsNearby nearby) yield return nearby.PlantId;
                else if (condition is PlantsTouching touching)
                {
                    yield return touching.PlantA;
                    yield return touching.PlantB;
                }
            }
        }

        private static bool Possible(CreatureSpec creature, HashSet<string> plants, HashSet<string> biomes, List<CreatureSpec> chosen)
        {
            foreach (var condition in creature.Conditions)
            {
                switch (condition)
                {
                    case PlantsNearby nearby when !plants.Contains(nearby.PlantId): return false;
                    case PlantsTouching touching when !plants.Contains(touching.PlantA) || !plants.Contains(touching.PlantB): return false;
                    case CreatureLivesHere lives when chosen.All(c => c.Id != lives.CreatureId): return false;
                    case InBiome inBiome when !biomes.Contains(inBiome.BiomeId): return false;
                }
            }
            return true;
        }

        // One rare creature on every planet, and on half the new planets a legendary one that needs two lands.
        private static void AddSignatures(PlanetRecipe recipe, Random random)
        {
            var rare = Rare(recipe, random);
            if (rare == null) return;
            recipe.Signatures.Add(rare);
            recipe.Creatures.Add(rare.Spec);

            if (recipe.IsHome || random.NextDouble() >= 0.5) return;
            var legendary = Legendary(recipe, rare, random);
            if (legendary == null) return;
            recipe.Signatures.Add(legendary);
            recipe.Creatures.Add(legendary.Spec);
        }

        private static SignatureCreature Rare(PlanetRecipe recipe, Random random)
        {
            var lands = recipe.Biomes.Where(b => PlantsIn(recipe, b).Count >= 2).ToList();
            if (lands.Count == 0) return null;
            // Prefer a land the player has to wake first, so the signature is a later goal.
            var later = lands.Where(b => !b.StartsUnlocked).ToList();
            var pickFrom = later.Count > 0 && random.NextDouble() < 0.75 ? later : lands;
            var land = pickFrom[random.Next(pickFrom.Count)];

            var plants = PlantsIn(recipe, land);
            var a = plants[random.Next(plants.Count)];
            var b = plants.Where(p => p != a).ElementAt(random.Next(plants.Count - 1));
            var c = plants[random.Next(plants.Count)];

            var conditions = new List<SpawnCondition>
            {
                new PlantsTouching(a.Id, b.Id, 1),
                new PlantsNearby(c.Id, 2, 1),
                new InBiome(land.Id),
            };
            var hint = new List<string> { Capital(Noun(a)) + " leaning on " + Noun(b), "plenty of " + Noun(c) };

            if (land.WaterFraction > 0f && random.NextDouble() < 0.6)
            {
                conditions.Add(new NearWater(2));
                hint.Add("still water close by");
            }

            var friends = recipe.Creatures.Where(x => x.Rarity == Rarity.Common).ToList();
            if (friends.Count > 0 && random.NextDouble() < 0.5)
            {
                conditions.Add(new CreatureLivesHere(friends[random.Next(friends.Count)].Id));
                hint.Add("and it never comes alone");
            }

            string name = Word(random, CreatureStarts, CreatureMiddles, CreatureEnds);
            return new SignatureCreature
            {
                Name = name,
                Hint = string.Join(", ", hint) + ", somewhere in " + (land.HintNoun ?? "the " + land.Id) + "...",
                Description = "A shy creature found only on " + recipe.Name + ". It makes its home where " + Noun(a) +
                              " meets " + Noun(b) + ".",
                Shape = random.Next(ShapeCount),
                Hue = (float)random.NextDouble(),
                Size = 1.15f + (float)random.NextDouble() * 0.25f,
                Spec = new CreatureSpec
                {
                    Id = "sig-" + recipe.Seed + "-1",
                    Rarity = Rarity.Rare,
                    CheckIntervalSeconds = 18f,
                    ChancePerCheck = 0.25f,
                    PopulationCap = 1,
                    LeaveAfterSeconds = 180f,
                    DiscoveryReward = 200,
                    Life = 25f,
                    Conditions = conditions,
                },
            };
        }

        private static SignatureCreature Legendary(PlanetRecipe recipe, SignatureCreature rare, Random random)
        {
            var lands = recipe.Biomes.Where(b => PlantsIn(recipe, b).Count > 0).ToList();
            if (lands.Count < 2) return null;
            int first = random.Next(lands.Count);
            int second = (first + 1 + random.Next(lands.Count - 1)) % lands.Count;

            // The finest plant of each land (the one unlocked last) that does not also grow in the other land,
            // so the creature really needs the two lands side by side.
            var a = PlantsIn(recipe, lands[first]).Where(p => !lands[second].Allows(p.Id)).OrderByDescending(p => p.UnlockLevel).FirstOrDefault();
            var b = PlantsIn(recipe, lands[second]).Where(p => !lands[first].Allows(p.Id)).OrderByDescending(p => p.UnlockLevel).FirstOrDefault();
            if (a == null || b == null) return null;

            string name = Word(random, CreatureStarts, CreatureMiddles, CreatureEnds);
            return new SignatureCreature
            {
                Name = name,
                Hint = "Where " + Noun(a) + " and " + Noun(b) + " grow within reach of each other... once the " + rare.Name + " has settled.",
                Description = "The legend of " + recipe.Name + ". It appears only where two lands show their finest, and only after the " +
                              rare.Name + " has made its home here.",
                Shape = random.Next(ShapeCount),
                Hue = (float)random.NextDouble(),
                Size = 1.5f,
                Spec = new CreatureSpec
                {
                    Id = "sig-" + recipe.Seed + "-2",
                    Rarity = Rarity.Legendary,
                    CheckIntervalSeconds = 25f,
                    ChancePerCheck = 0.2f,
                    PopulationCap = 1,
                    LeaveAfterSeconds = 240f,
                    DiscoveryReward = 400,
                    Life = 40f,
                    Conditions = new List<SpawnCondition>
                    {
                        new PlantsNearby(a.Id, 1, 2),
                        new PlantsNearby(b.Id, 1, 2),
                        new CreatureLivesHere(rare.Spec.Id),
                    },
                },
            };
        }

        private static List<PlantSpec> PlantsIn(PlanetRecipe recipe, BiomeSpec biome) =>
            recipe.Plants.Where(p => biome.Allows(p.Id)).ToList();

        private static string Noun(PlantSpec plant) => string.IsNullOrEmpty(plant.HintNoun) ? plant.Id : plant.HintNoun;

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static string Word(Random random, string[] starts, string[] middles, string[] ends)
        {
            string word = starts[random.Next(starts.Length)] + middles[random.Next(middles.Length)] + ends[random.Next(ends.Length)];
            return Capital(word.ToLowerInvariant());
        }
    }
}
