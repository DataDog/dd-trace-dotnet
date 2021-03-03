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
        private DiagnosticSourceStub _diagnosticSource;

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
            ConsoleWrite.LineLine($"Starting {this.GetType().Name}.{nameof(Run)}.");

            ConsoleWrite.LineLine($"Initializing new Diagnostic Source.");
            CreateNewSource();
            ConsoleWrite.LineLine($"Finsihed initializing new Diagnostic Source.");

            Random rnd = new Random();

            int currIteration = CurrentIteration;
            while (currIteration < _maxInterations)
            {
                DiagnosticSourceStub diagnosticSource = _diagnosticSource;
                if (!diagnosticSource.IsNoOpStub)
                {
                    try
                    {
                        if (diagnosticSource.IsEnabled(DiagnosticEventsSpecification.StubbedSourceEventName))
                        {
                            diagnosticSource.Write(DiagnosticEventsSpecification.StubbedSourceEventName,
                                                   new DiagnosticEventsSpecification.EventPayload(currIteration, DiagnosticEventsSpecification.StubbedSourceName));
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleWrite.Exception(ex);
                        _diagnosticSource = DiagnosticSourceStub.NoOpStub;

                        ConsoleWrite.LineLine($"Writing to Diagnostic Source failed. Scheduling a re-initialization.");
                        Task _ = Task.Run(async () =>
                            {
                                await Task.Delay(100);

                                ConsoleWrite.LineLine($"Re-initializing Diagnostic Source.");
                                CreateNewSource();
                                ConsoleWrite.LineLine($"Finished re-initializing Diagnostic Source.");
                            });
                    }
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

            ConsoleWrite.Line();
            ConsoleWrite.Line($"Finishing {this.GetType().Name}.{nameof(Run)}.");

            {
                DiagnosticSourceStub diagnosticSource = _diagnosticSource;
                if (diagnosticSource is IDisposable disposableSource)
                {
                    try
                    {
                        disposableSource.Dispose();
                    }
                    catch (Exception ex)
                    {
                        ConsoleWrite.Exception(ex);
                    }
                }
            }

            ConsoleWrite.Line($"Finished {this.GetType().Name}.{nameof(Run)}.");
        }

        private void CreateNewSource()
        {
            try
            {
                DiagnosticSourceStub newDiagSrc = DiagnosticListening.CreateNewSource(DiagnosticEventsSpecification.StubbedSourceName);
                _diagnosticSource = newDiagSrc;
            }
            catch(Exception ex)
            {
                ConsoleWrite.Exception(ex);
                _diagnosticSource = DiagnosticSourceStub.NoOpStub;
            }
        }
    }
}
