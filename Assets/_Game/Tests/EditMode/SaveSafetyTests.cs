using System;
using System.IO;
using NUnit.Framework;
using TinyPlanet.Presentation;
using TinyPlanet.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyPlanet.Tests
{
    public class SaveSafetyTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        private ManualClock device;
        private long uptime;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            device = new ManualClock { UtcNow = Noon };
            uptime = TimeSpan.FromHours(5).Ticks;
            folder = Path.Combine(Path.GetTempPath(), "TinyPlanetSaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        private TrustedClock NewClock(ClockState state = null) => new TrustedClock(device, () => uptime, state);

        // Real time passing: the device clock and the uptime move together.
        private void Wait(TimeSpan time)
        {
            device.UtcNow += time;
            uptime += time.Ticks;
        }

        // ---------- the clock ----------

        [Test]
        public void Clock_FollowsTheDevice_WhenNobodyTouchesIt()
        {
            var clock = NewClock();
            Assert.AreEqual(Noon, clock.UtcNow);
            Wait(TimeSpan.FromHours(30));
            Assert.AreEqual(Noon.AddHours(30), clock.UtcNow);
        }

        [Test]
        public void Clock_MovedForward_DoesNotFastForwardTheGame()
        {
            var clock = NewClock();
            _ = clock.UtcNow;
            Wait(TimeSpan.FromMinutes(10));
            device.UtcNow += TimeSpan.FromDays(3); // the player sets the date three days ahead

            var now = clock.UtcNow;
            Assert.That(now, Is.EqualTo(Noon.AddMinutes(10)).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Clock_MovedBack_NeverRunsTheGameBackwards()
        {
            var clock = NewClock();
            Wait(TimeSpan.FromHours(1));
            var before = clock.UtcNow;
            device.UtcNow -= TimeSpan.FromDays(2);
            Wait(TimeSpan.FromMinutes(5));
            Assert.That(clock.UtcNow, Is.EqualTo(before.AddMinutes(5)).Within(TimeSpan.FromSeconds(20)));
        }

        [Test]
        public void Clock_SmallCorrections_AreTakenAsTheyAre()
        {
            var clock = NewClock();
            _ = clock.UtcNow;
            Wait(TimeSpan.FromMinutes(30));
            device.UtcNow += TimeSpan.FromSeconds(40); // network time sync
            Assert.AreEqual(Noon.AddMinutes(30).AddSeconds(40), clock.UtcNow);
        }

        [Test]
        public void Clock_RemembersAcrossSessions_AndUsesTheUptimeForTheTimeAway()
        {
            var state = new ClockState();
            _ = NewClock(state).UtcNow;
            var saved = SaveSerializer.GameFromJson(SaveSerializer.ToJson(Game(state)));

            // App closed for 8 hours, during which the date was moved a week ahead.
            Wait(TimeSpan.FromHours(8));
            device.UtcNow += TimeSpan.FromDays(7);
            var later = NewClock(saved.clock);
            Assert.That(later.UtcNow, Is.EqualTo(Noon.AddHours(8)).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Clock_SkewWearsOff_SoAWrongClockThatWasFixedSettlesBack()
        {
            var clock = NewClock();
            _ = clock.UtcNow;
            device.UtcNow += TimeSpan.FromHours(1);
            _ = clock.UtcNow;
            Assert.Less(clock.State.skewTicks, 0);

            for (int i = 0; i < 200; i++)
            {
                Wait(TimeSpan.FromMinutes(10));
                _ = clock.UtcNow;
            }
            Assert.AreEqual(0, clock.State.skewTicks);
            Assert.AreEqual(device.UtcNow, clock.UtcNow);
        }

        [Test]
        public void Clock_AfterARestart_TrustsTheDevice_ButNeverGoesBack()
        {
            var state = new ClockState();
            _ = NewClock(state).UtcNow;

            uptime = TimeSpan.FromMinutes(3).Ticks; // restarted
            device.UtcNow = Noon.AddHours(6);
            Assert.AreEqual(Noon.AddHours(6), NewClock(state).UtcNow);

            uptime = TimeSpan.FromMinutes(1).Ticks; // restarted again, clock set into the past
            device.UtcNow = Noon.AddHours(-10);
            Assert.AreEqual(Noon.AddHours(6), NewClock(state).UtcNow);
        }

        [Test]
        public void OldSaves_LoadWithAFreshClock()
        {
            var save = SaveSerializer.GameFromJson("{\"version\":4,\"planets\":[{\"version\":4,\"seed\":3,\"energy\":5}]}");
            Assert.IsNotNull(save);
            Assert.IsNotNull(save.clock);
            Assert.AreEqual(0, save.clock.lastUtcTicks);
        }

        // ---------- the file ----------

        private static GameSave Game(ClockState state = null, double energy = 42)
        {
            var game = new GameSave { clock = state ?? new ClockState() };
            game.planets.Add(new SaveData { seed = 7, energy = energy });
            return game;
        }

        private string FilePath(string name) => Path.Combine(folder, name);

        [Test]
        public void Write_KeepsABackupOfTheLastGoodSave()
        {
            SaveStore.Write(folder, Game(energy: 1));
            SaveStore.Write(folder, Game(energy: 2));
            Assert.IsTrue(File.Exists(FilePath("save.backup.json")));
            Assert.IsFalse(File.Exists(FilePath("save.json.tmp")));
            Assert.AreEqual(2, SaveStore.Load(folder).planets[0].energy);
        }

        [Test]
        public void Load_FallsBackToTheBackup_WhenTheSaveIsDamaged()
        {
            SaveStore.Write(folder, Game(energy: 5));
            File.WriteAllText(FilePath("save.json"), "{\"version\":4,\"planets\":[{\"seed\":");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("restored from save.backup.json"));
            Assert.AreEqual(5, SaveStore.Load(folder).planets[0].energy);
        }

        [Test]
        public void Load_FindsAWriteThatWasCutShort()
        {
            // The app died after the old save was removed and before the new one took its place.
            SaveStore.Write(folder, Game(energy: 1));
            File.WriteAllText(FilePath("save.json.tmp"), SaveSerializer.ToJson(Game(energy: 9)));
            File.Delete(FilePath("save.json"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("restored from save.json.tmp"));
            Assert.AreEqual(9, SaveStore.Load(folder).planets[0].energy);
        }

        [Test]
        public void Load_PutsAnUnreadableSaveAside_InsteadOfLosingIt()
        {
            File.WriteAllText(FilePath("save.json"), "{\"version\":99,\"planets\":[]}");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("kept as save.unreadable-"));
            Assert.IsNull(SaveStore.Load(folder));
            Assert.IsFalse(File.Exists(FilePath("save.json")));
            Assert.AreEqual(1, Directory.GetFiles(folder, "save.unreadable-*.json").Length);
        }
    }
}
