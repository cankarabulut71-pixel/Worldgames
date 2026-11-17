using System;
using System.IO;
using System.Text;
using TinyPlanet.Simulation;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Reads and writes the single local save file, which holds every planet. Every write goes to a temporary
    /// file first and the last good save is kept as a backup, so a crash, a full disk or a damaged file never
    /// costs the player their garden. A file that can't be read is put aside rather than overwritten.
    /// </summary>
    public static class SaveStore
    {
        private static string Folder => Application.persistentDataPath;
        private static string MainPath => Path.Combine(Folder, "save.json");
        private static string TempPath => MainPath + ".tmp";
        private static string BackupPath => Path.Combine(Folder, "save.backup.json");

        public static GameSave Load() => Load(Folder);

        /// <summary>Loads from a folder, trying the save, then an unfinished write, then the backup.</summary>
        public static GameSave Load(string folder)
        {
            string main = Path.Combine(folder, "save.json");
            string[] candidates = { main, main + ".tmp", Path.Combine(folder, "save.backup.json") };
            foreach (string path in candidates)
            {
                var game = TryRead(path);
                if (game == null) continue;
                if (path != main) Debug.LogWarning("The save file could not be read; restored from " + Path.GetFileName(path) + ".");
                return game;
            }

            // Nothing readable. Keep the damaged file (it may be from a newer version of the game), so the
            // fresh start that follows doesn't overwrite it.
            if (File.Exists(main))
            {
                try
                {
                    string aside = Path.Combine(folder, "save.unreadable-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Move(main, aside);
                    Debug.LogWarning("The save file could not be read and was kept as " + Path.GetFileName(aside) + ".");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not put the unreadable save aside: " + exception.Message);
                }
            }
            return null;
        }

        public static void Write(GameSave data) => Write(Folder, data);

        public static void Write(string folder, GameSave data)
        {
            string main = Path.Combine(folder, "save.json");
            string temp = main + ".tmp";
            string backup = Path.Combine(folder, "save.backup.json");
            try
            {
                // 1. The new save goes to a temporary file, flushed to the disk.
                WriteDurably(temp, SaveSerializer.ToJson(data));
                if (SaveSerializer.GameFromJson(File.ReadAllText(temp)) == null)
                    throw new IOException("the written save did not read back");

                // 2. The temporary file becomes the save. If the app dies in between, Load finds the temporary file.
                if (File.Exists(main)) File.Delete(main);
                File.Move(temp, main);

                // 3. The backup follows the save, so it is always the last good one.
                File.Copy(main, backup, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not write the save file: " + exception.Message);
            }
        }

        public static void Delete()
        {
            foreach (string path in new[] { MainPath, TempPath, BackupPath })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not delete " + Path.GetFileName(path) + ": " + exception.Message);
                }
            }
        }

        private static GameSave TryRead(string path)
        {
            try
            {
                return File.Exists(path) ? SaveSerializer.GameFromJson(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteDurably(string path, string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }
}
