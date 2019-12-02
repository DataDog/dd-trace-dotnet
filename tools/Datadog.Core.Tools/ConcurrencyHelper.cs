using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Core.Tools
{
    public class ConcurrencyHelper
    {
        private readonly ManualResetEventSlim _initiateLevelsEvent = new ManualResetEventSlim(initialState: false); // Start blocked
        private readonly ManualResetEventSlim _testFinishedEvent = new ManualResetEventSlim(initialState: false); // Start blocked
        private int _remainingLevels;

        public enum HelperStatus
        {
            AwaitingConfig,
            Ready,
            Running,
            Errored,
            Finished
        }

        public List<Level> Levels { get; set; } = new List<Level>();

        public HelperStatus Status { get; private set; } = HelperStatus.AwaitingConfig;

        public DateTime Started { get; private set; }

        public DateTime Finished { get; private set; }

        public void RegisterLevel(Action action, int iterations, string friendlyName = null, int numberOfRegisters = 1)
        {
            while (numberOfRegisters-- > 0)
            {
                Levels.Add(new Level()
                {
                    Status = HelperStatus.Ready,
                    Action = action,
                    Name = friendlyName,
                    Iterations = iterations,
                });
            }

            Status = HelperStatus.Ready;
        }

        public async Task Start()
        {
            Status = HelperStatus.Running;

            var registry = new ConcurrentQueue<Thread>();

            Started = DateTime.Now;

            var workers =
                Levels
                   .Select(level => new Thread(
                               thread =>
                               {
                                   Interlocked.Increment(ref _remainingLevels);
                                   _initiateLevelsEvent.Wait();
                                   level.Started = DateTime.Now;
                                   level.Status = HelperStatus.Running;

                                   try
                                   {
                                       for (var i = 0; i < level.Iterations; i++)
                                       {
                                           try
                                           {
                                               level.Action();
                                           }
                                           catch (Exception ex)
                                           {
                                               level.Exceptions.Add(ex);
                                           }
                                       }
                                   }
                                   finally
                                   {
                                       level.Finished = DateTime.Now;
                                       level.Status = HelperStatus.Finished;
                                       Interlocked.Decrement(ref _remainingLevels);

                                       if (_remainingLevels == 0)
                                       {
                                           // The run is finished
                                           _testFinishedEvent.Set();
                                       }
                                   }
                               }));

            foreach (var worker in workers)
            {
                registry.Enqueue(worker);
                worker.Start();
            }

            // Run everything
            _initiateLevelsEvent.Set();

            // Wait for everything to finish
            _testFinishedEvent.Wait();

            Finished = DateTime.Now;
            Status = HelperStatus.Finished;

            // Pause for the next run if this class is reused
            _initiateLevelsEvent.Reset();

            await Task.FromResult(0);
        }

        public Dictionary<string, int> GetExceptionSummary()
        {
            return Levels.SelectMany(l => l.Exceptions).GroupBy(ex => ex.Message).ToDictionary(group => group.Key, group => group.Count());
        }

        public double GetTotalRuntime()
        {
            return Levels.Sum(l => l.TotalMilliseconds);
        }

        public double GetAverageActionRuntime()
        {
            return Levels.Average(l => l.AverageCallMilliseconds);
        }

        public class Level
        {
            public string Name { get; set; }

            public Action Action { get; set; }

            public int Iterations { get; set; }

            public ConcurrentBag<Exception> Exceptions { get; set; } = new ConcurrentBag<Exception>();

            public DateTime Started { get; set; }

            public DateTime Finished { get; set; }

            public double TotalMilliseconds => (Finished - Started).TotalMilliseconds;

            public double AverageCallMilliseconds => TotalMilliseconds / Iterations;

            public HelperStatus Status { get; set; }
        }
    }
}
