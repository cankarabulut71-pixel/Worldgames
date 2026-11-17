using System;
using System.IO;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>Reads and writes the single local save file.</summary>
    public static class SaveStore
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load()
        {
            try
            {
                return File.Exists(FilePath) ? SaveSerializer.FromJson(File.ReadAllText(FilePath)) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read the save file: " + exception.Message);
                return null;
            }
        }

        public static void Write(SaveData data)
        {
            try
            {
                // Write to a temporary file first, so an interrupted write never corrupts the real save.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, SaveSerializer.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not write the save file: " + exception.Message);
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not delete the save file: " + exception.Message);
            }
        }
    }
}
