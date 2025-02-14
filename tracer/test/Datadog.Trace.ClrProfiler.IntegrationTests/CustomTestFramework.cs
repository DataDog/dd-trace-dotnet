// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Datadog.Trace.ClrProfiler.IntegrationTests.CustomTestFramework", "Datadog.Trace.ClrProfiler.IntegrationTests")]

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CustomTestFramework : TestHelpers.AutoInstrumentation.DockerTestFramework
    {
        public CustomTestFramework(IMessageSink messageSink)
            : base(messageSink, typeof(Instrumentation))
        {
        }
    }
}
