using System;
using System.Collections.Generic;
using System.Diagnostics;

using Datadog.Util;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.LoadUnloadPlugin.NetCore31
{
    internal static class UseDiagnosticSource
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
            ConsoleWrite.LineLine($"STARTING DEMO '{nameof(UseDiagnosticSource)}'.");

            SetupListening();

            ConsoleWrite.LineLine("Starting to create new Diagnostic Sources.");

            DiagnosticSource diagnosticSource1 = new DiagnosticListener("DemoXxx.UseDiagnosticSource.Name1");
            DiagnosticSource diagnosticSource2 = new DiagnosticListener("DemoXxx.UseDiagnosticSource.Name2");

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

            ConsoleWrite.LineLine($"FINISHED DEMO '{nameof(UseDiagnosticSource)}'.");
        }

        private static void SetupListening()
        {
            ConsoleWrite.LineLine("Starting setting up DiagnosticSource listening.");

            IDisposable listenerSubscription = DiagnosticListener.AllListeners.Subscribe(ObserverAdapter.OnNextHandler(
                    (DiagnosticListener diagLstnr) =>
                    {
                        // This lambda looks at ALL Diagnostic Listeners (aka Sources),
                        // picks the one it is inderested in and subscibes to that particular Source.

                        ConsoleWrite.Line($"Subscriber called: OnNext(diagLstnr.Name: \"{diagLstnr.Name}\")");

                        if (diagLstnr.Name.Equals("DemoXxx.UseDiagnosticSource.Name1", StringComparison.Ordinal))
                        {
                            IDisposable eventSubscription = diagLstnr.Subscribe(ObserverAdapter.OnNextHandler(
                                    (KeyValuePair<string, object> eventInfo) =>
                                    {
                                        ConsoleWrite.Line($"Event Handler called: OnNext(eventInfo.Key: \"{eventInfo.Key}\", eventInfo.Value: {(eventInfo.Value ?? "<null>")})");
                                    }),
                                    (name, arg1, arg2) =>
                                    {
                                        Validate.NotNull(name, nameof(name));
                                        bool res = name.StartsWith("EventXyzName", StringComparison.OrdinalIgnoreCase)
                                                        && (arg1 == null || !(arg1 is Int32 arg1Val) || arg1Val >= 0);
                                        ConsoleWrite.Line($"Filter called: IsEnabled(name: \"{name}\", arg1: {(arg1 ?? "<null>")}, arg2: {(arg2 ?? "<null>")})."
                                                        + $" Returning: {res}.");
                                        return res;
                                    });
                        }
                    }));

            ConsoleWrite.Line("Finished setting up DiagnosticSource listening.");
        }
    }
}
