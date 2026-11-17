using System;

namespace TinyPlanet.Simulation
{
    public sealed class SkySettings
    {
        /// <summary>Local hour when night falls, and when the day comes back.</summary>
        public double NightStartHour = 20;
        public double DayStartHour = 6;

        /// <summary>How long dusk and dawn take, in hours.</summary>
        public double TwilightHours = 1;

        /// <summary>Chance that a whole (local) day is rainy.</summary>
        public double RainChance = 0.25;

        /// <summary>How much faster plants grow while it rains.</summary>
        public double RainGrowthMultiplier = 1.5;
    }

    /// <summary>
    /// Night and day follow the player's own clock; rain falls on whole days picked from the planet's seed.
    /// The forecast is fixed in advance, so growth can be worked out for any stretch of time, including time
    /// spent away from the game.
    /// </summary>
    public sealed class Sky
    {
        private readonly int seed;
        private readonly SkySettings settings;
        private readonly long offsetTicks;

        public Sky(int seed, SkySettings settings, TimeSpan utcOffset)
        {
            this.seed = seed;
            this.settings = settings ?? new SkySettings();
            offsetTicks = utcOffset.Ticks;
        }

        /// <summary>Always day and never rain: plants grow at their plain speed.</summary>
        public static Sky Clear() => new Sky(0, new SkySettings { RainChance = 0, NightStartHour = 24, DayStartHour = 0 }, TimeSpan.Zero);

        /// <summary>Development builds only: force night or rain on (true), off (false), or follow the clock (null).</summary>
        public bool? DebugNight { get; set; }
        public bool? DebugRain { get; set; }

        public long DayIndex(long utcTicks) => FloorDiv(utcTicks + offsetTicks, TimeSpan.TicksPerDay);

        public double LocalHour(long utcTicks)
        {
            long local = utcTicks + offsetTicks;
            long intoDay = local - FloorDiv(local, TimeSpan.TicksPerDay) * TimeSpan.TicksPerDay;
            return intoDay / (double)TimeSpan.TicksPerHour;
        }

        public bool IsNight(long utcTicks)
        {
            if (DebugNight.HasValue) return DebugNight.Value;
            double hour = LocalHour(utcTicks);
            return hour >= settings.NightStartHour || hour < settings.DayStartHour;
        }

        /// <summary>1 in full day, 0 in full night, easing through dusk and dawn. For lighting only.</summary>
        public float Daylight(long utcTicks)
        {
            if (DebugNight.HasValue) return DebugNight.Value ? 0f : 1f;
            double hour = LocalHour(utcTicks);
            double half = Math.Max(0.01, settings.TwilightHours * 0.5);
            // Distance into the day from dawn, and to the end of the day at dusk.
            double dawn = Ramp(HourDistance(settings.DayStartHour, hour), half);
            double dusk = Ramp(HourDistance(hour, settings.NightStartHour), half);
            double light = IsDayHour(hour) ? Math.Min(dawn, dusk) : 1 - Math.Min(1 - dawn, 1 - dusk);
            return (float)Math.Max(0, Math.Min(1, light));
        }

        public bool IsRaining(long utcTicks) => DebugRain ?? IsRainyDay(DayIndex(utcTicks));

        public bool IsRainyDay(long dayIndex)
        {
            if (settings.RainChance <= 0) return false;
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)dayIndex * 0x85EBCA77u ^ (uint)(dayIndex >> 32) * 0xC2B2AE3Du;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return h / (double)uint.MaxValue < settings.RainChance;
            }
        }

        /// <summary>
        /// The moment a plant that starts growing at <paramref name="fromUtcTicks"/> has done
        /// <paramref name="growthSeconds"/> of growing, counting rainy days as faster. Uses the forecast only,
        /// never the debug override, so the answer never changes.
        /// </summary>
        public long AfterGrowth(long fromUtcTicks, double growthSeconds)
        {
            if (growthSeconds <= 0) return fromUtcTicks;
            double fast = Math.Max(1.0, settings.RainGrowthMultiplier);
            if (settings.RainChance <= 0 || fast <= 1.0)
                return fromUtcTicks + (long)(growthSeconds * TimeSpan.TicksPerSecond);

            long time = fromUtcTicks;
            double left = growthSeconds;
            // Walk one local day at a time; even a three-day plant only looks at a handful of days.
            for (int guard = 0; guard < 4000; guard++)
            {
                long day = DayIndex(time);
                long dayEnd = (day + 1) * TimeSpan.TicksPerDay - offsetTicks;
                double rate = IsRainyDay(day) ? fast : 1.0;
                double canGrow = (dayEnd - time) / (double)TimeSpan.TicksPerSecond * rate;
                if (canGrow >= left) return time + (long)(left / rate * TimeSpan.TicksPerSecond);
                left -= canGrow;
                time = dayEnd;
            }
            return time + (long)(left * TimeSpan.TicksPerSecond);
        }

        private bool IsDayHour(double hour) => hour >= settings.DayStartHour && hour < settings.NightStartHour;

        // Hours from a to b going forward round the clock.
        private static double HourDistance(double a, double b)
        {
            double d = (b - a) % 24.0;
            return d < 0 ? d + 24.0 : d;
        }

        // 0.5 exactly at the boundary, easing to 1 one half-twilight inside the day.
        private static double Ramp(double hoursIntoDay, double half)
        {
            double signed = hoursIntoDay > 12 ? hoursIntoDay - 24 : hoursIntoDay;
            double t = Math.Max(0, Math.Min(1, (signed + half) / (2 * half)));
            return t * t * (3 - 2 * t);
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
