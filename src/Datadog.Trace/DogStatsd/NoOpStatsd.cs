using System;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal class NoOpStatsd : IDogStatsd
    {
        public ITelemetryCounters TelemetryCounters => null;

        public void Dispose()
        {
        }

        public void Configure(StatsdConfig config)
        {
        }

        public void Counter<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Decrement(string statName, int value = 1, double sampleRate = 1, params string[] tags)
        {
        }

        public void Event(string title, string text, string alertType = null, string aggregationKey = null, string sourceType = null, int? dateHappened = null, string priority = null, string hostname = null, string[] tags = null)
        {
        }

        public void Gauge<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Histogram<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Distribution<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Set<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public IDisposable StartTimer(string name, double sampleRate = 1, string[] tags = null)
        {
            return NoOpTimer.Instance;
        }

        public void Time(Action action, string statName, double sampleRate = 1, string[] tags = null)
        {
        }

        public T Time<T>(Func<T> func, string statName, double sampleRate = 1, string[] tags = null)
        {
            return func();
        }

        public void Timer<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void ServiceCheck(string name, Status status, int? timestamp = null, string hostname = null, string[] tags = null, string message = null)
        {
        }

        private class NoOpTimer : IDisposable
        {
            internal static readonly NoOpTimer Instance = new NoOpTimer();

            public void Dispose()
            {
            }
        }
    }
}
