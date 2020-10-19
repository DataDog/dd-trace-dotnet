using System.Collections.Generic;

namespace Datadog.Trace.DogStatsd
{
    internal readonly struct Batch
    {
        private readonly IBatchStatsd _statsd;
        private readonly List<string> _commands;

        public Batch(IBatchStatsd statsd, int initialCapacity)
        {
            _statsd = statsd;
            _commands = statsd != null ? new List<string>(initialCapacity) : null;
        }

        public void Append(string command)
        {
            if (!string.IsNullOrEmpty(command))
            {
                _commands?.Add(command);
            }
        }

        public void Send()
        {
            _statsd?.Send(string.Join("\n", _commands));
        }
    }
}
