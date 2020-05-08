using System;
using System.Collections.Concurrent;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal class NoOpStatsd : IStatsd
    {
        public ConcurrentBag<string> Commands { get; }

        public void Send<TCommandType, T>(string name, T value, double sampleRate, params string[] tags)
            where TCommandType : Statsd.Metric
        {
            // no-op
        }

        public void Add<TCommandType, T>(string name, T value, double sampleRate, params string[] tags)
            where TCommandType : Statsd.Metric
        {
            // no-op
        }

        public void Send(string title, string text, string alertType, string aggregationKey, string sourceType, int? dateHappened, string priority, string hostname, string[] tags, bool truncateIfTooLong = false)
        {
            // no-op
        }

        public void Add(string title, string text, string alertType, string aggregationKey, string sourceType, int? dateHappened, string priority, string hostname, string[] tags, bool truncateIfTooLong = false)
        {
            // no-op
        }

        public void Send(string command)
        {
            // no-op
        }

        public void Send()
        {
            // no-op
        }

        public void Add(Action actionToTime, string statName, double sampleRate, params string[] tags)
        {
            // no-op
        }

        public void Send(Action actionToTime, string statName, double sampleRate, params string[] tags)
        {
            // no-op
        }

        public void Add(string name, int status, int? timestamp, string hostname, string[] tags, string serviceCheckMessage, bool truncateIfTooLong)
        {
            // no-op
        }

        public void Send(string name, int status, int? timestamp, string hostname, string[] tags, string serviceCheckMessage, bool truncateIfTooLong)
        {
            // no-op
        }
    }
}
