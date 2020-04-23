using System;

namespace Datadog.Trace
{
    internal static class CoreLogging
    {
        private static Func<Type, ICoreLogger> _strategy = null;

        public static void SetStrategy(Func<Type, ICoreLogger> strategy)
        {
            // First come, first set
            if (_strategy == null)
            {
                _strategy = strategy;
            }
            else
            {
                // TODO: log that we have loaded multiple versions?
            }
        }

        public static ICoreLogger For<T>()
        {
            if (_strategy == null)
            {
                throw new Exception("Core logger strategy must be set.");
            }

            return _strategy.Invoke(typeof(T));
        }
    }
}
