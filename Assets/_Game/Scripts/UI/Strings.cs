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

        public static string Duration(double seconds)
        {
            if (seconds < 60) return ((int)System.Math.Ceiling(seconds)) + "s";
            if (seconds < 3600) return ((int)System.Math.Ceiling(seconds / 60)) + "m";
            return (seconds / 3600).ToString("0.#") + "h";
        }
    }
}
