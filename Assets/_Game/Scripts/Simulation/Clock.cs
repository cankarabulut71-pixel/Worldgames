using System;

namespace TinyPlanet.Simulation
{
    /// <summary>Source of "now". Injected so growth and offline progress can be tested and fast-forwarded.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Wraps another clock and adds an offset. Used by the debug "skip time" buttons.</summary>
    public sealed class OffsetClock : IClock
    {
        private readonly IClock inner;

        public OffsetClock(IClock inner, double offsetSeconds = 0)
        {
            this.inner = inner;
            OffsetSeconds = offsetSeconds;
        }

        public double OffsetSeconds { get; set; }

        public DateTime UtcNow => inner.UtcNow.AddSeconds(OffsetSeconds);
    }
}
