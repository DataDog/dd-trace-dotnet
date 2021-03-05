using System;
using System.Collections.Generic;
using Datadog.DynamicDiagnosticSourceBindings;
using Datadog.Util;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.Slimple.NetCore31
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
            // This demo shows one of several possible ways for dealing with dynamic invocation exceptions.
            // The corresponding Net Fx demo shows how to use the APIs directly.
            // Other demos show other approaches for dealing with these exceptions.

            ConsoleWrite.LineLine($"STARTING DEMO '{nameof(UseDiagnosticSourceStub)}'.");

            SetupListening();

            DiagnosticSourceSafeExtensions.Configure.LogComponentMoniker = "Demo.Slimple.NetCore31";
            DiagnosticSourceSafeExtensions.Configure.IsLogExceptionsEnabled = true;

            ConsoleWrite.LineLine("Starting to create new Diagnostic Sources.");

            if (!DiagnosticSourceSafeExtensions.CreateNewSourceSafe("DemoXxx.UseDiagnosticSource.Name1", out DiagnosticSourceStub diagnosticSource1, out _))
            {
                ConsoleWrite.Line("Cannot create DiagnosticSource. Error was logged. Bailing out.");
                return;
            }

            if (!DiagnosticSourceSafeExtensions.CreateNewSourceSafe("DemoXxx.UseDiagnosticSource.Name2", out DiagnosticSourceStub diagnosticSource2, out _))
            {
                ConsoleWrite.Line("Cannot create DiagnosticSource. Error was logged. Bailing out.");
                return;
            }

            ConsoleWrite.Line("Finished creating new Diagnostic Sources.");

            ConsoleWrite.LineLine("Starting to emit DiagnosticSource events.");
            for (int i = 0; i < 1000; i++)
            {
                if (diagnosticSource1.IsEnabledSafe("EventXyzName.A", out bool isEventEnabled, out _) && isEventEnabled)
                {
                    diagnosticSource1.WriteSafe("EventXyzName", new EventXyzNamePayload("Foo", 42, i), out _);
                }

                if (diagnosticSource1.IsEnabledSafe("EventXyzName.B", arg1: "Something", arg2: 13.7, out isEventEnabled, out _) && isEventEnabled)
                {
                    diagnosticSource1.WriteSafe("EventXyzName", new EventXyzNamePayload("Bar", new[] { 1, 2, 3 }, i), out _);
                }

                if (diagnosticSource1.IsEnabledSafe("EventXyzName.C", arg1: -1, out isEventEnabled, out _) && isEventEnabled)
                {
                    diagnosticSource1.WriteSafe("EventXyzName", new EventXyzNamePayload(null, null, i), out _);
                }

                diagnosticSource2.WriteSafe("EventAbcName", new { Value = "Something", IterationNr = i }, out _);

                ConsoleWrite.LineLine($"-----------{i}-----------");
            }

            ConsoleWrite.Line("Finished to emit DiagnosticSource events.");

            ConsoleWrite.LineLine($"FINISHED DEMO '{nameof(UseDiagnosticSourceStub)}'.");
        }

        private static void SetupListening()
        {
            ConsoleWrite.LineLine("Starting setting up DiagnosticSource listening.");

            if (!DiagnosticSourceSafeExtensions.SubscribeToAllSourcesSafe(ObserverAdapter.OnNextHandler(
                    (DiagnosticListenerStub diagnosticListener) =>
                    {
                        diagnosticListener.GetNameSafe(out string diagnosticListenerName, out _);
                        ConsoleWrite.Line($"Subscriber called: diagnosticSourceObserver(diagnosticListener.Name: \"{diagnosticListenerName}\")");

                        if (diagnosticListenerName.Equals("DemoXxx.UseDiagnosticSource.Name1", StringComparison.Ordinal))
                        {
                            if (!diagnosticListener.SubscribeToEventsSafe(
                                    ObserverAdapter.OnNextHandler((KeyValuePair<string, object> eventInfo) =>
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
                                    },
                                    out IDisposable eventSubscription,
                                    out _))
                            {
                                ConsoleWrite.LineLine("Could not set up an events subscription. Likely no events will be received. Error has been logged.");
                            }
                        }
                    }),
                    out IDisposable listenerSubscription,
                    out _))
            {
                ConsoleWrite.LineLine("Could not set up an all-sources-subscription. Likely no events will be received. Error has been logged.");
            }

            ConsoleWrite.Line("Finished setting up DiagnosticSource listening.");
        }
    }
}
