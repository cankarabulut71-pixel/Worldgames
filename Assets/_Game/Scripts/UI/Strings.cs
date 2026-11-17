namespace TinyPlanet.UI
{
    /// <summary>All player-facing text in one place, ready to be swapped for localisation later.</summary>
    public static class Strings
    {
        public const string EnergyFormat = "Energy  {0}";
        public const string CostFormat = "{0}";
        public const string RemoveTool = "Remove";
        public const string RemoveHint = "Tap a plant";
        public const string NotEnoughEnergy = "Not enough energy";
        public const string NothingToRemove = "Nothing to remove here";
        public const string RemovedFormat = "Removed {0}  +{1}";
        public const string GrowingFormat = "{0} - grown in {1}";
        public const string MatureFormat = "{0} - {1} energy/min";
        public const string WelcomeBackFormat = "Welcome back!  +{0} energy";

        public const string WaterHere = "That's water - plant on land";

        public const string PlanetsTitle = "Planets";
        public const string PlanetsSubtitle = "Each world has its own lands, life and secrets";
        public const string PlanetHere = "You're here";
        public const string PlanetVisit = "Visit";
        public const string PlanetStatusFormat = "Level {0}  -  Codex {1}/{2}";
        public const string PlanetUnvisited = "Not visited yet";
        public const string NewWorld = "A new world";
        public const string NewWorldCostFormat = "Spend {0} energy from this planet to find a new one.";
        public const string NewWorldLevelFormat = "Grow this planet to level {0} to find a new one.";
        public const string DiscoverFormat = "Discover  {0}";
        public const string Locked = "Locked";
        public const string NewPlanetHeading = "A new planet!";
        public const string CodexOverallFormat = "All planets: {0} of {1}  -  {2}%";
        public const string HomePlanet = "Home planet";

        /// <summary>A short description of a planet's character, e.g. "Small, watery, craggy".</summary>
        public static string PlanetTraits(TinyPlanet.Simulation.PlanetRecipe recipe)
        {
            if (recipe.IsHome) return HomePlanet;
            var parts = new System.Collections.Generic.List<string>();
            if (recipe.Size == TinyPlanet.Simulation.PlanetSize.Small) parts.Add("small");
            if (recipe.Size == TinyPlanet.Simulation.PlanetSize.Large) parts.Add("large");
            if (recipe.Water == TinyPlanet.Simulation.PlanetWater.Dry) parts.Add("dry");
            if (recipe.Water == TinyPlanet.Simulation.PlanetWater.Watery) parts.Add("watery");
            if (recipe.Relief == TinyPlanet.Simulation.PlanetRelief.Gentle) parts.Add("gentle hills");
            if (recipe.Relief == TinyPlanet.Simulation.PlanetRelief.Craggy) parts.Add("craggy peaks");
            if (parts.Count == 0) parts.Add("temperate");
            string text = string.Join(", ", parts);
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        public const string DormantFormat = "This {0} is still asleep - wake it in Upgrades";
        public const string WrongBiomeFormat = "{0} doesn't grow in the {1}";
        public const string LockedSeed = "Unlock this seed in Upgrades first";

        public const string LevelButtonFormat = "Planet  Lv {0}";
        public const string UpgradesButton = "Upgrades";
        public const string UpgradesTitle = "Upgrades";
        public const string UpgradesLands = "Lands";
        public const string UpgradesSeeds = "Seeds";
        public const string PlanetLevelFormat = "Planet level {0}";
        public const string LifeProgressFormat = "Life {0} / {1}  to reach level {2}";
        public const string LifeMaxFormat = "Life {0}  -  highest level reached";
        public const string UpgradeAwake = "Awake";
        public const string UpgradeOwned = "Owned";
        public const string UpgradeNeedsLevelFormat = "Level {0}";
        public const string UpgradeCostFormat = "Unlock  {0}";
        public const string LevelUpHeading = "Planet level up!";
        public const string LevelUpFormat = "Level {0}";
        public const string LandAwakeHeading = "New land awakens!";
        public const string SeedUnlockedHeading = "New seed!";
        public const string GrowsInFormat = "Grows in: {0}";
        public const string SeedDetailFormat = "Grows in: {0}  -  {1} energy/min";

        public const string CodexButtonFormat = "Codex  {0}/{1}";
        public const string CodexTitle = "Codex";
        public const string CodexProgressFormat = "{0} of {1} found  -  {2}%";
        public const string CodexClose = "Close";
        public const string CodexPlants = "Plants";
        public const string CodexCreatures = "Creatures";
        public const string Unknown = "???";
        public const string Plant = "Plant";
        public const string LivingHereFormat = "Living here now: {0}";
        public const string GrowingHereFormat = "Growing here now: {0}";
        public const string NotFoundYet = "Not found yet";

        public const string NewPlant = "New plant grown!";
        public const string NewCreature = "New creature discovered!";
        public const string NewRareCreatureFormat = "A {0} creature appeared!";
        public const string RewardFormat = "+{0} energy";
        public const string CreatureTapFormat = "{0}  -  {1}";

        public static string Rarity(TinyPlanet.Simulation.Rarity rarity)
        {
            switch (rarity)
            {
                case TinyPlanet.Simulation.Rarity.Uncommon: return "Uncommon";
                case TinyPlanet.Simulation.Rarity.Rare: return "Rare";
                case TinyPlanet.Simulation.Rarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        public const string DebugAddEnergy = "+100";
        public const string DebugSkipMinute = "+1 min";
        public const string DebugSkipHour = "+1 hour";
        public const string DebugReset = "Reset";
        public const string DebugUnlockAll = "Unlock all";
        public const string DebugNewSeed = "New seed";

        public static string Duration(double seconds)
        {
            if (seconds < 60) return ((int)System.Math.Ceiling(seconds)) + "s";
            if (seconds < 3600) return ((int)System.Math.Ceiling(seconds / 60)) + "m";
            return (seconds / 3600).ToString("0.#") + "h";
        }
    }
}
