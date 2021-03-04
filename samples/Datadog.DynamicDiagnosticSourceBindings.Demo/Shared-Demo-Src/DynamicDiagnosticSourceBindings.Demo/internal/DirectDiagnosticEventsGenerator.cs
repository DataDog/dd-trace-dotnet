using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal class DirectDiagnosticEventsGenerator
    {
        private const int MaxSleepMillis = 4;

        private readonly int _maxInterations;
        private readonly int _phaseOneIterations;
        private int _currentIteration;

        public ManualResetEventSlim PhaseOneCompletedEvent { get; }

        public int CurrentIteration { get { return Interlocked.Add(ref _currentIteration, 0); } }

        public DirectDiagnosticEventsGenerator(int maxInterations, int phaseOneIterations)
        {
            _maxInterations = (maxInterations > 0) ? maxInterations : 1000;
            _phaseOneIterations = (phaseOneIterations > 0 && phaseOneIterations < maxInterations) ? phaseOneIterations : _maxInterations;

            _currentIteration = 0;
            PhaseOneCompletedEvent = new ManualResetEventSlim(initialState: false);
        }

        public async Task Run()
        {
            ConsoleWrite.Line();
            ConsoleWrite.Line($"Starting {this.GetType().Name}.{nameof(Run)}.");

            string srcName = DiagnosticEventsSpecification.DirectSourceName;
            DiagnosticSource diagnosticSource = new DiagnosticListener(srcName);

            Random rnd = new Random();

            int currIteration = CurrentIteration;
            while (currIteration < _maxInterations)
            {
                if (diagnosticSource.IsEnabled(DiagnosticEventsSpecification.DirectSourceEventName))
                {
                    diagnosticSource.Write(DiagnosticEventsSpecification.DirectSourceEventName, new DiagnosticEventsSpecification.EventPayload(currIteration, srcName));
                }

                if (currIteration == _phaseOneIterations)
                {
                    PhaseOneCompletedEvent.Set();
                }

                int sleepMillis = rnd.Next(MaxSleepMillis);
                if (sleepMillis == 0)
                {
                    ;
                }
                if (sleepMillis == 1)
                {
                    Thread.Yield();
                }
                else
                {
                    await Task.Delay(sleepMillis);
                }

                currIteration = Interlocked.Increment(ref _currentIteration);
            }

            ConsoleWrite.Line();
            ConsoleWrite.Line($"Finishing {this.GetType().Name}.{nameof(Run)}.");

            if (diagnosticSource is IDisposable disposableSource)
            {
                disposableSource.Dispose();
            }

            ConsoleWrite.Line($"Finished {this.GetType().Name}.{nameof(Run)}.");
        }
    }
}
