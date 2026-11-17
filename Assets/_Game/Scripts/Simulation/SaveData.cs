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

    [Serializable]
    public sealed class SaveData
    {
        // 1: plants and energy. 2: creatures and discoveries.
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int seed;
        public int cellCount;
        public double energy;
        public long lastAccrualUtcTicks;
        public long savedAtUtcTicks;
        public double debugClockOffsetSeconds;
        public List<PlantSave> plants = new List<PlantSave>();
        public List<CreatureSave> creatures = new List<CreatureSave>();
        public List<string> discovered = new List<string>();
        public int nextCreatureId = 1;
    }

    public static class SaveSerializer
    {
        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);

        /// <summary>Returns null when the text is empty, unreadable, or from a newer save format.</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.version < 1 || data.version > SaveData.CurrentVersion) return null;

                // Older saves simply have no creatures or discoveries yet.
                if (data.plants == null) data.plants = new List<PlantSave>();
                if (data.creatures == null) data.creatures = new List<CreatureSave>();
                if (data.discovered == null) data.discovered = new List<string>();
                if (data.nextCreatureId < 1) data.nextCreatureId = 1;
                data.version = SaveData.CurrentVersion;
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
