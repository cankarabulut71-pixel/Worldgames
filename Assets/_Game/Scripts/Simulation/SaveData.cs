using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyPlanet.Simulation
{
    [Serializable]
    public sealed class PlantSave
    {
        public int cell;
        public string specId;
        public long plantedAtUtcTicks;
    }

    [Serializable]
    public sealed class CreatureSave
    {
        public int id;
        public string specId;
        public int homeCell;
        public long arrivedAtUtcTicks;
        public long unhappySinceUtcTicks;
    }

    /// <summary>One planet's state.</summary>
    [Serializable]
    public sealed class SaveData
    {
        // 1: plants and energy. 2: creatures and discoveries. 3: biomes, planet level and unlocks.
        // 4: kept inside a GameSave with other planets; "started" marks a planet that has been visited.
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public int seed;
        public int cellCount;

        /// <summary>False for a planet that was discovered but never opened: it starts fresh on first visit.</summary>
        public bool started = true;
        public double energy;
        public long lastAccrualUtcTicks;
        public long savedAtUtcTicks;
        public double debugClockOffsetSeconds;
        public List<PlantSave> plants = new List<PlantSave>();
        public List<CreatureSave> creatures = new List<CreatureSave>();
        public List<string> discovered = new List<string>();
        public int nextCreatureId = 1;
        public int planetLevel = 1;
        public List<string> unlockedBiomes = new List<string>();
        public List<string> unlockedPlants = new List<string>();

        /// <summary>The format the file was written in, before upgrading. Not saved.</summary>
        [NonSerialized] public int loadedFromVersion = CurrentVersion;
    }

    /// <summary>The whole save file: every planet the player has, and which one they are on.</summary>
    [Serializable]
    public sealed class GameSave
    {
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public int activePlanet;
        public double debugClockOffsetSeconds;
        public List<SaveData> planets = new List<SaveData>();

        public SaveData Active => planets.Count > 0 ? planets[Math.Max(0, Math.Min(activePlanet, planets.Count - 1))] : null;
    }

    public static class SaveSerializer
    {
        [Serializable]
        private sealed class VersionProbe
        {
            public int version;
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);

        public static string ToJson(GameSave data) => JsonUtility.ToJson(data);

        /// <summary>Reads one planet. Returns null when the text is empty, unreadable, or from a newer format.</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return Upgrade(JsonUtility.FromJson<SaveData>(json));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the save file. A file from before multiple planets becomes a game with that one planet.
        /// Returns null when the text is empty, unreadable, or from a newer format.
        /// </summary>
        public static GameSave GameFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var probe = JsonUtility.FromJson<VersionProbe>(json);
                if (probe == null || probe.version < 1 || probe.version > GameSave.CurrentVersion) return null;

                if (probe.version < 4)
                {
                    var single = FromJson(json);
                    if (single == null) return null;
                    var game = new GameSave { debugClockOffsetSeconds = single.debugClockOffsetSeconds };
                    game.planets.Add(single);
                    return game;
                }

                var data = JsonUtility.FromJson<GameSave>(json);
                if (data == null) return null;
                var planets = new List<SaveData>();
                foreach (var planet in data.planets ?? new List<SaveData>())
                {
                    var upgraded = Upgrade(planet);
                    if (upgraded != null) planets.Add(upgraded);
                }
                if (planets.Count == 0) return null;
                data.planets = planets;
                data.activePlanet = Math.Max(0, Math.Min(data.activePlanet, planets.Count - 1));
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SaveData Upgrade(SaveData data)
        {
            if (data == null || data.version < 1 || data.version > SaveData.CurrentVersion) return null;

            // Older saves simply have no creatures, discoveries or unlocks yet.
            if (data.plants == null) data.plants = new List<PlantSave>();
            if (data.creatures == null) data.creatures = new List<CreatureSave>();
            if (data.discovered == null) data.discovered = new List<string>();
            if (data.nextCreatureId < 1) data.nextCreatureId = 1;
            if (data.unlockedBiomes == null) data.unlockedBiomes = new List<string>();
            if (data.unlockedPlants == null) data.unlockedPlants = new List<string>();
            if (data.planetLevel < 1) data.planetLevel = 1;
            // Every planet written before version 4 had been played.
            if (data.version < 4) data.started = true;
            data.loadedFromVersion = data.version;
            data.version = SaveData.CurrentVersion;
            return data;
        }
    }
}
