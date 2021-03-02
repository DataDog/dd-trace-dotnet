using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.DynamicDiagnosticSourceBindings;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal class StubbedDiagnosticEventsGenerator
    {
        private const int MaxSleepMillis = 10;

        private readonly int _maxInterations;
        private readonly int _phaseOneIterations;
        private int _currentIteration;

        public ManualResetEventSlim PhaseOneCompletedEvent { get; }

        public int CurrentIteration { get { return Interlocked.Add(ref _currentIteration, 0); } }

        public StubbedDiagnosticEventsGenerator(int maxInterations, int phaseOneIterations)
        {
            _maxInterations = (maxInterations > 0) ? maxInterations : 1000;
            _phaseOneIterations = (phaseOneIterations > 0 && phaseOneIterations < maxInterations) ? phaseOneIterations : _maxInterations;

            _currentIteration = 0;
            PhaseOneCompletedEvent = new ManualResetEventSlim(initialState: false);
        }

        public async Task Run()
        {
            Console.WriteLine();
            Console.WriteLine($"Starting {this.GetType().Name}.{nameof(Run)}.");

            string srcName = DiagnosticEventsSpecification.StubbedSourceName;
            DiagnosticSourceStub diagnosticSource = DiagnosticListening.CreateNewSource(srcName);

            Random rnd = new Random();

            int currIteration = CurrentIteration;
            while (currIteration < _maxInterations)
            {
                if (diagnosticSource.IsEnabled(DiagnosticEventsSpecification.StubbedSourceEventName))
                {
                    diagnosticSource.Write(DiagnosticEventsSpecification.DirectSourceEventName, new DiagnosticEventsSpecification.EventPayload(currIteration, srcName));
                }

                if (currIteration == _phaseOneIterations)
                {
                    PhaseOneCompletedEvent.Set();
                }

                int sleepMillis = rnd.Next(MaxSleepMillis);
                if (sleepMillis > 0)
                {
                    await Task.Delay(sleepMillis);
                }

                currIteration = Interlocked.Increment(ref _currentIteration);
            }

            Console.WriteLine();
            Console.WriteLine($"Finishing {this.GetType().Name}.{nameof(Run)}.");

            if (diagnosticSource is IDisposable disposableSource)
            {
                disposableSource.Dispose();
            }

            Console.WriteLine($"Finished {this.GetType().Name}.{nameof(Run)}.");
        }
    }
}
