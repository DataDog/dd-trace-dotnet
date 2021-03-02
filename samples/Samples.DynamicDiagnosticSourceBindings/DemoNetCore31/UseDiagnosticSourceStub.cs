using System;
using System.Collections.Generic;
using Datadog.DynamicDiagnosticSourceBindings;
using Datadog.Util;

namespace DemoNetCore31
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
            Console.WriteLine();
            Console.WriteLine($"STARTING DEMO '{nameof(UseDiagnosticSourceStub)}'.");

            SetupListening();

            Console.WriteLine();
            Console.WriteLine("Starting to create new Diagnostic Sources.");

            DiagnosticSourceStub diagnosticSource1 = DiagnosticListening.CreateNewSource("DemoXxx.UseDiagnosticSource.Name1");
            DiagnosticSourceStub diagnosticSource2 = DiagnosticListening.CreateNewSource("DemoXxx.UseDiagnosticSource.Name2");

            Console.WriteLine("Finished creating new Diagnostic Sources.");

            Console.WriteLine();
            Console.WriteLine("Starting to emit DiagnosticSource events.");
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

                diagnosticSource2.Write("EventAbcName", new { Value="Something", IterationNr = i });

                Console.WriteLine($"{Environment.NewLine}-----------{i}-----------");
            }

            Console.WriteLine("Finished to emit DiagnosticSource events.");

            Console.WriteLine();
            Console.WriteLine($"FINISHED DEMO '{nameof(UseDiagnosticSourceStub)}'.");
        }

        private static void SetupListening()
        {
            Console.WriteLine();
            Console.WriteLine("Starting setting up DiagnosticSource listening.");

            IDisposable listenerSubscription = DiagnosticListening.SubscribeToAllSources(ObserverAdapter.OnAllHandlers(
                    (DiagnosticListenerStub diagnosticListener) =>
                    {
                        Console.WriteLine($"Subscriber called: diagnosticSourceObserver(diagnosticListener.Name: \"{diagnosticListener.Name}\")");

                        if (diagnosticListener.Name.Equals("DemoXxx.UseDiagnosticSource.Name1", StringComparison.Ordinal))
                        {
                            IDisposable eventSubscription = diagnosticListener.SubscribeToEvents(ObserverAdapter.OnAllHandlers(
                                            (KeyValuePair<string, object> eventInfo) =>
                                            {
                                                Console.WriteLine($"Event Handler called: eventObserver(eventName: \"{eventInfo.Key}\", payloadValue: {(eventInfo.Value ?? "<null>")})");
                                            },
                                            (Exception error) =>
                                            {
                                                Console.WriteLine($"Error passed to the observer subscribed the \"{diagnosticListener.Name}\"-event source: {error?.ToString() ?? "<null>"}");
                                            },
                                            () =>
                                            {
                                            }),
                                    (string eventName, object arg1, object arg2) =>
                                    {
                                        Validate.NotNull(eventName, nameof(eventName));
                                        bool res = eventName.StartsWith("EventXyzName", StringComparison.OrdinalIgnoreCase)
                                                        && (arg1 == null || !(arg1 is Int32 arg1Val) || arg1Val >= 0);
                                        Console.WriteLine($"Filter called: isEventEnabledFilter(eventName: \"{eventName}\", arg1: {(arg1 ?? "<null>")}, arg2: {(arg2 ?? "<null>")})."
                                                        + $" Returning: {res}.");
                                        return res;
                                    });
                        }
                    },
                    (Exception error) =>
                    {
                        Console.WriteLine($"Error passed to the observer subscribed to all event sources: {error?.ToString() ?? "<null>"}");
                    },
                    () =>
                    {
                    }));


            //IDisposable listenerSubscription = DiagnosticListening.SubscribeToAllSources(
            //        (DiagnosticListenerStub diagnosticListener) =>
            //        {
            //            // This lambda looks at ALL Diagnostic Listeners (aka Sources),
            //            // picks the one it is inderested in and subscibes to that particular Source.

            //            Console.WriteLine($"Subscriber called: diagnosticSourceObserver(diagnosticListener.Name: \"{diagnosticListener.Name}\")");

            //            if (diagnosticListener.Name.Equals("DemoXxx.UseDiagnosticSource.Name1", StringComparison.Ordinal))
            //            {
            //                IDisposable eventSubscription = diagnosticListener.SubscribeToEvents(
            //                        (string eventName, object payloadValue) =>
            //                        {
            //                            Console.WriteLine($"Event Handler called: eventObserver(eventName: \"{eventName}\", payloadValue: {(payloadValue ?? "<null>")})");
            //                        },
            //                        (string eventName, object arg1, object arg2) =>
            //                        {
            //                            Validate.NotNull(eventName, nameof(eventName));
            //                            bool res = eventName.StartsWith("EventXyzName", StringComparison.OrdinalIgnoreCase)
            //                                            && (arg1 == null || !(arg1 is Int32 arg1Val) || arg1Val >= 0);
            //                            Console.WriteLine($"Filter called: isEventEnabledFilter(eventName: \"{eventName}\", arg1: {(arg1 ?? "<null>")}, arg2: {(arg2 ?? "<null>")})."
            //                                            + $" Returning: {res}.");
            //                            return res;
            //                        });
            //            }
            //        });

            Console.WriteLine("Finished setting up DiagnosticSource listening.");
        }
    }
}
