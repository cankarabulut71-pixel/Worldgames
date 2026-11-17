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
