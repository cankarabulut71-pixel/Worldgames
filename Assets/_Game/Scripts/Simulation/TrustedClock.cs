using System;

namespace TinyPlanet.Simulation
{
    /// <summary>What a <see cref="TrustedClock"/> remembers between sessions, kept in the save.</summary>
    [Serializable]
    public sealed class ClockState
    {
        /// <summary>Game time minus device time. Zero unless the device clock was moved.</summary>
        public long skewTicks;

        /// <summary>The last game time handed out; game time never goes back past it.</summary>
        public long lastUtcTicks;

        /// <summary>Time since the device started, when <see cref="lastUtcTicks"/> was handed out.</summary>
        public long lastUptimeTicks;
    }

    /// <summary>
    /// Game time that follows the device clock but cannot be fast-forwarded by changing it. While the device has
    /// not restarted, its uptime says how much real time has passed; a clock jump beyond that is absorbed into a
    /// skew, so the garden neither gains nor loses from it. A skew wears off slowly as real time passes, so a
    /// clock that was simply wrong and then fixed settles back to the device time. After a restart the uptime
    /// starts over and can't be compared, so the device clock is trusted again, except that game time never
    /// runs backwards.
    /// </summary>
    public sealed class TrustedClock : IClock
    {
        /// <summary>Clock corrections smaller than this (network time sync, drift) are taken as they are.</summary>
        public static readonly long SlackTicks = TimeSpan.FromMinutes(2).Ticks;

        /// <summary>How much of a skew wears off per unit of real time.</summary>
        public const double HealRate = 0.05;

        private readonly IClock device;
        private readonly Func<long> uptimeTicks;

        public TrustedClock(IClock device, Func<long> uptimeTicks, ClockState state = null)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.uptimeTicks = uptimeTicks ?? throw new ArgumentNullException(nameof(uptimeTicks));
            State = state ?? new ClockState();
        }

        public ClockState State { get; }

        public DateTime UtcNow => new DateTime(Read(), DateTimeKind.Utc);

        private long Read()
        {
            long wall = device.UtcNow.Ticks;
            long uptime = uptimeTicks();
            var state = State;

            if (state.lastUtcTicks <= 0)
            {
                Remember(wall + state.skewTicks, uptime);
                return state.lastUtcTicks;
            }

            long game;
            if (uptime >= state.lastUptimeTicks)
            {
                // Same boot: the uptime tells how much real time has really passed.
                long elapsed = uptime - state.lastUptimeTicks;
                long expected = state.lastUtcTicks + elapsed;
                long candidate = wall + state.skewTicks;
                if (candidate > expected + SlackTicks || candidate < expected - SlackTicks)
                {
                    // The clock was moved: absorb the jump. The skew starts wearing off from the next reading.
                    state.skewTicks += expected - candidate;
                }
                else
                {
                    long heal = (long)(elapsed * HealRate);
                    if (state.skewTicks > 0) state.skewTicks = Math.Max(0, state.skewTicks - heal);
                    else if (state.skewTicks < 0) state.skewTicks = Math.Min(0, state.skewTicks + heal);
                }

                game = wall + state.skewTicks;
            }
            else
            {
                // The device restarted: trust its clock, but never go back in time.
                game = Math.Max(wall + state.skewTicks, state.lastUtcTicks);
                state.skewTicks = game - wall;
            }

            if (game < state.lastUtcTicks)
            {
                game = state.lastUtcTicks;
                state.skewTicks = game - wall;
            }
            Remember(game, uptime);
            return game;
        }

        private void Remember(long game, long uptime)
        {
            State.lastUtcTicks = game;
            State.lastUptimeTicks = uptime;
        }
    }
}
