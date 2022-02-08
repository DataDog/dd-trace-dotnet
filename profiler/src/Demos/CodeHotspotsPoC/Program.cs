// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Datadog.TestUtil;

namespace Datadog.Demos.CodeHotspotsPoC
{
    public class Program
    {
        private AsyncLocal<MockSpan> _currentSpan = TraceContextTrackingUtils.CreateAsyncSpanPropagator();

        public static void Main()
        {
            (new Program()).Run();
        }

        // -----------

        public void Run()
        {
            EnvironmentInfo.PrintDescriptionToConsole();

            Console.WriteLine($"Main thread. ThreadId={Thread.CurrentThread.ManagedThreadId}.");

            Task mainTask = Task.Run(RunAsync);
            mainTask.GetAwaiter().GetResult();

            Console.WriteLine("\n\nFinished.");
        }

        public async Task RunAsync()
        {
            Console.WriteLine($"Initialization step:"
                            + $" ThreadId={Thread.CurrentThread.ManagedThreadId}.");

            var requestTasks = new List<Task>();
            for (uint i = 1; i <= 10; i++)
            {
                requestTasks.Add(ProcessRequestSystem(i));
            }

            await Task.WhenAll(requestTasks);
        }

        private async Task ProcessRequestSystem(uint requestId)
        {
            // This method simulates stuff in the app server..

            ulong traceId = requestId; // In reality, obtain traceId from propagated context.
            MockSpan rootSpan = new MockSpan(traceId, 0);

            MockSpan prevSpanInfo = _currentSpan.Value;
            _currentSpan.Value = rootSpan;

            try
            {
                await ProcessRequestUser(requestId);
            }
            finally
            {
                _currentSpan.Value = prevSpanInfo;
            }
        }

        private async Task ProcessRequestUser(uint requestId)
        {
            // this method simulates user code of processing a service request.

            // Construct a moniker for displaying:
            const string Indent = "    ";
            string requestMoniker = $"{Indent}[requestTraceId={requestId}]";
            for (int i = 0; i < requestId; i++)
            {
                requestMoniker = Indent + requestMoniker;
            }

            // Simulate work with several async transitions like one would expect for I/O/
            for (int i = 100; i < 110; i++)
            {
                // This simulates either manual or automatic tracing. I.e., a sub-span is created.
                MockSpan prevSpan = _currentSpan.Value;
                _currentSpan.Value = new MockSpan(prevSpan.TraceId, (ulong)i);

                try
                {
                    Console.WriteLine($"\n{requestMoniker} [i={i}]"
                                    + $" asLoc=\"{_currentSpan.Value}\";"
                                    // + $" ThrdInfo.State=\"{CurrentThreadInfo.CurrentState}\";"
                                    // + $" ThrdInfo.IsInit={CurrentThreadInfo.IsInitialized};"
                                    + $" Before Delay: ThreadId={Thread.CurrentThread.ManagedThreadId}.");

                    await Task.Delay(TimeSpan.FromMilliseconds(500));

                    Console.WriteLine($"\n{requestMoniker} [i={i}]"
                                    + $" asLoc=\"{_currentSpan.Value}\";"
                                    // + $" ThrdInfo.State=\"{CurrentThreadInfo.CurrentState}\";"
                                    // + $" ThrdInfo.IsInit={CurrentThreadInfo.IsInitialized};"
                                    + $" After Delay: ThreadId={Thread.CurrentThread.ManagedThreadId}.");
                }
                finally
                {
                    _currentSpan.Value = prevSpan;
                }
            }
        }
    }
}
