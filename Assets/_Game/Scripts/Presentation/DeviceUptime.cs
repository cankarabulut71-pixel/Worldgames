using System;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Time since the device started, including time asleep. Changing the date in the settings doesn't move it,
    /// which is what lets the game clock tell real time passing from a clock being moved.
    /// </summary>
    public static class DeviceUptime
    {
        private static int cachedFrame = -1;
        private static long cachedTicks;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass systemClock;
#endif

        public static long Ticks
        {
            get
            {
                // Read once a frame: the clock is asked for "now" many times while a frame is simulated.
                int frame = Time.frameCount;
                if (frame != cachedFrame)
                {
                    cachedFrame = frame;
                    cachedTicks = Read();
                }
                return cachedTicks;
            }
        }

        /// <summary>Forgets the value read this frame, for when the app has just come back from the background.</summary>
        public static void Refresh() => cachedFrame = -1;

        private static long Read()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (systemClock == null) systemClock = new AndroidJavaClass("android.os.SystemClock");
                return systemClock.CallStatic<long>("elapsedRealtime") * TimeSpan.TicksPerMillisecond;
            }
            catch (Exception)
            {
                return MonotonicTicks();
            }
#else
            return MonotonicTicks();
#endif
        }

        // Elsewhere (the editor, desktop): the high-resolution timer, which on Windows keeps counting in sleep.
        private static long MonotonicTicks() =>
            (long)(System.Diagnostics.Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency));
    }
}
