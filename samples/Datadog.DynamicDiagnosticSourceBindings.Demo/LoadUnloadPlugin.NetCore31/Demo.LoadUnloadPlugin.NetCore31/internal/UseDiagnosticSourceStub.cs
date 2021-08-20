using System;
using System.Collections.Generic;
using Datadog.DynamicDiagnosticSourceBindings;
using Datadog.Util;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.LoadUnloadPlugin.NetCore31
{
    internal static class UseDiagnosticSourceStub
    {
        private class EventXyzNamePayload
        {
            public string DataItem1 { get; }
            public object DataItem2 { get; }
            public int Iteration { get; }

            private EventXyzNamePayload() { }

            public EventXyzNamePayload(string dataItem1, object dataItem2, int iteration)
            {
                DataItem1 = dataItem1;
                DataItem2 = dataItem2;
                Iteration = iteration;
            }

            public override string ToString()
            {
                return $"EventXyzNamePayload {{DataItem1=\"{(DataItem1 ?? "<null>")}\", DataItem2={DataItem2}, Iteration={Iteration}}}";
            }
        }

        public static void Run()
        {
            ConsoleWrite.LineLine($"STARTING DEMO '{nameof(UseDiagnosticSourceStub)}'.");

            SetupListening();

            ConsoleWrite.LineLine("Starting to create new Diagnostic Sources.");

            DiagnosticSourceStub diagnosticSource1 = DiagnosticListening.CreateNewSource("DemoXxx.UseDiagnosticSource.Name1");
            DiagnosticSourceStub diagnosticSource2 = DiagnosticListening.CreateNewSource("DemoXxx.UseDiagnosticSource.Name2");

            ConsoleWrite.Line("Finished creating new Diagnostic Sources.");

            ConsoleWrite.LineLine("Starting to emit DiagnosticSource events.");
            for (int i = 0; i < 1000; i++)
            {
                if (diagnosticSource1.IsEnabled("EventXyzName.A"))
                {
                    diagnosticSource1.Write("EventXyzName", new EventXyzNamePayload("Foo", 42, i));
                }

                if (diagnosticSource1.IsEnabled("EventXyzName.B", arg1: "Something", arg2: 13.7))
                {
                    diagnosticSource1.Write("EventXyzName", new EventXyzNamePayload("Bar", new[] { 1, 2, 3 }, i));
                }

                if (diagnosticSource1.IsEnabled("EventXyzName.C", arg1: -1))
                {
                    diagnosticSource1.Write("EventXyzName", new EventXyzNamePayload(null, null, i));
                }

                diagnosticSource2.Write("EventAbcName", new { Value = "Something", IterationNr = i });

                ConsoleWrite.LineLine($"-----------{i}-----------");
            }

            ConsoleWrite.Line("Finished to emit DiagnosticSource events.");

            ConsoleWrite.LineLine($"FINISHED DEMO '{nameof(UseDiagnosticSourceStub)}'.");
        }

        private static void SetupListening()
        {
            ConsoleWrite.LineLine("Starting setting up DiagnosticSource listening.");

            IDisposable listenerSubscription = DiagnosticListening.SubscribeToAllSources(ObserverAdapter.OnNextHandler(
                    (DiagnosticListenerStub diagnosticListener) =>
                    {
                        ConsoleWrite.Line($"Subscriber called: diagnosticSourceObserver(diagnosticListener.Name: \"{diagnosticListener.Name}\")");

                        if (diagnosticListener.Name.Equals("DemoXxx.UseDiagnosticSource.Name1", StringComparison.Ordinal))
                        {
                            IDisposable eventSubscription = diagnosticListener.SubscribeToEvents(ObserverAdapter.OnNextHandler(
                                            (KeyValuePair<string, object> eventInfo) =>
                                            {
                                                ConsoleWrite.Line($"Event Handler called: eventObserver(eventName: \"{eventInfo.Key}\", payloadValue: {(eventInfo.Value ?? "<null>")})");
                                            }),
                                    (string eventName, object arg1, object arg2) =>
                                    {
                                        Validate.NotNull(eventName, nameof(eventName));
                                        bool res = eventName.StartsWith("EventXyzName", StringComparison.OrdinalIgnoreCase)
                                                        && (arg1 == null || !(arg1 is Int32 arg1Val) || arg1Val >= 0);
                                        ConsoleWrite.Line($"Filter called: isEventEnabledFilter(eventName: \"{eventName}\", arg1: {(arg1 ?? "<null>")}, arg2: {(arg2 ?? "<null>")})."
                                                        + $" Returning: {res}.");
                                        return res;
                                    });
                        }
                    }));

            ConsoleWrite.Line("Finished setting up DiagnosticSource listening.");
        }
    }
}
