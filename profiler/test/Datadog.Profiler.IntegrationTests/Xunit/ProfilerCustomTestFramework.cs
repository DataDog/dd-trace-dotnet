// <copyright file="ProfilerCustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    /// <summary>
    /// Custom test framework for profiler integration tests that supports flaky test retries.
    /// Uses the shared FlakyTestFrameworkExecutor from Datadog.Trace.TestHelpers.
    /// </summary>
    public class ProfilerCustomTestFramework : XunitTestFramework
    {
        public ProfilerCustomTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new FlakyTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
        }
    }
}
