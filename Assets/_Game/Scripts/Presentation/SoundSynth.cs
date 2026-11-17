using System;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Makes every sound in the game from code: soft bells, chirps, wind, water and a calm music loop.
    /// Returns raw mono samples so the long loops can be made on a worker thread; AudioDirector turns them
    /// into clips. Original sounds, no recorded or third-party audio.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 22050;

        private const int TableSize = 4096;
        private static readonly float[] SinTable = BuildTable();

        // Pentatonic notes (C major) used by every melodic sound, so they never clash with the music.
        private static readonly int[] Pentatonic = { 72, 76, 79, 81, 84, 86, 88, 91, 93, 96 };

        public static float[] Plant()
        {
            var b = Buffer(0.3f);
            Tone(b, 0f, 0.2f, 520f, 170f, 0.55f, 0.003f, 20f);
            Noise(b, 0f, 0.14f, 0.22f, 0.002f, 28f, 0.25f, 1);
            return Finish(b);
        }

        public static float[] Grow()
        {
            var b = Buffer(1f);
            Tone(b, 0f, 0.8f, Midi(79), Midi(79), 0.2f, 0.004f, 6f, 0.25f);
            Tone(b, 0.08f, 0.9f, Midi(84), Midi(84), 0.2f, 0.004f, 5f, 0.25f);
            return Finish(b);
        }

        /// <summary>A rising arpeggio: longer and brighter for rarer creatures (rarity 0 to 3).</summary>
        public static float[] Discover(int rarity)
        {
            int count = Math.Min(Pentatonic.Length - 1, 3 + rarity);
            const float spacing = 0.085f;
            var b = Buffer(spacing * count + 1.8f);
            for (int i = 0; i < count; i++)
                Tone(b, i * spacing, 1.6f, Midi(Pentatonic[i]), Midi(Pentatonic[i]), 0.22f, 0.004f, 3f, 0.3f, 0.1f);
            if (rarity >= 2)
            {
                float end = count * spacing;
                Tone(b, end, 1.6f, Midi(Pentatonic[count] + 12), Midi(Pentatonic[count] + 12), 0.1f, 0.01f, 2.4f, 0f, 0f, 6f, 0.004f);
            }
            return Finish(b);
        }

        public static float[] Unlock()
        {
            var b = Buffer(1.8f);
            int[] notes = { 79, 84, 88, 91 };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.06f, 0.6f, Midi(notes[i]), Midi(notes[i]), 0.16f, 0.003f, 6f, 0.2f);
            foreach (int note in new[] { 84, 88, 91 })
                Tone(b, 0.26f, 1.5f, Midi(note), Midi(note), 0.13f, 0.01f, 2.2f, 0.25f);
            return Finish(b);
        }

        public static float[] LevelUp()
        {
            var b = Buffer(3f);
            foreach (int note in new[] { 60, 64, 67, 72 })
                Tone(b, 0f, 2.8f, Midi(note), Midi(note), 0.12f, 0.18f, 1.3f, 0.3f, 0.1f);
            int[] arpeggio = { 84, 88, 91, 96 };
            for (int i = 0; i < arpeggio.Length; i++)
                Tone(b, 0.12f + i * 0.1f, 1.6f, Midi(arpeggio[i]), Midi(arpeggio[i]), 0.14f, 0.004f, 3f, 0.25f);
            return Finish(b);
        }

        public static float[] Click()
        {
            var b = Buffer(0.06f);
            Tone(b, 0f, 0.06f, 1700f, 1300f, 0.3f, 0.001f, 70f);
            return Finish(b);
        }

        public static float[] Deny()
        {
            var b = Buffer(0.4f);
            Tone(b, 0f, 0.14f, 330f, 300f, 0.3f, 0.005f, 14f, 0f, 0.15f);
            Tone(b, 0.12f, 0.22f, 262f, 240f, 0.3f, 0.005f, 12f, 0f, 0.15f);
            return Finish(b);
        }

        /// <summary>The little noise a creature makes when touched, by body shape (CreatureShape order).</summary>
        public static float[] Chirp(int shape)
        {
            var b = Buffer(1.3f);
            switch (shape)
            {
                case 0: // hopper: a springy boing
                    Tone(b, 0f, 0.18f, 380f, 780f, 0.4f, 0.005f, 11f, 0.2f);
                    break;
                case 1: // flutter: a soft trill
                    Tone(b, 0f, 0.24f, 2400f, 2650f, 0.22f, 0.005f, 9f, 0f, 0f, 35f, 0.06f);
                    break;
                case 2: // crawler: two squeaks
                    Tone(b, 0f, 0.12f, 900f, 1400f, 0.28f, 0.004f, 16f);
                    Tone(b, 0.13f, 0.11f, 1000f, 1500f, 0.25f, 0.004f, 16f);
                    break;
                case 3: // climber: a quick chitter
                    for (int i = 0; i < 3; i++) Tone(b, i * 0.085f, 0.07f, 1500f, 1950f, 0.25f, 0.003f, 22f);
                    break;
                case 4: // glow: a shimmering bell
                    Tone(b, 0f, 1.2f, Midi(91), Midi(91), 0.16f, 0.004f, 3f, 0.3f, 0f, 5f, 0.003f);
                    Tone(b, 0.06f, 1.2f, Midi(96), Midi(96), 0.12f, 0.004f, 3f, 0.3f, 0f, 5f, 0.003f);
                    break;
                default: // shell: a round bloop
                    Tone(b, 0f, 0.3f, 260f, 420f, 0.4f, 0.01f, 6f, 0.4f);
                    break;
            }
            return Finish(b);
        }

        /// <summary>A short bird call; the variant picks one of several patterns.</summary>
        public static float[] Bird(int variant)
        {
            var random = new Random(variant * 7919 + 3);
            var b = Buffer(1f);
            int calls = 2 + random.Next(3);
            float time = 0f;
            float high = 3000f + (float)random.NextDouble() * 900f;
            for (int i = 0; i < calls; i++)
            {
                float length = 0.05f + (float)random.NextDouble() * 0.05f;
                float low = high * (0.6f + (float)random.NextDouble() * 0.2f);
                Tone(b, time, length, high, low, 0.16f, 0.004f, 14f, 0.1f);
                time += length + 0.05f + (float)random.NextDouble() * 0.08f;
            }
            return Finish(b);
        }

        public static float[] Frog()
        {
            var b = Buffer(0.8f);
            Tone(b, 0f, 0.18f, 150f, 125f, 0.3f, 0.01f, 6f, 0.6f, 0.4f, 28f, 0.25f);
            Tone(b, 0.26f, 0.2f, 140f, 120f, 0.3f, 0.01f, 6f, 0.6f, 0.4f, 28f, 0.25f);
            return Finish(b);
        }

        public static float[] Plink()
        {
            var b = Buffer(0.15f);
            Tone(b, 0f, 0.1f, 900f, 1800f, 0.25f, 0.002f, 30f);
            return Finish(b);
        }

        /// <summary>Endless soft wind, seamless when looped.</summary>
        public static float[] Wind(float seconds = 12f)
        {
            int length = (int)(seconds * Rate);
            int fade = Rate;
            var raw = new float[length + fade];
            var random = new Random(11);
            float low = 0f, lower = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                // Slow gusts, a whole number of cycles per loop so the swell lines up at the seam.
                float gust = 0.55f + 0.3f * Sin(t / seconds * 2f) + 0.15f * Sin(t / seconds * 5f + 0.3f);
                float cutoff = 0.02f + 0.03f * gust;
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * cutoff;
                lower += (low - lower) * cutoff;
                raw[i] = lower * gust * 6f;
            }
            return Normalise(Seamless(raw, length, fade), 0.5f);
        }

        /// <summary>Steady rain on leaves with scattered drops, seamless when looped.</summary>
        public static float[] Rain(float seconds = 8f)
        {
            int length = (int)(seconds * Rate);
            int fade = Rate;
            var raw = new float[length + fade];
            var random = new Random(37);
            float low = 0f, lower = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float swell = 0.8f + 0.2f * Sin(t / seconds * 2f);
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * 0.3f;
                lower += (low - lower) * 0.05f;
                // Hiss from the band between the two filters, a soft rumble from the lowest one.
                raw[i] = ((low - lower) * 0.7f + lower * 1.5f) * swell;
            }
            for (float t = 0.05f; t < seconds - 0.1f; t += 0.02f + (float)random.NextDouble() * 0.09f)
            {
                float pitch = 2200f + (float)random.NextDouble() * 2400f;
                Tone(raw, t, 0.025f, pitch, pitch * 0.7f, 0.04f + (float)random.NextDouble() * 0.05f, 0.001f, 70f);
            }
            return Normalise(Seamless(raw, length, fade), 0.45f);
        }

        /// <summary>A cricket: two quick trills; the variant changes its pitch and rhythm.</summary>
        public static float[] Cricket(int variant)
        {
            var random = new Random(variant * 104729 + 17);
            var b = Buffer(1.1f);
            float pitch = 4000f + (float)random.NextDouble() * 900f;
            int pulses = 3 + random.Next(2);
            float gap = 0.045f + (float)random.NextDouble() * 0.02f;
            for (int chirp = 0; chirp < 2; chirp++)
            {
                float start = chirp * (0.4f + (float)random.NextDouble() * 0.15f);
                for (int p = 0; p < pulses; p++)
                    Tone(b, start + p * gap, 0.03f, pitch, pitch * 0.98f, 0.14f, 0.003f, 30f, 0.2f);
            }
            return Finish(b);
        }

        /// <summary>Lapping water with the odd bubble, seamless when looped.</summary>
        public static float[] Water(float seconds = 10f)
        {
            int length = (int)(seconds * Rate);
            int fade = Rate;
            var raw = new float[length + fade];
            var random = new Random(23);
            float low = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float lap = 0.5f + 0.5f * Sin(t * 0.45f) * Sin(t * 0.17f + 0.2f);
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * 0.08f;
                raw[i] = low * (0.4f + 0.6f * lap) * 2.5f;
            }
            for (float t = 0.4f; t < seconds - 0.3f; t += 0.35f + (float)random.NextDouble() * 1.4f)
            {
                float pitch = 700f + (float)random.NextDouble() * 900f;
                Tone(raw, t, 0.08f, pitch, pitch * 1.8f, 0.12f, 0.002f, 35f);
            }
            return Normalise(Seamless(raw, length, fade), 0.45f);
        }

        /// <summary>
        /// A calm loop: four soft pad chords that blend into each other, a low root, and a few bell notes
        /// from the pentatonic scale. About half a minute, seamless when looped.
        /// </summary>
        public static float[] Music(int seed = 5)
        {
            int[][] chords =
            {
                new[] { 48, 52, 55, 59, 62 }, // C maj9
                new[] { 45, 48, 52, 55, 59 }, // A min9
                new[] { 41, 45, 48, 52, 55 }, // F maj9
                new[] { 43, 47, 50, 52, 57 }, // G 6/9
            };
            const float chordSeconds = 8f;
            const float blend = 1.6f;
            float loop = chords.Length * chordSeconds;
            int length = (int)(loop * Rate);
            // One extra second, crossfaded into the start at the end, hides the seam of the pad's sine waves.
            var b = new float[length + Rate];

            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float sample = 0f;
                for (int c = 0; c < chords.Length; c++)
                {
                    // Distance to this chord's middle, measured around the loop so the last fades into the first.
                    float centre = (c + 0.5f) * chordSeconds;
                    float d = Math.Abs(t - centre);
                    d = Math.Min(d, loop - d);
                    float weight = Clamp01((chordSeconds * 0.5f + blend * 0.5f - d) / blend);
                    if (weight <= 0f) continue;
                    weight = weight * weight * (3f - 2f * weight);

                    foreach (int note in chords[c])
                    {
                        float f = Midi(note + 12);
                        sample += weight * 0.05f * (Sin(f * t * 1.0015f) + Sin(f * t * 0.9985f) + 0.18f * Sin(f * 2f * t));
                    }
                    float root = Midi(chords[c][0]);
                    sample += weight * 0.07f * Sin(root * 0.5f * t);
                }
                float swell = 0.85f + 0.15f * Sin(t / loop * 4f);
                b[i] = sample * swell;
            }

            var random = new Random(seed);
            for (int c = 0; c < chords.Length; c++)
            {
                int bells = 4 + random.Next(4);
                for (int n = 0; n < bells; n++)
                {
                    float at = c * chordSeconds + random.Next((int)(chordSeconds * 2f)) * 0.5f;
                    if (at > loop - 3f) continue; // keep the loop seam clean
                    int note = Pentatonic[random.Next(5)];
                    Tone(b, at, 2.8f, Midi(note), Midi(note), 0.06f, 0.006f, 1.8f, 0.2f, 0.05f);
                }
            }

            // A gentle low-pass for warmth.
            float smooth = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                smooth += (b[i] - smooth) * 0.45f;
                b[i] = smooth;
            }
            return Normalise(Seamless(b, length, Rate), 0.7f);
        }

        // ---------- building blocks ----------

        private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        private static float Midi(int note) => 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);

        /// <summary>A sine of <paramref name="cycles"/> whole turns, from a table.</summary>
        private static float Sin(double cycles)
        {
            double fraction = cycles - Math.Floor(cycles);
            return SinTable[(int)(fraction * TableSize) & (TableSize - 1)];
        }

        private static float[] BuildTable()
        {
            var table = new float[TableSize];
            for (int i = 0; i < TableSize; i++) table[i] = (float)Math.Sin(i * 2.0 * Math.PI / TableSize);
            return table;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        /// <summary>
        /// Adds a note that glides from <paramref name="from"/> to <paramref name="to"/> Hz, with a quick attack and
        /// an exponential decay, optional 2nd and 3rd harmonics and a vibrato.
        /// </summary>
        private static void Tone(float[] buffer, float start, float duration, float from, float to, float amplitude,
            float attack, float decay, float second = 0f, float third = 0f, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int first = (int)(start * Rate);
            int count = (int)(duration * Rate);
            double phase = 0.0;
            for (int i = 0; i < count && first + i < buffer.Length; i++)
            {
                float t = i / (float)Rate;
                float k = t / duration;
                float glide = 1f - (1f - k) * (1f - k);
                float f = from + (to - from) * glide;
                if (vibratoHz > 0f) f *= 1f + vibratoDepth * Sin(vibratoHz * t);
                phase += f / Rate;

                float envelope = Math.Min(1f, t / attack) * (float)Math.Exp(-t * decay);
                // Fade the last few milliseconds so nothing clicks when a note is cut short.
                float tail = Math.Min(1f, (count - i) / (Rate * 0.004f));
                float value = Sin(phase) + second * Sin(phase * 2.0) + third * Sin(phase * 3.0);
                buffer[first + i] += amplitude * envelope * tail * value;
            }
        }

        private static void Noise(float[] buffer, float start, float duration, float amplitude, float attack, float decay, float smoothing, int seed)
        {
            var random = new Random(seed);
            int first = (int)(start * Rate);
            int count = (int)(duration * Rate);
            float low = 0f;
            for (int i = 0; i < count && first + i < buffer.Length; i++)
            {
                float t = i / (float)Rate;
                low += ((float)random.NextDouble() * 2f - 1f - low) * smoothing;
                float envelope = Math.Min(1f, t / attack) * (float)Math.Exp(-t * decay);
                buffer[first + i] += amplitude * envelope * low * 2f;
            }
        }

        // Crossfades the extra samples past the loop end into its start, so the loop has no seam.
        private static float[] Seamless(float[] raw, int length, int fade)
        {
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (i < fade)
                {
                    float w = i / (float)fade;
                    result[i] = raw[i] * w + raw[length + i] * (1f - w);
                }
                else result[i] = raw[i];
            }
            return result;
        }

        private static float[] Normalise(float[] buffer, float peak)
        {
            float loudest = 1e-5f;
            foreach (float value in buffer) loudest = Math.Max(loudest, Math.Abs(value));
            float gain = peak / loudest;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
            return buffer;
        }

        private static float[] Finish(float[] buffer) => Normalise(buffer, 0.8f);
    }
}
