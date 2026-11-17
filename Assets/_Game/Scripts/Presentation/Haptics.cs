using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Short, gentle vibrations on Android (a tick for planting, a longer pulse for a discovery).
    /// Does nothing in the Editor and on devices without a vibrator.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effectClass;
        private static bool ready;
        private static int sdk;

        private static void Prepare()
        {
            if (ready) return;
            ready = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (sdk >= 26) effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception)
            {
                vibrator = null;
            }
        }

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!Enabled) return;
            Prepare();
            if (vibrator == null) return;
            try
            {
                if (effectClass != null)
                {
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", milliseconds);
            }
            catch (System.Exception)
            {
                vibrator = null;
            }
        }

        // Never called; referencing it makes Unity add the VIBRATE permission to the Android manifest.
        private static void RequestPermission() => Handheld.Vibrate();
#else
        private static void Vibrate(long milliseconds, int amplitude) { }
#endif

        /// <summary>A tiny tick: planting, picking a seed.</summary>
        public static void Light() => Vibrate(12, 60);

        /// <summary>A firmer tap: a plant grown, a creature petted.</summary>
        public static void Medium() => Vibrate(22, 120);

        /// <summary>A celebration: a discovery, a level up.</summary>
        public static void Strong() => Vibrate(45, 200);
    }
}
