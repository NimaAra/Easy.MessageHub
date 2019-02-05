namespace Easy.MessageHub
{
    using System;
    using System.Diagnostics;

    internal sealed class Subscription
    {
        private const long TicksMultiplier = 1000 * TimeSpan.TicksPerMillisecond;
        private readonly long _throttleByTicks;
        private double? _lastHandleTimestamp;

        internal Subscription(Type type, Guid token, TimeSpan throttleBy, object handler)
        {
            Type = type;
            Token = token;
            Handler = handler;
            _throttleByTicks = throttleBy.Ticks;
        }

        internal void Handle<T>(T message)
        {
            if (!CanHandle()) { return; }

            ((Action<T>)Handler)(message);
        }

        private bool CanHandle()
        {
            if (_throttleByTicks == 0) { return true; }

            if (_lastHandleTimestamp == null)
            {
                _lastHandleTimestamp = Stopwatch.GetTimestamp();
                return true;
            }

            var now = Stopwatch.GetTimestamp();
            var durationInTicks = (now - _lastHandleTimestamp) / Stopwatch.Frequency * TicksMultiplier;

            if (durationInTicks >= _throttleByTicks)
            {
                _lastHandleTimestamp = now;
                return true;
            }

            return false;
        }

        internal Guid Token { get; }
        internal Type Type { get; }
        private object Handler { get; }
    }
}