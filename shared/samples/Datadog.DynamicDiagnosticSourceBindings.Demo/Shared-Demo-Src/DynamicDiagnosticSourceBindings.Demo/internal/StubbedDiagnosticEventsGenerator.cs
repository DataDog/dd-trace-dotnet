using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.DynamicDiagnosticSourceBindings;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal class StubbedDiagnosticEventsGenerator
    {
        private const int MaxSleepMillis = 5;

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

        private void OnDynamicDiagnosticSourceInvokerInitialized(DiagnosticSourceAssembly.IDynamicInvoker dynamicInvoker, object state)
        {
            Random rnd = (Random) state;

            ConsoleWrite.LineLine($"This {this.GetType().Name} noticed that an"
                                + $" {nameof(DiagnosticSourceAssembly)}.{nameof(DiagnosticSourceAssembly.IDynamicInvoker)} became available."
                                + $" DiagnosticSourceAssemblyName: \"{dynamicInvoker.DiagnosticSourceAssemblyName}\".");

            int sessionId = rnd.Next(1000);
            ConsoleWrite.LineLine($"Initializing new Diagnostic Source generator session \"{sessionId.ToString("000")}\".");
            CreateNewSource(ref _diagnosticSource);
            ConsoleWrite.LineLine($"Finsihed initializing new Diagnostic Source generator session \"{sessionId.ToString("000")}\".");

            dynamicInvoker.SubscribeInvalidatedListener(OnDynamicDiagnosticSourceInvokerInvalidated, sessionId);
        }

        private void OnDynamicDiagnosticSourceInvokerInvalidated(DiagnosticSourceAssembly.IDynamicInvoker dynamicInvoker, object state)
        {
            _diagnosticSource = DiagnosticSourceStub.NoOpStub;

            // This listener method is just here for demo purposes. It does not perform any business logic (but it could).
            int sessionId = (state is int stateInt) ? stateInt : -1;
            ConsoleWrite.LineLine($"This {this.GetType().Name} noticed that a dynamic DiagnosticSource invoker was invalidated (session {sessionId})."
                                + $" Some errors may be temporarily observed until stubs are re-initialized.");
        }

        public async Task Run()
        {
            ConsoleWrite.LineLine($"Starting {this.GetType().Name}.{nameof(Run)}.");

            Random rnd = new Random();

            DiagnosticSourceAssembly.SubscribeDynamicInvokerInitializedListener(OnDynamicDiagnosticSourceInvokerInitialized, rnd);

            {
                ConsoleWrite.LineLine($"Kicking off the DS magic.");
                bool prevInit = DiagnosticSourceAssembly.IsInitialized;
                bool nowInit = DiagnosticSourceAssembly.EnsureInitialized();
                ConsoleWrite.Line($"DiagnosticSourceAssembly-magic status: prevInit={prevInit}, nowInit={nowInit}.");
            }

            int currIteration = CurrentIteration;
            while (currIteration < _maxInterations)
            {
                WriteIfEnabled(_diagnosticSource, currIteration);

                if (currIteration == _phaseOneIterations)
                {
                    PhaseOneCompletedEvent.Set();
                }

                int sleepMillis = rnd.Next(MaxSleepMillis);
                if (sleepMillis == 0)
                {
                    ;
                }
                else if (sleepMillis == 1)
                {
                    Thread.Yield();
                }
                else
                {
                    await Task.Delay(sleepMillis - 2);
                }

                currIteration = Interlocked.Increment(ref _currentIteration);
            }

            ConsoleWrite.Line();
            ConsoleWrite.Line($"Finishing {this.GetType().Name}.{nameof(Run)}.");

            Dispose(_diagnosticSource);

            ConsoleWrite.Line($"Finished {this.GetType().Name}.{nameof(Run)}.");
        }

        private static void CreateNewSource(ref DiagnosticSourceStub diagnosticSource)
        {
            try
            {
                DiagnosticSourceStub newDiagSrc = DiagnosticListening.CreateNewSource(DiagnosticEventsSpecification.StubbedSourceName);
                diagnosticSource = newDiagSrc;
            }
            catch (Exception ex)
            {
                diagnosticSource = DiagnosticSourceStub.NoOpStub;

                // If there was some business logic required to handle such errors, it would go here.
                ConsoleWrite.Exception(ex);
            }
        }

        private static void WriteIfEnabled(DiagnosticSourceStub diagnosticSource, int currentIteration)
        {
            try
            {
                if (!diagnosticSource.IsNoOpStub && diagnosticSource.IsEnabled(DiagnosticEventsSpecification.StubbedSourceEventName))
                {
                    diagnosticSource.Write(DiagnosticEventsSpecification.StubbedSourceEventName,
                                           new DiagnosticEventsSpecification.EventPayload(currentIteration, DiagnosticEventsSpecification.StubbedSourceName));
                }
            }
            catch (Exception ex)
            {
                // If there was some business logic required to handle such errors, it would go here.
                ConsoleWrite.Exception(ex);
            }
        }

        private static void Dispose(DiagnosticSourceStub diagnosticSource)
        {
            try
            {
                diagnosticSource.Dispose();
            }
            catch (Exception ex)
            {
                // If there was some business logic required to handle such errors, it would go here.
                ConsoleWrite.Exception(ex);
            }
        }
    }
}
