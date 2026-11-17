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
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int seed;
        public int cellCount;
        public double energy;
        public long lastAccrualUtcTicks;
        public long savedAtUtcTicks;
        public double debugClockOffsetSeconds;
        public List<PlantSave> plants = new List<PlantSave>();
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
                if (data.plants == null) data.plants = new List<PlantSave>();
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
