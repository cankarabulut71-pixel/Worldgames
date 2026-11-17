using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>What a land sounds like.</summary>
    public enum Ambience
    {
        Birds,
        Marsh,
        Wind,
    }

    /// <summary>
    /// Plays the game's sounds: one-shots for actions, a music loop, and an ambient bed (wind, water, birds,
    /// frogs) that follows whichever land the camera is looking at. All clips come from SoundSynth; the long
    /// loops are made on a worker thread and fade in when ready.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const float MusicVolume = 0.32f;
        private const float WindVolume = 0.22f;
        private const float WaterVolume = 0.2f;
        private const float RainVolume = 0.3f;

        private readonly AudioSource[] voices = new AudioSource[8];
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly AudioClip[] chirps = new AudioClip[6];
        private readonly AudioClip[] discover = new AudioClip[4];
        private readonly AudioClip[] birds = new AudioClip[4];
        private readonly AudioClip[] crickets = new AudioClip[3];
        private int nextVoice;
        private float lastGrow = -1f;

        private AudioSource music;
        private AudioSource wind;
        private AudioSource water;
        private AudioSource rain;
        private Task<float[]> rainTask;
        private float night;
        private bool raining;
        private Task<float[]> musicTask;
        private Task<float[]> windTask;
        private Task<float[]> waterTask;

        private Func<Ambience> currentAmbience;
        private Ambience ambience = Ambience.Birds;
        private float ambienceTimer;
        private float callTimer = 3f;

        public void Init(Func<Ambience> ambienceInView)
        {
            currentAmbience = ambienceInView;

            for (int i = 0; i < voices.Length; i++) voices[i] = Source("Voice " + i, loop: false);
            music = Source("Music", loop: true);
            wind = Source("Wind", loop: true);
            water = Source("Water", loop: true);
            rain = Source("Rain", loop: true);

            clips["plant"] = Clip("Plant", SoundSynth.Plant());
            clips["grow"] = Clip("Grow", SoundSynth.Grow());
            clips["unlock"] = Clip("Unlock", SoundSynth.Unlock());
            clips["level"] = Clip("Level Up", SoundSynth.LevelUp());
            clips["click"] = Clip("Click", SoundSynth.Click());
            clips["deny"] = Clip("Deny", SoundSynth.Deny());
            clips["frog"] = Clip("Frog", SoundSynth.Frog());
            clips["plink"] = Clip("Plink", SoundSynth.Plink());
            for (int i = 0; i < chirps.Length; i++) chirps[i] = Clip("Chirp " + i, SoundSynth.Chirp(i));
            for (int i = 0; i < discover.Length; i++) discover[i] = Clip("Discover " + i, SoundSynth.Discover(i));
            for (int i = 0; i < birds.Length; i++) birds[i] = Clip("Bird " + i, SoundSynth.Bird(i));
            for (int i = 0; i < crickets.Length; i++) crickets[i] = Clip("Cricket " + i, SoundSynth.Cricket(i));

            musicTask = Task.Run(() => SoundSynth.Music());
            windTask = Task.Run(() => SoundSynth.Wind());
            waterTask = Task.Run(() => SoundSynth.Water());
            rainTask = Task.Run(() => SoundSynth.Rain());
        }

        /// <summary>How dark it is (0 day, 1 night) and whether it rains: crickets replace birds, rain hisses.</summary>
        public void SetWeather(float nightAmount, bool isRaining)
        {
            night = nightAmount;
            raining = isRaining;
        }

        public void Plant() => Play(clips["plant"], 0.55f, Variation(0.08f));

        public void Grow()
        {
            // Many plants can finish at once (after time away): one chime stands for them all.
            if (Time.unscaledTime - lastGrow < 0.2f) return;
            lastGrow = Time.unscaledTime;
            Play(clips["grow"], 0.3f, Variation(0.05f));
        }

        public void Discover(int rarity) => Play(discover[Mathf.Clamp(rarity, 0, discover.Length - 1)], 0.6f, 1f);

        public void Unlock() => Play(clips["unlock"], 0.5f, 1f);

        public void LevelUp() => Play(clips["level"], 0.55f, 1f);

        public void Click() => Play(clips["click"], 0.18f, Variation(0.05f));

        public void Deny() => Play(clips["deny"], 0.35f, 1f);

        /// <summary>A creature's voice: its shape picks the sound, its size the pitch.</summary>
        public void Creature(int shape, float size) =>
            Play(chirps[Mathf.Clamp(shape, 0, chirps.Length - 1)], 0.45f, Mathf.Clamp(1.15f / Mathf.Max(0.5f, size), 0.6f, 1.6f) * Variation(0.06f));

        private void Update()
        {
            StartLoop(ref musicTask, music, "Music");
            StartLoop(ref windTask, wind, "Wind");
            StartLoop(ref waterTask, water, "Water");
            StartLoop(ref rainTask, rain, "Rain");

            ambienceTimer -= Time.unscaledDeltaTime;
            if (ambienceTimer <= 0f && currentAmbience != null)
            {
                ambienceTimer = 0.5f;
                ambience = currentAmbience();
            }

            // Ease the beds towards the land in view.
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.5f);
            if (music.isPlaying) music.volume = Mathf.Lerp(music.volume, MusicVolume, k * 0.5f);
            if (wind.isPlaying) wind.volume = Mathf.Lerp(wind.volume, ambience == Ambience.Wind ? WindVolume : WindVolume * 0.3f, k);
            if (water.isPlaying) water.volume = Mathf.Lerp(water.volume, ambience == Ambience.Marsh ? WaterVolume : 0f, k);
            if (rain.isPlaying) rain.volume = Mathf.Lerp(rain.volume, raining ? RainVolume : 0f, k * 0.6f);

            callTimer -= Time.unscaledDeltaTime;
            if (callTimer <= 0f)
            {
                callTimer = UnityEngine.Random.Range(4f, 10f);
                switch (ambience)
                {
                    case Ambience.Birds when night > 0.5f:
                    case Ambience.Wind when night > 0.5f:
                        Play(crickets[UnityEngine.Random.Range(0, crickets.Length)], 0.1f, Variation(0.05f));
                        break;
                    case Ambience.Birds when !raining: Play(birds[UnityEngine.Random.Range(0, birds.Length)], 0.12f, Variation(0.1f)); break;
                    case Ambience.Marsh:
                        Play(UnityEngine.Random.value < 0.6f ? clips["frog"] : clips["plink"], 0.14f, Variation(0.12f));
                        break;
                }
            }
        }

        private void StartLoop(ref Task<float[]> task, AudioSource source, string name)
        {
            if (task == null || !task.IsCompleted) return;
            if (task.Status == TaskStatus.RanToCompletion)
            {
                source.clip = Clip(name, task.Result);
                source.volume = 0f;
                // Start the loops at different points, so they never line up the same way twice.
                source.time = UnityEngine.Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
            task = null;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.clip = clip;
            voice.Play();
        }

        private static float Variation(float amount) => 1f + UnityEngine.Random.Range(-amount, amount);

        private AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SoundSynth.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
