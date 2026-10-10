// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Datadog.Trace.DuckTyping.Tests.CustomTestFramework", "Datadog.Trace.DuckTyping.Tests")]

namespace Datadog.Trace.DuckTyping.Tests
{
    public class CustomTestFramework : TestHelpers.CustomTestFramework
    {
        public CustomTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            DuckTypeTestRuntimeBootstrap.Initialize();
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new DiscoveryFlushingExecutor(base.CreateExecutor(assemblyName));
        }

        private sealed class DiscoveryFlushingExecutor : LongLivedMarshalByRefObject, ITestFrameworkExecutor
        {
            private readonly ITestFrameworkExecutor _executor;

            public DiscoveryFlushingExecutor(ITestFrameworkExecutor executor)
            {
                _executor = executor;
            }

            public ITestCase Deserialize(string value) => _executor.Deserialize(value);

            public void RunAll(IMessageSink executionMessageSink, ITestFrameworkDiscoveryOptions discoveryOptions, ITestFrameworkExecutionOptions executionOptions)
                => _executor.RunAll(new DiscoveryFlushingSink(executionMessageSink), discoveryOptions, executionOptions);

            public void RunTests(IEnumerable<ITestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
                => _executor.RunTests(testCases, new DiscoveryFlushingSink(executionMessageSink), executionOptions);

            public void Dispose() => _executor.Dispose();
        }

        private sealed class DiscoveryFlushingSink : LongLivedMarshalByRefObject, IMessageSink
        {
            private readonly IMessageSink _sink;

            public DiscoveryFlushingSink(IMessageSink sink)
            {
                _sink = sink;
            }

            public bool OnMessage(IMessageSinkMessage message)
            {
                if (message is ITestAssemblyFinished)
                {
                    // VSTest can terminate the testhost as soon as completion is reported,
                    // interrupting a ProcessExit flush and losing the last discovery mappings.
                    DuckTypeAotDiscoveryRecorder.Flush();
                }

                return _sink.OnMessage(message);
            }
        }
    }
}
